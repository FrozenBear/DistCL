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
		[OperationContract]
		Agent GetDescription();

		[OperationContract(IsOneWay = true)]
		void RegisterAgent(Agent request);
	}

	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	public interface IAgentPool : ICompileCoordinator
	{
		[OperationContract]
		Agent[] GetAgents();
	}

	public interface IAgent
	{
		Guid Guid { get; }
		string Name { get; }
		int Cores { get; }
		int CPUUsage { get; }
		Uri[] AgentPoolUrls { get; }
		Uri[] CompilerUrls { get; }
		string[] CompilerVersions { get; }
	}

	#region Agent

	[DataContract(Namespace = GeneralSettings.CoordinatorMessageNamespace)]
	public class Agent : IEquatable<IAgent>, IAgent
	{
		public Agent()
		{
		}

		public Agent(IAgent agent)
		{
			Guid = agent.Guid;
			Name = agent.Name;
			Cores = agent.Cores;
			CPUUsage = agent.CPUUsage;
			AgentPoolUrls = agent.AgentPoolUrls;
			CompilerUrls = agent.CompilerUrls;
			CompilerVersions = agent.CompilerVersions;
		}

		public Agent(Guid guid, string name, int cores, int cpuUsage, Uri[] agentPoolUrls, Uri[] compilerUrls, string[] compilerVersions)
		{
			Guid = guid;
			Name = name;
			Cores = cores;
			CPUUsage = cpuUsage;
			AgentPoolUrls = agentPoolUrls;
			CompilerUrls = compilerUrls;
			CompilerVersions = compilerVersions;
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
		public string[] CompilerVersions { get; private set; }

		[DataMember]
		public int Cores { get; private set; }

		[DataMember]
		public int CPUUsage { get; private set; }

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

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}
			if (ReferenceEquals(this, obj))
			{
				return true;
			}
			if (obj.GetType() != this.GetType())
			{
				return false;
			}
			return AgentEqualityComparer.AgentComparer.Equals(this, (IAgent) obj);
		}

		public bool Equals(IAgent other)
		{
			return AgentEqualityComparer.AgentComparer.Equals(this, other);
		}

		public override int GetHashCode()
		{
			return AgentEqualityComparer.AgentComparer.GetHashCode(this);
		}

		#endregion
	}

	#endregion

	#region Comparer
	
	public sealed class AgentEqualityComparer : IEqualityComparer<IAgent>
	{
		private static readonly IEqualityComparer<IAgent> AgentComparerInstance = new AgentEqualityComparer();

		public static IEqualityComparer<IAgent> AgentComparer
		{
			get { return AgentComparerInstance; }
		}

		public bool Equals(IAgent x, IAgent y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}
			if (ReferenceEquals(x, null))
			{
				return false;
			}
			if (ReferenceEquals(y, null))
			{
				return false;
			}
			return x.Guid.Equals(y.Guid) &&
					string.Equals(x.Name, y.Name) &&
					x.Cores == y.Cores &&
					x.CPUUsage == y.CPUUsage &&
					UrlArraysEquals(x.AgentPoolUrls, y.AgentPoolUrls) &&
					UrlArraysEquals(x.CompilerUrls, y.CompilerUrls);
		}

		public int GetHashCode(IAgent obj)
		{
			unchecked
			{
				// TODO url arrays
				var hashCode = obj.Guid.GetHashCode();
				hashCode = (hashCode * 397) ^ (obj.Name != null ? obj.Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (obj.AgentPoolUrls != null ? obj.AgentPoolUrls.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (obj.CompilerUrls != null ? obj.CompilerUrls.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ obj.Cores;
				hashCode = (hashCode * 397) ^ obj.CPUUsage;
				return hashCode;
			}
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
	}
	
	#endregion
}