using System;
using System.Diagnostics;
using System.Threading;

namespace DistCL.Hooks
{
	public class RemoteHooks : MarshalByRefObject
	{
		public bool ProcessTerminated { get; set; }

		public void IsInstalled(Int32 InClientPID)
		{
			Console.WriteLine("DistCL hook has been installed in target {0}.\r\n", InClientPID);
		}

		public void ReportException(Exception InInfo)
		{
			Console.WriteLine("The target process has reported an error:\r\n" + InInfo.ToString());
		}

		public void Ping()
		{
		}

		//public override object InitializeLifetimeService()
		//{
		//	return null;
		//}

		// ----- Win32 API hook handlers ---
		public void OnCreateFile(Int32 InClientPID, string InFileName)
		{
			Console.WriteLine("[CREATE_FILE], file: {2}, process id: {0}, thread id: {1}", Process.GetCurrentProcess().Id, Thread.CurrentThread.ManagedThreadId, InFileName);
		}

		public void OnGetFileAttrs(string lpFileName)
		{
			Console.WriteLine("[GET_FILE_ATTRS]: {0}", lpFileName);
		}

		public void OnFindFirstFile(string lpFileName)
		{
			Console.WriteLine("[FIND FIRST FILE]: {0}", lpFileName);
		}
	}
}
