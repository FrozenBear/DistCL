using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.Threading;
using System.Threading.Tasks;
using DistCL.RemoteCompilerService;
using DistCL.Utils;

namespace DistCL
{
	public class CompileServiceHost : ServiceHost
	{
		private static ServiceModelSectionGroup _serviceModelSectionGroup;
		private static Dictionary<string, Binding> _bindings;
		private readonly object _syncRoot = new object();
		private bool _closed;
		private string _physicalPath;
		private ICompilerProvider _compilerProvider;

		public CompileServiceHost(params Uri[] baseAddresses) : base(new Compiler(), baseAddresses)
		{
		}

		private static ServiceModelSectionGroup ServiceModelSectionGroup
		{
			get
			{
				if (_serviceModelSectionGroup == null)
				{
					_serviceModelSectionGroup = ServiceModelSectionGroup.GetSectionGroup(
						ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None));
				}
				return _serviceModelSectionGroup;
			}
		}

		public IDictionary<string, Binding> Bindings
		{
			get
			{
				if (_bindings == null)
				{
					_bindings = new Dictionary<string, Binding>();

					foreach (Binding binding in Description.Endpoints.Select(endpoint => endpoint.Binding))
					{
						string schema = GetSchemaByBindingType(binding.GetType());

						if (schema != null && ! _bindings.ContainsKey(schema))
						{
							_bindings.Add(schema, binding);
						}
					}

					foreach (BindingCollectionElement binding in ServiceModelSectionGroup.Bindings.BindingCollections)
					{
						string schema = GetSchemaByBindingType(binding.BindingType);

						if (schema != null && ! _bindings.ContainsKey(schema))
						{
							foreach (var configuredBinding in binding.ConfiguredBindings)
							{
								_bindings.Add(schema, (Binding)Activator.CreateInstance(binding.BindingType, configuredBinding.Name));
							}
						}
					}
				}
				return _bindings;
			}
		}

		private static string GetSchemaByBindingType(Type bindingType)
		{
			if (typeof(BasicHttpBinding).IsAssignableFrom(bindingType))
			{
				return "http";
			}
			
			if (typeof(BasicHttpsBinding).IsAssignableFrom(bindingType))
			{
				return "https";
			}
			
			if (typeof(NetTcpBinding).IsAssignableFrom(bindingType))
			{
				return "net.tcp";
			}

			if (typeof(NetPeerTcpBinding).IsAssignableFrom(bindingType))
			{
				return "net.p2p";
			}

			return null;
		}

		private string PhysicalPath
		{
			get
			{
				if (_physicalPath == null)
				{
					// if hosted in IIS
					_physicalPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;

					if (String.IsNullOrEmpty(_physicalPath))
					{
						// for hosting outside of IIS
						_physicalPath = Path.GetDirectoryName(GetType().Assembly.Location);
					}
				}
				return _physicalPath;
			}
		}

		public Compiler CompilerInstance
		{
			get { return (Compiler) SingletonInstance; }
		}

		private ICompilerProvider CompilerProvider
		{
			get
			{
				lock (_syncRoot)
				{
					if (_compilerProvider == null)
					{
						var agentPoolUrls = new List<Uri>();
						var compilerUrls = new List<Uri>();

						foreach (var endpoint in Description.Endpoints)
						{
							if (typeof (IAgentPool).IsAssignableFrom(endpoint.Contract.ContractType))
							{
								agentPoolUrls.Add(endpoint.Address.Uri.IsLoopback
													? new UriBuilder(endpoint.Address.Uri) {Host = Environment.MachineName}.Uri
													: endpoint.Address.Uri);
							}

							if (typeof(ICompiler).IsAssignableFrom(endpoint.Contract.ContractType))
							{
								compilerUrls.Add(endpoint.Address.Uri.IsLoopback
													? new UriBuilder(endpoint.Address.Uri) { Host = Environment.MachineName }.Uri
													: endpoint.Address.Uri);
							}
						}

						_compilerProvider = new LocalCompilerProvider(CompilerInstance, agentPoolUrls.ToArray(), compilerUrls.ToArray());

					}
				}

				return _compilerProvider;
			}
		}

		protected override void ApplyConfiguration()
		{
			var servicesSection = ConfigurationManager.GetSection("system.serviceModel/services") as ServicesSection;

			if (servicesSection == null ||
				servicesSection.Services.Cast<ServiceElement>().All(element => element.Name != Description.ConfigurationName))
			{
				var configFilename = Path.Combine(PhysicalPath, "DistCL.config");

				if (!string.IsNullOrEmpty(configFilename) && File.Exists(configFilename))
				{
					var fileMap = new ExeConfigurationFileMap {ExeConfigFilename = configFilename};
					var config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
					var serviceModel = ServiceModelSectionGroup.GetSectionGroup(config);

					if (serviceModel != null)
					{
						foreach (ServiceElement element in serviceModel.Services.Services)
						{
							if (element.Name == Description.ConfigurationName)
							{
								LoadConfigurationSection(element);
								return;
							}
						}
					}
				}
			}

			base.ApplyConfiguration();
		}

		protected override void OnOpening()
		{
			base.OnOpening();

			CompilerInstance.Bindings = Bindings;
		}

		protected override void OnOpened()
		{
			base.OnOpened();

			_closed = false;

			var updateAgentsThread = new Thread(UpdateAgents) {IsBackground = true};
			updateAgentsThread.Start();
		}

		protected override void OnClosing()
		{
			base.OnClosing();

			lock (_syncRoot)
			{
				_closed = true;
				Monitor.PulseAll(_syncRoot);
			}
		}

		private void UpdateAgents(object o)
		{
			var pools = GetAgentPools(ServiceModelSectionGroup);

			var localAgent = new RemoteCompilerService.AgentReqistrationMessage
				{
					Guid = CompilerProvider.Agent.Guid,
					Name = CompilerProvider.Agent.Name,
					Cores = CompilerProvider.Agent.Cores,
					AgentPoolUrls = CompilerProvider.Agent.AgentPoolUrls,
					CompilerUrls = CompilerProvider.Agent.CompilerUrls
				};

			Logger.LogAgent("CompileServiceHost.UpdateAgents.Init", localAgent.Name);

			lock (_syncRoot)
			{
				var problemPools = new ConcurrentQueue<AgentPoolClient>();

				while (!_closed)
				{
					var iterationStarted = DateTime.Now;

					var ts = new CancellationTokenSource();

					foreach (var item in pools.Values)
					{
						var pool = item;

						((RemoteCompilerService.IAgentPool) pool)
							.RegisterAgentAsync(localAgent)
							.ContinueWith(
								delegate(Task task)
									{
										if (task.Exception != null)
										{
											Logger.Info("CompileServiceHost.UpdateAgents.RegisterAgent" + task.Exception.Message);
										}

										if (task.IsFaulted)
										{
											if (pool.IncreaseErrorCount())
											{
												problemPools.Enqueue(pool);
											}
										}
										else
										{
											pool.ResetErrorCount();
										}
									}); // without CancellationToken - result check required				
					}

					CompilerInstance.AgentPool.RegisterAgent(CompilerProvider);

					CompilerInstance.AgentPool.Clean(DateTime.Now.Subtract(CompilerSettings.Default.AgentsSilenceLimit));

					ConvertRegisteredAgents2AgentPools(
						pools.Values.Concat(new IAgentPoolInternal[] {CompilerInstance.AgentPool}),
						localAgent,
						pools,
						ts,
						true);

					var iterationEnded = DateTime.Now;
					var iterationLimit = iterationStarted.Add(CompilerSettings.Default.AgentsUpdatePeriod);
					if (iterationLimit > iterationEnded)
					{
						Monitor.Wait(_syncRoot, iterationLimit.Subtract(iterationEnded));
					}

					ts.Cancel();

					while (! problemPools.IsEmpty)
					{
						AgentPoolClient pool;
						if (problemPools.TryDequeue(out pool))
						{
							RemovePool(pools, pool);
						}
					}
				}
			}
		}

		private void ConvertRegisteredAgents2AgentPools(
			IEnumerable<IAgentPoolInternal> knownAgentPools,
			RemoteCompilerService.AgentReqistrationMessage localAgent,
			ConcurrentDictionary<Uri, AgentPoolClient> pools,
			CancellationTokenSource ts,
			bool tryKnownAgents)
		{
			var poolsCount = pools.Count;
			var cookie = new ConvertRegisteredAgents2AgentPoolsToken(localAgent, pools, ts, tryKnownAgents);

			var tasks = knownAgentPools
				.Select(knownAgentPool => knownAgentPool.GetAgentsAsync())
				.Select(getAgentsTask => getAgentsTask.ContinueWith(ProcessAgents, cookie, ts.Token))
				.ToArray();

			Task.Factory.ContinueWhenAll(
				tasks,
				delegate
					{
						//Console.WriteLine("Pools count: was {0}, is {1}", poolsCount, cookie.Pools.Count);

						if (poolsCount < pools.Count)
						{
							ConvertRegisteredAgents2AgentPools(pools.Values, localAgent, pools, ts, false);
						}
					},
				ts.Token);
		}

		private void ProcessAgents(Task<IEnumerable<IAgent>> getAgentsTask, object state)
		{
			if (getAgentsTask.Exception != null)
			{
				Logger.LogException("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools.GetAgents",
							getAgentsTask.Exception);
				return;
			}

			if (getAgentsTask.Status != TaskStatus.RanToCompletion)
			{
				return;
			}

			var cookie = (ConvertRegisteredAgents2AgentPoolsToken) state;

			var tasks = new List<Task>();

			foreach (var agent in getAgentsTask.Result)
			{
				try
				{
					if (agent.Guid == cookie.LocalAgent.Guid)
					{
						continue;
					}

					if (!cookie.TryKnownAgents && CompilerInstance.AgentPool.HasAgent(agent.Guid))
					{
						continue;
					}

					if (agent.AgentPoolUrls.Any(cookie.Pools.ContainsKey))
					{
						continue;
					}

					Task<bool> registerAgentTask = null;

					foreach (var url in agent.AgentPoolUrls)
					{
						var pool = new AgentPoolClient(
							CompilerInstance.GetBinding(url),
							new EndpointAddress(url));

						if (registerAgentTask == null)
						{
							registerAgentTask = ((RemoteCompilerService.IAgentPool) pool).RegisterAgentAsync(cookie.LocalAgent).ContinueWith(
								delegate(Task task)
									{
										if (task.Status == TaskStatus.RanToCompletion)
										{
											AddPool(cookie.Pools, pool);
											return true;
										}
										if (task.Exception != null)
										{
											Logger.WarnFormat("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools: {0}", task.Exception.Message);
										}
										return false;
									},
								cookie.CancellationTokenSource.Token);
						}
						else
						{
							registerAgentTask.ContinueWith(
								delegate(Task<bool> task)
									{
										if (task.Status == TaskStatus.RanToCompletion && task.Result)
										{
											return true;
										}
										if (task.Exception != null)
										{
											Logger.WarnFormat("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools: {0}", task.Exception.Message);
										}

										((RemoteCompilerService.IAgentPool) pool).RegisterAgent(cookie.LocalAgent);
										AddPool(cookie.Pools, pool);
										return true;
									},
								cookie.CancellationTokenSource.Token);
						}
					}

					if (registerAgentTask != null)
					{
						tasks.Add(registerAgentTask);
					}
				}
				catch (CommunicationException e)
				{
					Logger.LogException("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools", e);
					// Remove AgentPool from list
				}
				catch (TimeoutException e)
				{
					Logger.LogException("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools", e);
					// Remove AgentPool from list
				}
			}

			Task.WaitAll(tasks.ToArray(), cookie.CancellationTokenSource.Token);
		}

		private class ConvertRegisteredAgents2AgentPoolsToken
		{
			private readonly RemoteCompilerService.AgentReqistrationMessage _localAgent;
			private readonly ConcurrentDictionary<Uri, AgentPoolClient> _pools;
			private readonly CancellationTokenSource _cancellationTokenSource;
			private readonly bool _tryKnownAgents;

			public ConvertRegisteredAgents2AgentPoolsToken(
				RemoteCompilerService.AgentReqistrationMessage localAgent,
				ConcurrentDictionary<Uri, AgentPoolClient> pools,
				CancellationTokenSource cancellationTokenSource, 
				bool tryKnownAgents)
			{
				_localAgent = localAgent;
				_pools = pools;
				_cancellationTokenSource = cancellationTokenSource;
				_tryKnownAgents = tryKnownAgents;
			}

			public RemoteCompilerService.AgentReqistrationMessage LocalAgent
			{
				get { return _localAgent; }
			}

			public ConcurrentDictionary<Uri, AgentPoolClient> Pools
			{
				get { return _pools; }
			}

			public CancellationTokenSource CancellationTokenSource
			{
				get { return _cancellationTokenSource; }
			}

			public bool TryKnownAgents
			{
				get { return _tryKnownAgents; }
			}
		}

		private static ConcurrentDictionary<Uri, AgentPoolClient> GetAgentPools(ServiceModelSectionGroup serviceModelSectionGroup)
		{
			var agentPoolContracts = new HashSet<string>();
			foreach (ServiceContractAttribute attribute in
				typeof (RemoteCompilerService.IAgentPool).GetCustomAttributes(typeof (ServiceContractAttribute), true))
			{
				agentPoolContracts.Add(attribute.ConfigurationName);
			}

			var pools = new ConcurrentDictionary<Uri, AgentPoolClient>();
			foreach (ChannelEndpointElement endpoint in serviceModelSectionGroup.Client.Endpoints)
			{
				if (agentPoolContracts.Contains(endpoint.Contract))
				{
					AddPool(pools, new AgentPoolClient(endpoint.Name));
				}
			}
			return pools;
		}

		private static void AddPool(ConcurrentDictionary<Uri, AgentPoolClient> pools, AgentPoolClient pool)
		{
			if (!pools.TryAdd(pool.Endpoint.ListenUri, pool))
			{
				Logger.WarnFormat("CompileServiceHost.UpdateAgents: Pool {0} already registered", pool.Endpoint.ListenUri);
			}
			else
			{
				Logger.InfoFormat("CompileServiceHost.UpdateAgents: Added pool '{0}'", pool.Endpoint.ListenUri);
			}
		}

		private static void RemovePool(ConcurrentDictionary<Uri, AgentPoolClient> pools, AgentPoolClient pool)
		{
			if (pools.TryRemove(pool.Endpoint.ListenUri, out pool))
			{
				Logger.Info("CompileServiceHost.UpdateAgents:" + string.Format("Removed pool '{0}'", pool.Endpoint.ListenUri));
			}
		}
	}
}