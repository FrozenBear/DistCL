using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace DistCL
{
	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	public interface ICompileCoordinator
	{
		[OperationContract(IsOneWay = true)]
		void RegisterAgent(AgentReqistrationMessage request);
	}

	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	public interface IAgentPool : ICompileCoordinator
	{
		[OperationContract]
		Agent[] GetAgents();
	}

	internal interface IAgentPoolInternal
	{
		IEnumerable<IAgent> GetAgents();
		System.Threading.Tasks.Task<IEnumerable<IAgent>> GetAgentsAsync();
	}

	public interface IAgent
	{
		Guid Guid { get; }

		string Name { get; }

		int Cores { get; }

		Uri[] AgentPoolUrls { get; }

		Uri[] CompilerUrls { get; }
	}

	#region Agent

	[DataContract(Namespace = GeneralSettings.CoordinatorMessageNamespace)]
	public class Agent : IEquatable<Agent>, IAgent
	{
		public Agent()
		{
		}

		public Agent(IAgent agent)
		{
			Guid = agent.Guid;
			Name = agent.Name;
			Cores = agent.Cores;
			AgentPoolUrls = agent.AgentPoolUrls;
			CompilerUrls = agent.CompilerUrls;
		}

		public Agent(Guid guid, string name, int cores, Uri[] agentPoolUrls, Uri[] compilerUrls)
		{
			Guid = guid;
			Name = name;
			Cores = cores;
			AgentPoolUrls = agentPoolUrls;
			CompilerUrls = compilerUrls;
		}

		[DataMember]
		public Guid Guid { get; private set; }

		[DataMember]
		public string Name { get; private set; }

		[DataMember]
		public Uri[] AgentPoolUrls { get; private set; }

		[DataMember]
		public Uri[] CompilerUrls { get; private set; }

		[DataMember]
		public int Cores { get; private set; }

		public override string ToString()
		{
			return string.Format(
				"Guid: {0}, Name: {1}, Cores: {2}, AgentPoolUrls: {3}, CompilerUrls: {4}",
				Guid,
				Name,
				Cores,
				string.Join(", ", AgentPoolUrls.Select(url => url.ToString()).ToArray()),
				string.Join(", ", CompilerUrls.Select(url => url.ToString()).ToArray()));
		}

		#region Equals

		public bool Equals(Agent other)
		{
			if (Guid.Equals(other.Guid) &&
				string.Equals(Name, other.Name) &&
				Cores == other.Cores &&
				AgentPoolUrls.Length == other.AgentPoolUrls.Length &&
				CompilerUrls.Length == other.CompilerUrls.Length)
			{
				return UrlArraysEquals(AgentPoolUrls, other.AgentPoolUrls) && UrlArraysEquals(CompilerUrls, other.CompilerUrls);
			}

			return false;
		}

		private static bool UrlArraysEquals(Uri[] a, Uri[] b)
		{
			for (var i = 0; i < a.Length; i++)
			{
				if (!a[i].Equals(b[i]) && !Array.Exists(b, url => Equals(url, a[i])))
				{
					return false;
				}
			}

			return true;
		}

		#endregion
	}

	[DataContract(Namespace = GeneralSettings.CoordinatorMessageNamespace)]
	public class AgentReqistrationMessage : Agent
	{
	}

	#endregion
}