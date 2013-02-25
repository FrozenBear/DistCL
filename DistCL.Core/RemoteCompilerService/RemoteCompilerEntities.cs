using System;
using System.Threading;
using System.Threading.Tasks;

namespace DistCL.RemoteCompilerService
{

	internal interface IRemoteCompileCoordinator
	{
		IAgent GetDescription();
		Task<IAgent> GetDescriptionAsync();

		void RegisterAgent(Agent agent);
		Task RegisterAgentAsync(Agent agent);
	}

	partial class CompileCoordinatorClient : IRemoteCompileCoordinator
	{
		IAgent IRemoteCompileCoordinator.GetDescription()
		{
			return GetDescription();
		}

		Task<IAgent> IRemoteCompileCoordinator.GetDescriptionAsync()
		{
			return GetDescriptionAsync().ContinueWith(task => (IAgent)task.Result);
		}
	}

	partial class AgentPoolClient : IRemoteCompileCoordinator
	{
		IAgent IRemoteCompileCoordinator.GetDescription()
		{
			return GetDescription();
		}

		Task<IAgent> IRemoteCompileCoordinator.GetDescriptionAsync()
		{
			return GetDescriptionAsync().ContinueWith(task => (IAgent)task.Result);
		}
	}

/*
	partial class AgentPoolClient //: IAgentPoolInternal
	{
		public string Name { get { return Endpoint.ListenUri.ToString(); } }

//		IEnumerable<IAgent> IAgentPoolInternal.GetAgents()
//		{
//			return GetAgents();
//		}
//
//		Task<IEnumerable<IAgent>> IAgentPoolInternal.GetAgentsAsync()
//		{
//			return GetAgentsAsync().ContinueWith(task => (IEnumerable<IAgent>) task.Result);
//		}

	}
*/

	partial class Agent : IAgent
	{
	}

	partial class Agent : IAgent
	{
		public Agent(){}

		public Agent(
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

		public Agent(IAgent agent)
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
