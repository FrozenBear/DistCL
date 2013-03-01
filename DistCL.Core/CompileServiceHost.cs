using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using DistCL.Utils;

namespace DistCL
{
	internal interface IBindingsProvider
	{
		Binding GetBinding(Uri url);
	}

	public class CompileServiceHost : ServiceHost, IBindingsProvider
	{
		private readonly Logger _logger = new Logger("SERVICE");

		private static ServiceModelSectionGroup _serviceModelSectionGroup;
		private static Dictionary<string, Binding> _bindings;
		private string _physicalPath;
		private readonly NetworkBuilder _networkBuilder;

		public CompileServiceHost(params Uri[] baseAddresses) : base(new Compiler(), baseAddresses)
		{
			CompilerInstance.BindingsProvider = this;

			var hostName = Dns.GetHostName();
			var ipHostEntry = Dns.GetHostEntry(hostName);

			var agentPoolUrls = new List<Uri>();
			var compilerUrls = new List<Uri>();

			var agentPoolType = typeof(IAgentPool);
			var compilerType = typeof(ICompiler);
			var compileManagerType = typeof(ICompileManager);

			foreach (var endpoint in Description.Endpoints)
			{
				if (compileManagerType.IsAssignableFrom(endpoint.Contract.ContractType))
				{
					agentPoolType = compileManagerType;
					compilerType = compileManagerType;
					break;
				}
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

			_networkBuilder = new NetworkBuilder(
				ServiceModelSectionGroup,
				this,
				new LocalAgentManager(CompilerInstance.AgentPool, CompilerInstance, agentPoolUrls.ToArray(), compilerUrls.ToArray()),
				CompilerInstance.AgentPool);
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

					foreach (Binding binding in Description.Endpoints.Select(endpoint => endpoint.Binding))
					{
						string schema = GetSchemaByBindingType(binding.GetType());

						if (schema != null && ! _bindings.ContainsKey(schema))
						{
							_bindings.Add(schema, binding);
						}
					}

					foreach (BindingCollectionElement binding in ServiceModelSectionGroup.Bindings.BindingCollections)
					{
						string schema = GetSchemaByBindingType(binding.BindingType);

						if (schema != null && ! _bindings.ContainsKey(schema))
						{
							foreach (var configuredBinding in binding.ConfiguredBindings)
							{
								_bindings.Add(schema, (Binding)Activator.CreateInstance(binding.BindingType, configuredBinding.Name));
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

		private NetworkBuilder NetworkBuilder
		{
			get { return _networkBuilder; }
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

			NetworkBuilder.Open();
		}

		protected override void OnClosing()
		{
			base.OnClosing();

			NetworkBuilder.Close();
		}

		public Binding GetBinding(Uri url)
		{
			return Bindings[url.Scheme.ToLower()];
		}
	}
}