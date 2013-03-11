using System;
using System.ServiceProcess;
using DistCL.Utils;

namespace DistCL.Service
{
	partial class CompileService : ServiceBase
	{
		private CompileServiceHost _serviceHost;
		private readonly Logger _logger = new Logger("SERVICE");

		public CompileService()
		{
			InitializeComponent();
		}

		public Logger Logger
		{
			get { return _logger; }
		}

		protected override void OnStart(string[] args)
		{
			try
			{
				if (_serviceHost != null)
				{
					_serviceHost.Close();
				}

				Logger.Debug("WCF service starting...");
				_serviceHost = new CompileServiceHost();
				_serviceHost.Open();
				Logger.Info("WCF service started");
			}
			catch (Exception e)
			{
				Logger.LogException("OnStart error", e);
				throw;
			}
		}

		protected override void OnStop()
		{
			try
			{
				Logger.Debug("WCF service stopping...");
				if (_serviceHost != null)
				{
					_serviceHost.Close();
					_serviceHost = null;
				}
				Logger.Info("WCF service stopped");
			}
			catch (Exception e)
			{
				Logger.LogException("OnStop error", e);
				throw;
			}
		}
	}
}