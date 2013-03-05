using System.ServiceProcess;

namespace DistCL.Service
{
	partial class CompileService : ServiceBase
	{
		private CompileServiceHost _serviceHost;

		public CompileService()
		{
			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
			if (_serviceHost != null)
			{
				_serviceHost.Close();
			}

			_serviceHost = new CompileServiceHost();


			_serviceHost.Open();

			EventLog.WriteEntry("WCF service started");
		}

		protected override void OnStop()
		{
			if (_serviceHost != null)
			{
				_serviceHost.Close();
				_serviceHost = null;
			}
		}
	}
}