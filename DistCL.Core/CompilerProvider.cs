using System;
using System.Diagnostics;
using System.ServiceModel;
using DistCL.RemoteCompilerService;
using DistCL.Utils;

namespace DistCL
{
	internal interface ICompilerProvider
	{
		IAgent Description { get; }
		ICompiler GetCompiler();
	}

	internal class LocalCompilerProvider : ICompilerProvider
	{
		private readonly Logger _logger = new Logger("AGENT");
		private readonly ICompiler _compiler;
		private readonly Agent _stub;
		private RemoteCompilerService.AgentRegistrationMessage _registrationMessage;
		private ICompilerProvider _snapshot;

		private readonly PerformanceCounter _cpuCounter;
		DateTime _nextSample = DateTime.MinValue;
		private int _cpuUsage;

		public LocalCompilerProvider(ICompiler compiler, Uri[] agentPoolUrls, Uri[] compilerUrls)
		{
			_compiler = compiler;
			_stub = new Agent(
				Guid.NewGuid(),
				CompilerSettings.Default.InstanceName,
				Math.Max(1, Environment.ProcessorCount - 1),
				-1,
				agentPoolUrls,
				compilerUrls);

			_cpuCounter = new PerformanceCounter
				{
					CategoryName = "Processor",
					CounterName = "% Processor Time",
					InstanceName = "_Total"
				};
			_cpuCounter.NextValue(); // initial call

			_logger.Debug("Created local agent description");
		}

		public RemoteCompilerService.AgentRegistrationMessage RegistrationMessage
		{
			get
			{
				if (_registrationMessage == null || _registrationMessage.CPUUsage != CPUUsage)
				{
					_registrationMessage = new RemoteCompilerService.AgentRegistrationMessage(
						_stub.Guid, _stub.Name, _stub.Cores, CPUUsage, _stub.AgentPoolUrls, _stub.CompilerUrls);
					_logger.DebugFormat("Registration message updated (cpu = {0})", _registrationMessage.CPUUsage);
				}
				return _registrationMessage;
			}
		}

		public ICompilerProvider Snapshot
		{
			get
			{
				if (_snapshot == null || _snapshot.Description.CPUUsage != CPUUsage)
				{
					_snapshot = new LocalCompilerProviderSnapshot(_compiler, RegistrationMessage);
				}
				return _snapshot;
			}
		}

		IAgent ICompilerProvider.Description
		{
			get { return RegistrationMessage; }
		}

		public ICompiler GetCompiler()
		{
			return _compiler;
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

		private class LocalCompilerProviderSnapshot : ICompilerProvider
		{
			private readonly ICompiler _compiler;
			private readonly IAgent _description;

			public LocalCompilerProviderSnapshot(ICompiler compiler, IAgent description)
			{
				_compiler = compiler;
				_description = description;
			}

			public ICompiler GetCompiler()
			{
				return _compiler;
			}

			public IAgent Description
			{
				get { return _description; }
			}
		}
	}

	internal class RemoteCompilerProvider : ICompilerProvider
	{
		private readonly IBindingsProvider _bindingsProvider;
		private readonly Agent _description;

		public RemoteCompilerProvider(IBindingsProvider bindingsProvider, Agent description)
		{
			_bindingsProvider = bindingsProvider;
			_description = description;
		}

		public IAgent Description
		{
			get { return _description; }
		}

		public ICompiler GetCompiler()
		{
			foreach (var url in _description.CompilerUrls)
			{
				RemoteCompilerService.ICompiler compiler = new CompilerClient(_bindingsProvider.GetBinding(url), new EndpointAddress(url));

				return compiler.IsReady() ? new CompilerProxy(compiler) : null;
			}

			throw new InvalidOperationException();
		}

		private class CompilerProxy : ICompiler
		{
			private readonly RemoteCompilerService.ICompiler _compiler;

			public CompilerProxy(RemoteCompilerService.ICompiler compiler)
			{
				_compiler = compiler;
			}

			public bool IsReady()
			{
				return _compiler.IsReady();
			}

			public CompileOutput Compile(CompileInput localInput)
			{
				var remoteInput = new RemoteCompilerService.CompileInput
					{
						Arguments = localInput.Arguments,
						Src = localInput.Src,
						SrcLength = localInput.SrcLength,
						SrcName = localInput.SrcName
					};

				RemoteCompilerService.CompileOutput remoteOutput = _compiler.Compile(remoteInput);

				return
					new CompileOutput(
						new CompileStatus(
							remoteOutput.Status.Success,
							remoteOutput.Status.ExitCode,
							remoteOutput.Status.Cookies),
						remoteOutput.ResultData);
			}
		}
	}
}