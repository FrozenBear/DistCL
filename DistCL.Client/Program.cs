using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using DistCL.Client.CompileService;
using DistCL.Utils;
using DistCL.Utils.ProcessExtensions;

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
			Logger.InfoFormat(CompilerVersion.VersionStringForDefine);
			Logger.InfoFormat(CompilerVersion.VersionString);
			

			Logger.InfoFormat("Start compilation: {0}...", string.Join(" ", arguments.Select(s => string.Format("[{0}]", s))));

			var driver = new CLDriver(arguments);

			try
			{
				ILocalCompiler compiler = new LocalCompilerClient();

				Logger.Info("Send compilation request...");

				var preprocessToken = compiler.GetPreprocessToken(driver.SourceFiles[0], CompilerVersion.VersionString);
				var output = compiler.LocalCompile(new LocalCompileInput
					{
						CompilerVersion = CompilerVersion.VersionString,
						Arguments = driver.RemoteCommandLine,
						SrcName = driver.SourceFiles[0],
						Src = driver.SourceFiles[0],
						PreprocessToken = preprocessToken
					});

				Logger.InfoFormat("Compilation finished with exit code {0}, processing result...", output.Status.ExitCode);

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
							var tmpFile = Path.Combine(Path.GetTempPath(), artifact.Name);
							var resultFile = driver.OutputFiles[0].Path;
							if (File.Exists(resultFile))
							{
								Logger.DebugFormat("Delete old '{0}'", resultFile);
								File.Delete(resultFile);
							}
							Logger.DebugFormat("Move '{0}' to {1}", tmpFile, resultFile);
							File.Move(tmpFile, resultFile);
							break;
						default:
							throw new NotSupportedException("Not supported stream type");
					}
				}

				CompileResultHelper.Unpack(output.ResultData, output.Status.Cookies, streams);
				output.ResultData.Close();

				foreach (var stream in streams)
				{
					if (stream.Key != CompileArtifactType.Out && stream.Key != CompileArtifactType.Err)
					{
						stream.Value.Close();
					}
				}

				Logger.Info("Result processing finished");

				return output.Status.ExitCode;
			}
			catch (FaultException<CompilerNotFoundFaultContract> ex)
			{
				Logger.WarnFormat("DistCL service can't find specified compiler ({0})", ex.Detail.CompilerVersion);

				 ProcessEx process = ProcessRunner.Run(
					CompilerSettings.CLExeFilename,
					string.Join(" ", arguments.Select(s => s.Contains(" ") ? s.QuoteString() : s).ToArray()),
					Console.Out,
					Console.Error,
					Environment.CurrentDirectory);
				return process.ExitCode;
			}
			catch (Exception ex)
			{
				Logger.LogException("Unexpected error", ex);
				throw;
			}
			finally
			{
				Logger.Info("End");
			}
		}
	}
}
