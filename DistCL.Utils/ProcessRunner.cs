﻿using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Permissions;

namespace DistCL.Utils
{
	[SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
	public sealed class ProcessRunner
	{

		public static int Run(
			string fileName,
			string arguments,
			TextWriter stdOutput,
			TextWriter stdErr,
			string workingDirectory)
		{
			Contract.Requires(fileName != null);
			Contract.Requires(arguments != null);
			Contract.Requires(workingDirectory != null);

			using (var process = new Process())
			{
				process.StartInfo.FileName = fileName;
				process.StartInfo.Arguments = arguments;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.StartInfo.WorkingDirectory = workingDirectory;

				process.OutputDataReceived += (sender, args) =>
					{ if (!String.IsNullOrEmpty(args.Data)) stdOutput.WriteLine(args.Data); };

				process.ErrorDataReceived += (sender, args) =>
					{ if (!String.IsNullOrEmpty(args.Data)) stdErr.WriteLine(args.Data); };

				process.Start();

				process.BeginOutputReadLine();
				process.BeginErrorReadLine();

				if (!process.HasExited)
				{
					process.WaitForExit();
				}

				return process.ExitCode;
			}
		}
	}
}