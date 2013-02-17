using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DistCL.RemoteCompilerService
{
	partial class AgentPoolClient : IAgentPoolInternal
	{
		private const int MaxErrorCount = 3;
		private int _errorCount;

		public string Name { get { return Endpoint.ListenUri.ToString(); } }

		IEnumerable<IAgent> IAgentPoolInternal.GetAgents()
		{
			return GetAgents();
		}

		Task<IEnumerable<IAgent>> IAgentPoolInternal.GetAgentsAsync()
		{
			return GetAgentsAsync().ContinueWith(task => (IEnumerable<IAgent>) task.Result);
		}

		public bool IncreaseErrorCount()
		{
			return Interlocked.Increment(ref _errorCount) >= MaxErrorCount;
		}

		public void ResetErrorCount()
		{
			_errorCount = 0;
		}
	}

	partial class Agent : IAgent
	{
	}

	partial class AgentRegistrationMessage : IAgent
	{
		public AgentRegistrationMessage(){}

		public AgentRegistrationMessage(
			Guid guid,
			string name,
			int cores,
			int cpuUsage,
			Uri[] agentPoolUrls,
			Uri[] compilerUrls)
		{
			Guid = guid;
			Name = name;
			Cores = cores;
			CPUUsage = cpuUsage;
			AgentPoolUrls = agentPoolUrls;
			CompilerUrls = compilerUrls;
		}

		public AgentRegistrationMessage(IAgent agent)
		{
			Guid = agent.Guid;
			Name = agent.Name;
			Cores = agent.Cores;
			CPUUsage = agent.CPUUsage;
			AgentPoolUrls = agent.AgentPoolUrls;
			CompilerUrls = agent.CompilerUrls;
		}

		Guid IAgent.Guid
		{
			get { return Guid; }
		}

		string IAgent.Name
		{
			get { return Name; }
		}

		int IAgent.Cores
		{
			get { return Cores; }
		}

		Uri[] IAgent.AgentPoolUrls
		{
			get { return AgentPoolUrls; }
		}

		Uri[] IAgent.CompilerUrls
		{
			get { return CompilerUrls; }
		}
	}
}
