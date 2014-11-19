using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using EasyHook;

namespace DistCL.InjectLib
{
	public class HookEntryPoint : IEntryPoint
	{
		private DistCL.Hooks.RemoteHooks _remoteObserver;
		private LocalHook _createFileHook;
		private LocalHook _getFileAttributesHook;
		private LocalHook _findFirstFileHook;

		public HookEntryPoint(
			RemoteHooking.IContext context,
			String channelName)
		{
			// connect to host...
			_remoteObserver = RemoteHooking.IpcConnectClient<DistCL.Hooks.RemoteHooks>(channelName);
			_remoteObserver.Ping();
		}

		public void Run(
			RemoteHooking.IContext context,
			String channelName)
		{
			Load();
			MainLoop();
			Unload();
		}

		public void MainLoop()
		{
			try
			{
				// All of our program's main execution takes place within the OnCreateFile() hook callback. So we don't really do anything here.
				// Except Ping() our FileMonitorController program to make sure it's still alive; if it's closed, we should also close
				while (!_remoteObserver.ProcessTerminated)
				{
					Thread.Sleep(0);
					_remoteObserver.Ping();
				}

				// When this method returns (and, consequently, Run()), our injected DLL terminates and that's the end of our program (though Unload() will be called first)
			}
			catch (Exception e)
			{
				_remoteObserver.ReportException(e);
			}
		}

		private void Load()
		{
			// install hook...
			try
			{
				var acl = new Int32[] { 0 };

				_createFileHook = LocalHook.Create(
					LocalHook.GetProcAddress("kernel32.dll", "CreateFileW"),
					new DCreateFile(CreateFile_Hooked),
					this);
				_createFileHook.ThreadACL.SetExclusiveACL(acl);

				_getFileAttributesHook = LocalHook.Create(
					LocalHook.GetProcAddress("kernel32.dll", "GetFileAttributesW"),
					new DGetFileAttributes(GetFileAttributes_Hooked),
					this);
				_getFileAttributesHook.ThreadACL.SetExclusiveACL(acl);

				_findFirstFileHook =  LocalHook.Create(
					LocalHook.GetProcAddress("kernel32.dll", "FindFirstFileW"),
					new DFindFirstFile(FindFirstFile_Hooked),
					this);
				_findFirstFileHook.ThreadACL.SetExclusiveACL(acl);

				// All hooks start de-activated
				// The following ensures that this hook can be intercepted from all threads of this process
				_remoteObserver.IsInstalled(RemoteHooking.GetCurrentProcessId());

				RemoteHooking.WakeUpProcess();
			}
			catch (Exception e)
			{
				_remoteObserver.ReportException(e);
			}
		}

		private void Unload()
		{
			try
			{
				// We're exiting our program now
				_createFileHook.Dispose();
				_getFileAttributesHook.Dispose();
				_findFirstFileHook.Dispose();

				Console.WriteLine("[DistCLHOOK] Unloaded");
			}
			catch (Exception ex)
			{
				_remoteObserver.ReportException(ex);
			}
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall,
			CharSet = CharSet.Unicode,
			SetLastError = true)]
		private delegate IntPtr DCreateFile(
			String InFileName,
			UInt32 InDesiredAccess,
			UInt32 InShareMode,
			IntPtr InSecurityAttributes,
			UInt32 InCreationDisposition,
			UInt32 InFlagsAndAttributes,
			IntPtr InTemplateFile);

		// just use a P-Invoke implementation to get native API access from C# (this step is not necessary for C++.NET)
		[DllImport("kernel32.dll",
			CharSet = CharSet.Unicode,
			SetLastError = true,
			CallingConvention = CallingConvention.StdCall)]
		private static extern IntPtr CreateFile(
			String InFileName,
			UInt32 InDesiredAccess,
			UInt32 InShareMode,
			IntPtr InSecurityAttributes,
			UInt32 InCreationDisposition,
			UInt32 InFlagsAndAttributes,
			IntPtr InTemplateFile);

		// this is where we are intercepting all file accesses!
		private IntPtr CreateFile_Hooked(
			String InFileName,
			UInt32 InDesiredAccess,
			UInt32 InShareMode,
			IntPtr InSecurityAttributes,
			UInt32 InCreationDisposition,
			UInt32 InFlagsAndAttributes,
			IntPtr InTemplateFile)
		{

			try
			{
				//Console.WriteLine("[CREATEFILE HOOK]: process id: {0}, thread id: {1}", Process.GetCurrentProcess().Id,
				//				  Thread.CurrentThread.ManagedThreadId);

				_remoteObserver.OnCreateFile(RemoteHooking.GetCurrentProcessId(), "[" + RemoteHooking.GetCurrentProcessId() + ":" +
				                                                                 RemoteHooking.GetCurrentThreadId() + "]: \"" +
				                                                                 InFileName + "\"");
				//Console.WriteLine("2[CREATEFILE HOOK]");
			}
			catch (Exception e)
			{
				_remoteObserver.ReportException(e);
			}

			// call original API...
			return CreateFile(
				InFileName,
				InDesiredAccess,
				InShareMode,
				InSecurityAttributes,
				InCreationDisposition,
				InFlagsAndAttributes,
				InTemplateFile);
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall,
			CharSet = CharSet.Unicode,
			SetLastError = true)]
		private delegate uint DGetFileAttributes(string lpFileName);

		// just use a P-Invoke implementation to get native API access from C# (this step is not necessary for C++.NET)
		[DllImport("kernel32.dll",
			CharSet = CharSet.Unicode,
			SetLastError = true,
			CallingConvention = CallingConvention.StdCall)]
		private static extern UInt32 GetFileAttributes(string lpFileName);


		// this is where we are intercepting all file accesses!
		private UInt32 GetFileAttributes_Hooked(String lpFileName)
		{
			try
			{
				_remoteObserver.OnGetFileAttrs(lpFileName);
			}
			catch (Exception e)
			{
				_remoteObserver.ReportException(e);
			}

			// call original API...
			return GetFileAttributes(lpFileName);
		}

		public const int MAX_PATH = 260;
		public const int MAX_ALTERNATE = 14;

		[StructLayout(LayoutKind.Sequential)]
		public struct FILETIME
		{
			public uint dwLowDateTime;
			public uint dwHighDateTime;
		};

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct WIN32_FIND_DATA
		{
			public FileAttributes dwFileAttributes;
			public FILETIME ftCreationTime;
			public FILETIME ftLastAccessTime;
			public FILETIME ftLastWriteTime;
			public uint nFileSizeHigh; //changed all to uint, otherwise you run into unexpected overflow
			public uint nFileSizeLow;  //|
			public uint dwReserved0;   //|
			public uint dwReserved1;   //v
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
			public string cFileName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_ALTERNATE)]
			public string cAlternate;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

		[UnmanagedFunctionPointer(CallingConvention.StdCall,
			CharSet = CharSet.Unicode,
			SetLastError = true)]
		private delegate IntPtr DFindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

		// this is where we are intercepting all file accesses!
		private IntPtr FindFirstFile_Hooked(string lpFileName, out WIN32_FIND_DATA lpFindFileData)
		{
			try
			{
				_remoteObserver.OnFindFirstFile(lpFileName);
			}
			catch (Exception e)
			{
				_remoteObserver.ReportException(e);
			}

			// call original API...
			return FindFirstFile(lpFileName, out lpFindFileData);
		}


		[DllImport("kernel32", CharSet = CharSet.Unicode)]
		private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool FindClose(SafeHandle hFindFile);
	}
}
