using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Configuration;
using System.Threading;
using DistCL.RemoteCompilerService;

namespace DistCL
{
	public class CompileServiceHost : ServiceHost
	{
		private readonly object _syncRoot = new object();
		private bool _closed;
		private string _physicalPath;

		public CompileServiceHost(params Uri[] baseAddresses) : base(new Compiler(), baseAddresses)
		{
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

			_closed = false;

			var updateAgentsThread = new Thread(UpdateAgents) {IsBackground = true};
			updateAgentsThread.Start();
		}

		private void UpdateAgents(object o)
		{
			ServiceModelSectionGroup serviceModelSectionGroup =
				ServiceModelSectionGroup.GetSectionGroup(
					ConfigurationManager.OpenExeConfiguration(
						ConfigurationUserLevel.None));

			var contracts = new HashSet<string>();
			foreach (ServiceContractAttribute attribute in
				typeof (RemoteCompilerService.IAgentPool).GetCustomAttributes(typeof (ServiceContractAttribute), true))
			{
				contracts.Add(attribute.ConfigurationName);
			}

			var pools = new List<RemoteCompilerService.IAgentPool>();
			foreach (ChannelEndpointElement endpoint in serviceModelSectionGroup.Client.Endpoints)
			{
				if (contracts.Contains(endpoint.Contract))
				{
					pools.Add(new AgentPoolClient(endpoint.Name));
				}
			}

			var agentPoolUrls = new List<Uri>();
			var compilerUrls = new List<Uri>();
			foreach (var endpoint in Description.Endpoints)
			{
				if (typeof (IAgentPool).IsAssignableFrom(endpoint.Contract.ContractType))
				{
					agentPoolUrls.Add(endpoint.Address.Uri);
				}

				if (typeof (ICompiler).IsAssignableFrom(endpoint.Contract.ContractType))
				{
					compilerUrls.Add(endpoint.Address.Uri);
				}
			}

			var localProvider = new LocalCompilerProvider(CompilerInstance, agentPoolUrls.ToArray(), compilerUrls.ToArray());
			var agent = new RemoteCompilerService.AgentRequest
				{
					Guid = localProvider.Agent.Guid,
					Name = localProvider.Agent.Name,
					Cores = localProvider.Agent.Cores,
					AgentPoolUrls = localProvider.Agent.AgentPoolUrls,
					CompilerUrls = localProvider.Agent.CompilerUrls
				};

			lock (_syncRoot)
			{
				while (! _closed)
				{
					foreach (var pool in pools)
					{
						pool.RegisterAgent(agent);
					}

					CompilerInstance.Agents.RegisterAgent(localProvider);

					CompilerInstance.Agents.Clean(DateTime.Now.Subtract(CompilerSettings.Default.AgentsSilenceLimit));

					Monitor.Wait(_syncRoot, CompilerSettings.Default.AgentsUpdatePeriod);
				}
			}
		}

		protected override void OnClosing()
		{
			base.OnClosing();

			lock (_syncRoot)
			{
				_closed = true;
				Monitor.PulseAll(_syncRoot);
			}
		}
	}
}