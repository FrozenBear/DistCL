using System.Collections;
using System.ComponentModel;
using System.ServiceProcess;

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
	}
}