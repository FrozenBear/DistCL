using System;

namespace DistCL.Utils
{
	public static class Logger
	{
		public const string ClientSource = "CLIENT";

		public static void LogAgent(string source, string agentName)
		{
			Log(source, string.Format("Name='{0}'", agentName));
		}

		public static void Log(string source, object obj)
		{
			Log(source, Convert.ToString(obj));
		}

		public static void Log(string source, string message)
		{
			Console.WriteLine("{0} [INFO] {1}: {2}", DateTime.Now.ToString("s"), source, message);
		}

		public static void Warning(string source, string message)
		{
			Console.WriteLine("{0} [WARNING] {1}: {2}", DateTime.Now.ToString("s"), source, message);
		}
	}
}