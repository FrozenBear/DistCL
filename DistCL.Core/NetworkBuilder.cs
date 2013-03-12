using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Configuration;
using System.Threading;
using System.Threading.Tasks;
using DistCL.Proxies;
using DistCL.RemoteCompilerService;
using DistCL.Utils;

namespace DistCL
{
	internal class NetworkBuilder
	{
		private readonly Logger _logger = new Logger("BUILDER");
		private Thread _updateAgentsThread;

		private readonly ICompilerServicesCollection _compilerServices;

		private readonly object _syncRoot = new object();
		private bool _started;

		public NetworkBuilder(ICompilerServicesCollection compilerServices)
		{
			_compilerServices = compilerServices;
		}

		public Logger Logger
		{
			get { return _logger; }
		}

		public ICompilerServicesCollection CompilerServices
		{
			get { return _compilerServices; }
		}

		public void Start()
		{
			Logger.Debug("Network builder starting...");
			lock (_syncRoot)
			{
				if (Volatile.Read(ref _started))
				{
					throw new InvalidOperationException("Builder is already started");
				}

				_started = true;

				_updateAgentsThread = new Thread(UpdateAgents) {IsBackground = true};
				_updateAgentsThread.Start();
				Logger.Debug("Network builder started");
			}
		}

		public void Stop()
		{
			Logger.Debug("Network builder stopping...");
			lock (_syncRoot)
			{
				if (!Volatile.Read(ref _started))
				{
					throw new InvalidOperationException("Builder doesn't started");
				}

				_started = false;
				Monitor.PulseAll(_syncRoot);
			}

			_updateAgentsThread.Join();
			Logger.Debug("Network builder stopped");
		}

		private void UpdateAgents(object o)
		{
			IEnumerable<IAgent> agentsSnapshot = null;
			var nextBuild = DateTime.MinValue;  
			var nextInitialAgentsCheck = DateTime.MinValue;  

			var configPools = GetAgentPools(CompilerServices.ServiceModelSectionGroup);

			var pools = new ConcurrentDictionary<Guid, ICompileCoordinatorProxy>();
			pools.TryAdd(
				CompilerServices.LocalAgentManager.AgentProxy.Description.Guid,
				CompilerServices.LocalAgentManager.AgentProxy.GetCoordinator());

			Logger.DebugFormat("Init '{0}'", CompilerServices.LocalAgentManager.RegistrationMessage.Name);

			CompilerServices.AgentPool.AgentRegistered +=
				(sender, args) =>
				ConvertAgents2PoolsProcessAgent(
					pools,
					CompilerServices.LocalAgentManager, args.Agent.Description, () => args.Agent);

			lock (_syncRoot)
			{
				var problemPools = new ConcurrentQueue<ICompileCoordinatorProxy>();

				while (Volatile.Read(ref _started))
				{
					var iterationStarted = DateTime.Now;

					var ts = new CancellationTokenSource();
					var localAgent = CompilerServices.LocalAgentManager.AgentProxy;

					if (DateTime.Now > nextInitialAgentsCheck)
					{
						foreach (var configPool in configPools)
						{
							if (configPool.Guid.HasValue && pools.ContainsKey(configPool.Guid.Value))
								continue;

							var pool = configPool;
							configPool.Client.GetDescriptionAsync().ContinueWith(delegate(Task<IAgent> task)
								{
									if (task.Status == TaskStatus.RanToCompletion)
									{
										pool.Guid = task.Result.Guid;
										if (task.Result.AgentPoolUrls != null && task.Result.AgentPoolUrls.Length > 0)
										{
											var coordinator = new RemoteAgentProxy(CompilerServices.Bindings, task.Result).GetCoordinator();
											if (coordinator != null)
											{
												AddCoordinator(pools, coordinator);
												coordinator.RegisterAgent(localAgent);
											}
										}
									}
								});
						}
						nextInitialAgentsCheck = DateTime.Now.Add(CompilerSettings.Default.AgentsFromConfigCheckPeriod);
					}

					foreach (var item in pools.Values)
					{
						var pool = item;

						pool.RegisterAgentAsync(localAgent)
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

					CompilerServices.AgentPool.Clean(DateTime.Now.Subtract(CompilerSettings.Default.AgentsSilenceLimit));

					var agents = CompilerServices.LocalAgentManager.AgentProxy.GetAgentPool().GetAgents();
					if (!ReferenceEquals(agents, agentsSnapshot) || nextBuild < DateTime.Now)
					{
						ConvertAgents2PoolsRequestAgents(
							pools.Values.OfType<IAgentPoolProxy>(),
							CompilerServices.LocalAgentManager, 
							pools,
							ts,
							true);

						nextBuild = DateTime.Now.Add(CompilerSettings.Default.NetworkBuildPeriod);
						agentsSnapshot = agents;
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
						ICompileCoordinatorProxy pool;
						if (problemPools.TryDequeue(out pool))
						{
							RemoveCoordinator(pools, pool);
						}
					}
				}
			}
		}

		private void ConvertAgents2PoolsRequestAgents(
			IEnumerable<IAgentPoolProxy> knownAgentPools,
			LocalAgentManager localAgent,
			ConcurrentDictionary<Guid, ICompileCoordinatorProxy> pools,
			CancellationTokenSource ts,
			bool tryKnownAgents)
		{
			Logger.Debug("Converting agents to pools" + (tryKnownAgents ? "..." : " again..."));

			var poolsCount = pools.Count;
			var cookie = new ConvertAgents2PoolsToken(localAgent, pools, ts, tryKnownAgents);

			var tasks = knownAgentPools
				.Select(
					knownAgentPool =>
					knownAgentPool.GetAgentsAsync().ContinueWith(
						ConvertAgents2PoolsProcessAgents,
						new KeyValuePair<IAgentPoolProxy, object>(knownAgentPool, cookie),
						ts.Token))
				.ToArray();

			Task.Factory.ContinueWhenAll(
				tasks,
				delegate
				{
					if (poolsCount < pools.Count)
					{
						ConvertAgents2PoolsRequestAgents(pools.Values.OfType<IAgentPoolProxy>(), localAgent, pools, ts, false);
					}
					else
					{
						Logger.Debug("Agents to pools conversion finished");
					}
				},
				ts.Token);
		}

		private void ConvertAgents2PoolsProcessAgents(Task<IEnumerable<IAgent>> getAgentsTask, object state)
		{
			var statePair = (KeyValuePair<IAgentPoolProxy, object>) state;

			if (getAgentsTask.Exception != null)
			{
				Logger.WarnFormat("GetAgents ({0}): {1}", statePair.Key.Proxy.Description.Name, getAgentsTask.Exception.Message);
				return;
			}

			if (getAgentsTask.Status != TaskStatus.RanToCompletion)
			{
				return;
			}

			var cookie = (ConvertAgents2PoolsToken) statePair.Value;

			var tasks = new List<Task>();

			foreach (var agent in getAgentsTask.Result)
			{
//				if (agent.Guid == cookie.LocalAgent.Guid)
//				{
//					continue;
//				}

				if (!cookie.TryKnownAgents && CompilerServices.AgentPool.HasAgent(agent.Guid))
				{
					continue;
				}

				IAgent localAgent = agent;
				var task = ConvertAgents2PoolsProcessAgent(
					cookie.Pools,
					cookie.LocalAgent,
					agent,
					() => new RemoteAgentProxy(CompilerServices.Bindings, localAgent));
				if (task != null)
				{
					tasks.Add(task);
				}
			}

			Task.WaitAll(tasks.ToArray(), cookie.CancellationTokenSource.Token);
		}

		private Task ConvertAgents2PoolsProcessAgent(ConcurrentDictionary<Guid, ICompileCoordinatorProxy> pools, LocalAgentManager localAgent, IAgent agent, Func<IAgentProxy> getAgent)
		{
			if (!pools.ContainsKey(agent.Guid))
			{
				var proxy = getAgent();
				var coordinator = proxy.GetCoordinator();
				if (coordinator != null)
				{
					return coordinator.GetDescriptionAsync().ContinueWith(delegate(Task<IAgent> task)
						{
							if (task.Status == TaskStatus.RanToCompletion)
							{
								if (task.Result.Guid == proxy.Description.Guid)
								{
									AddCoordinator(pools, coordinator);
									coordinator.RegisterAgent(localAgent.AgentProxy);
								}
							}
						});
				}
			}

			return null;
		}

		private class ConvertAgents2PoolsToken
		{
			private readonly LocalAgentManager _localAgent;
			private readonly ConcurrentDictionary<Guid, ICompileCoordinatorProxy> _pools;
			private readonly CancellationTokenSource _cancellationTokenSource;
			private readonly bool _tryKnownAgents;

			public ConvertAgents2PoolsToken(
				LocalAgentManager localAgent,
				ConcurrentDictionary<Guid, ICompileCoordinatorProxy> pools,
				CancellationTokenSource cancellationTokenSource,
				bool tryKnownAgents)
			{
				_localAgent = localAgent;
				_pools = pools;
				_cancellationTokenSource = cancellationTokenSource;
				_tryKnownAgents = tryKnownAgents;
			}

			public LocalAgentManager LocalAgent
			{
				get { return _localAgent; }
			}

			public ConcurrentDictionary<Guid, ICompileCoordinatorProxy> Pools
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

		private class AgentFromConfig
		{
			public AgentFromConfig(IRemoteCompileCoordinator client)
			{
				Client = client;
			}

			public Guid? Guid { get; set; }

			public IRemoteCompileCoordinator Client { get; private set; }
		}

		private AgentFromConfig[] GetAgentPools(ServiceModelSectionGroup serviceModelSectionGroup)
		{
			var pools = new List<AgentFromConfig>();

			var coordinatorContracts = new HashSet<string>();
			foreach (ServiceContractAttribute attribute in
				typeof(RemoteCompilerService.ICompileCoordinator).GetCustomAttributes(typeof(ServiceContractAttribute), true))
			{
				coordinatorContracts.Add(attribute.ConfigurationName);
			}
			foreach (ChannelEndpointElement endpoint in serviceModelSectionGroup.Client.Endpoints)
			{
				if (coordinatorContracts.Contains(endpoint.Contract))
				{
					pools.Add(new AgentFromConfig(new CompileCoordinatorClient(endpoint.Name)));
				}
			}

			var poolContracts = new HashSet<string>();
			foreach (ServiceContractAttribute attribute in
				typeof(RemoteCompilerService.IAgentPool).GetCustomAttributes(typeof(ServiceContractAttribute), true))
			{
				poolContracts.Add(attribute.ConfigurationName);
			}

			foreach (ChannelEndpointElement endpoint in serviceModelSectionGroup.Client.Endpoints)
			{
				if (poolContracts.Contains(endpoint.Contract))
				{
					pools.Add(new AgentFromConfig(new AgentPoolClient(endpoint.Name)));
				}
			}
			return pools.ToArray();
		}

		private void AddCoordinator(ConcurrentDictionary<Guid, ICompileCoordinatorProxy> pools, ICompileCoordinatorProxy pool)
		{
			if (!pools.TryAdd(pool.Proxy.Description.Guid, pool))
			{
				Logger.WarnFormat("Coordinator {0} already registered", pool.Proxy.Description.Name);
			}
			else
			{
				Logger.InfoFormat("Added coordinator '{0}'", pool.Proxy.Description.Name);
			}
		}

		private void RemoveCoordinator(ConcurrentDictionary<Guid, ICompileCoordinatorProxy> pools, ICompileCoordinatorProxy pool)
		{
			if (pools.TryRemove(pool.Proxy.Description.Guid, out pool))
			{
				Logger.InfoFormat("Removed coordinator '{0}'", pool.Proxy.Description.Name);
			}
		}
	}
}
