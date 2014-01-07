using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.ServiceModel;
using DistCL.Client.CompileService;
using DistCL.Utils;

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

			// Run preprocessor
			var ppFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", Guid.NewGuid().ToString());
			try
			{
				ILocalCompiler compiler = new LocalCompilerClient();

				Logger.Info("Send preprocess token request...");

				var preprocessToken = compiler.GetPreprocessToken(driver.SourceFiles[0], CompilerVersion.VersionString);

				Logger.Info("Token obtained, starting preprocess...");

				using (var preprocOutput = new FileStream(ppFileName, FileMode.Create, FileAccess.Write, FileShare.Read))
				using (var stdOut = new StreamWriter(preprocOutput))
				using (var stdErr = new StringWriter())
				{
					var errCode = ProcessRunner.Run(
						CompilerSettings.CLExeFilename,
						driver.LocalCommandLine,
						stdOut,
						stdErr,
						Environment.CurrentDirectory);

					if (errCode != 0)
						throw new ApplicationException(string.Format("{0} error: {1}", CompilerSettings.CLExeFilename, stdErr));
				}

				Logger.Info("Preprocess finished, send compilation request...");

				var rule = new FileSystemAccessRule(preprocessToken.AccountName, FileSystemRights.Read, AccessControlType.Allow);
				var security = File.GetAccessControl(ppFileName);
				security.SetAccessRule(rule);
				File.SetAccessControl(ppFileName, security);

				var output = compiler.LocalCompile(new LocalCompileInput
					{
						CompilerVersion = CompilerVersion.VersionString,
						Arguments = driver.RemoteCommandLine,
						SrcName = driver.SourceFiles[0],
						Src = ppFileName,
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
							var tmpFile = Path.Combine(Path.GetDirectoryName(ppFileName), artifact.Name);
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

				return ProcessRunner.Run(
					CompilerSettings.CLExeFilename,
					string.Join(" ", arguments.Select(s => s.Contains(" ") ? s.QuoteString() : s).ToArray()),
					Console.Out,
					Console.Error,
					Environment.CurrentDirectory);
			}
			catch (Exception ex)
			{
				Logger.LogException("Unexpected error", ex);
				throw;
			}
			finally
			{
				File.Delete(ppFileName);
				Logger.Info("End");
			}
		}
	}
}
