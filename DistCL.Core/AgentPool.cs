using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DistCL.Proxies;
using DistCL.Utils;

namespace DistCL
{
	internal class AgentAddEventArgs : EventArgs
	{
		public AgentAddEventArgs(IAgentProxy agent)
		{
			Agent = agent;
		}

		public IAgentProxy Agent { get; private set; }
	}

	internal class AgentPool : IAgentPoolProxy
	{
		private readonly Dictionary<Guid, RegisteredAgent> _agents = new Dictionary<Guid, RegisteredAgent>();

		readonly Logger _logger = new Logger("POOL");

		private readonly ReaderWriterLock _agentsLock = new ReaderWriterLock();
		private readonly ReaderWriterLock _weightsSnapshotLock = new ReaderWriterLock();
		private readonly ReaderWriterLock _agentsSnapshotLock = new ReaderWriterLock();
		private readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(1);
		private readonly Random _random = new Random();
		private Dictionary<string, List<MeasuredAgent>> _weightsSnapshot;
		private Agent[] _agentsSnapshot;

		private readonly ICompilerServicesCollection _compilerServices;

		public AgentPool(ICompilerServicesCollection compilerServices)
		{
			_compilerServices = compilerServices;
		}

		public ICompilerServicesCollection CompilerServices
		{
			get { return _compilerServices; }
		}

		public Logger Logger
		{
			get { return _logger; }
		}

		public event EventHandler<AgentAddEventArgs> AgentRegistered;

		public void RegisterAgent(IAgentProxy request)
		{
			_agentsLock.AcquireWriterLock(_lockTimeout);
			try
			{
				RegisteredAgent agent;
				if (!_agents.TryGetValue(request.Description.Guid, out agent))
				{
					agent = new RegisteredAgent(request);
					_agents.Add(request.Description.Guid, agent);
					_weightsSnapshot = null;
					_agentsSnapshot = null;
					Logger.InfoFormat("Add agent '{0}'", request.Description.Name);
					
					var handler = AgentRegistered;
					if (handler != null)
					{
						handler(this, new AgentAddEventArgs(request));
					}
				}
				else
				{
					if (! AgentEqualityComparer.AgentComparer.Equals(agent.Proxy.Description, request.Description))
					{
						_agents[request.Description.Guid] = new RegisteredAgent(request);
						_weightsSnapshot = null;
						_agentsSnapshot = null;
						Logger.DebugFormat("Update agent '{0}'", request.Description.Name);
					}
					else
					{
						agent.UpdateTime();
					}
				}
			}
			finally
			{
				_agentsLock.ReleaseWriterLock();
			}
		}

		public bool HasAgent(Guid guid)
		{
			_agentsLock.AcquireReaderLock(_lockTimeout);
			try
			{
				return _agents.ContainsKey(guid);
			}
			finally
			{
				_agentsLock.ReleaseReaderLock();
			}
		}

		public Agent[] GetAgents()
		{
			_agentsLock.AcquireReaderLock(_lockTimeout);
			try
			{
				if (_agentsSnapshot == null)
				{
					_agentsSnapshotLock.AcquireWriterLock(_lockTimeout);
					try
					{
						if (_agentsSnapshot == null)
						{
							Logger.Debug("Take agents list snapshot");
							_agentsSnapshot = _agents.Values
													.Select(agent => agent.Proxy.Description as Agent
																	?? new Agent(agent.Proxy.Description))
													.ToArray();
						}
					}
					finally
					{
						_agentsSnapshotLock.ReleaseWriterLock();
					}
				}

				return _agentsSnapshot;
			}
			finally
			{
				_agentsLock.ReleaseReaderLock();
			}
		}

		private List<MeasuredAgent> GetWeights(string compilerVersion)
		{
			_agentsLock.AcquireReaderLock(_lockTimeout);
			try
			{
				if (_weightsSnapshot == null)
				{
					_weightsSnapshotLock.AcquireWriterLock(_lockTimeout);
					try
					{
						if (_weightsSnapshot == null)
						{
							Logger.Debug("Calculate weights");

							var weights = new Dictionary<string, List<MeasuredAgent>>();
							var weightPositions = new Dictionary<string,int>();

							foreach (var agent in _agents.Values)
							{
								foreach (var version in agent.Proxy.Description.CompilerVersions)
								{
									int weightPosition;
									weightPositions.TryGetValue(version, out weightPosition);

									var item = new MeasuredAgent(agent.Proxy, weightPosition);

									List<MeasuredAgent> list;
									if (!weights.TryGetValue(version, out list))
									{
										list = new List<MeasuredAgent>();
										weights.Add(version, list);
									}
									list.Add(item);

									weightPositions[version] = weightPosition + item.Weight;
								}
							}

							//weights.Sort((a, b) => a.WeightStart.CompareTo(b.WeightStart));

							_weightsSnapshot = weights;
						}
					}
					finally
					{
						_weightsSnapshotLock.ReleaseWriterLock();
					}
				}

				return _weightsSnapshot[compilerVersion];
			}
			finally
			{
				_agentsLock.ReleaseReaderLock();
			}
		}

		public ICompiler GetRandomCompiler(string compilerVersion, out string agentName)
		{
			if (string.IsNullOrEmpty(compilerVersion))
				throw new ArgumentNullException("compilerVersion");

			ICompiler compiler = null;
			string name = null;

			SpinWait.SpinUntil(delegate
				{
					compiler = GetRandomCompilerInternal(compilerVersion, out name);
					return compiler != null;
				});

			agentName = name;
			return compiler;
		}

		private ICompiler GetRandomCompilerInternal(string compilerVersion, out string agentName)
		{
			agentName = null;
			var weights = GetWeights(compilerVersion);

			if (weights.Count == 0)
			{
				throw new Exception("No any agents with specified compiler found");
			}

			var target = new MeasuredAgent(_random.Next(weights[weights.Count - 1].WeightEnd));

			var result = weights.BinarySearch(target);

			var index = result < 0
							? ~result - 1
							: result;

			var compilerProvider = weights[index].Agent;
			var compiler = compilerProvider.GetCompiler();

			if (compiler != null)
			{
				Logger.DebugFormat("Found ready compiler '{0}'", compilerProvider.Description.Name);
				agentName = compilerProvider.Description.Name;
			}

			return compiler;
		}

		public void Clean(DateTime limit)
		{
			_agentsLock.AcquireReaderLock(_lockTimeout);
			try
			{
				var expired = (from item in _agents where item.Value.RegistrationTime < limit select item.Key).ToList();

				if (expired.Count > 0)
				{
					var cookie = _agentsLock.UpgradeToWriterLock(_lockTimeout);
					try
					{
						foreach (var item in expired)
						{
							Logger.InfoFormat("Remove agent '{0}'", _agents[item].Proxy.Description.Name);
							_agents.Remove(item);
						}

						_weightsSnapshot = null;
						_agentsSnapshot = null;
					}
					finally
					{
						_agentsLock.DowngradeFromWriterLock(ref cookie);
					}
				}
			}
			finally
			{
				_agentsLock.ReleaseReaderLock();
			}
		}

		#region IAgentPoolProxy

		public IAgentProxy Proxy
		{
			get { return CompilerServices.LocalAgentManager.AgentProxy; }
		}

		string ICompileCoordinatorProxy.Name { get { return "<local agent pool>"; } }

		IEnumerable<IAgent> IAgentPoolProxy.GetAgents()
		{
			return GetAgents();
		}

		Task<IEnumerable<IAgent>> IAgentPoolProxy.GetAgentsAsync()
		{
			return Task.FromResult((IEnumerable<IAgent>)GetAgents());
		}

		Task ICompileCoordinatorProxy.RegisterAgentAsync(IAgentProxy request)
		{
			return Task.Run(() => RegisterAgent(request));
		}

		bool ICompileCoordinatorProxy.IncreaseErrorCount()
		{
			return false;
		}

		void ICompileCoordinatorProxy.ResetErrorCount()
		{
		}

		public IAgent GetDescription()
		{
			return CompilerServices.LocalAgentManager.RegistrationMessage;
		}

		public Task<IAgent> GetDescriptionAsync()
		{
			return Task.FromResult(GetDescription());
		}

		#endregion

		#region RegisteredAgent

		private class RegisteredAgent
		{
			private readonly IAgentProxy _proxy;
			private DateTime _registrationTime = DateTime.Now;

			public RegisteredAgent(IAgentProxy proxy)
			{
				_proxy = proxy;
			}

			public IAgentProxy Proxy
			{
				get { return _proxy; }
			}

			public DateTime RegistrationTime
			{
				get { return _registrationTime; }
			}

			public void UpdateTime()
			{
				_registrationTime = DateTime.Now;
			}
		}

		#endregion

		#region MeasuredAgent

		private class MeasuredAgent : IComparable<MeasuredAgent>
		{
			private readonly IAgentProxy _agentProxy;
			private readonly int _weight;
			private readonly int _weightStart;
			private readonly int _weightEnd;

			public MeasuredAgent(int target)
			{
				_weight = 0;
				_weightStart = target;
				_weightEnd = target;
			}

			public MeasuredAgent(IAgentProxy agent, int weightStart)
			{
				_agentProxy = agent;
				_weight = Math.Max(0, _agentProxy.Description.Cores * (100 - _agentProxy.Description.CPUUsage));
				_weightStart = weightStart;
				_weightEnd = weightStart + _weight;
			}

			public IAgentProxy Agent
			{
				get { return _agentProxy; }
			}

			public int Weight
			{
				get { return _weight; }
			}

			public int WeightStart
			{
				get { return _weightStart; }
			}

			public int WeightEnd
			{
				get { return _weightEnd; }
			}

			public int CompareTo(MeasuredAgent other)
			{
				return WeightStart.CompareTo(other.WeightStart);
			}
		}

		#endregion
	}
}