using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

	internal class AgentPool : IAgentPoolInternal
	{
		private readonly Dictionary<Guid, RegisteredAgent> _agents = new Dictionary<Guid, RegisteredAgent>();

		readonly Logger _logger = new Logger("POOL");

		private readonly ReaderWriterLock _agentsLock = new ReaderWriterLock();
		private readonly ReaderWriterLock _weightsSnapshotLock = new ReaderWriterLock();
		private readonly ReaderWriterLock _agentsSnapshotLock = new ReaderWriterLock();
		private readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(1);
		private readonly Random _random = new Random();
		private List<MeasuredAgent> _weightsSnapshot;
		private Agent[] _agentsSnapshot;

		public Logger Logger
		{
			get { return _logger; }
		}

		// TODO total optimization

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
					Logger.LogAgent("Add agent", request.Description.Name);
					
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
						Logger.LogAgent("Update agent", request.Description.Name);
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

		private List<MeasuredAgent> GetWeights()
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

							var weights = new List<MeasuredAgent>();
							var weightPosition = 0;

							foreach (var agent in _agents.Values)
							{
								var item = new MeasuredAgent(agent.Proxy, weightPosition);
								weightPosition += item.Weight;
								weights.Add(item);
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

				return _weightsSnapshot;
			}
			finally
			{
				_agentsLock.ReleaseReaderLock();
			}
		}

		public ICompiler GetRandomCompiler()
		{
			ICompiler compiler = null;

			SpinWait.SpinUntil(delegate
				{
					compiler = GetRandomCompilerInternal();
					return compiler != null;
				});

			return compiler;
		}

		private ICompiler GetRandomCompilerInternal()
		{
			var weights = GetWeights();

			if (weights.Count == 0)
				return null;

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
							Logger.LogAgent("Remove agent", _agents[item].Proxy.Description.Name);
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

		#region IAgentPoolInternal

		string ICompileCoordinatorInternal.Name { get { return "<local agent pool>"; } }

		IEnumerable<IAgent> IAgentPoolInternal.GetAgents()
		{
			return GetAgents();
		}

		Task<IEnumerable<IAgent>> IAgentPoolInternal.GetAgentsAsync()
		{
			return Task.FromResult((IEnumerable<IAgent>)GetAgents());
		}

		Task ICompileCoordinatorInternal.RegisterAgentAsync(IAgentProxy request)
		{
			return Task.Run(() => RegisterAgent(request));
		}

		bool ICompileCoordinatorInternal.IncreaseErrorCount()
		{
			return false;
		}

		void ICompileCoordinatorInternal.ResetErrorCount()
		{
		}

		public IAgent GetDescription()
		{
			return Manager.RegistrationMessage;
		}

		public Task<IAgent> GetDescriptionAsync()
		{
			return Task.FromResult(GetDescription());
		}

		// TODO rework
		internal LocalAgentManager Manager { get; set; }
		public IAgentProxy Proxy
		{
			get { return Manager.AgentProxy; }
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