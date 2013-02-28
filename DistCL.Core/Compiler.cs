using System;
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
		private const int PreprocessWorkerCount = 1;
		private const int CompileWorkerCount = 2 * PreprocessWorkerCount;
		private readonly Semaphore _semaphore;
		private readonly int _maxWorkersCount;

		private readonly AgentPool _agentPool = new AgentPool();
		private readonly Logger _logger = new Logger("COMPILER");
		readonly ConcurrentDictionary<Guid, string> _preprocessTokens = new ConcurrentDictionary<Guid, string>();
		private readonly Dictionary<string, string> _compilerVersions;

		public Compiler()
		{
			_maxWorkersCount = Math.Max(1, Environment.ProcessorCount-1);
			_semaphore = new Semaphore(0, _maxWorkersCount*CompileWorkerCount);

			var compilerVersions = new Dictionary<string, string>();
			using (var visualStudioRegistry = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio"))
			{
				if (visualStudioRegistry == null)
					throw new InvalidOperationException("Visual Studio required");

				foreach (var version in visualStudioRegistry.GetSubKeyNames())
				{
					using (var vs = visualStudioRegistry.OpenSubKey(version))
					{
						if (vs == null)
							continue;

						var installDir = (string) vs.GetValue("InstallDir");

						if (! string.IsNullOrEmpty(installDir))
						{
							var vcPath = Path.Combine(installDir, @"..\..\VC\bin");

							var clPath = Path.Combine(vcPath, Utils.CompilerSettings.CLExeFilename);
							if (!File.Exists(clPath))
								continue;

							compilerVersions[FileVersionInfo.GetVersionInfo(clPath).FileVersion] = clPath;
						}
					}
				}
			}
			string envPathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
			foreach (var folder in new[] {"."}.Concat(envPathValue.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries).Reverse()))
			{
				var clPath = Path.Combine(folder, Utils.CompilerSettings.CLExeFilename);
				if (!File.Exists(clPath))
					continue;

				compilerVersions[FileVersionInfo.GetVersionInfo(clPath).FileVersion] = clPath;	// reverse + dict[] set = first win
			}
			_compilerVersions = compilerVersions;
		}

		public int MaxWorkersCount
		{
			get { return _maxWorkersCount; }
		}

		public Dictionary<string, string> CompilerVersions
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
			AcquireWorkers(CompileWorkerCount);

			try
			{
				return CompileInternal(input);
			}
			finally
			{
				ReleaseWorkers(CompileWorkerCount);
			}
		}

		private CompileOutput CompileInternal(CompileInput input)
		{
			Logger.InfoFormat("Compiling '{0}'...", input.SrcName);

			try
			{
				string clPath;
				if (!CompilerVersions.TryGetValue(input.CompilerVersion, out clPath))
				{
					var error = string.Format("Compiler with specified version not found ({0})", input.CompilerVersion);
					Logger.Warn(error);
					throw new Exception(error);
				}

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
					var errorCode = RunCompiler(clPath, input.Arguments, srcName, tmpPath, out streams);

					if (errorCode == 0)
					{
						Logger.InfoFormat("'{0}' compiled successfully", srcName);
					}
					else
					{
						Logger.WarnFormat("'{0}' compiled with non-zero error code", srcName);
					}

					return new CompileOutput(errorCode == 0, errorCode, streams, null);
				}
				finally
				{
					Directory.Delete(tmpPath, true);
				}
			}
			catch (Exception e)
			{
				Logger.LogException(string.Format("Exception in CompileInternal({0})", input.SrcName), e);
				throw;
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
			var ready = _semaphore.WaitOne(0); // true will be returned even if we can run only preprocess, not compile
			
			if (ready)
			{
				_semaphore.Release();
			}
			
			return ready;
		}

		public Guid GetPreprocessToken(string name)
		{
			Logger.DebugFormat("Preprocess token requested ({0})", name);

			AcquireWorkers(PreprocessWorkerCount);

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
				ReleaseWorkers(PreprocessWorkerCount);
			}
		}

		public LocalCompileOutput LocalCompile(LocalCompileInput input)
		{
			Logger.InfoFormat("Processing local compile request '{0}'...", input.SrcName);

			string preprocessName;
			if (_preprocessTokens.TryRemove(input.PreprocessToken, out preprocessName))
			{
				ReleaseWorkers(PreprocessWorkerCount);
			}
			else
			{
				Logger.WarnFormat("Preprocess token already expired: {0}", input.SrcName);
			}

			using (var inputStream = File.OpenRead(input.Src))
			{
				var remoteInput = new CompileInput
					{
						CompilerVersion = input.CompilerVersion,
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

		private int RunCompiler(
			string clPath,
			string commmandLine,
			string inputPath,
			string outputPath,
			out Dictionary<CompileArtifactDescription, Stream> streams)
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

				errCode = ProcessRunner.Run(clPath, commmandLine, outWriter, errWriter);
				Logger.DebugFormat("Compilation is completed.");
			}

			stdOutStream.Position = 0;
			stdErrStream.Position = 0;

			if (Logger.DebugEnabled)
			{
				using (var outReader = new StreamReader(stdOutStream, encoding, true, bufferSize, true))
				using (var errReader = new StreamReader(stdErrStream, encoding, true, bufferSize, true))
				{

					Logger.DebugFormat("errorCode: {0}, stdout: {1}, stderr: {2}",
										errCode, outReader.ReadToEnd(), errReader.ReadToEnd());
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

		private void AcquireWorkers(int count)
		{
			for (var i = 0; i < count; i++)
			{
				_semaphore.WaitOne();
			}
		}
		private void ReleaseWorkers(int count)
		{
			_semaphore.Release(count);
		}
	}
}
