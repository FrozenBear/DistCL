using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using DistCL.Utils;

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
		ConcurrentDictionary<Guid, string> _preprocessTokens = new ConcurrentDictionary<Guid, string>();

		public Compiler()
		{
			_maxWorkersCount = Math.Max(1, Environment.ProcessorCount-1);
		}

		public int MaxWorkersCount
		{
			get { return _maxWorkersCount; }
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

		public CompileOutput CompileInternal(CompileInput input)
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

				var streams = RunCompiler(input.Arguments, srcName, tmpPath);

				Logger.InfoFormat("'{0}' compiled successfully", srcName);

				return new CompileOutput(true, 0, streams);
			}
			finally
			{
				Directory.Delete(tmpPath, true);
			}
		}

		public void RegisterAgent(AgentRegistrationMessage request)
		{
			_agentPool.RegisterAgent(new RemoteCompilerProvider(BindingsProvider, request));
		}

		Agent[] IAgentPool.GetAgents()
		{
			return _agentPool.GetAgents();
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

				// TODO move IsReady check into GetRandomCompiler, result should be ICompiler instead of ICompilerProvider
				using (var remoteOutput = AgentPool.GetRandomCompiler().GetCompiler().Compile(remoteInput))
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
								var fileStream =
									File.OpenWrite(Path.Combine(Path.GetDirectoryName(input.Src), artifact.Name));
								remoteStreams.Add(artifact.Type, fileStream);
								break;
						}
					}
					CompileResultHelper.Unpack(remoteOutput.ResultData, remoteOutput.Status.Cookies, remoteStreams);

					var localStreams = new Dictionary<CompileArtifactDescription, Stream>();
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
								break;
						}
					}

					Logger.InfoFormat("Completed local compile '{0}'", input.SrcName);
					return new LocalCompileOutput(true, remoteOutput.Status.ExitCode, localStreams);
				}
			}
		}

		private Dictionary<CompileArtifactDescription, Stream> RunCompiler(string commmandLine, string inputPath, string outputPath)
		{
			var streams = new Dictionary<CompileArtifactDescription, Stream>();

			byte[] stdOutBuf = null;
			byte[] stdErrBuf = null;
			int stdOutStreamLen = 0;
			int stdErrStreamLen = 0;

			var fileName = Guid.NewGuid() + "myFile.obj";

			using (MemoryStream stdOutStream = new MemoryStream())
			using (MemoryStream stdErrStream = new MemoryStream())
			using (StreamWriter stdWriter = new StreamWriter(stdOutStream))
			using (StreamWriter errWriter = new StreamWriter(stdErrStream))
			{
				// TODO dirty code.
				commmandLine += " /Fo" + StringUtils.QuoteString(Path.Combine(outputPath, fileName));
				commmandLine += " " + StringUtils.QuoteString(inputPath);
				Logger.DebugFormat("Call compiler '{0}' with cmdline '{1}'", Utils.CompilerSettings.CLExeFilename, commmandLine);

				int errCode = ProcessRunner.Run(Utils.CompilerSettings.CLExeFilename, commmandLine, stdWriter, errWriter);
				Logger.DebugFormat("Compilation is completed.");

				if (errCode != 0)
					throw new ApplicationException(String.Format("{0}: {1}", Utils.CompilerSettings.CLExeFilename, errCode));

				stdErrStreamLen = (int)stdErrStream.Length;
				stdErrBuf = stdErrStream.GetBuffer();

				stdOutStreamLen = (int)stdOutStream.Length;
				stdOutBuf = stdOutStream.GetBuffer();
			}

			streams.Add(new CompileArtifactDescription(CompileArtifactType.Out, "stdout"),
				new MemoryStream(stdOutBuf, 0, stdOutStreamLen));
			streams.Add(new CompileArtifactDescription(CompileArtifactType.Err, "stderr"),
				new MemoryStream(stdErrBuf, 0, stdErrStreamLen));

			Logger.DebugFormat("stdout: {0}, stderr: {1}", System.Text.UTF8Encoding.UTF8.GetString(stdOutBuf, 0, stdOutStreamLen),
				System.Text.UTF8Encoding.UTF8.GetString(stdErrBuf, 0, stdErrStreamLen));

			streams.Add(new CompileArtifactDescription(CompileArtifactType.Obj, "myFile.obj"),
				new TempFileStreamWrapper(Path.Combine(outputPath, fileName)));

			return streams;
		}
	}
}
