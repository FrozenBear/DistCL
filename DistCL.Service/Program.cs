using System.Collections.Generic;
using System.ServiceProcess;

namespace DistCL.Service
{
	internal static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		private static void Main()
		{
			var servicesToRun = new List<ServiceBase>();

			servicesToRun.Add(new CompileService());

			ServiceBase.Run(servicesToRun.ToArray());
		}
	}
}