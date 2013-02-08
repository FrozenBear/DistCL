using System;

namespace DistCL.ConsoleTest
{
	class Program
	{
		static void Main(string[] args)
		{
			var serviceHost = new CompileServiceHost();

			serviceHost.Open();
			Console.WriteLine("WCF service started");

			Console.WriteLine("Press any key for exit");
			Console.ReadKey();

			serviceHost.Close();
		}
	}
}
