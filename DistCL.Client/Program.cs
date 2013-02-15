using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.Policy;
using DistCL.Client.CompileService;
using DistCL.Utils;
using CompileArtifactType = DistCL.Client.CompileService.CompileArtifactType;

namespace DistCL.Client
{
	internal class Program
	{
		private static int Main(string[] args)
		{
			//IAgentPool agentsPool = new AgentPoolClient("basicHttpEndpoint_AgentPool");
			//Console.WriteLine(agentsPool.GetAgents());

			//return Compile("test arguments", ConfigurationManager.OpenExeConfiguration(typeof(Program).Assembly.Location).FilePath);
			return Compile("test arguments", @"D:\temp\deadlocks\5\poa.debug.log.dev07");
		}

		public static int Compile(string arguments, string srcFile)
		{
			ILocalCompiler compiler = new LocalCompilerClient("basicHttpEndpoint_LocalCompiler");

			LocalCompileOutput output = compiler.LocalCompile(new LocalCompileInput {Arguments = arguments, Src = srcFile});

			var streams = new Dictionary<Utils.CompileArtifactType, Stream>();

			foreach (var artifact in output.Status.Cookies)
			{
				switch (artifact.Type)
				{
					case CompileArtifactType.Out:
						streams.Add(((ICompileArtifactCookie)artifact).Type, Console.OpenStandardOutput());
						break;

					case CompileArtifactType.Err:
						streams.Add(((ICompileArtifactCookie)artifact).Type, Console.OpenStandardError());
						break;

					default:
						throw new NotSupportedException("Not supported stream type");
				}
			}

			CompileResultHelper.Unpack(output.ResultData, output.Status.Cookies, streams);
			output.ResultData.Close();

			foreach (var stream in streams.Values)
			{
				stream.Close();
			}

			return output.Status.ExitCode;
		}
	}
}