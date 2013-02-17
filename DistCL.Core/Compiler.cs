using System;
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

				Logger.InfoFormat("{0} compiled successfully");

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

		public LocalCompileOutput LocalCompile(LocalCompileInput input)
		{
			using (var inputStream = File.OpenRead(input.Src))
			{
				var remoteInput = new CompileInput
					{
						Arguments = input.Arguments,
						Src = inputStream,
						SrcLength = inputStream.Length,
						SrcName = input.SrcName
					};

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

					return new LocalCompileOutput(true, remoteOutput.Status.ExitCode, localStreams);
				}
			}
		}

		private Dictionary<CompileArtifactDescription, Stream> RunCompiler(string commmandLine, string inputPath, string outputPath)
		{
			var artifacts = new List<CompileArtifactDescription>
				{
					new CompileArtifactDescription(CompileArtifactType.Err, "stderr"),
					new CompileArtifactDescription(CompileArtifactType.Out, "stdout"),
					new CompileArtifactDescription(CompileArtifactType.Obj, "myFile.obj")
					//new CompileArtifactDescription(CompileArtifactType.Pdb, "myFile.pdb")
				};

			using (StringWriter stdOut = new StringWriter())
            using (StringWriter stdErr = new StringWriter())
            {
				int errCode = ProcessRunner.Run(Utils.CompilerSettings.CLExeFilename, commmandLine, stdOut, stdErr);
                if (errCode != 0)
                    throw new Win32Exception(errCode, "cl.exe error");
            }

			var streams = new Dictionary<CompileArtifactDescription, Stream>();

			//foreach (var artifact in artifacts)
			//{
				//streams.Add(artifact, new TempFileStreamWrapper(Path.Combine(tmpPath, artifact.Name)));
			//}

			foreach (var artifact in artifacts)
			{
				using (var writer = new StreamWriter(Path.Combine(outputPath, artifact.Name)))
				{
					writer.WriteLine("{0}: {1} DATA", artifact.Name, artifact.Type);
				}
			}

			return streams;
		}
	}
}
