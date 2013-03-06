using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.Threading;
using DistCL.Utils;

namespace DistCL
{
	internal interface IBindingsCollection
	{
		Binding GetBinding(Uri url);
	}

	internal interface ICompilerServicesCollection
	{
		ServiceModelSectionGroup ServiceModelSectionGroup { get; }
		LocalAgentManager LocalAgentManager { get; }
		AgentPool AgentPool { get; }
		NetworkBuilder NetworkBuilder { get; }
		Compiler Compiler { get; }
		IBindingsCollection Bindings { get; }
	}

	internal class CompilerServicesCollection : ICompilerServicesCollection
	{
		private readonly ServiceModelSectionGroup _serviceModelSectionGroup;
		private Compiler _compiler;
		private IBindingsCollection _bindings;
		private LocalAgentManager _localAgentManager;
		private AgentPool _agentPool;
		private NetworkBuilder _networkBuilder;

		public CompilerServicesCollection(ServiceModelSectionGroup serviceModelSectionGroup)
		{
			_serviceModelSectionGroup = serviceModelSectionGroup;
		}

		public ServiceModelSectionGroup ServiceModelSectionGroup
		{
			get { return _serviceModelSectionGroup; }
		}

		public AgentPool AgentPool
		{
			get { return LazyInitializer.EnsureInitialized(ref _agentPool, () => new AgentPool(this)); }
		}

		public NetworkBuilder NetworkBuilder
		{
			get { return LazyInitializer.EnsureInitialized(ref _networkBuilder, () => new NetworkBuilder(this)); }
		}

		public Compiler Compiler
		{
			get { return _compiler; }
			set
			{
				if (Interlocked.CompareExchange(ref _compiler, value, null) != null)
				{
					throw new InvalidOperationException("Compiler already initialized");
				}
			}
		}

		public IBindingsCollection Bindings
		{
			get { return _bindings; }
			set
			{
				if (Interlocked.CompareExchange(ref _bindings, value, null) != null)
				{
					throw new InvalidOperationException("Bindings collection already initialized");
				}
			}
		}

		public LocalAgentManager LocalAgentManager
		{
			get { return _localAgentManager; }
			set
			{
				if (Interlocked.CompareExchange(ref _localAgentManager, value, null) != null)
				{
					throw new InvalidOperationException("Local agent manager already initialized");
				}
			}
		}
	}

	public class CompileServiceHost : ServiceHost, IBindingsCollection
	{
		private readonly Logger _logger = new Logger("SERVICE");

		private static ServiceModelSectionGroup _serviceModelSectionGroup;
		private static Dictionary<string, Binding> _bindings;
		private string _physicalPath;
		private readonly CompilerServicesCollection _compilerServices;

		public CompileServiceHost(params Uri[] baseAddresses)
			: base(new Compiler(new CompilerServicesCollection(ServiceModelSectionGroup)), baseAddresses)
		{
			_compilerServices = ((CompilerServicesCollection) CompilerInstance.CompilerServices);
			_compilerServices.Bindings = this;

			var hostName = Dns.GetHostName();
			var ipHostEntry = Dns.GetHostEntry(hostName);

			var agentPoolUrls = new List<Uri>();
			var compilerUrls = new List<Uri>();

			var agentPoolType = typeof (IAgentPool);
			var compilerType = typeof (ICompiler);
			var compileManagerType = typeof (ICompileManager);

			if (Description.Endpoints.Any(endpoint => compileManagerType.IsAssignableFrom(endpoint.Contract.ContractType)))
			{
				agentPoolType = compileManagerType;
				compilerType = compileManagerType;
			}

			foreach (var endpoint in Description.Endpoints)
			{
				if (agentPoolType.IsAssignableFrom(endpoint.Contract.ContractType))
				{
					if (endpoint.Address.Uri.IsLoopback)
					{
						var ub = new UriBuilder(endpoint.Address.Uri);

						foreach (var ip in ipHostEntry.AddressList)
						{
							if (Equals(ip, IPAddress.Loopback) || Equals(ip, IPAddress.IPv6Loopback))
								continue;

							ub.Host = ip.ToString();
							agentPoolUrls.Add(ub.Uri);
							Logger.DebugFormat("Found agent endpoint '{0}' ({1})", ub.Uri, endpoint.Contract.ContractType);
						}
					}
					else
					{
						agentPoolUrls.Add(endpoint.Address.Uri);
					}
				}

				if (compilerType.IsAssignableFrom(endpoint.Contract.ContractType))
				{
					if (endpoint.Address.Uri.IsLoopback)
					{
						var ub = new UriBuilder(endpoint.Address.Uri);

						foreach (var ip in ipHostEntry.AddressList)
						{
							if (Equals(ip, IPAddress.Loopback) || Equals(ip, IPAddress.IPv6Loopback))
								continue;

							ub.Host = ip.ToString();
							compilerUrls.Add(ub.Uri);
							Logger.DebugFormat("Found compiler endpoint '{0}' ({1})", ub.Uri, endpoint.Contract.ContractType);
						}
					}
					else
					{
						compilerUrls.Add(endpoint.Address.Uri);
					}
				}
			}

			_compilerServices.LocalAgentManager = new LocalAgentManager(
				_compilerServices, 
				agentPoolUrls.ToArray(), compilerUrls.ToArray());
		}

		public Logger Logger
		{
			get { return _logger; }
		}

		private static ServiceModelSectionGroup ServiceModelSectionGroup
		{
			get
			{
				if (_serviceModelSectionGroup == null)
				{
					_serviceModelSectionGroup = ServiceModelSectionGroup.GetSectionGroup(
						ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None));
				}
				return _serviceModelSectionGroup;
			}
		}

		public IDictionary<string, Binding> Bindings
		{
			get
			{
				if (_bindings == null)
				{
					_bindings = new Dictionary<string, Binding>();

					var clientBindings = new HashSet<string>();
					foreach (ChannelEndpointElement endpoint in ServiceModelSectionGroup.Client.Endpoints)
					{
						clientBindings.Add(endpoint.BindingConfiguration);
					}

					// TODO preferred binding name in config

					foreach (var binding in ServiceModelSectionGroup.Bindings.BindingCollections)
					{
						var schema = GetSchemaByBindingType(binding.BindingType);

						if (schema != null && ! _bindings.ContainsKey(schema))
						{
							foreach (var configuredBinding in binding.ConfiguredBindings
																	.OrderByDescending(b => clientBindings.Contains(b.Name)))
							{
								_bindings.Add(schema, (Binding)Activator.CreateInstance(binding.BindingType, configuredBinding.Name));
								break;
							}
						}
					}
				}
				return _bindings;
			}
		}

		private static string GetSchemaByBindingType(Type bindingType)
		{
			if (typeof(BasicHttpBinding).IsAssignableFrom(bindingType))
			{
				return "http";
			}
			
			if (typeof(BasicHttpsBinding).IsAssignableFrom(bindingType))
			{
				return "https";
			}
			
			if (typeof(NetTcpBinding).IsAssignableFrom(bindingType))
			{
				return "net.tcp";
			}

			return null;
		}

		private string PhysicalPath
		{
			get
			{
				if (_physicalPath == null)
				{
					// if hosted in IIS
					_physicalPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;

					if (String.IsNullOrEmpty(_physicalPath))
					{
						// for hosting outside of IIS
						_physicalPath = Path.GetDirectoryName(GetType().Assembly.Location);
					}
				}
				return _physicalPath;
			}
		}

		public Compiler CompilerInstance
		{
			get { return (Compiler) SingletonInstance; }
		}

		protected override void ApplyConfiguration()
		{
			var servicesSection = ConfigurationManager.GetSection("system.serviceModel/services") as ServicesSection;

			if (servicesSection == null ||
				servicesSection.Services.Cast<ServiceElement>().All(element => element.Name != Description.ConfigurationName))
			{
				var configFilename = Path.Combine(PhysicalPath, "DistCL.config");

				if (!string.IsNullOrEmpty(configFilename) && File.Exists(configFilename))
				{
					var fileMap = new ExeConfigurationFileMap {ExeConfigFilename = configFilename};
					var config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
					var serviceModel = ServiceModelSectionGroup.GetSectionGroup(config);

					if (serviceModel != null)
					{
						foreach (ServiceElement element in serviceModel.Services.Services)
						{
							if (element.Name == Description.ConfigurationName)
							{
								LoadConfigurationSection(element);
								return;
							}
						}
					}
				}
			}

			base.ApplyConfiguration();
		}

		protected override void OnOpened()
		{
			base.OnOpened();

			_compilerServices.NetworkBuilder.Open();
		}

		protected override void OnClosing()
		{
			base.OnClosing();

			_compilerServices.NetworkBuilder.Close();
		}

		public Binding GetBinding(Uri url)
		{
			Binding binding;
			return Bindings.TryGetValue(url.Scheme.ToLower(), out binding) ? binding : null;
		}
	}
}