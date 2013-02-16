using System.Diagnostics;
using System.IO;
using System.Security.Permissions;

namespace DistCL.Utils
{
	[SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
	public sealed class ProcessRunner
	{
		public static int Run(
			string fileName, string arguments, TextWriter stdOutput, TextWriter errOutput)
		{
			var process = new Process();
			using (process)
			{
				process.StartInfo.FileName = fileName;
				process.StartInfo.Arguments = arguments;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;

				process.Start();

				ReadProcessOutput(process, stdOutput, errOutput);

				return process.ExitCode;
			}
		}

		private static void ReadProcessOutput(
			Process cmdLineProcess, TextWriter stdOutput, TextWriter errorOutput)
		{
			cmdLineProcess.OutputDataReceived += (sender, args) => stdOutput.Write(args.Data);
			cmdLineProcess.ErrorDataReceived += (sender, args) => errorOutput.Write(args.Data);
			cmdLineProcess.BeginOutputReadLine();
			cmdLineProcess.BeginErrorReadLine();

			if (!cmdLineProcess.HasExited)
			{
				cmdLineProcess.WaitForExit();
			}
		}
	}
}