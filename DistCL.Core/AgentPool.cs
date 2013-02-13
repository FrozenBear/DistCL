using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DistCL
{
	internal class AgentPool : IAgentPoolInternal
	{
		private readonly Dictionary<Guid, RegisteredAgent> _agents = new Dictionary<Guid, RegisteredAgent>();

		private readonly ReaderWriterLock _agentsLock = new ReaderWriterLock();
		private readonly ReaderWriterLock _weightsSnapshotLock = new ReaderWriterLock();
		private readonly ReaderWriterLock _agentsSnapshotLock = new ReaderWriterLock();
		private readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(1);
		private readonly Random _random = new Random();
		private List<MeasuredAgent> _weightsSnapshot;
		private Agent[] _agentsSnapshot;

		// TODO total optimization

		public void RegisterAgent(ICompilerProvider request)
		{
			_agentsLock.AcquireWriterLock(_lockTimeout);
			try
			{
				RegisteredAgent agent;
				if (!_agents.TryGetValue(request.Agent.Guid, out agent))
				{
					agent = new RegisteredAgent(request);
					_agents.Add(request.Agent.Guid, agent);
					_weightsSnapshot = null;
					_agentsSnapshot = null;
					Logger.Log("AgentPool.RegisterAgent.New", request.Agent);
				}
				else
				{
					if (!agent.Agent.Agent.Equals(request.Agent))
					{
						_agents[request.Agent.Guid] = new RegisteredAgent(request);
						_weightsSnapshot = null;
						_agentsSnapshot = null;
						Logger.Log("AgentPool.RegisterAgent.Update", request.Agent);
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
							_agentsSnapshot = _agents.Values.Select(agent => new Agent(agent.Agent.Agent)).ToArray();
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
							var weights = new List<MeasuredAgent>();
							int weightPosition = 0;

							foreach (var agent in _agents.Values)
							{
								var item = new MeasuredAgent(agent.Agent, weightPosition);
								weightPosition += item.Weight;
								weights.Add(item);
							}

							weights.Sort((a, b) => a.WeightStart.CompareTo(b.WeightStart));

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

		public ICompilerProvider GetRandomCompiler()
		{
			var weights = GetWeights();

			if (weights.Count == 0)
				return null;

			var target = new MeasuredAgent(_random.Next(weights[weights.Count - 1].WeightEnd));

			var result = weights.BinarySearch(target);

			int index = result < 0
							? ~result - 1
							: result;

			return weights[index].Agent;
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
							Logger.Log("AgentPool.RegisterAgent.Remove", _agents[item].Agent.Agent);
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

		IEnumerable<IAgent> IAgentPoolInternal.GetAgents()
		{
			return GetAgents();
		}

		public Task<IEnumerable<IAgent>> GetAgentsAsync()
		{
			return Task.FromResult((IEnumerable<IAgent>)GetAgents());
		}

		#endregion

		#region RegisteredAgent

		private class RegisteredAgent
		{
			private readonly ICompilerProvider _agent;
			private DateTime _registrationTime = DateTime.Now;

			public RegisteredAgent(ICompilerProvider agent)
			{
				_agent = agent;
			}

			public ICompilerProvider Agent
			{
				get { return _agent; }
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
			private readonly ICompilerProvider _agent;
			private readonly int _weight;
			private readonly int _weightStart;
			private readonly int _weightEnd;

			public MeasuredAgent(int target)
			{
				_weight = 0;
				_weightStart = target;
				_weightEnd = target;
			}

			public MeasuredAgent(ICompilerProvider agent, int weightStart)
			{
				_agent = agent;
				_weight = _agent.Agent.Cores;
				_weightStart = weightStart;
				_weightEnd = weightStart + _weight;
			}

			public ICompilerProvider Agent
			{
				get { return _agent; }
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
				return _weightStart.CompareTo(other._weightStart);
			}
		}

		#endregion
	}
}