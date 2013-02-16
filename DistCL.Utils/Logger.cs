using System;
using System.Reflection;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace DistCL.Utils
{
	public static class Logger
	{
		private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public static void LogAgent(string source, string agentName)
		{
			Info(string.Format("{0}: Name='{1}'", source, agentName));
		}

		public static void Info(string message)
		{
			_logger.Info(message);
		}

		public static void InfoFormat(string message, params object[] args)
		{
			_logger.InfoFormat(message, args);
		}

		public static void Warn(string message)
		{
			_logger.Warn(message);
		}

		public static void WarnFormat(string message, params object[] args)
		{
			_logger.WarnFormat(message, args);
		}

		public static void LogException(string message, Exception e)
		{
			_logger.Fatal(message, e);
		}
	}
}