﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using DistCL.RemoteCompilerService;

namespace DistCL
{
	internal interface IAgentProxy
	{
		IAgent Description { get; }
		ICompiler GetCompiler();
		ICompileCoordinatorInternal GetCoordinator();
		IAgentPoolInternal GetAgentPool();
	}

	internal class RemoteAgentProxy : IAgentProxy
	{
		private readonly IBindingsProvider _bindingsProvider1;
		private readonly IAgent _description;

		private CompilerProxy _compiler;
		private ICompileCoordinatorInternal _compileCoordinatorProxy;
		private AgentPoolProxy _agentPool;

		public RemoteAgentProxy(IBindingsProvider bindingsProvider, IAgent description)
		{
			_bindingsProvider1 = bindingsProvider;
			_description = description;
		}

		public IAgent Description
		{
			get { return _description; }
		}

		public ICompiler GetCompiler()
		{
			LazyInitializer.EnsureInitialized(ref _compiler, GetCompilerInternal);
			return _compiler;
		}

		public ICompileCoordinatorInternal GetCoordinator()
		{
			LazyInitializer.EnsureInitialized(ref _compileCoordinatorProxy,
											() =>
											Description.AgentPoolUrls != null && Description.AgentPoolUrls.Length > 0
												? (ICompileCoordinatorInternal)GetAgentPool()
												: new CompileCoordinatorProxy(this));
			return _compileCoordinatorProxy;
		}

		public IAgentPoolInternal GetAgentPool()
		{
			return LazyInitializer.EnsureInitialized(ref _agentPool, () => new AgentPoolProxy(this));
		}

		private CompilerProxy GetCompilerInternal()
		{
			foreach (var url in _description.CompilerUrls)
			{
				try
				{
					RemoteCompilerService.ICompiler compiler = new CompilerClient(
						_bindingsProvider1.GetBinding(url),
						new EndpointAddress(url));
					{
						return compiler.IsReady() ? new CompilerProxy(compiler) : null;
					}
				}
				catch (Exception e)
				{
					// TODO logger?
				}
			}

			return null;
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
					// TODO logging
				}

				SearchGoodClientEndpoint<TClient, object>(syncRoot, setClient, creation, uris, (clnt) =>
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
							// TODO logging
							task => task.IsFaulted
										? SearchGoodClientEndpoint(syncRoot, setClient, creation, uris, func)
										: task.Result);
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
						var client = creation(_bindingsProvider1.GetBinding(url), new EndpointAddress(url));
						var result = func(client).Result;
						setClient(client);
						return result;
					}
					catch (AggregateException e)
					{
						exceptions.AddRange(e.InnerExceptions);
					}
					catch (Exception e)
					{
						exceptions.Add(e);
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
						Arguments = localInput.Arguments,
						Src = localInput.Src,
						SrcLength = localInput.SrcLength,
						SrcName = localInput.SrcName
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

			private readonly RemoteAgentProxy _proxy1;
			private IAgent _remoteDescription;

			private readonly object _syncRoot = new object();
			private readonly object _descriptionSyncRoot = new object();
			protected TClient Client;

			protected CompileCoordinatorProxyBase(RemoteAgentProxy proxy)
			{
				_proxy1 = proxy;
			}

			public string Name
			{
				get
				{
					return RemoteProxy.Description.Name;
				}
			}

			public IAgentProxy Proxy
			{
				get { return RemoteProxy; }
			}

			protected RemoteAgentProxy RemoteProxy
			{
				get { return _proxy1; }
			}

			protected object SyncRoot
			{
				get { return _syncRoot; }
			}

			protected abstract void ConnectToAgent(Action<TClient> func);

			protected abstract Task<TResult> ConnectToAgent<TResult>(Func<TClient, Task<TResult>> func) where TResult : class;

			public IAgent GetDescription()
			{
				LazyInitializer.EnsureInitialized(ref _remoteDescription, () =>
				{
					IAgent agent = null;
					ConnectToAgent(client => agent = client.GetDescription());
					return agent;
				});

				return _remoteDescription;
			}

			public Task<IAgent> GetDescriptionAsync()
			{
				return ConnectToAgent(client => client.GetDescriptionAsync()).ContinueWith(
					delegate(Task<IAgent> task)
						{
							// TODO lock
							_remoteDescription = task.Result;
							return task.Result;
						},
					TaskContinuationOptions.OnlyOnRanToCompletion);
			}

			public void Dispose()
			{
				var disposable = Client as IDisposable;

				if (disposable != null)
				{
					disposable.Dispose();
				}
			}

			public bool IncreaseErrorCount()
			{
				return Interlocked.Increment(ref _errorCount) >= MaxErrorCount;
			}

			public void ResetErrorCount()
			{
				_errorCount = 0;
			}
		}

		private class CompileCoordinatorProxy : CompileCoordinatorProxyBase<CompileCoordinatorClient>, ICompileCoordinatorInternal
		{
			public CompileCoordinatorProxy(RemoteAgentProxy proxy) : base(proxy)
			{
			}

			public void RegisterAgent(IAgentProxy proxy)
			{
				var message = proxy.Description as RemoteCompilerService.AgentRegistrationMessage
							?? new RemoteCompilerService.AgentRegistrationMessage(proxy.Description);

				ConnectToAgent(pool => pool.RegisterAgent(message));
			}

			public Task RegisterAgentAsync(IAgentProxy proxy)
			{
				var message = proxy.Description as RemoteCompilerService.AgentRegistrationMessage
							?? new RemoteCompilerService.AgentRegistrationMessage(proxy.Description);

				return ConnectToAgent(pool => pool.RegisterAgentAsync(message).ContinueWith(task =>
					{
						task.GetAwaiter().GetResult();
						return "";
					}));
			}

			protected override void ConnectToAgent(Action<CompileCoordinatorClient> func)
			{
				RemoteProxy.ConnectToAgent(
					SyncRoot,
					() => Client,
					(client) => Client = client,
					(binding, endpoint) => new CompileCoordinatorClient(binding, endpoint),
					RemoteProxy.Description.AgentPoolUrls, 
					func);
			}

			protected override Task<TResult> ConnectToAgent<TResult>(Func<CompileCoordinatorClient, Task<TResult>> func) 
			{
				return RemoteProxy.ConnectToAgent(
					SyncRoot,
					() => Client,
					(client) => Client = client,
					(binding, endpoint) => new CompileCoordinatorClient(binding, endpoint),
					RemoteProxy.Description.AgentPoolUrls, 
					func);
			}
		}

		private class AgentPoolProxy : CompileCoordinatorProxyBase<AgentPoolClient>, IAgentPoolInternal
		{
			public AgentPoolProxy(RemoteAgentProxy proxy)
				: base(proxy)
			{
			}

			public void RegisterAgent(IAgentProxy proxy)
			{
				var message = proxy.Description as RemoteCompilerService.AgentRegistrationMessage
							?? new RemoteCompilerService.AgentRegistrationMessage(proxy.Description);

				ConnectToAgent(pool => pool.RegisterAgent(message));
			}

			public Task RegisterAgentAsync(IAgentProxy proxy)
			{
				var message = proxy.Description as RemoteCompilerService.AgentRegistrationMessage
							?? new RemoteCompilerService.AgentRegistrationMessage(proxy.Description);

				return ConnectToAgent(pool => pool.RegisterAgentAsync(message).ContinueWith(task =>
				{
					task.GetAwaiter().GetResult();
					return "";
				}));
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
					(client) => Client = client,
					(binding, endpoint) => new AgentPoolClient(binding, endpoint),
					RemoteProxy.Description.AgentPoolUrls,
					func);
			}

			protected override Task<TResult> ConnectToAgent<TResult>(Func<AgentPoolClient, Task<TResult>> func)
			{
				return RemoteProxy.ConnectToAgent(
					SyncRoot,
					() => Client,
					(client) => Client = client,
					(binding, endpoint) => new AgentPoolClient(binding, endpoint),
					RemoteProxy.Description.AgentPoolUrls,
					func);
			}
		}
	}
}