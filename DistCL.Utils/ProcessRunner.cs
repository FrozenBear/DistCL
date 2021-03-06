﻿using System;
using System.Diagnostics;
using System.IO;
using System.Security.Permissions;
using DistCL.Utils.ProcessExtensions;

namespace DistCL.Utils
{
	[SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
	public sealed class ProcessRunner
	{

		public static ProcessEx Run(
			string fileName,
			string arguments,
			TextWriter stdOutput,
			TextWriter stdErr,
			string workingDirectory)
		{
			var process = new ProcessEx();
			using (process)
			{
				process.StartInfo.FileName = fileName;
				process.StartInfo.Arguments = arguments;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.StartInfo.WorkingDirectory = workingDirectory;
				process.StartInfo.CreationFlags = ProcessCreationFlags.CREATE_SUSPENDED;

				process.OutputDataReceived += (sender, args) =>
					{ if (!String.IsNullOrEmpty(args.Data)) stdOutput.WriteLine(args.Data); };

				process.ErrorDataReceived += (sender, args) =>
					{ if (!String.IsNullOrEmpty(args.Data)) stdErr.WriteLine(args.Data); };

				process.Start();

				return process;
			}
		}

		public static int Wakeup(ProcessEx process)
		{
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
