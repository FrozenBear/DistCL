﻿using System;
using System.Diagnostics;
using DistCL.Proxies;
using DistCL.Utils;

namespace DistCL
{
	internal class LocalAgentManager
	{
		private readonly Logger _logger = new Logger("AGENT");
		private readonly AgentPool _agentPool;
		private readonly ICompiler _compiler;
		private readonly Agent _stub;
		private RemoteCompilerService.Agent _registrationMessage;
		private IAgentProxy _agentProxy;

		private readonly PerformanceCounter _cpuCounter;
		DateTime _nextSample = DateTime.MinValue;
		private int _cpuUsage;

		public LocalAgentManager(AgentPool agentPool, Compiler compiler, Uri[] agentPoolUrls, Uri[] compilerUrls)
		{
			// TODO rework
			agentPool.Manager = this;

			_agentPool = agentPool;
			_compiler = compiler;
			_stub = new Agent(
				Guid.NewGuid(),
				string.Format("{0}@{1}", CompilerSettings.Default.InstanceName, Environment.MachineName),
				compiler.MaxWorkersCount,
				-1,
				agentPoolUrls,
				compilerUrls,
				compiler.CompilerVersions);

			_cpuCounter = new PerformanceCounter
				{
					CategoryName = "Processor",
					CounterName = "% Processor Time",
					InstanceName = "_Total"
				};
			_cpuCounter.NextValue(); // initial call

			_logger.Debug("Created local agent description");
		}

		public RemoteCompilerService.Agent RegistrationMessage
		{
			get
			{
				if (_registrationMessage == null || _registrationMessage.CPUUsage != CPUUsage)
				{
					_registrationMessage = new RemoteCompilerService.Agent(
						_stub.Guid, _stub.Name, _stub.Cores, CPUUsage, _stub.AgentPoolUrls, _stub.CompilerUrls, _stub.CompilerVersions);
					_logger.DebugFormat("Registration message updated (cpu = {0})", _registrationMessage.CPUUsage);
				}
				return _registrationMessage;
			}
		}

		public IAgentProxy AgentProxy
		{
			get
			{
				if (_agentProxy == null || _agentProxy.Description.CPUUsage != CPUUsage)
				{
					_agentProxy = new LocalAgentProxy(RegistrationMessage, _compiler, _agentPool);
				}
				return _agentProxy;
			}
		}

		private int CPUUsage
		{
			get
			{
				if (DateTime.Now > _nextSample)
				{
					_cpuUsage = GetIntUsage(_cpuCounter.NextValue());
					_nextSample = DateTime.Now.AddSeconds(15);
				}
				return _cpuUsage;
			}
		}

		private static int GetIntUsage(float usage)
		{
			return 20 * ((int)(usage / 20));
		}

		private class LocalAgentProxy : IAgentProxy
		{
			private readonly IAgent _description;
			private readonly ICompiler _compiler;
			private readonly IAgentPoolProxy _agentPool;

			public LocalAgentProxy(IAgent description, ICompiler compiler, IAgentPoolProxy agentPool)
			{
				_compiler = compiler;
				_agentPool = agentPool;
				_description = description;
			}

			public IAgent Description
			{
				get { return _description; }
			}

			public ICompiler GetCompiler()
			{
				return _compiler.IsReady() ? _compiler : null;
			}

			public IAgentPoolProxy GetAgentPool()
			{
				return _agentPool;
			}

			public ICompileCoordinatorProxy GetCoordinator()
			{
				return _agentPool;
			}
		}
	}
}