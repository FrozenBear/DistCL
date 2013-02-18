using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Configuration;
using System.Threading;
using System.Threading.Tasks;
using DistCL.RemoteCompilerService;
using DistCL.Utils;

namespace DistCL
{
	internal class NetworkBuilder
	{
		private readonly Logger _logger = new Logger("BUILDER");

		private readonly ServiceModelSectionGroup _serviceModelSectionGroup;
		private readonly IBindingsProvider _bindingsProvider;
		private readonly LocalCompilerManager _compilerManager;
		private readonly AgentPool _agentPool;

		private Agent[] _agentsSnapshot;
		private DateTime _nextBuild = DateTime.MinValue;  

		private readonly object _syncRoot = new object();
		private bool _closed;

		public NetworkBuilder(
			ServiceModelSectionGroup serviceModelSectionGroup,
			IBindingsProvider bindingsProvider,
			LocalCompilerManager compilerManager,
			AgentPool agentPool)
		{
			_compilerManager = compilerManager;
			_agentPool = agentPool;
			_serviceModelSectionGroup = serviceModelSectionGroup;
			_bindingsProvider = bindingsProvider;
		}

		public Logger Logger
		{
			get { return _logger; }
		}

		public AgentPool AgentPool
		{
			get { return _agentPool; }
		}

		private ServiceModelSectionGroup ServiceModelSectionGroup
		{
			get { return _serviceModelSectionGroup; }
		}

		private LocalCompilerManager CompilerManager
		{
			get { return _compilerManager; }
		}

		public IBindingsProvider BindingsProvider
		{
			get { return _bindingsProvider; }
		}

		public void Open()
		{
			_closed = false;

			var updateAgentsThread = new Thread(UpdateAgents) { IsBackground = true };
			updateAgentsThread.Start();
		}

		public void Close()
		{
			lock (_syncRoot)
			{
				_closed = true;
				Monitor.PulseAll(_syncRoot);
			}
		}

		private void UpdateAgents(object o)
		{
			var pools = GetAgentPools(ServiceModelSectionGroup);

			Logger.LogAgent("Init", CompilerManager.RegistrationMessage.Name);

			lock (_syncRoot)
			{
				var problemPools = new ConcurrentQueue<AgentPoolClient>();

				while (!_closed)
				{
					var iterationStarted = DateTime.Now;

					var ts = new CancellationTokenSource();
					var localAgent = CompilerManager.RegistrationMessage;

					foreach (var item in pools.Values)
					{
						var pool = item;

						((RemoteCompilerService.IAgentPool)pool)
							.RegisterAgentAsync(localAgent)
							.ContinueWith(
								delegate(Task task)
								{
									if (task.Exception != null)
									{
										Logger.LogException(string.Format("RegisterAgent ({0})", pool.Name), task.Exception);
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
								}); // without CancellationToken - final result check shouldn't be cancelled
					}

					AgentPool.RegisterAgent(CompilerManager.CompilerProvider);

					AgentPool.Clean(DateTime.Now.Subtract(CompilerSettings.Default.AgentsSilenceLimit));


					var agents = AgentPool.GetAgents();
					if (!ReferenceEquals(agents, _agentsSnapshot) || _nextBuild < DateTime.Now)
					{
						ConvertAgents2Pools(
							pools.Values.Concat(new IAgentPoolInternal[] {AgentPool}),
							localAgent,
							pools,
							ts,
							true);

						_nextBuild = DateTime.Now.Add(CompilerSettings.Default.NetworkBuildPeriod);
						_agentsSnapshot = agents;
					}

					var iterationEnded = DateTime.Now;
					var iterationLimit = iterationStarted.Add(CompilerSettings.Default.AgentsUpdatePeriod);
					if (iterationLimit > iterationEnded)
					{
						Monitor.Wait(_syncRoot, iterationLimit.Subtract(iterationEnded));
					}

					ts.Cancel();

					while (!problemPools.IsEmpty)
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

		private void ConvertAgents2Pools(
			IEnumerable<IAgentPoolInternal> knownAgentPools,
			RemoteCompilerService.AgentRegistrationMessage localAgent,
			ConcurrentDictionary<Uri, AgentPoolClient> pools,
			CancellationTokenSource ts,
			bool tryKnownAgents)
		{
			Logger.Debug("Converting agents to pools" + (tryKnownAgents ? "..." : " again..."));

			var poolsCount = pools.Count;
			var cookie = new ConvertRegisteredAgents2AgentPoolsToken(localAgent, pools, ts, tryKnownAgents);

			var tasks = knownAgentPools
				.Select(
					knownAgentPool =>
					knownAgentPool.GetAgentsAsync().ContinueWith(
					ProcessAgents, 
					new KeyValuePair<IAgentPoolInternal, object>(knownAgentPool, cookie), 
					ts.Token))
				.ToArray();

			Task.Factory.ContinueWhenAll(
				tasks,
				delegate
				{
					if (poolsCount < pools.Count)
					{
						ConvertAgents2Pools(pools.Values, localAgent, pools, ts, false);
					}
					else
					{
						Logger.Debug("Agents to pools conversion finished");
					}
				},
				ts.Token);
		}

		private void ProcessAgents(Task<IEnumerable<IAgent>> getAgentsTask, object state)
		{
			var statePair = (KeyValuePair<IAgentPoolInternal, object>) state;

			if (getAgentsTask.Exception != null)
			{
				Logger.WarnFormat("GetAgents ({0}): {1}", statePair.Key.Name, getAgentsTask.Exception.Message);
				return;
			}

			if (getAgentsTask.Status != TaskStatus.RanToCompletion)
			{
				return;
			}

			var cookie = (ConvertRegisteredAgents2AgentPoolsToken)statePair.Value;

			var tasks = new List<Task>();

			foreach (var agent in getAgentsTask.Result)
			{
//				try
//				{
					if (agent.Guid == cookie.LocalAgent.Guid)
					{
						continue;
					}

					if (!cookie.TryKnownAgents && AgentPool.HasAgent(agent.Guid))
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
							BindingsProvider.GetBinding(url),
							new EndpointAddress(url));

						if (registerAgentTask == null)
						{
							registerAgentTask = ((RemoteCompilerService.IAgentPool)pool).RegisterAgentAsync(cookie.LocalAgent).ContinueWith(
								delegate(Task task)
								{
									if (task.Status == TaskStatus.RanToCompletion)
									{
										AddPool(cookie.Pools, pool);
										return true;
									}
									if (task.Exception != null)
									{
										Logger.WarnFormat("RegisterAgent ({0}): {1}", pool.Name, task.Exception.Message);
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
										Logger.WarnFormat("RegisterAgent ({0}): {1}", pool.Name, task.Exception.Message);
									}

									((RemoteCompilerService.IAgentPool)pool).RegisterAgent(cookie.LocalAgent);
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
//				}
//				catch (CommunicationException e)
//				{
//					Utils.Logger.LogException("CompileServiceHost.UpdateAgents.ConvertAgents2Pools", e);
//					// Remove AgentPool from list
//				}
//				catch (TimeoutException e)
//				{
//					Utils.Logger.LogException("CompileServiceHost.UpdateAgents.ConvertAgents2Pools", e);
//					// Remove AgentPool from list
//				}
			}

			Task.WaitAll(tasks.ToArray(), cookie.CancellationTokenSource.Token);
		}

		private class ConvertRegisteredAgents2AgentPoolsToken
		{
			private readonly RemoteCompilerService.AgentRegistrationMessage _localAgent;
			private readonly ConcurrentDictionary<Uri, AgentPoolClient> _pools;
			private readonly CancellationTokenSource _cancellationTokenSource;
			private readonly bool _tryKnownAgents;

			public ConvertRegisteredAgents2AgentPoolsToken(
				RemoteCompilerService.AgentRegistrationMessage localAgent,
				ConcurrentDictionary<Uri, AgentPoolClient> pools,
				CancellationTokenSource cancellationTokenSource,
				bool tryKnownAgents)
			{
				_localAgent = localAgent;
				_pools = pools;
				_cancellationTokenSource = cancellationTokenSource;
				_tryKnownAgents = tryKnownAgents;
			}

			public RemoteCompilerService.AgentRegistrationMessage LocalAgent
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

		private ConcurrentDictionary<Uri, AgentPoolClient> GetAgentPools(ServiceModelSectionGroup serviceModelSectionGroup)
		{
			var agentPoolContracts = new HashSet<string>();
			foreach (ServiceContractAttribute attribute in
				typeof(RemoteCompilerService.IAgentPool).GetCustomAttributes(typeof(ServiceContractAttribute), true))
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

		private void AddPool(ConcurrentDictionary<Uri, AgentPoolClient> pools, AgentPoolClient pool)
		{
			if (!pools.TryAdd(pool.Endpoint.ListenUri, pool))
			{
				Logger.WarnFormat("Pool {0} already registered", pool.Endpoint.ListenUri);
			}
			else
			{
				Logger.InfoFormat("Added pool '{0}'", pool.Endpoint.ListenUri);
			}
		}

		private void RemovePool(ConcurrentDictionary<Uri, AgentPoolClient> pools, AgentPoolClient pool)
		{
			if (pools.TryRemove(pool.Endpoint.ListenUri, out pool))
			{
				Logger.InfoFormat("Removed pool '{0}'", pool.Endpoint.ListenUri);
			}
		}
	}
}
