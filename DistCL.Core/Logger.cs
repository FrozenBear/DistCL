using System;
using System.Linq;

namespace DistCL
{
	class Logger
	{
		public static void Log(string source, IAgent agent)
		{
			Log(source, string.Format("Name='{1}'", agent.Guid, agent.Name));
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

		public static void Log(string source, object obj)
		{
			Log(source, Convert.ToString(obj));
		}

		public static void Log(string source, string message)
		{
			Console.WriteLine("[INFO] {0}: {1}", source, message);
		}

		public static void Warning(string source, string message)
		{
			Console.WriteLine("[WARNING] {0}: {1}", source, message);
		}
	}
}
