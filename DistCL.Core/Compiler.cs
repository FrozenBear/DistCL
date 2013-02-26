﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DistCL.Proxies;
using DistCL.Utils;
using System.Linq;
using Microsoft.Win32;

namespace DistCL
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode=ConcurrencyMode.Multiple)]
	[BindingNamespaceBehavior]
	public class Compiler : ICompileManager, ILocalCompiler
	{
		private readonly AgentPool _agentPool = new AgentPool();
		private readonly Logger _logger = new Logger("COMPILER");
		private readonly int _maxWorkersCount;
		private int _workersCount;
		private readonly object _syncRoot = new object();
		readonly ConcurrentDictionary<Guid, string> _preprocessTokens = new ConcurrentDictionary<Guid, string>();
		private readonly string[] _compilerVersions;

		public Compiler()
		{
			_maxWorkersCount = Math.Max(1, Environment.ProcessorCount-1);


			var compilerVersions = new HashSet<string>();
			using (var visualStudioRegistry = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio"))
			{
				if (visualStudioRegistry == null)
					throw new InvalidOperationException("Visual Studio required");

				foreach (var version in visualStudioRegistry.GetSubKeyNames())
				{
					using (var vs = visualStudioRegistry.OpenSubKey(version))
					{
						var installDir = (string) vs.GetValue("InstallDir");

						if (! string.IsNullOrEmpty(installDir))
						{
							var vcPath = Path.Combine(installDir, @"..\..\VC\bin");

							var clPath = Path.Combine(vcPath, Utils.CompilerSettings.CLExeFilename);
							if (!File.Exists(clPath))
								continue;

							compilerVersions.Add(FileVersionInfo.GetVersionInfo(clPath).FileVersion);
						}
					}
				}
			}
			_compilerVersions = compilerVersions.ToArray();
		}

		public int MaxWorkersCount
		{
			get { return _maxWorkersCount; }
		}

		public string[] CompilerVersions
		{
			get { return _compilerVersions; }
		}

		internal IBindingsProvider BindingsProvider { get; set; }

		public Logger Logger
		{
			get { return _logger; }
		}

		internal AgentPool AgentPool
		{
			get { return _agentPool; }
		}

		public CompileOutput Compile(CompileInput input)
		{
			lock (_syncRoot)
			{
				while (_workersCount >= _maxWorkersCount)
				{
					Monitor.Wait(_syncRoot);
				}

				_workersCount ++;
			}

			try
			{
				return CompileInternal(input);
			}
			finally
			{
				lock (_syncRoot)
				{
					_workersCount--;
					Monitor.Pulse(_syncRoot);
				}
			}
		}

		private CompileOutput CompileInternal(CompileInput input)
		{
			Logger.InfoFormat("Compiling '{0}'...", input.SrcName);

			var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			if (!Directory.Exists(tmpPath))
				Directory.CreateDirectory(tmpPath);

			try
			{
				var srcName = Path.Combine(tmpPath, Path.GetFileName(input.SrcName));
				using (var src = File.OpenWrite(srcName))
				{
					Logger.DebugFormat("Copying source to {0}...", srcName);
					input.Src.CopyTo(src);
					Logger.Debug("Source copied to local file");
				}

				Dictionary<CompileArtifactDescription, Stream> streams;
				var errorCode = RunCompiler(input.Arguments, srcName, tmpPath, out streams);

				if (errorCode == 0)
				{
					Logger.InfoFormat("'{0}' compiled successfully", srcName);
				}
				else
				{
					Logger.WarnFormat("'{0}' compiled with errors", srcName);
				}
				
				return new CompileOutput(errorCode == 0, errorCode, streams, null);
			}
			finally
			{
				Directory.Delete(tmpPath, true);
			}
		}

		public void RegisterAgent(Agent request)
		{
			_agentPool.RegisterAgent(new RemoteAgentProxy(BindingsProvider, request));
		}

		Agent[] IAgentPool.GetAgents()
		{
			return _agentPool.GetAgents();
		}

		public Agent GetDescription()
		{
			//  TODO get off new object creation
			return new Agent(AgentPool.Manager.AgentProxy.Description);
		}

		public bool IsReady()
		{
			lock (_syncRoot)
			{
				return _workersCount < _maxWorkersCount;
			}
		}

		public Guid GetPreprocessToken(string name)
		{
			Logger.DebugFormat("Preprocess token requested ({0})", name);
			lock (_syncRoot)
			{
				while (_workersCount >= _maxWorkersCount)
				{
					Monitor.Wait(_syncRoot);
				}

				_workersCount++;
			}

			var token = Guid.NewGuid();
			Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(PreprocessTokenRemove, token);
			_preprocessTokens[token] = name;
			Logger.DebugFormat("Preprocess token created ({0})", name);
			return token;
		}

		private void PreprocessTokenRemove(Task task, object state)
		{
			var token = (Guid) state;
			string name;

			if (_preprocessTokens.TryRemove(token, out name))
			{
				Logger.WarnFormat("Preprocess token expired: {0}", name);
				lock (_syncRoot)
				{
					_workersCount--;
					Monitor.Pulse(_syncRoot);
				}
			}
		}

		public LocalCompileOutput LocalCompile(LocalCompileInput input)
		{
			Logger.InfoFormat("Processing local compile request '{0}'...", input.SrcName);

			string preprocessName;
			if (_preprocessTokens.TryRemove(input.PreprocessToken, out preprocessName))
			{
				lock (_syncRoot)
				{
					_workersCount--;
					Monitor.Pulse(_syncRoot);
				}
			}
			else
			{
				Logger.WarnFormat("Preprocess token already expired: {0}", input.SrcName);
			}

			using (var inputStream = File.OpenRead(input.Src))
			{
				var remoteInput = new CompileInput
					{
						Arguments = input.Arguments,
						Src = inputStream,
						SrcLength = inputStream.Length,
						SrcName = input.SrcName
					};

				using (var remoteOutput = AgentPool.GetRandomCompiler(input.CompilerVersion).Compile(remoteInput))
				{
					var remoteStreams = new Dictionary<CompileArtifactType, Stream>();
					var cookies = new List<CompileArtifactCookie>();
					foreach (var artifact in remoteOutput.Status.Cookies)
					{
						switch (artifact.Type)
						{
							case CompileArtifactType.Out:
							case CompileArtifactType.Err:
								var artifactStream = new MemoryStream();
								remoteStreams.Add(artifact.Type, artifactStream);
								cookies.Add(artifact);
								break;

							default:
								var fileStream = File.Open(
									Path.Combine(Path.GetDirectoryName(input.Src), artifact.Name),
									FileMode.Create,
									FileAccess.Write,
									FileShare.None);
								remoteStreams.Add(artifact.Type, fileStream);
								break;
						}
					}
					CompileResultHelper.Unpack(remoteOutput.ResultData, remoteOutput.Status.Cookies, remoteStreams);

					var localStreams = new Dictionary<CompileArtifactDescription, Stream>();
					var localFiles = new List<CompileArtifactDescription>();
					foreach (var artifact in remoteOutput.Status.Cookies)
					{
						var stream = remoteStreams[artifact.Type];
						switch (artifact.Type)
						{
							case CompileArtifactType.Out:
							case CompileArtifactType.Err:
								stream.Position = 0;
								localStreams.Add(artifact, stream);
								break;

							default:
								stream.Close();
								localFiles.Add(artifact);
								break;
						}
					}

					Logger.InfoFormat("Completed local compile '{0}'", input.SrcName);
					return new LocalCompileOutput(true, remoteOutput.Status.ExitCode, localStreams, localFiles);
				}
			}
		}

		private int RunCompiler(string commmandLine, string inputPath, string outputPath, out Dictionary<CompileArtifactDescription, Stream> streams)
		{
			streams = new Dictionary<CompileArtifactDescription, Stream>();

			var fileName = Guid.NewGuid() + ".obj";

			var stdOutStream = new MemoryStream();
			var stdErrStream = new MemoryStream();

			const int bufferSize = 4086;
			var encoding = Encoding.UTF8;

			int errCode;

			var objFilename = Path.Combine(outputPath, fileName);

			using (var outWriter = new StreamWriter(stdOutStream, encoding, bufferSize, true))
			using (var errWriter = new StreamWriter(stdErrStream, encoding, bufferSize, true))
			{
				// TODO dirty code.
				commmandLine += " /Fo" + StringUtils.QuoteString(objFilename);
				commmandLine += " " + StringUtils.QuoteString(inputPath);
				
				Logger.DebugFormat("Call compiler '{0}' with cmdline '{1}'", Utils.CompilerSettings.CLExeFilename, commmandLine);

				errCode = ProcessRunner.Run(Utils.CompilerSettings.CLExeFilename, commmandLine, outWriter, errWriter);
				Logger.DebugFormat("Compilation is completed.");
			}

			stdOutStream.Position = 0;
			stdErrStream.Position = 0;

			if (Logger.DebugEnabled)
			{
				using (var outReader = new StreamReader(stdOutStream, encoding, true, bufferSize, true))
				using (var errReader = new StreamReader(stdErrStream, encoding, true, bufferSize, true))
				{

					Logger.DebugFormat("errorCode: {0}, stdout: {1}, stderr: {2}", errCode, outReader.ReadToEnd(), errReader.ReadToEnd());
				}
			}

			stdOutStream.Position = 0;
			stdErrStream.Position = 0;

			streams.Add(new CompileArtifactDescription(CompileArtifactType.Out, "stdout"), stdOutStream);
			streams.Add(new CompileArtifactDescription(CompileArtifactType.Err, "stderr"), stdErrStream);

			if (File.Exists(objFilename))
			{
				streams.Add(
					new CompileArtifactDescription(CompileArtifactType.Obj, fileName),
					new TempFileStreamWrapper(objFilename));
			}

			return errCode;
		}
	}
}
