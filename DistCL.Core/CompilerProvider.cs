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

	internal class LocalCompilerManager
	{
		private readonly Logger _logger = new Logger("AGENT");
		private readonly ICompiler _compiler;
		private readonly Agent _stub;
		private RemoteCompilerService.AgentRegistrationMessage _registrationMessage;
		private ICompilerProvider _compilerProvider;

		private readonly PerformanceCounter _cpuCounter;
		DateTime _nextSample = DateTime.MinValue;
		private int _cpuUsage;

		public LocalCompilerManager(Compiler compiler, Uri[] agentPoolUrls, Uri[] compilerUrls)
		{
			_compiler = compiler;
			_stub = new Agent(
				Guid.NewGuid(),
				CompilerSettings.Default.InstanceName,
				compiler.MaxWorkersCount,
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

		public ICompilerProvider CompilerProvider
		{
			get
			{
				if (_compilerProvider == null || _compilerProvider.Description.CPUUsage != CPUUsage)
				{
					_compilerProvider = new LocalCompilerProvider(_compiler, RegistrationMessage);
				}
				return _compilerProvider;
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

		private class LocalCompilerProvider : ICompilerProvider
		{
			private readonly ICompiler _compiler;
			private readonly IAgent _description;

			public LocalCompilerProvider(ICompiler compiler, IAgent description)
			{
				_compiler = compiler;
				_description = description;
			}

			public ICompiler GetCompiler()
			{
				return _compiler.IsReady() ? _compiler : null;
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
				try
				{
					RemoteCompilerService.ICompiler compiler = new CompilerClient(
						_bindingsProvider.GetBinding(url),
						new EndpointAddress(url));
					return compiler.IsReady() ? new CompilerProxy(compiler) : null;
				}
				catch (Exception e)
				{
					// TODO logger?
				}
			}

			return null;
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