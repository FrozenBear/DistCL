using System;
using System.ServiceModel;
using DistCL.RemoteCompilerService;

namespace DistCL
{
	internal interface ICompilerProvider
	{
		Agent Agent { get; }
		ICompiler GetCompiler();
	}

	internal class LocalCompilerProvider : ICompilerProvider
	{
		private readonly Agent _agent;
		private readonly ICompiler _compiler;

		public LocalCompilerProvider(ICompiler compiler, Uri[] agentPoolUrls, Uri[] compilerUrls)
		{
			_compiler = compiler;
			_agent = new Agent(
				Guid.NewGuid(),
				CompilerSettings.Default.InstanceName,
				Math.Max(1, Environment.ProcessorCount - 1),
				agentPoolUrls, 
				compilerUrls);
		}

		public Agent Agent
		{
			get { return _agent; }
		}

		public ICompiler GetCompiler()
		{
			return _compiler;
		}
	}

	internal class RemoteCompilerProvider : ICompilerProvider
	{
		private readonly Agent _agent;
		private readonly Compiler _localCompiler;

		public RemoteCompilerProvider(Compiler localCompiler, Agent agent)
		{
			_agent = agent;
			_localCompiler = localCompiler;
		}

		public Agent Agent
		{
			get { return _agent; }
		}

		public ICompiler GetCompiler()
		{
			foreach (var url in _agent.CompilerUrls)
			{
				RemoteCompilerService.ICompiler compiler = new CompilerClient(_localCompiler.GetBinding(url), new EndpointAddress(url));

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