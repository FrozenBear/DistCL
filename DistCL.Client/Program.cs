using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using DistCL.Client.CompileService;
using DistCL.Utils;
using LocalCompileService = DistCL.Client.CompileService;

namespace DistCL.Client
{
	internal class Program
	{
		private static int Main(string[] args)
		{
			//IAgentPool agentsPool = new AgentPoolClient("basicHttpEndpoint_AgentPool");
			//Console.WriteLine(agentsPool.GetAgents());

            return Compile(args);
        }

        public static int Compile(string[] arguments)
        {
			Logger.Info("Start compilation...");

			CLDriver driver = new CLDriver(arguments);

			// Run preprocessor
			string ppFilename = Path.GetTempFileName();
			try
			{
				using (FileStream preprocOutput = new FileStream(ppFilename, FileMode.Truncate, FileAccess.Write, FileShare.Read))
				using (StreamWriter stdOut = new StreamWriter(preprocOutput))
				using (StringWriter stdErr = new StringWriter())
				{
					int errCode = ProcessRunner.Run(CompilerSettings.CLExeFilename, driver.LocalCommandLine, stdOut, stdErr);
					if (errCode != 0)
						throw new Win32Exception(errCode, String.Format("{0} error: {1}", CompilerSettings.CLExeFilename, stdErr.ToString()));
				}

				LocalCompileService.ILocalCompiler compiler = new LocalCompilerClient("basicHttpEndpoint_LocalCompiler");

				LocalCompileService.LocalCompileOutput output = compiler.LocalCompile(new LocalCompileService.LocalCompileInput
				{
					Arguments = driver.RemoteCommandLine,
					SrcName = driver.SourceFiles[0],
					Src = ppFilename
				});

				var streams = new Dictionary<Utils.CompileArtifactType, Stream>();

				using (FileStream objFile = new FileStream(driver.OutputFiles[0].Path, FileMode.Truncate, FileAccess.Write))
				{
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
							case CompileArtifactType.Obj:
								streams.Add(((ICompileArtifactCookie)artifact).Type, objFile);
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
				}

				return output.Status.ExitCode;
			}
			finally
			{
				File.Delete(ppFilename);
			}
        }
    }
}
