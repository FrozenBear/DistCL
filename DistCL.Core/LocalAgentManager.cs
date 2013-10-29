using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using DistCL.Proxies;
using DistCL.Utils;

namespace DistCL
{
	internal class LocalAgentManager
	{
		private readonly Logger _logger = new Logger("AGENT");
		private readonly ICompilerServicesCollection _compilerServices;
		private readonly Agent _stub;
		private RemoteCompilerService.Agent _registrationMessage;
		private Agent _description;
		private IAgentProxy _agentProxy;

		private readonly PerformanceCounter _cpuCounter;
		DateTime _nextSample = DateTime.MinValue;
		private int _cpuUsage;

		public LocalAgentManager(ICompilerServicesCollection compilerServices, Uri[] agentPoolUrls, Uri[] compilerUrls)
		{
			Contract.Requires(compilerServices != null);
			Contract.Requires(agentPoolUrls != null);
			Contract.Requires(compilerUrls != null);

			_compilerServices = compilerServices;
			_stub = new Agent(
				Guid.NewGuid(),
				string.Format("{0}@{1}", CompilerSettings.Default.InstanceName, Environment.MachineName),
				_compilerServices.Compiler.MaxWorkersCount,
				-1,
				agentPoolUrls,
				compilerUrls,
				_compilerServices.Compiler.CompilerVersions.Keys.ToArray());

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
					_description = null;
					_logger.DebugFormat("Registration message updated (cpu = {0})", _registrationMessage.CPUUsage);
				}
				return _registrationMessage;
			}
		}

		public Agent Description
		{
			get
			{
				if (_description == null)
				{
					_description = new Agent(RegistrationMessage);
				}
				return _description;
			}
		}

		public IAgentProxy AgentProxy
		{
			get
			{
				if (_agentProxy == null || _agentProxy.Description.CPUUsage != CPUUsage)
				{
					_agentProxy = new LocalAgentProxy(RegistrationMessage, _compilerServices.Compiler, _compilerServices.AgentPool);
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