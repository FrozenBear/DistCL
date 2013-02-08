﻿using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using DistCL.Utils;

namespace DistCL
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
	public class Compiler : ICompileManager, ILocalCompiler
	{
		private readonly AgentPool _agents = new AgentPool();

		public CompileOutput Compile(CompileInput input)
		{
			var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			if (!Directory.Exists(tmpPath))
				Directory.CreateDirectory(tmpPath);

			try
			{
				using (var src = File.OpenWrite(Path.Combine(tmpPath, Path.GetFileName(input.SrcName))))
				{
					input.Src.CopyTo(src);
				}

				var artifacts = FakeCompile(tmpPath);

				var streams = new Dictionary<CompileArtifactDescription, Stream>();

				foreach (var artifact in artifacts)
				{
					streams.Add(artifact, new TempFileStreamWrapper(Path.Combine(tmpPath, artifact.Name)));
				}

				return new CompileOutput(true, 0, streams);
			}
			finally
			{
				Directory.Delete(tmpPath, true);
			}
		}

		public void RegisterAgent(AgentRequest request)
		{
			_agents.RegisterAgent(new RemoteCompilerProvider(request));
		}

		IEnumerable<Agent> IAgentPool.GetAgents()
		{
			return _agents.GetAgents();
		}

		public bool IsReady()
		{
			return true;
		}

		public CompileOutput LocalCompile(LocalCompileInput input)
		{
			using (var inputStream = File.OpenRead(input.Src))
			{
				var remoteInput = new CompileInput
					{
						Arguments = input.Arguments,
						Src = inputStream,
						SrcLength = inputStream.Length,
						SrcName = input.Src
					};

				using (var remoteOutput = Compile(remoteInput))
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

					return new CompileOutput(true, remoteOutput.Status.ExitCode, localStreams);
				}
			}
		}

		internal AgentPool Agents
		{
			get { return _agents; }
		}

		private CompileArtifactDescription[] FakeCompile(string tmpPath)
		{
			var artifacts = new List<CompileArtifactDescription>
				{
					new CompileArtifactDescription(CompileArtifactType.Err, "stderr"),
					new CompileArtifactDescription(CompileArtifactType.Out, "stdout"),
					new CompileArtifactDescription(CompileArtifactType.Obj, "myFile.obj"),
					new CompileArtifactDescription(CompileArtifactType.Pdb, "myFile.pdb")
				};

			foreach (var artifact in artifacts)
			{
				using (var writer = new StreamWriter(Path.Combine(tmpPath, artifact.Name)))
				{
					writer.WriteLine("{0}: {1} DATA", artifact.Name, artifact.Type);
				}
			}

			return artifacts.ToArray();
		}
	}
}
