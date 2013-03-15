using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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

	[ContractClass(typeof(AgentContract))]
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

	#region AgentContract
	[ContractClassFor(typeof(IAgent))]
	abstract class AgentContract : IAgent
	{
		private string _name;
		private int _cores;
		private int _cpuUsage;
		private Uri[] _agentPoolUrls;
		private Uri[] _compilerUrls;
		private string[] _compilerVersions;

		protected AgentContract(
			string name,
			int cores,
			int cpuUsage,
			Uri[] agentPoolUrls,
			Uri[] compilerUrls,
			string[] compilerVersions)
		{
			Contract.Requires(!string.IsNullOrEmpty(name));
			Contract.Requires(cores > 0);
			Contract.Requires(cpuUsage >= 0 && cpuUsage <= 100);
			Contract.Requires(agentPoolUrls != null);
			Contract.Requires(compilerUrls != null);
			Contract.Requires(compilerVersions != null);
			Contract.Requires(compilerVersions.Length >= 0 || compilerUrls.Length == 0);

			_name = name;
			_cores = cores;
			_cpuUsage = cpuUsage;
			_agentPoolUrls = agentPoolUrls;
			_compilerUrls = compilerUrls;
			_compilerVersions = compilerVersions;
		}

		public abstract Guid Guid { get; }

		public string Name
		{
			get { return _name; }
			set
			{
				Contract.Requires(!string.IsNullOrEmpty(value));
				_name = value;
			}
		}

		public int Cores
		{
			get { return _cores; }
			set
			{
				Contract.Requires(value > 0);
				_cores = value;
			}
		}

		public int CPUUsage
		{
			get { return _cpuUsage; }
			set
			{
				Contract.Requires(value >= 0 && value <= 100);
				_cpuUsage = value;
			}
		}

		public Uri[] AgentPoolUrls
		{
			get { return _agentPoolUrls; }
			set
			{
				Contract.Requires(value != null);
				_agentPoolUrls = value;
			}
		}

		public Uri[] CompilerUrls
		{
			get { return _compilerUrls; }
			set
			{
				Contract.Requires(value != null);
				Contract.Requires(value.Length == 0 || CompilerVersions == null || CompilerVersions.Length > 0);
				_compilerUrls = value;
			}
		}

		public string[] CompilerVersions
		{
			get { return _compilerVersions; }
			set
			{
				Contract.Requires(value != null);
				Contract.Requires(value.Length >= 0 || CompilerUrls == null || CompilerUrls.Length == 0);
				_compilerVersions = value;
			}
		}

//		[ContractInvariantMethod]
//		private void ObjectInvariant()
//		{
//			Contract.Invariant(Name != null);
//			Contract.Invariant(AgentPoolUrls != null);
//			Contract.Invariant(CompilerUrls != null);
//			Contract.Invariant(CompilerVersions != null);
//		}
	}
	#endregion

	#region Agent

	[DataContract(Namespace = GeneralSettings.CoordinatorMessageNamespace)]
	public class Agent : IEquatable<IAgent>, IAgent
	{
		public Agent()
		{
		}

		public Agent(IAgent agent)
		{
			Contract.Requires(agent != null);

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
			Contract.Requires(name != null);
			Contract.Requires(agentPoolUrls != null);
			Contract.Requires(compilerUrls != null);
			Contract.Requires(compilerVersions != null);

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