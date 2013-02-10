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

			Logger.Log("CompileServiceHost.UpdateAgents.Init", localAgent);

			lock (_syncRoot)
			{
				while (! _closed)
				{
					var iterationStarted = DateTime.Now;

					var ts = new CancellationTokenSource();

					foreach (var pool in pools.Values)
					{
						try
						{
//							RemoteCompilerService.IAgentPool knownPool = pool;
//
//							var registerAgentTask = knownPool.RegisterAgentAsync(localAgent);
//
//							var getAgentsTask = registerAgentTask.ContinueWith(delegate { return knownPool.GetAgents(); }, ts.Token);
//
//							var processAgentsTask = getAgentsTask.ContinueWith(
//								delegate(Task<RemoteCompilerService.Agent[]> agentsTask)
//									{
//										foreach (RemoteCompilerService.Agent remoteAgent in agentsTask.Result)
//										{
//											foreach (Uri poolUrl in remoteAgent.AgentPoolUrls)
//											{
//												pools.TryAdd(
//													poolUrl,
//													new AgentPoolClient(
//														GetBinding(poolUrl),
//														new EndpointAddress(poolUrl)));
//											}
//										}
//									},
//								ts.Token);
//
//							agentsTasks.Add(processAgentsTask);
							
							((RemoteCompilerService.IAgentPool)pool).RegisterAgent(localAgent);
						}
						catch (CommunicationException e)
						{
							Logger.Log("CompileServiceHost.UpdateAgents.RegisterAgent", e.Message);
							// Remove AgentPool from list
						}
						catch (TimeoutException e)
						{
							Logger.Log("CompileServiceHost.UpdateAgents.RegisterAgent", e.Message);
							// Remove AgentPool from list
						}
					}

					CompilerInstance.AgentPool.RegisterAgent(CompilerProvider);

					CompilerInstance.AgentPool.Clean(DateTime.Now.Subtract(CompilerSettings.Default.AgentsSilenceLimit));

					ConvertRegisteredAgents2AgentPools(
						pools.Values.Concat(new IAgentPoolInternal[] {CompilerInstance.AgentPool}),
						localAgent,
						pools,
						ts);

					var iterationEnded = DateTime.Now;
					var iterationLimit = iterationStarted.Add(CompilerSettings.Default.AgentsUpdatePeriod);
					if (iterationLimit > iterationEnded)
					{
						Monitor.Wait(_syncRoot, iterationLimit.Subtract(iterationEnded));
					}

					ts.Cancel();
				}
			}
		}

		private void ConvertRegisteredAgents2AgentPools(
			IEnumerable<IAgentPoolInternal> knownAgentPools,
			RemoteCompilerService.AgentReqistrationMessage localAgent,
			ConcurrentDictionary<Uri, AgentPoolClient> pools,
			CancellationTokenSource ts)
		{
			foreach (var knownAgentPool in knownAgentPools)
			{
				IEnumerable<IAgent> agents = null;
				try
				{
					agents = knownAgentPool.GetAgents();
				}
				catch (CommunicationException e)
				{
					Logger.Log("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools", e.Message);
					// Remove AgentPool from list
				}
				catch (TimeoutException e)
				{
					Logger.Log("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools", e.Message);
					// Remove AgentPool from list
				}

				if (agents == null)
					continue;

				foreach (var agent in agents)
				{
					try
					{
						if (agent.Guid == localAgent.Guid)
						{
							continue;
						}

						if (agent.AgentPoolUrls.Any(pools.ContainsKey))
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
								registerAgentTask = ((RemoteCompilerService.IAgentPool) pool).RegisterAgentAsync(localAgent).ContinueWith(
									delegate(Task task)
										{
											if (task.Status == TaskStatus.RanToCompletion)
											{
												pools.TryAdd(pool.Endpoint.ListenUri, pool);
												return true;
											}
											if (task.Exception != null)
											{
												Logger.Warning("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools ", task.Exception.Message);
											}
											return false;
										},
									ts.Token);
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
												Logger.Warning("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools", task.Exception.Message);
											}

											((RemoteCompilerService.IAgentPool) pool).RegisterAgent(localAgent);
											pools.TryAdd(pool.Endpoint.ListenUri, pool);
											return true;
										},
									ts.Token);
							}
						}
					}
					catch (CommunicationException e)
					{
						Logger.Log("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools", e.Message);
						// Remove AgentPool from list
					}
					catch (TimeoutException e)
					{
						Logger.Log("CompileServiceHost.UpdateAgents.ConvertRegisteredAgents2AgentPools", e.Message);
						// Remove AgentPool from list
					}
				}
			}
		}

		private static
			ConcurrentDictionary<Uri, AgentPoolClient> GetAgentPools(ServiceModelSectionGroup serviceModelSectionGroup)
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
					if (! pools.TryAdd(endpoint.Address, new AgentPoolClient(endpoint.Name)))
					{
						Logger.Warning("CompileServiceHost.UpdateAgents.Init", string.Format("Agents pool {0} already registered", endpoint.Address));
					}
				}
			}
			return pools;
		}
	}
}