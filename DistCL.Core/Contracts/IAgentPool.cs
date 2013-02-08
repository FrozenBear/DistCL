using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace DistCL
{
	[ServiceContract]
	public interface ICompileCoordinator
	{
		[OperationContract(IsOneWay = true)]
		void RegisterAgent(AgentRequest request);
	}

	[ServiceContract]
	public interface IAgentPool : ICompileCoordinator
	{
		[OperationContract]
		IEnumerable<Agent> GetAgents();
	}

//	[CollectionDataContract(ItemName = "item", Namespace = GeneralSettings.Namespace)]
//	public class AgentList : List<Agent>
//	{
//		public AgentList(IEnumerable<Agent> collection) : base(collection)
//		{
//		}
//	}

	[MessageContract]
	public class AgentRequest : Agent
	{
	}

	[MessageContract]
	public class Agent : IEquatable<Agent>
	{
		public Agent()
		{
		}

		public Agent(Guid guid, string name, int cores, Uri[] agentPoolUrls, Uri[] compilerUrls)
		{
			Guid = guid;
			Name = name;
			Cores = cores;
			AgentPoolUrls = agentPoolUrls;
			CompilerUrls = compilerUrls;
		}

		[MessageBodyMember]
		public Guid Guid { get; private set; }

		[MessageBodyMember]
		public string Name { get; private set; }

		[MessageBodyMember]
		public Uri[] AgentPoolUrls { get; private set; }

		[MessageBodyMember]
		public Uri[] CompilerUrls { get; private set; }

		[MessageBodyMember]
		public int Cores { get; private set; }

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
}