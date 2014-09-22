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
		private const int PreprocessWorkerCount = 2;
		private const int CompileWorkerCount = PreprocessWorkerCount * 3 / 2;
		private readonly Semaphore _semaphore;
		private readonly object _acquireWorkersSyncRoot = new object();
		private readonly int _maxWorkersCount;
		private ManualResetEventSlim _preprocessWaitEventSlim = new ManualResetEventSlim();

		private readonly ICompilerServicesCollection _compilerServices;
		private readonly Logger _compilerLogger = new Logger("COMPILER");
		private readonly Logger _localLogger = new Logger("LOCAL");
		readonly ConcurrentDictionary<Guid, string> _preprocessTokens = new ConcurrentDictionary<Guid, string>();
		private readonly Dictionary<string, string> _compilerVersions;

		internal Compiler(CompilerServicesCollection compilerServices)
		{
			_compilerServices = compilerServices;
			compilerServices.Compiler = this;

			_maxWorkersCount = Math.Max(1, Environment.ProcessorCount-1);
			var semaphoreMaximumCount = _maxWorkersCount*CompileWorkerCount;
			_semaphore = new Semaphore(semaphoreMaximumCount, semaphoreMaximumCount);

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

		internal ICompilerServicesCollection CompilerServices
		{
			get { return _compilerServices; }
		}

		internal int MaxWorkersCount
		{
			get { return _maxWorkersCount; }
		}

		internal Dictionary<string, string> CompilerVersions
		{
			get { return _compilerVersions; }
		}

		private Logger CompilerLogger
		{
			get { return _compilerLogger; }
		}

		private Logger LocalLogger
		{
			get { return _localLogger; }
		}

		public void Close()
		{
			_preprocessWaitEventSlim.Set();
		}

		#region ILocalCompiler Operations

		PreprocessToken ILocalCompiler.GetPreprocessToken(string name, string compilerVersion)
		{
			LocalLogger.DebugFormat("Preprocess token requested ({0})", name);

			if (! CompilerVersions.ContainsKey(compilerVersion))
			{
				LocalLogger.WarnFormat("Compiler version '{0}' not found", compilerVersion);
				throw new FaultException<CompilerNotFoundFaultContract>(new CompilerNotFoundFaultContract
					{
						CompilerVersion = compilerVersion
					});
			}

			var requested = DateTime.Now;
			AcquireWorkers(PreprocessWorkerCount);
			var created = DateTime.Now;

			var token = Guid.NewGuid();
			Task.Run((Action) PreprocessTokenWait).ContinueWith(PreprocessTokenRemove, token);
			_preprocessTokens[token] = name;
			LocalLogger.DebugFormat("Preprocess token created ({0})", name);

			return new PreprocessToken(token, System.Security.Principal.WindowsIdentity.GetCurrent().Name, requested, created);
		}

		private void PreprocessTokenWait()
		{
			_preprocessWaitEventSlim.Wait(TimeSpan.FromMinutes(1));
		}
		private void PreprocessTokenRemove(Task task, object state)
		{
			var token = (Guid)state;
			string name;

			if (_preprocessTokens.TryRemove(token, out name))
			{
				LocalLogger.WarnFormat("Preprocess token expired: {0}", name);
				ReleaseWorkers(PreprocessWorkerCount);
			}
		}

		LocalCompileOutput ILocalCompiler.LocalCompile(LocalCompileInput input)
		{
			LocalLogger.InfoFormat("Received local compile request '{0}'", input.SrcName);
			var preprocessDuration = DateTime.Now.Subtract(input.PreprocessToken.Created);
			var stopwatch = Stopwatch.StartNew();

			string preprocessName;
			if (_preprocessTokens.TryRemove(input.PreprocessToken.Guid, out preprocessName))
			{
				ReleaseWorkers(PreprocessWorkerCount);
			}
			else
			{
				LocalLogger.WarnFormat("Preprocess token already expired: {0}", input.SrcName);
			}

			if (!CompilerVersions.ContainsKey(input.CompilerVersion))
			{
				LocalLogger.WarnFormat("Compiler version '{0}' not found", input.CompilerVersion);
				throw new FaultException<CompilerNotFoundFaultContract>(new CompilerNotFoundFaultContract
					{
						CompilerVersion = input.CompilerVersion
					});
			}

			string compilationToken = null;
			var inputFileName = input.Src;

			string agentName;
			var remoteCompiler = CompilerServices.AgentPool.GetRandomCompiler(input.CompilerVersion, out agentName);
			var compilerSearchDuration = stopwatch.Elapsed;

			while (true)
			{
				using (var inputStream = File.OpenRead(inputFileName))
				{
					var remoteInput = new CompileInput
						{
							CompilerVersion = input.CompilerVersion,
							Arguments = input.Arguments,
							Src = inputStream,
							SrcLength = inputStream.Length,
							SrcName = input.SrcName,
							CompilationToken = compilationToken
						};

					using (var remoteOutput = remoteCompiler.Compile(remoteInput))
					{
						if (remoteOutput.Result.Finished)
						{
							var compileResult = (FinishedCompileResult) remoteOutput.Result;
							var remoteStreams = new Dictionary<CompileArtifactType, Stream>();
							var cookies = new List<CompileArtifactCookie>();
							foreach (var artifact in compileResult.Cookies)
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
							CompileResultHelper.Unpack(remoteOutput.ResultData, compileResult.Cookies, remoteStreams);

							var localStreams = new Dictionary<CompileArtifactDescription, Stream>();
							var localFiles = new List<CompileArtifactDescription>();
							foreach (var artifact in compileResult.Cookies)
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

							LocalLogger.InfoFormat("Completed local compile '{0}'", input.SrcName);
							stopwatch.Stop();
							LocalLogger.DebugFormat(@"Stats for '{0}':
	agent             {1},
	preprocess        {2},
	compiler search   {3}
	compile           {4}",
													input.SrcName,
													agentName,
													preprocessDuration,
													compilerSearchDuration,
													stopwatch.Elapsed);

							return new LocalCompileOutput(true, compileResult.ExitCode, localStreams, localFiles);
						}

						var sourceFileRequest = (SourceFileRequest) remoteOutput.Result;
						compilationToken = sourceFileRequest.CompilationToken;
						inputFileName = sourceFileRequest.FileName;
					}
				}
			}
		}

		#endregion

		#region ICompiler Operations

		bool ICompiler.IsReady()
		{
			var ready = _semaphore.WaitOne(0); // true will be returned even if we can run only preprocess, not compile

			if (ready)
			{
				_semaphore.Release();
			}

			return ready;
		}

		CompileOutput ICompiler.Compile(CompileInput input)
		{
			CompilerLogger.DebugFormat("Received compile request '{0}'", input.SrcName);
			var stopwatch = Stopwatch.StartNew();

			if (!CompilerVersions.ContainsKey(input.CompilerVersion))
			{
				CompilerLogger.WarnFormat("Compiler version '{0}' not found", input.CompilerVersion);
				throw new FaultException<CompilerNotFoundFaultContract>(new CompilerNotFoundFaultContract
				{
					CompilerVersion = input.CompilerVersion
				});
			}

			AcquireWorkers(CompileWorkerCount);

			CompilerLogger.InfoFormat("Processing '{0}'...", input.SrcName);

			try
			{
				string clPath;
				if (!CompilerVersions.TryGetValue(input.CompilerVersion, out clPath))
				{
					var error = string.Format("Compiler with specified version not found ({0})", input.CompilerVersion);
					CompilerLogger.Warn(error);
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
						CompilerLogger.DebugFormat("Copying source to {0}...", srcName);
						input.Src.CopyTo(src);
						CompilerLogger.Debug("Source copied to local file");
					}

					Dictionary<CompileArtifactDescription, Stream> streams;
					var errorCode = RunCompiler(clPath, input.Arguments, srcName, tmpPath, out streams);

					return new CompileOutput(errorCode, streams, null);
				}
				finally
				{
					Directory.Delete(tmpPath, true);
				}
			}
			catch (Exception e)
			{
				CompilerLogger.LogException(string.Format("Exception in CompileInternal({0})", input.SrcName), e);
				throw;
			}
			finally
			{
				ReleaseWorkers(CompileWorkerCount);
				stopwatch.Stop();
				CompilerLogger.InfoFormat("Processing of '{0}' completed ({1})", input.SrcName, stopwatch.Elapsed);
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
			var encoding = new UTF8Encoding(false);

			int exitCode;

			var objFilename = Path.Combine(outputPath, fileName);

			using (var outWriter = new StreamWriter(stdOutStream, encoding, bufferSize, true))
			using (var errWriter = new StreamWriter(stdErrStream, encoding, bufferSize, true))
			{
				// TODO dirty code.
				commmandLine += " /Fo" + StringUtils.QuoteString(objFilename);
				commmandLine += " " + StringUtils.QuoteString(inputPath);

				CompilerLogger.DebugFormat("Call compiler '{0}' with cmdline '{1}'", Utils.CompilerSettings.CLExeFilename, commmandLine);

				exitCode = ProcessRunner.Run(clPath, commmandLine, outWriter, errWriter, outputPath);

				var message = string.Format("Compilation is completed with exit code {0}", exitCode);
				if (exitCode == 0)
				{
					CompilerLogger.Debug(message);
				}
				else
				{
					CompilerLogger.Warn(message);
				}
			}

			stdOutStream.Position = 0;
			stdErrStream.Position = 0;

			if (CompilerLogger.IsDebugEnabled)
			{
				using (var outReader = new StreamReader(stdOutStream, encoding, true, bufferSize, true))
				using (var errReader = new StreamReader(stdErrStream, encoding, true, bufferSize, true))
				{
					var outText = outReader.ReadToEnd();
					var errText = errReader.ReadToEnd();

					if (!string.IsNullOrWhiteSpace(outText))
					{
						CompilerLogger.DebugFormat("Output message: {0}", outText.Trim());
					}

					if (!string.IsNullOrWhiteSpace(errText))
					{
						CompilerLogger.WarnFormat("Error message: {0}", outText.Trim());
					}
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

			return exitCode;
		}

		#endregion

		#region ICompileCoordinator Operations

		Agent ICompileCoordinator.GetDescription()
		{
			return CompilerServices.LocalAgentManager.Description;
		}

		void ICompileCoordinator.RegisterAgent(Agent request)
		{
			CompilerServices.AgentPool.RegisterAgent(new RemoteAgentProxy(CompilerServices.Bindings, request));
		}

		#endregion

		#region IAgentPool Operations

		Agent[] IAgentPool.GetAgents()
		{
			return CompilerServices.AgentPool.GetAgents();
		}

		#endregion

		#region Workers Counting

		private void AcquireWorkers(int count)
		{
			lock (_acquireWorkersSyncRoot)	// only one thread should be in increase loop
			{
				for (var i = 0; i < count; i++)
				{
					_semaphore.WaitOne();
				}
			}
		}

		private void ReleaseWorkers(int count)
		{
			_semaphore.Release(count);
		}

		#endregion
	}
}
