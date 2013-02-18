using System;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace DistCL.Utils
{
	public class Logger
	{
		private readonly log4net.ILog _logger;

		public Logger(string name)
		{
			_logger = log4net.LogManager.GetLogger(name);
		}

		public void LogAgent(string message, string agentName)
		{
			Info(string.Format("{0}: Name='{1}'", message, agentName));
		}

		public void Debug(string message)
		{
			_logger.Debug(message);
		}

		public void DebugFormat(string message, params object[] args)
		{
			_logger.DebugFormat(message, args);
		}

		public void Info(string message)
		{
			_logger.Info(message);
		}

		public void InfoFormat(string message, params object[] args)
		{
			_logger.InfoFormat(message, args);
		}

		public void Warn(string message)
		{
			_logger.Warn(message);
		}

		public void WarnFormat(string message, params object[] args)
		{
			_logger.WarnFormat(message, args);
		}

		public void LogException(string message, Exception e)
		{
			_logger.Fatal(message, e);
		}

		public bool DebugEnabled
		{
			get { return _logger.IsDebugEnabled; }
		}
	}
}