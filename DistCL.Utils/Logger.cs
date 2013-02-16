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
			Info(string.Format("Name='{0}'", agentName));
			//Log(source, string.Format("Guid='{0}', Name='{1}'", agent.Guid, agent.Name));
//			Log(source, string.Format(
//				"\n\tGuid: {0},\n\tName: {1},\n\tCores: {2},\n\tAgentPoolUrls: {3},\n\tCompilerUrls: {4}",
//				agent.Guid,
//				agent.Name,
//				agent.Cores,
//				string.Join(", ", agent.AgentPoolUrls.Select(url => url.ToString()).ToArray()),
//				string.Join(", ", agent.CompilerUrls.Select(url => url.ToString()).ToArray()))
//				);
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