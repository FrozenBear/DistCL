using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using DistCL.Client.CompileService;
using DistCL.Utils;
using LocalCompileService = DistCL.Client.CompileService;
using System.Linq;

namespace DistCL.Client
{
	internal class Program
	{
		private static readonly Logger Logger = new Logger("CLIENT");

		private static int Main(string[] args)
		{
//			if (args.Any(item => item.Contains("CustomerAPIC")))
//				for (int i = 0; i < 100; i++)
//				{
//					if (Debugger.IsAttached)
//						break;
//					Thread.Sleep(100);
//				}

			//IAgentPool agentsPool = new AgentPoolClient("basicHttpEndpoint_AgentPool");
			//Console.WriteLine(agentsPool.GetAgents());

			return Compile(args);
		}

		public static int Compile(string[] arguments)
		{
			Logger.Info("Start compilation...");

			var driver = new CLDriver(arguments);

			// Run preprocessor
			string ppFilename = Path.GetTempFileName();
			try
			{
				ILocalCompiler compiler = new LocalCompilerClient("basicHttpEndpoint_LocalCompiler");

				var preprocessToken = compiler.GetPreprocessToken(driver.SourceFiles[0]);

				using (var preprocOutput = new FileStream(ppFilename, FileMode.Create, FileAccess.Write, FileShare.Read))
				using (var stdOut = new StreamWriter(preprocOutput))
				using (var stdErr = new StringWriter())
				{
					var errCode = ProcessRunner.Run(CompilerSettings.CLExeFilename, driver.LocalCommandLine, stdOut, stdErr);
					if (errCode != 0)
						throw new Win32Exception(
							errCode,
							String.Format("{0} error: {1}", CompilerSettings.CLExeFilename, stdErr));
				}


				var output = compiler.LocalCompile(new LocalCompileInput
					{
						Arguments = driver.RemoteCommandLine,
						SrcName = driver.SourceFiles[0],
						Src = ppFilename,
						PreprocessToken = preprocessToken
					});

				var streams = new Dictionary<CompileArtifactType, Stream>();

				foreach (var artifact in output.Status.Cookies)
				{
					switch (artifact.Type)
					{
						case CompileArtifactType.Out:
							streams.Add(((ICompileArtifactCookie) artifact).Type, Console.OpenStandardOutput());
							break;

						case CompileArtifactType.Err:
							streams.Add(((ICompileArtifactCookie) artifact).Type, Console.OpenStandardError());
							break;
						case CompileArtifactType.Obj:
							File.Move(Path.Combine(Path.GetDirectoryName(ppFilename), artifact.Name), driver.OutputFiles[0].Path);
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
			catch (Exception ex)
			{
				// TODO rework
				Console.WriteLine(ex);
				throw;
			}
			finally
			{
				File.Delete(ppFilename);
			}
		}
	}
}
