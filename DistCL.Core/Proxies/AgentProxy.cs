using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using DistCL.RemoteCompilerService;
using DistCL.Utils;

namespace DistCL.Proxies
{
	internal class RemoteAgentProxy : IAgentProxy
	{
		private readonly IBindingsCollection _bindingsCollection;
		private readonly IAgent _description;

		private static Logger _logger;

		private readonly object _compilerSyncRoot = new object();
		private DateTime _nextCompilerGetTry = DateTime.MinValue;
		private volatile CompilerProxy _compiler;
		private ICompileCoordinatorProxy _compileCoordinatorProxy;
		private AgentPoolProxy _agentPool;

		public RemoteAgentProxy(IBindingsCollection bindingsCollection, IAgent description)
		{
			_bindingsCollection = bindingsCollection;
			_description = description;
		}

		public static Logger Logger
		{
			get { return LazyInitializer.EnsureInitialized(ref _logger, () => new Logger("PROXY")); }
		}

		public IAgent Description
		{
			get { return _description; }
		}

		public ICompiler GetCompiler()
		{
			bool? isReady = null;

			if (_compiler == null)
			{
				lock (_compilerSyncRoot)
				{
					if (_compiler == null && DateTime.Now > _nextCompilerGetTry)
					{
						bool isReadyLocal;
						_compiler = GetCompilerInternal(out isReadyLocal);
						isReady = isReadyLocal;
						_nextCompilerGetTry = DateTime.Now.AddMinutes(1);
					}
				}
			}

			if (! isReady.HasValue && _compiler != null)
			{
				isReady = _compiler.IsReady();
			}

			return isReady.HasValue && isReady.Value ? _compiler : null;
		}

		private CompilerProxy GetCompilerInternal(out bool isReady)
		{
			foreach (var url in _description.CompilerUrls)
			{
				try
				{
					RemoteCompilerService.ICompiler compiler = new CompilerClient(
						_bindingsCollection.GetBinding(url),
						new EndpointAddress(url));
					{
						isReady = compiler.IsReady();
						return new CompilerProxy(compiler);
					}
				}
				catch (Exception ex)
				{
					Logger.WarnException(string.Format("Get compiler proxy for {0} by {1}", _description.Name, url), ex);
				}
			}

			isReady = false;
			return null;
		}

		public ICompileCoordinatorProxy GetCoordinator()
		{
			return LazyInitializer.EnsureInitialized(ref _compileCoordinatorProxy,
											() =>
											Description.AgentPoolUrls != null && Description.AgentPoolUrls.Length > 0
												? (ICompileCoordinatorProxy)GetAgentPool()
												: new CompileCoordinatorProxy(this));
		}

		public IAgentPoolProxy GetAgentPool()
		{
			return LazyInitializer.EnsureInitialized(ref _agentPool, () => new AgentPoolProxy(this));
		}

		protected void ConnectToAgent<TClient>(
			object syncRoot,
			Func<TClient> getClient,
			Action<TClient> setClient,
			Func<Binding, EndpointAddress, TClient> creation,
			Uri[] uris,
			Action<TClient> func)
			where TClient : class
		{
			if (uris == null || uris.Length == 0)
			{
				throw new InvalidOperationException("empty uris array");
			}

			lock (syncRoot)
			{
				try
				{
					var client = getClient();
					if (client != null)
					{
						func(client);
					}
				}
				catch (Exception ex)
				{
					Logger.WarnException(string.Format("Agent {0} call error", _description.Name), ex);
				}

				SearchGoodClientEndpoint(syncRoot, setClient, creation, uris, clnt =>
					{
						func(clnt);
						return Task.FromResult<object>(null);
					});
			}
		}

		protected Task<TTaskResult> ConnectToAgent<TClient, TTaskResult>(
			object syncRoot,
			Func<TClient> getClient,
			Action<TClient> setClient,
			Func<Binding, EndpointAddress, TClient> creation,
			Uri[] uris,
			Func<TClient, Task<TTaskResult>> func)
			where TClient : class
			where TTaskResult : class
		{
			if (uris != null && uris.Length > 0)
			{
				lock (syncRoot)
				{
					var client = getClient();
					if (client != null)
					{
						return func(client).ContinueWith(
							delegate(Task<TTaskResult> task)
								{
									if (task.IsFaulted)
									{
										Logger.WarnException(string.Format("Agent {0} call error", _description.Name), task.Exception);
									}
									return task.IsFaulted
												? SearchGoodClientEndpoint(syncRoot, setClient, creation, uris, func)
												: task.Result;
								});
					}

					return
						Task<TTaskResult>.Factory.StartNew(
							() => SearchGoodClientEndpoint(syncRoot, setClient, creation, uris, func));
				}
			}

			throw new InvalidOperationException("empty uris array");
		}

		private TTaskResult SearchGoodClientEndpoint<TClient, TTaskResult>(
			object syncRoot,
			Action<TClient> setClient,
			Func<Binding, EndpointAddress, TClient> creation,
			Uri[] uris,
			Func<TClient,Task<TTaskResult>> func)
			where TClient : class
			where TTaskResult : class
		{
			lock (syncRoot)
			{
				var exceptions = new List<Exception>();

				foreach (var url in uris)
				{
					try
					{
						var client = creation(_bindingsCollection.GetBinding(url), new EndpointAddress(url));
						var result = func(client).Result;
						setClient(client);
						return result;
					}
					catch (AggregateException e)
					{
						exceptions.AddRange(e.InnerExceptions);

						foreach (var exception in e.InnerExceptions)
						{
							Logger.WarnException(string.Format("Connect to agent {0} by {1}", _description.Name, url), exception);
						}
					}
					catch (Exception e)
					{
						exceptions.Add(e);

						Logger.WarnException(string.Format("Connect to agent {0} by {1}", _description.Name, url), e);
					}
				}

				throw new AggregateException(exceptions);
			}
		}

		private class CompilerProxy : ICompiler, IDisposable
		{
			private readonly RemoteCompilerService.ICompiler _compiler;

			public CompilerProxy(RemoteCompilerService.ICompiler compiler)
			{
				_compiler = compiler;
			}

			public bool IsReady()
			{
				return _compiler.IsReady();
			}

			public CompileOutput Compile(CompileInput localInput)
			{
				var remoteInput = new RemoteCompilerService.CompileInput
					{
						CompilerVersion = localInput.CompilerVersion,
						Arguments = localInput.Arguments,
						Src = localInput.Src,
						SrcLength = localInput.SrcLength,
						Artifacts = localInput.Artifacts
					};

				RemoteCompilerService.CompileOutput remoteOutput = _compiler.Compile(remoteInput);

				return
					new CompileOutput(
						new CompileStatus(
							remoteOutput.Status.Success,
							remoteOutput.Status.ExitCode,
							remoteOutput.Status.Cookies),
						remoteOutput.ResultData);
			}

			public void Dispose()
			{
				var disposable = _compiler as IDisposable;

				if (disposable != null)
				{
					disposable.Dispose();
				}
			}
		}

		private abstract class CompileCoordinatorProxyBase<TClient> : IDisposable
			where TClient : IRemoteCompileCoordinator
		{
			private const int MaxErrorCount = 3;
			private int _errorCount;

			private readonly RemoteAgentProxy _proxy;

			private IAgent _remoteDescription;
			private bool _remoteDescriptionInitialized;
			private object _remoteDescriptionSyncRoot;

			private readonly object _syncRoot = new object();
			protected TClient Client;

			protected CompileCoordinatorProxyBase(RemoteAgentProxy proxy)
			{
				_proxy = proxy;
			}

			public IAgentProxy Proxy
			{
				get { return RemoteProxy; }
			}
			protected RemoteAgentProxy RemoteProxy
			{
				get { return _proxy; }
			}
			protected object SyncRoot
			{
				get { return _syncRoot; }
			}

			public string Name
			{
				get
				{
					return RemoteProxy.Description.Name;
				}
			}

			public IAgent GetDescription()
			{
				return LazyInitializer.EnsureInitialized(
					ref _remoteDescription,
					ref _remoteDescriptionInitialized,
					ref _remoteDescriptionSyncRoot,
					delegate
						{
							IAgent agent = null;
							ConnectToAgent(client => agent = client.GetDescription());
							return agent;
						});
			}
			public Task<IAgent> GetDescriptionAsync()
			{
				return ConnectToAgent(client => client.GetDescriptionAsync()).ContinueWith(
					task => LazyInitializer.EnsureInitialized(
						ref _remoteDescription,
						ref _remoteDescriptionInitialized,
						ref _remoteDescriptionSyncRoot,
						() => task.Result),
					TaskContinuationOptions.OnlyOnRanToCompletion);
			}

			public void RegisterAgent(IAgentProxy proxy)
			{
				var message = proxy.Description as RemoteCompilerService.Agent
							?? new RemoteCompilerService.Agent(proxy.Description);

				ConnectToAgent(pool => pool.RegisterAgent(message));
			}
			public Task RegisterAgentAsync(IAgentProxy proxy)
			{
				var message = proxy.Description as RemoteCompilerService.Agent
							?? new RemoteCompilerService.Agent(proxy.Description);

				return ConnectToAgent(pool => pool.RegisterAgentAsync(message).ContinueWith(task =>
				{
					task.GetAwaiter().GetResult();
					return "";
				}));
			}

			public bool IncreaseErrorCount()
			{
				return Interlocked.Increment(ref _errorCount) >= MaxErrorCount;
			}
			public void ResetErrorCount()
			{
				_errorCount = 0;
			}

			protected abstract void ConnectToAgent(Action<TClient> func);
			protected abstract Task<TResult> ConnectToAgent<TResult>(Func<TClient, Task<TResult>> func) where TResult : class;

			public void Dispose()
			{
				var disposable = Client as IDisposable;

				if (disposable != null)
				{
					disposable.Dispose();
				}
			}
		}

		private class CompileCoordinatorProxy : CompileCoordinatorProxyBase<CompileCoordinatorClient>, ICompileCoordinatorProxy
		{
			public CompileCoordinatorProxy(RemoteAgentProxy proxy) : base(proxy)
			{
			}

			protected override void ConnectToAgent(Action<CompileCoordinatorClient> func)
			{
				RemoteProxy.ConnectToAgent(
					SyncRoot,
					() => Client,
					client => Client = client,
					(binding, endpoint) => new CompileCoordinatorClient(binding, endpoint),
					RemoteProxy.Description.AgentPoolUrls, 
					func);
			}
			protected override Task<TResult> ConnectToAgent<TResult>(Func<CompileCoordinatorClient, Task<TResult>> func) 
			{
				return RemoteProxy.ConnectToAgent(
					SyncRoot,
					() => Client,
					client => Client = client,
					(binding, endpoint) => new CompileCoordinatorClient(binding, endpoint),
					RemoteProxy.Description.AgentPoolUrls, 
					func);
			}
		}

		private class AgentPoolProxy : CompileCoordinatorProxyBase<AgentPoolClient>, IAgentPoolProxy
		{
			public AgentPoolProxy(RemoteAgentProxy proxy)
				: base(proxy)
			{
			}

			public IEnumerable<IAgent> GetAgents()
			{
				IEnumerable<IAgent> result = null;
				ConnectToAgent(pool => result = pool.GetAgents());
				return result;
			}
			public Task<IEnumerable<IAgent>> GetAgentsAsync()
			{
				return ConnectToAgent(pool => pool.GetAgentsAsync().ContinueWith(task => task.Result.Cast<IAgent>()));
			}

			protected override void ConnectToAgent(Action<AgentPoolClient> func)
			{
				RemoteProxy.ConnectToAgent(
					SyncRoot,
					() => Client,
					client => Client = client,
					(binding, endpoint) => new AgentPoolClient(binding, endpoint),
					RemoteProxy.Description.AgentPoolUrls,
					func);
			}
			protected override Task<TResult> ConnectToAgent<TResult>(Func<AgentPoolClient, Task<TResult>> func)
			{
				return RemoteProxy.ConnectToAgent(
					SyncRoot,
					() => Client,
					client => Client = client,
					(binding, endpoint) => new AgentPoolClient(binding, endpoint),
					RemoteProxy.Description.AgentPoolUrls,
					func);
			}
		}
	}
}