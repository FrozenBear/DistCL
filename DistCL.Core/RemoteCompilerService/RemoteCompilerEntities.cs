﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DistCL.RemoteCompilerService
{
	partial class AgentPoolClient : IAgentPoolInternal
	{
		private const int MaxErrorCount = 3;
		private int _errorCount;

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
			return MaxErrorCount <= ++_errorCount;
		}
		public void ResetErrorCount()
		{
			_errorCount = 0;
		}
	}

	partial class Agent : IAgent
	{
		
	}

	partial class AgentReqistrationMessage : IAgent
	{
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
