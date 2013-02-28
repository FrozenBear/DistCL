using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DistCL.Client.CompileService;
using DistCL.Utils;

namespace DistCL.Client
{
	internal class Program
	{
		private static readonly Logger Logger = new Logger("CLIENT");
		private static string _compilerVersion;

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
			var ppFileName = Path.GetTempFileName();
			try
			{
				ILocalCompiler compiler = new LocalCompilerClient("basicHttpEndpoint_LocalCompiler");

				var preprocessToken = compiler.GetPreprocessToken(driver.SourceFiles[0]);

				using (var preprocOutput = new FileStream(ppFileName, FileMode.Create, FileAccess.Write, FileShare.Read))
				using (var stdOut = new StreamWriter(preprocOutput))
				using (var stdErr = new StringWriter())
				{
					var errCode = ProcessRunner.Run(CompilerSettings.CLExeFilename, driver.LocalCommandLine, stdOut, stdErr, Environment.CurrentDirectory);
					if (errCode != 0)
						throw new Win32Exception(
							errCode,
							String.Format("{0} error: {1}", CompilerSettings.CLExeFilename, stdErr));
				}


				var output = compiler.LocalCompile(new LocalCompileInput
					{
						CompilerVersion = CompilerVersion,
						Arguments = driver.RemoteCommandLine,
						SrcName = driver.SourceFiles[0],
						Src = ppFileName,
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
				File.Delete(ppFileName);
			}
		}

		public static string CompilerVersion
		{
			get
			{
				if (_compilerVersion == null)
				{
					string envPathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
					foreach (var folder in new[] {"."}.Concat(envPathValue.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries)))
					{
						var clPath = Path.Combine(folder, Utils.CompilerSettings.CLExeFilename);
						if (!File.Exists(clPath))
							continue;

						_compilerVersion = FileVersionInfo.GetVersionInfo(clPath).FileVersion;
						break;
					}

					if (_compilerVersion == null)
					throw new Exception("Compiler not found");
				}
				return _compilerVersion;
			}
		}
	}
}
