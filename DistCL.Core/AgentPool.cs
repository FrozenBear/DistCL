using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DistCL
{
	internal class AgentPool
	{
		private readonly Dictionary<Guid, RegisteredAgent> _agents = new Dictionary<Guid, RegisteredAgent>();

		private readonly ReaderWriterLock _agentsLock = new ReaderWriterLock();
		private readonly ReaderWriterLock _weightLock = new ReaderWriterLock();
		private readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(1);
		private readonly Random _random = new Random();
		private int _currentWeightVersion = -1;
		private int _requiredWeightVersion;
		private List<MeasuredAgent> _weights;

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
					_requiredWeightVersion++;
				}
				else
				{
					if (!agent.Agent.Equals(request))
					{
						_agents[request.Agent.Guid] = new RegisteredAgent(request);
						_requiredWeightVersion ++;
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

		protected int RequiredWeightVersion
		{
			get { return _requiredWeightVersion; }
		}

		private List<MeasuredAgent> GetWeights()
		{
			_agentsLock.AcquireReaderLock(_lockTimeout);
			try
			{
				if (_currentWeightVersion != _requiredWeightVersion)
				{
					_weightLock.AcquireWriterLock(_lockTimeout);
					try
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

						_weights = weights;
						_currentWeightVersion = _requiredWeightVersion;
					}
					finally
					{
						_weightLock.ReleaseWriterLock();
					}
				}

				return _weights;
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
							_agents.Remove(item);
						}

						_requiredWeightVersion ++;
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

		public IEnumerable<Agent> GetAgents()
		{
			_agentsLock.AcquireReaderLock(_lockTimeout);
			try
			{
				return _agents.Values.Select(agent => agent.Agent.Agent);
			}
			finally
			{
				_agentsLock.ReleaseReaderLock();
			}
		}

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
	}
}