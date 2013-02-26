using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Configuration;
using System.ServiceProcess;
using HttpNamespaceManager.Lib;
using HttpNamespaceManager.Lib.AccessControl;

namespace DistCL.Service
{
	[RunInstaller(true)]
	public partial class ProjectInstaller : System.Configuration.Install.Installer
	{
		public ProjectInstaller()
		{
			InitializeComponent();
		}

		protected override void OnBeforeInstall(IDictionary savedState)
		{
			base.OnBeforeInstall(savedState);

			if (Context.Parameters.ContainsKey("username") && !string.IsNullOrEmpty(Context.Parameters["username"]))
			{
				serviceProcessInstaller.Account = ServiceAccount.User;
				serviceProcessInstaller.Username = Context.Parameters["username"];
			}

			if (Context.Parameters.ContainsKey("password") && !string.IsNullOrEmpty(Context.Parameters["password"]))
			{
				serviceProcessInstaller.Account = ServiceAccount.User;
				serviceProcessInstaller.Password = Context.Parameters["password"];
			}
		}

		public override void Commit(IDictionary savedState)
		{
			base.Commit(savedState);

			using (var httpApi = new HttpApi())
			{
				var sid = serviceProcessInstaller.Account == ServiceAccount.User
							? SecurityIdentity.SecurityIdentityFromName(serviceProcessInstaller.Username)
							: SecurityIdentity.SecurityIdentityFromWellKnownSid(WELL_KNOWN_SID_TYPE.WinNetworkServiceSid);

				foreach (
					var configuration in
						new[]
							{
								ConfigurationManager.OpenExeConfiguration(GetType().Assembly.Location),
								ConfigurationManager.OpenMappedExeConfiguration(
									new ExeConfigurationFileMap
										{
											ExeConfigFilename = Path.Combine(
												Path.GetDirectoryName(GetType().Assembly.Location),
												"DistCL.config")
										},
									ConfigurationUserLevel.None)
							})
				{
					var serviceModel = ServiceModelSectionGroup.GetSectionGroup(configuration);

					Dictionary<string, SecurityDescriptor> acls = httpApi.QueryHttpNamespaceAcls();

					var bindingModes = new Dictionary<string, HostNameComparisonMode>();

					foreach (BasicHttpBindingElement binding in serviceModel.Bindings.BasicHttpBinding.Bindings)
					{
						bindingModes[binding.Name] = binding.HostNameComparisonMode;
					}

					if (serviceModel != null && serviceModel.Services != null)
					{
						foreach (ServiceElement service in serviceModel.Services.Services)
						{
							var addresses = new HashSet<string>();

							var baseAddresses = new List<string>();
							if (service.Host != null)
							{
								foreach (BaseAddressElement baseAddress in service.Host.BaseAddresses)
								{
									try
									{
										var address = new Uri(baseAddress.BaseAddress);

										if (address.IsLoopback && address.Scheme == "http")
										{
											baseAddresses.Add(address.ToString());
										}
									}
									catch (UriFormatException)
									{
									}
								}
							}

							foreach (ServiceEndpointElement endpoint in service.Endpoints)
							{
								if (endpoint.Binding != "basicHttpBinding")
									continue;

								var uris = new List<Uri>();

								if (endpoint.Address.IsAbsoluteUri)
								{
									uris.Add(endpoint.Address);
								}
								else
								{
									uris.AddRange(baseAddresses.Select(baseAddress => new Uri(baseAddress)));
								}

								switch (bindingModes[endpoint.BindingConfiguration])
								{
									case HostNameComparisonMode.Exact:
										foreach (var uri in uris)
										{
											addresses.Add(uri.ToString());
										}
										break;

									case HostNameComparisonMode.StrongWildcard:
										foreach (var uri in uris)
										{
											var tmpHost = Guid.NewGuid().ToString();
											addresses.Add(new UriBuilder(uri) {Host = tmpHost}.Uri.AbsoluteUri.Replace(tmpHost, "+"));
										}
										break;
									case HostNameComparisonMode.WeakWildcard:
										foreach (var uri in uris)
										{
											var tmpHost = Guid.NewGuid().ToString();
											addresses.Add(new UriBuilder(uri) {Host = tmpHost}.Uri.AbsoluteUri.Replace(tmpHost, "*"));
										}
										break;
								}
							}

							foreach (var url in addresses)
							{
								var ace = new AccessControlEntry(sid) {AceType = AceType.AccessAllowed};
								ace.Add(AceRights.GenericAll);

								SecurityDescriptor acl;

								if (acls.TryGetValue(url, out acl) ||
									acls.TryGetValue(url.TrimEnd('/') + "/", out acl))
								{
									bool found = false;

									foreach (var entry in acl.DACL)
									{
										if (entry.AccountSID == sid)
										{
											entry.AceType = ace.AceType;
											entry.Clear();
											foreach (var rights in ace)
											{
												entry.Add(rights);
											}
											found = true;
											break;
										}
									}

									if (!found)
									{
										acl.DACL.Add(ace);
									}

									Context.LogMessage(string.Format("RemoveHttpHamespaceAcl('{0}')", url));
									httpApi.RemoveHttpHamespaceAcl(url);
								}
								else
								{
									acl = new SecurityDescriptor {DACL = new AccessControlList()};
									acl.DACL.Add(ace);
								}

								Context.LogMessage(string.Format("SetHttpNamespaceAcl('{0}', {1})", url, acl));
								httpApi.SetHttpNamespaceAcl(url, acl);
							}
						}
					}
				}
			}
		}
	}
}