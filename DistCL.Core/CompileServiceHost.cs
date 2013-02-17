using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;

namespace DistCL
{
	internal interface IBindingsProvider
	{
		Binding GetBinding(Uri url);
	}

	public class CompileServiceHost : ServiceHost, IBindingsProvider
	{
		private static ServiceModelSectionGroup _serviceModelSectionGroup;
		private static Dictionary<string, Binding> _bindings;
		private string _physicalPath;
		private readonly NetworkBuilder _networkBuilder;

		public CompileServiceHost(params Uri[] baseAddresses) : base(new Compiler(), baseAddresses)
		{
			CompilerInstance.BindingsProvider = this;

			var agentPoolUrls = new List<Uri>();
			var compilerUrls = new List<Uri>();

			foreach (var endpoint in Description.Endpoints)
			{
				if (typeof (IAgentPool).IsAssignableFrom(endpoint.Contract.ContractType))
				{
					agentPoolUrls.Add(endpoint.Address.Uri.IsLoopback
										? new UriBuilder(endpoint.Address.Uri) {Host = Environment.MachineName}.Uri
										: endpoint.Address.Uri);
				}

				if (typeof (ICompiler).IsAssignableFrom(endpoint.Contract.ContractType))
				{
					compilerUrls.Add(endpoint.Address.Uri.IsLoopback
										? new UriBuilder(endpoint.Address.Uri) {Host = Environment.MachineName}.Uri
										: endpoint.Address.Uri);
				}
			}

			_networkBuilder = new NetworkBuilder(
				ServiceModelSectionGroup,
				this,
				new LocalCompilerProvider(CompilerInstance, agentPoolUrls.ToArray(), compilerUrls.ToArray()),
				CompilerInstance.AgentPool);
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

			if (typeof(NetPeerTcpBinding).IsAssignableFrom(bindingType))
			{
				return "net.p2p";
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