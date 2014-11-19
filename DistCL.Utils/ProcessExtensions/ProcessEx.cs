using System.Collections.Specialized;
using System.Diagnostics;
using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace DistCL.Utils.ProcessExtensions
{

	[Flags]
	public enum ProcessCreationFlags: uint
	{
		ZERO_FLAG = 0x00000000,
		CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
		CREATE_DEFAULT_ERROR_MODE = 0x04000000,
		CREATE_NEW_CONSOLE = 0x00000010,
		CREATE_NEW_PROCESS_GROUP = 0x00000200,
		CREATE_NO_WINDOW = 0x08000000,
		CREATE_PROTECTED_PROCESS = 0x00040000,
		CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
		CREATE_SEPARATE_WOW_VDM = 0x00001000,
		CREATE_SHARED_WOW_VDM = 0x00001000,
		CREATE_SUSPENDED = 0x00000004,
		CREATE_UNICODE_ENVIRONMENT = 0x00000400,
		DEBUG_ONLY_THIS_PROCESS = 0x00000002,
		DEBUG_PROCESS = 0x00000001,
		DETACHED_PROCESS = 0x00000008,
		EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
		INHERIT_PARENT_AFFINITY = 0x00010000
	}

	public class DataReceivedEventArgs : EventArgs
	{
		internal string _data;
		public string Data
		{
			get
			{
				return this._data;
			}
		}
		internal DataReceivedEventArgs(string data)
		{
			this._data = data;
		}
	}

	internal class OrdinalCaseInsensitiveComparer : IComparer
	{
		internal static readonly OrdinalCaseInsensitiveComparer Default = new OrdinalCaseInsensitiveComparer();
		public int Compare(object a, object b)
		{
			string text = a as string;
			string text2 = b as string;
			if (text != null && text2 != null)
			{
				return string.Compare(text, text2, StringComparison.OrdinalIgnoreCase);
			}
			return Comparer.Default.Compare(a, b);
		}
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public OrdinalCaseInsensitiveComparer()
		{
		}
	}

	internal static class EnvironmentBlock
	{
		public static byte[] ToByteArray(StringDictionary sd, bool unicode)
		{
			string[] array = new string[sd.Count];
			sd.Keys.CopyTo(array, 0);
			string[] array2 = new string[sd.Count];
			sd.Values.CopyTo(array2, 0);
			Array.Sort(array, array2, OrdinalCaseInsensitiveComparer.Default);
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < sd.Count; i++)
			{
				stringBuilder.Append(array[i]);
				stringBuilder.Append('=');
				stringBuilder.Append(array2[i]);
				stringBuilder.Append('\0');
			}
			stringBuilder.Append('\0');
			byte[] bytes;
			if (unicode)
			{
				bytes = Encoding.Unicode.GetBytes(stringBuilder.ToString());
			}
			else
			{
				bytes = Encoding.Default.GetBytes(stringBuilder.ToString());
				if (bytes.Length > 65535)
				{
					throw new InvalidOperationException(String.Format("EnvironmentBlockTooLong: {0}", bytes.Length));
				}
			}
			return bytes;
		}
	}

	internal class ProcessWaitHandle : WaitHandle
	{
		internal ProcessWaitHandle(SafeProcessHandle processHandle)
		{
			SafeWaitHandle safeWaitHandle = null;
			if (!NativeMethods.DuplicateHandle(new HandleRef(this, NativeMethods.GetCurrentProcess()), processHandle, new HandleRef(this, NativeMethods.GetCurrentProcess()), out safeWaitHandle, 0, false, 2))
			{
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			}
			base.SafeWaitHandle = safeWaitHandle;
		}
	}

#pragma warning disable 649
	/// <summary>Provides access to local and remote processes and enables you to start and stop local system processes.</summary>
	/// <filterpriority>1</filterpriority>
	[HostProtection(SecurityAction.LinkDemand, SharedState = true, Synchronization = true, ExternalProcessMgmt = true, SelfAffectingProcessMgmt = true), PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust"), PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
	public class ProcessEx:IDisposable
	{
		public delegate void DataReceivedEventHandler(object sender, DataReceivedEventArgs e);

		private enum StreamReadMode
		{
			undefined,
			syncMode,
			asyncMode
		}
		private enum State
		{
			HaveId = 1,
			IsLocal,
			IsNt = 4,
			HaveProcessInfo = 8,
			Exited = 16,
			Associated = 32,
			IsWin2k = 64,
			HaveNtProcessInfo = 12
		}
		private bool haveProcessId;
		private int processId;
		private int mainThreadId;
		private bool haveMainThreadId;
		private bool haveProcessHandle;
		private SafeProcessHandle m_processHandle;
		//private ProcessInfo processInfo;
		private int m_processAccess;
		private ProcessThreadCollection threads;
		private ProcessModuleCollection modules;
		private bool haveMainWindow;
		private IntPtr mainWindowHandle;
		private string mainWindowTitle;
		private bool haveWorkingSetLimits;
		private IntPtr minWorkingSet;
		private IntPtr maxWorkingSet;
		private bool haveProcessorAffinity;
		private IntPtr processorAffinity;
		private bool havePriorityClass;
		private ProcessPriorityClass priorityClass;
		private ProcessStartInfoEx startInfo;
		private bool watchForExit;
		private bool watchingForExit;
		private EventHandler onExited;
		private bool exited;
		private int exitCode;
		private bool signaled;
		private DateTime exitTime;
		private bool haveExitTime;
		private bool responding;
		private bool haveResponding;
		private bool priorityBoostEnabled;
		private bool havePriorityBoostEnabled;
		private bool raisedOnExited;
		private RegisteredWaitHandle registeredWaitHandle;
		private WaitHandle waitHandle;
		private ISynchronizeInvoke synchronizingObject;
		private StreamReader standardOutput;
		private StreamWriter standardInput;
		private StreamReader standardError;
		private OperatingSystem operatingSystem;
		private bool disposed;
		private static object s_CreateProcessLock = new object();
		private ProcessEx.StreamReadMode outputStreamReadMode;
		private ProcessEx.StreamReadMode errorStreamReadMode;
		internal AsyncStreamReader output;
		internal AsyncStreamReader error;
		internal bool pendingOutputRead;
		internal bool pendingErrorRead;
		private static SafeFileHandle InvalidPipeHandle = new SafeFileHandle(IntPtr.Zero, false);
		internal static TraceSwitch processTracing = null;
		/// <summary>Occurs when an application writes to its redirected <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream.</summary>
		/// <filterpriority>2</filterpriority>
		public event DataReceivedEventHandler OutputDataReceived;
		/// <summary>Occurs when an application writes to its redirected <see cref="P:System.Diagnostics.Process.StandardError" /> stream.</summary>
		/// <filterpriority>2</filterpriority>
		public event DataReceivedEventHandler ErrorDataReceived;
		/// <summary>Occurs when a process exits.</summary>
		/// <filterpriority>2</filterpriority>
		public event EventHandler Exited
		{
			add
			{
				this.onExited = (EventHandler)Delegate.Combine(this.onExited, value);
			}
			remove
			{
				this.onExited = (EventHandler)Delegate.Remove(this.onExited, value);
			}
		}

		private bool Associated
		{
			get
			{
				return this.haveProcessId || this.haveProcessHandle;
			}
		}
		/// <summary>Gets the value that the associated process specified when it terminated.</summary>
		/// <returns>The code that the associated process specified when it terminated.</returns>
		/// <exception cref="T:System.InvalidOperationException">The process has not exited.-or- The process <see cref="P:System.Diagnostics.Process.Handle" /> is not valid. </exception>
		/// <exception cref="T:System.NotSupportedException">You are trying to access the <see cref="P:System.Diagnostics.Process.ExitCode" /> property for a process that is running on a remote computer. This property is available only for processes that are running on the local computer.</exception>
		/// <filterpriority>1</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MonitoringDescription("ProcessExitCode")]
		public int ExitCode
		{
			get
			{
				this.EnsureState(ProcessEx.State.Exited);
				return this.exitCode;
			}
		}
		/// <summary>Gets a value indicating whether the associated process has been terminated.</summary>
		/// <returns>true if the operating system process referenced by the <see cref="T:System.Diagnostics.Process" /> component has terminated; otherwise, false.</returns>
		/// <exception cref="T:System.InvalidOperationException">There is no process associated with the object. </exception>
		/// <exception cref="T:System.ComponentModel.Win32Exception">The exit code for the process could not be retrieved. </exception>
		/// <exception cref="T:System.NotSupportedException">You are trying to access the <see cref="P:System.Diagnostics.Process.HasExited" /> property for a process that is running on a remote computer. This property is available only for processes that are running on the local computer.</exception>
		/// <filterpriority>1</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MonitoringDescription("ProcessTerminated")]
		public bool HasExited
		{
			get
			{
				if (!this.exited)
				{
					this.EnsureState(ProcessEx.State.Associated);
					SafeProcessHandle safeProcessHandle = null;
					try
					{
						safeProcessHandle = this.GetProcessHandle(1049600, false);
						if (safeProcessHandle.IsInvalid)
						{
							this.exited = true;
						}
						else
						{
							int num;
							if (NativeMethods.GetExitCodeProcess(safeProcessHandle, out num) && num != 259)
							{
								this.exited = true;
								this.exitCode = num;
							}
							else
							{
								if (!this.signaled)
								{
									ProcessWaitHandle processWaitHandle = null;
									try
									{
										processWaitHandle = new ProcessWaitHandle(safeProcessHandle);
										this.signaled = processWaitHandle.WaitOne(0, false);
									}
									finally
									{
										if (processWaitHandle != null)
										{
											processWaitHandle.Close();
										}
									}
								}
								if (this.signaled)
								{
									if (!NativeMethods.GetExitCodeProcess(safeProcessHandle, out num))
									{
										throw new Win32Exception();
									}
									this.exited = true;
									this.exitCode = num;
								}
							}
						}
					}
					finally
					{
						this.ReleaseProcessHandle(safeProcessHandle);
					}
					if (this.exited)
					{
						this.RaiseOnExited();
					}
				}
				return this.exited;
			}
		}

		/// <summary>Gets the native handle of the associated process.</summary>
		/// <returns>The handle that the operating system assigned to the associated process when the process was started. The system uses this handle to keep track of process attributes.</returns>
		/// <exception cref="T:System.InvalidOperationException">The process has not been started or has exited. The <see cref="P:System.Diagnostics.Process.Handle" /> property cannot be read because there is no process associated with this <see cref="T:System.Diagnostics.Process" /> instance.-or- The <see cref="T:System.Diagnostics.Process" /> instance has been attached to a running process but you do not have the necessary permissions to get a handle with full access rights. </exception>
		/// <exception cref="T:System.NotSupportedException">You are trying to access the <see cref="P:System.Diagnostics.Process.Handle" /> property for a process that is running on a remote computer. This property is available only for processes that are running on the local computer.</exception>
		/// <filterpriority>1</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MonitoringDescription("ProcessHandle")]
		public IntPtr Handle
		{
			get
			{
				this.EnsureState(ProcessEx.State.Associated);
				return this.OpenProcessHandle(this.m_processAccess).DangerousGetHandle();
			}
		}
		/// <summary>Gets the unique identifier for the associated process.</summary>
		/// <returns>The system-generated unique identifier of the process that is referenced by this <see cref="T:System.Diagnostics.Process" /> instance.</returns>
		/// <exception cref="T:System.InvalidOperationException">The process's <see cref="P:System.Diagnostics.Process.Id" /> property has not been set.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object. </exception>
		/// <exception cref="T:System.PlatformNotSupportedException">The platform is Windows 98 or Windows Millennium Edition (Windows Me); set the <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property to false to access this property on Windows 98 and Windows Me.</exception>
		/// <filterpriority>1</filterpriority>
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MonitoringDescription("ProcessId")]
		public int Id
		{
			get
			{
				this.EnsureState(ProcessEx.State.HaveId);
				return this.processId;
			}
		}

		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MonitoringDescription("ProcessId")]
		public int MainThreadId
		{
			get
			{
				if (!haveMainThreadId)
					throw new InvalidOperationException("Main thread ID is absent");
				return this.mainThreadId;
			}
		}

		/// <summary>Gets or sets the properties to pass to the <see cref="M:System.Diagnostics.Process.Start" /> method of the <see cref="T:System.Diagnostics.Process" />.</summary>
		/// <returns>The <see cref="T:System.Diagnostics.ProcessStartInfo" /> that represents the data with which to start the process. These arguments include the name of the executable file or document used to start the process.</returns>
		/// <exception cref="T:System.ArgumentNullException">The value that specifies the <see cref="P:System.Diagnostics.Process.StartInfo" /> is null. </exception>
		/// <filterpriority>1</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Content), MonitoringDescription("ProcessStartInfo")]
		public ProcessStartInfoEx StartInfo
		{
			get
			{
				if (this.startInfo == null)
				{
					this.startInfo = new ProcessStartInfoEx(this);
				}
				return this.startInfo;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}
				this.startInfo = value;
			}
		}

		public ISynchronizeInvoke SynchronizingObject
		{
			get { return this.synchronizingObject; }
		}

		/// <summary>Gets or sets whether the <see cref="E:System.Diagnostics.Process.Exited" /> event should be raised when the process terminates.</summary>
		/// <returns>true if the <see cref="E:System.Diagnostics.Process.Exited" /> event should be raised when the associated process is terminated (through either an exit or a call to <see cref="M:System.Diagnostics.Process.Kill" />); otherwise, false. The default is false.</returns>
		/// <filterpriority>2</filterpriority>
		[Browsable(false), DefaultValue(false), MonitoringDescription("ProcessEnableRaisingEvents")]
		public bool EnableRaisingEvents
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.watchForExit;
			}
			set
			{
				if (value != this.watchForExit)
				{
					if (this.Associated)
					{
						if (value)
						{
							this.OpenProcessHandle();
							this.EnsureWatchingForExit();
						}
						else
						{
							this.StopWatchingForExit();
						}
					}
					this.watchForExit = value;
				}
			}
		}
		/// <summary>Gets a stream used to write the input of the application.</summary>
		/// <returns>A <see cref="T:System.IO.StreamWriter" /> that can be used to write the standard input stream of the application.</returns>
		/// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Diagnostics.Process.StandardInput" /> stream has not been defined because <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardInput" /> is set to false. </exception>
		/// <filterpriority>1</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MonitoringDescription("ProcessStandardInput")]
		public StreamWriter StandardInput
		{
			get
			{
				if (this.standardInput == null)
				{
					throw new InvalidOperationException("CantGetStandardIn");
				}
				return this.standardInput;
			}
		}
		/// <summary>Gets a stream used to read the output of the application.</summary>
		/// <returns>A <see cref="T:System.IO.StreamReader" /> that can be used to read the standard output stream of the application.</returns>
		/// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream has not been defined for redirection; ensure <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardOutput" /> is set to true and <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> is set to false.- or - The <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream has been opened for asynchronous read operations with <see cref="M:System.Diagnostics.Process.BeginOutputReadLine" />. </exception>
		/// <filterpriority>1</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MonitoringDescription("ProcessStandardOutput")]
		public StreamReader StandardOutput
		{
			get
			{
				if (this.standardOutput == null)
				{
					throw new InvalidOperationException("CantGetStandardOut");
				}
				if (this.outputStreamReadMode == ProcessEx.StreamReadMode.undefined)
				{
					this.outputStreamReadMode = ProcessEx.StreamReadMode.syncMode;
				}
				else
				{
					if (this.outputStreamReadMode != ProcessEx.StreamReadMode.syncMode)
					{
						throw new InvalidOperationException("CantMixSyncAsyncOperation");
					}
				}
				return this.standardOutput;
			}
		}
		/// <summary>Gets a stream used to read the error output of the application.</summary>
		/// <returns>A <see cref="T:System.IO.StreamReader" /> that can be used to read the standard error stream of the application.</returns>
		/// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Diagnostics.Process.StandardError" /> stream has not been defined for redirection; ensure <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardError" /> is set to true and <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> is set to false.- or - The <see cref="P:System.Diagnostics.Process.StandardError" /> stream has been opened for asynchronous read operations with <see cref="M:System.Diagnostics.Process.BeginErrorReadLine" />. </exception>
		/// <filterpriority>1</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MonitoringDescription("ProcessStandardError")]
		public StreamReader StandardError
		{
			get
			{
				if (this.standardError == null)
				{
					throw new InvalidOperationException("CantGetStandardError");
				}
				if (this.errorStreamReadMode == ProcessEx.StreamReadMode.undefined)
				{
					this.errorStreamReadMode = ProcessEx.StreamReadMode.syncMode;
				}
				else
				{
					if (this.errorStreamReadMode != ProcessEx.StreamReadMode.syncMode)
					{
						throw new InvalidOperationException("CantMixSyncAsyncOperation");
					}
				}
				return this.standardError;
			}
		}
		/// <summary>Initializes a new instance of the <see cref="T:System.Diagnostics.Process" /> class.</summary>
		public ProcessEx()
		{
			this.outputStreamReadMode = ProcessEx.StreamReadMode.undefined;
			this.errorStreamReadMode = ProcessEx.StreamReadMode.undefined;
			this.m_processAccess = 2035711;
		}

		/// <summary>Frees all the resources that are associated with this component.</summary>
		/// <filterpriority>2</filterpriority>
		public void Close()
		{
			if (this.Associated)
			{
				if (this.haveProcessHandle)
				{
					this.StopWatchingForExit();
					this.m_processHandle.Close();
					this.m_processHandle = null;
					this.haveProcessHandle = false;
				}
				this.haveProcessId = false;
				this.haveMainThreadId = false;
				this.raisedOnExited = false;
				this.standardOutput = null;
				this.standardInput = null;
				this.standardError = null;
				this.output = null;
				this.error = null;
				this.Refresh();
			}
		}

		public void Refresh()
		{
			//this.processInfo = null;
			this.threads = null;
			this.modules = null;
			this.mainWindowTitle = null;
			this.exited = false;
			this.signaled = false;
			this.haveMainWindow = false;
			this.haveWorkingSetLimits = false;
			this.haveProcessorAffinity = false;
			this.havePriorityClass = false;
			this.haveExitTime = false;
			this.haveResponding = false;
			this.havePriorityBoostEnabled = false;
		}

		/// <summary>Raises the <see cref="E:System.Diagnostics.Process.Exited" /> event.</summary>
		protected void OnExited()
		{
			EventHandler eventHandler = this.onExited;
			if (eventHandler != null)
			{
				if (this.SynchronizingObject != null && this.SynchronizingObject.InvokeRequired)
				{
					this.SynchronizingObject.BeginInvoke(eventHandler, new object[]
					{
						this,
						EventArgs.Empty
					});
					return;
				}
				eventHandler(this, EventArgs.Empty);
			}
		}

		/// <summary>Starts (or reuses) the process resource that is specified by the <see cref="P:System.Diagnostics.Process.StartInfo" /> property of this <see cref="T:System.Diagnostics.Process" /> component and associates it with the component.</summary>
		/// <returns>true if a process resource is started; false if no new process resource is started (for example, if an existing process is reused).</returns>
		/// <exception cref="T:System.InvalidOperationException">No file name was specified in the <see cref="T:System.Diagnostics.Process" /> component's <see cref="P:System.Diagnostics.Process.StartInfo" />.-or- The <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> member of the <see cref="P:System.Diagnostics.Process.StartInfo" /> property is true while <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardInput" />, <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardOutput" />, or <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardError" /> is true. </exception>
		/// <exception cref="T:System.ComponentModel.Win32Exception">There was an error in opening the associated file. </exception>
		/// <exception cref="T:System.ObjectDisposedException">The process object has already been disposed. </exception>
		/// <filterpriority>1</filterpriority>
		public bool Start()
		{
			this.Close();
			ProcessStartInfoEx processStartInfo = this.StartInfo;
			if (processStartInfo.FileName.Length == 0)
			{
				throw new InvalidOperationException("FileNameMissing");
			}

			return this.StartWithCreateProcess(processStartInfo);
		}
		/// <summary>Starts a process resource by specifying the name of an application, a user name, a password, and a domain and associates the resource with a new <see cref="T:System.Diagnostics.Process" /> component.</summary>
		/// <returns>A new <see cref="T:System.Diagnostics.Process" /> component that is associated with the process resource, or null if no process resource is started (for example, if an existing process is reused).</returns>
		/// <param name="fileName">The name of an application file to run in the process.</param>
		/// <param name="userName">The user name to use when starting the process.</param>
		/// <param name="password">A <see cref="T:System.Security.SecureString" /> that contains the password to use when starting the process.</param>
		/// <param name="domain">The domain to use when starting the process.</param>
		/// <exception cref="T:System.InvalidOperationException">No file name was specified. </exception>
		/// <exception cref="T:System.ComponentModel.Win32Exception">
		///   <paramref name="fileName" /> is not an executable (.exe) file.</exception>
		/// <exception cref="T:System.ComponentModel.Win32Exception">There was an error in opening the associated file. </exception>
		/// <exception cref="T:System.ObjectDisposedException">The process object has already been disposed. </exception>
		/// <filterpriority>1</filterpriority>
		public static ProcessEx Start(string fileName, string userName, SecureString password, string domain)
		{
			return ProcessEx.Start(new ProcessStartInfoEx(fileName)
			{
				UserName = userName,
				Password = password,
				Domain = domain,
			});
		}
		/// <summary>Starts a process resource by specifying the name of an application, a set of command-line arguments, a user name, a password, and a domain and associates the resource with a new <see cref="T:System.Diagnostics.Process" /> component.</summary>
		/// <returns>A new <see cref="T:System.Diagnostics.Process" /> component that is associated with the process resource, or null if no process resource is started (for example, if an existing process is reused).</returns>
		/// <param name="fileName">The name of an application file to run in the process. </param>
		/// <param name="arguments">Command-line arguments to pass when starting the process. </param>
		/// <param name="userName">The user name to use when starting the process.</param>
		/// <param name="password">A <see cref="T:System.Security.SecureString" /> that contains the password to use when starting the process.</param>
		/// <param name="domain">The domain to use when starting the process.</param>
		/// <exception cref="T:System.InvalidOperationException">No file name was specified.</exception>
		/// <exception cref="T:System.ComponentModel.Win32Exception">
		///   <paramref name="fileName" /> is not an executable (.exe) file.</exception>
		/// <exception cref="T:System.ComponentModel.Win32Exception">An error occurred when opening the associated file. -or-The sum of the length of the arguments and the length of the full path to the associated file exceeds 2080. The error message associated with this exception can be one of the following: "The data area passed to a system call is too small." or "Access is denied."</exception>
		/// <exception cref="T:System.ObjectDisposedException">The process object has already been disposed. </exception>
		/// <filterpriority>1</filterpriority>
		public static ProcessEx Start(string fileName, string arguments, string userName, SecureString password, string domain)
		{
			return ProcessEx.Start(new ProcessStartInfoEx(fileName, arguments)
			{
				UserName = userName,
				Password = password,
				Domain = domain,
			});
		}
		/// <summary>Starts a process resource by specifying the name of a document or application file and associates the resource with a new <see cref="T:System.Diagnostics.Process" /> component.</summary>
		/// <returns>A new <see cref="T:System.Diagnostics.Process" /> component that is associated with the process resource, or null, if no process resource is started (for example, if an existing process is reused).</returns>
		/// <param name="fileName">The name of a document or application file to run in the process. </param>
		/// <exception cref="T:System.ComponentModel.Win32Exception">An error occurred when opening the associated file. </exception>
		/// <exception cref="T:System.ObjectDisposedException">The process object has already been disposed. </exception>
		/// <exception cref="T:System.IO.FileNotFoundException">The PATH environment variable has a string containing quotes.</exception>
		/// <filterpriority>1</filterpriority>
		public static ProcessEx Start(string fileName)
		{
			return ProcessEx.Start(new ProcessStartInfoEx(fileName));
		}
		/// <summary>Starts a process resource by specifying the name of an application and a set of command-line arguments, and associates the resource with a new <see cref="T:System.Diagnostics.Process" /> component.</summary>
		/// <returns>A new <see cref="T:System.Diagnostics.Process" /> component that is associated with the process, or null, if no process resource is started (for example, if an existing process is reused).</returns>
		/// <param name="fileName">The name of an application file to run in the process. </param>
		/// <param name="arguments">Command-line arguments to pass when starting the process. </param>
		/// <exception cref="T:System.InvalidOperationException">The <paramref name="fileName" /> or <paramref name="arguments" /> parameter is null. </exception>
		/// <exception cref="T:System.ComponentModel.Win32Exception">An error occurred when opening the associated file. -or-The sum of the length of the arguments and the length of the full path to the process exceeds 2080. The error message associated with this exception can be one of the following: "The data area passed to a system call is too small." or "Access is denied."</exception>
		/// <exception cref="T:System.ObjectDisposedException">The process object has already been disposed. </exception>
		/// <exception cref="T:System.IO.FileNotFoundException">The PATH environment variable has a string containing quotes.</exception>
		/// <filterpriority>1</filterpriority>
		public static ProcessEx Start(string fileName, string arguments)
		{
			return ProcessEx.Start(new ProcessStartInfoEx(fileName, arguments));
		}
		/// <summary>Starts the process resource that is specified by the parameter containing process start information (for example, the file name of the process to start) and associates the resource with a new <see cref="T:System.Diagnostics.Process" /> component.</summary>
		/// <returns>A new <see cref="T:System.Diagnostics.Process" /> component that is associated with the process resource, or null if no process resource is started (for example, if an existing process is reused).</returns>
		/// <param name="startInfo">The <see cref="T:System.Diagnostics.ProcessStartInfo" /> that contains the information that is used to start the process, including the file name and any command-line arguments. </param>
		/// <exception cref="T:System.InvalidOperationException">No file name was specified in the <paramref name="startInfo" /> parameter's <see cref="P:System.Diagnostics.ProcessStartInfo.FileName" /> property.-or- The <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property of the <paramref name="startInfo" /> parameter is true and the <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardInput" />, <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardOutput" />, or <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardError" /> property is also true.-or-The <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property of the <paramref name="startInfo" /> parameter is true and the <see cref="P:System.Diagnostics.ProcessStartInfo.UserName" /> property is not null or empty or the <see cref="P:System.Diagnostics.ProcessStartInfo.Password" /> property is not null.</exception>
		/// <exception cref="T:System.ArgumentNullException">The <paramref name="startInfo" /> parameter is null. </exception>
		/// <exception cref="T:System.ObjectDisposedException">The process object has already been disposed. </exception>
		/// <exception cref="T:System.IO.FileNotFoundException">The file specified in the <paramref name="startInfo" /> parameter's <see cref="P:System.Diagnostics.ProcessStartInfo.FileName" /> property could not be found.</exception>
		/// <exception cref="T:System.ComponentModel.Win32Exception">An error occurred when opening the associated file. -or-The sum of the length of the arguments and the length of the full path to the process exceeds 2080. The error message associated with this exception can be one of the following: "The data area passed to a system call is too small." or "Access is denied."</exception>
		/// <filterpriority>1</filterpriority>
		public static ProcessEx Start(ProcessStartInfoEx startInfo)
		{
			ProcessEx process = new ProcessEx();
			if (startInfo == null)
			{
				throw new ArgumentNullException("startInfo");
			}
			process.StartInfo = startInfo;
			if (process.Start())
			{
				return process;
			}
			return null;
		}
		/// <summary>Immediately stops the associated process.</summary>
		/// <exception cref="T:System.ComponentModel.Win32Exception">The associated process could not be terminated. -or-The process is terminating.-or- The associated process is a Win16 executable.</exception>
		/// <exception cref="T:System.NotSupportedException">You are attempting to call <see cref="M:System.Diagnostics.Process.Kill" /> for a process that is running on a remote computer. The method is available only for processes running on the local computer.</exception>
		/// <exception cref="T:System.InvalidOperationException">The process has already exited. -or-There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object.</exception>
		/// <filterpriority>1</filterpriority>
		public void Kill()
		{
			SafeProcessHandle safeProcessHandle = null;
			try
			{
				safeProcessHandle = this.GetProcessHandle(1);
				if (!NativeMethods.TerminateProcess(safeProcessHandle, -1))
				{
					throw new Win32Exception();
				}
			}
			finally
			{
				this.ReleaseProcessHandle(safeProcessHandle);
			}
		}
		/// <summary>Instructs the <see cref="T:System.Diagnostics.Process" /> component to wait the specified number of milliseconds for the associated process to exit.</summary>
		/// <returns>true if the associated process has exited; otherwise, false.</returns>
		/// <param name="milliseconds">The amount of time, in milliseconds, to wait for the associated process to exit. The maximum is the largest possible value of a 32-bit integer, which represents infinity to the operating system. </param>
		/// <exception cref="T:System.ComponentModel.Win32Exception">The wait setting could not be accessed. </exception>
		/// <exception cref="T:System.SystemException">No process <see cref="P:System.Diagnostics.Process.Id" /> has been set, and a <see cref="P:System.Diagnostics.Process.Handle" /> from which the <see cref="P:System.Diagnostics.Process.Id" /> property can be determined does not exist.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object.-or- You are attempting to call <see cref="M:System.Diagnostics.Process.WaitForExit(System.Int32)" /> for a process that is running on a remote computer. This method is available only for processes that are running on the local computer. </exception>
		/// <filterpriority>1</filterpriority>
		public bool WaitForExit(int milliseconds)
		{
			SafeProcessHandle safeProcessHandle = null;
			ProcessWaitHandle processWaitHandle = null;
			bool flag;
			try
			{
				safeProcessHandle = this.GetProcessHandle(1048576, false);
				if (safeProcessHandle.IsInvalid)
				{
					flag = true;
				}
				else
				{
					processWaitHandle = new ProcessWaitHandle(safeProcessHandle);
					if (processWaitHandle.WaitOne(milliseconds, false))
					{
						flag = true;
						this.signaled = true;
					}
					else
					{
						flag = false;
						this.signaled = false;
					}
				}
			}
			finally
			{
				if (processWaitHandle != null)
				{
					processWaitHandle.Close();
				}
				if (this.output != null && milliseconds == -1)
				{
					this.output.WaitUtilEOF();
				}
				if (this.error != null && milliseconds == -1)
				{
					this.error.WaitUtilEOF();
				}
				this.ReleaseProcessHandle(safeProcessHandle);
			}
			if (flag && this.watchForExit)
			{
				this.RaiseOnExited();
			}
			return flag;
		}
		/// <summary>Instructs the <see cref="T:System.Diagnostics.Process" /> component to wait indefinitely for the associated process to exit.</summary>
		/// <exception cref="T:System.ComponentModel.Win32Exception">The wait setting could not be accessed. </exception>
		/// <exception cref="T:System.SystemException">No process <see cref="P:System.Diagnostics.Process.Id" /> has been set, and a <see cref="P:System.Diagnostics.Process.Handle" /> from which the <see cref="P:System.Diagnostics.Process.Id" /> property can be determined does not exist.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object.-or- You are attempting to call <see cref="M:System.Diagnostics.Process.WaitForExit" /> for a process that is running on a remote computer. This method is available only for processes that are running on the local computer. </exception>
		/// <filterpriority>1</filterpriority>
		public void WaitForExit()
		{
			this.WaitForExit(-1);
		}
		/// <summary>Begins asynchronous read operations on the redirected <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream of the application.</summary>
		/// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardOutput" /> property is false.- or - An asynchronous read operation is already in progress on the <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream.- or - The <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream has been used by a synchronous read operation. </exception>
		/// <filterpriority>2</filterpriority>
		[ComVisible(false)]
		public void BeginOutputReadLine()
		{
			if (this.outputStreamReadMode == ProcessEx.StreamReadMode.undefined)
			{
				this.outputStreamReadMode = ProcessEx.StreamReadMode.asyncMode;
			}
			else
			{
				if (this.outputStreamReadMode != ProcessEx.StreamReadMode.asyncMode)
				{
					throw new InvalidOperationException("CantMixSyncAsyncOperation");
				}
			}
			if (this.pendingOutputRead)
			{
				throw new InvalidOperationException("PendingAsyncOperation");
			}
			this.pendingOutputRead = true;
			if (this.output == null)
			{
				if (this.standardOutput == null)
				{
					throw new InvalidOperationException("CantGetStandardOut");
				}
				Stream baseStream = this.standardOutput.BaseStream;
				this.output = new AsyncStreamReader(this, baseStream, new UserCallBack(this.OutputReadNotifyUser), this.standardOutput.CurrentEncoding);
			}
			this.output.BeginReadLine();
		}
		/// <summary>Begins asynchronous read operations on the redirected <see cref="P:System.Diagnostics.Process.StandardError" /> stream of the application.</summary>
		/// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Diagnostics.ProcessStartInfo.RedirectStandardError" /> property is false.- or - An asynchronous read operation is already in progress on the <see cref="P:System.Diagnostics.Process.StandardError" /> stream.- or - The <see cref="P:System.Diagnostics.Process.StandardError" /> stream has been used by a synchronous read operation. </exception>
		/// <filterpriority>2</filterpriority>
		[ComVisible(false)]
		public void BeginErrorReadLine()
		{
			if (this.errorStreamReadMode == ProcessEx.StreamReadMode.undefined)
			{
				this.errorStreamReadMode = ProcessEx.StreamReadMode.asyncMode;
			}
			else
			{
				if (this.errorStreamReadMode != ProcessEx.StreamReadMode.asyncMode)
				{
					throw new InvalidOperationException("CantMixSyncAsyncOperation");
				}
			}
			if (this.pendingErrorRead)
			{
				throw new InvalidOperationException("PendingAsyncOperation");
			}
			this.pendingErrorRead = true;
			if (this.error == null)
			{
				if (this.standardError == null)
				{
					throw new InvalidOperationException("CantGetStandardError");
				}
				Stream baseStream = this.standardError.BaseStream;
				this.error = new AsyncStreamReader(this, baseStream, new UserCallBack(this.ErrorReadNotifyUser), this.standardError.CurrentEncoding);
			}
			this.error.BeginReadLine();
		}
		/// <summary>Cancels the asynchronous read operation on the redirected <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream of an application.</summary>
		/// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream is not enabled for asynchronous read operations. </exception>
		/// <filterpriority>2</filterpriority>
		[ComVisible(false)]
		public void CancelOutputRead()
		{
			if (this.output != null)
			{
				this.output.CancelOperation();
				this.pendingOutputRead = false;
				return;
			}
			throw new InvalidOperationException("NoAsyncOperation");
		}
		/// <summary>Cancels the asynchronous read operation on the redirected <see cref="P:System.Diagnostics.Process.StandardError" /> stream of an application.</summary>
		/// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Diagnostics.Process.StandardError" /> stream is not enabled for asynchronous read operations. </exception>
		/// <filterpriority>2</filterpriority>
		[ComVisible(false)]
		public void CancelErrorRead()
		{
			if (this.error != null)
			{
				this.error.CancelOperation();
				this.pendingErrorRead = false;
				return;
			}
			throw new InvalidOperationException("NoAsyncOperation");
		}
		internal void OutputReadNotifyUser(string data)
		{
			DataReceivedEventHandler outputDataReceived = this.OutputDataReceived;
			if (outputDataReceived != null)
			{
				DataReceivedEventArgs dataReceivedEventArgs = new DataReceivedEventArgs(data);
				if (this.SynchronizingObject != null && this.SynchronizingObject.InvokeRequired)
				{
					this.SynchronizingObject.Invoke(outputDataReceived, new object[]
					{
						this,
						dataReceivedEventArgs
					});
					return;
				}
				outputDataReceived(this, dataReceivedEventArgs);
			}
		}
		internal void ErrorReadNotifyUser(string data)
		{
			DataReceivedEventHandler errorDataReceived = this.ErrorDataReceived;
			if (errorDataReceived != null)
			{
				DataReceivedEventArgs dataReceivedEventArgs = new DataReceivedEventArgs(data);
				if (this.SynchronizingObject != null && this.SynchronizingObject.InvokeRequired)
				{
					this.SynchronizingObject.Invoke(errorDataReceived, new object[]
					{
						this,
						dataReceivedEventArgs
					});
					return;
				}
				errorDataReceived(this, dataReceivedEventArgs);
			}
		}
		private void ReleaseProcessHandle(SafeProcessHandle handle)
		{
			if (handle == null)
			{
				return;
			}
			if (this.haveProcessHandle && handle == this.m_processHandle)
			{
				return;
			}
			handle.Close();
		}
		private void CompletionCallback(object context, bool wasSignaled)
		{
			this.StopWatchingForExit();
			this.RaiseOnExited();
		}
		private void EnsureState(ProcessEx.State state)
		{
			if ((state & ProcessEx.State.Associated) != (ProcessEx.State)0 && !this.Associated)
			{
				throw new InvalidOperationException("NoAssociatedProcess");
			}
			if ((state & ProcessEx.State.HaveId) != (ProcessEx.State)0 && !this.haveProcessId)
			{
				if (!this.haveProcessHandle)
				{
					this.EnsureState(ProcessEx.State.Associated);
					throw new InvalidOperationException("ProcessIdRequired");
				}
				this.SetProcessId(GetProcessIdFromHandle(this.m_processHandle));
			}
			if ((state & ProcessEx.State.Exited) != (ProcessEx.State)0)
			{
				if (!this.HasExited)
				{
					throw new InvalidOperationException("WaitTillExit");
				}
				if (!this.haveProcessHandle)
				{
					throw new InvalidOperationException("NoProcessHandle");
				}
			}
		}
		private void EnsureWatchingForExit()
		{
			if (!this.watchingForExit)
			{
				bool flag = false;
				try
				{
					Monitor.Enter(this, ref flag);
					if (!this.watchingForExit)
					{
						this.watchingForExit = true;
						try
						{
							this.waitHandle = new ProcessWaitHandle(this.m_processHandle);
							this.registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(this.waitHandle, new WaitOrTimerCallback(this.CompletionCallback), null, -1, true);
						}
						catch
						{
							this.watchingForExit = false;
							throw;
						}
					}
				}
				finally
				{
					if (flag)
					{
						Monitor.Exit(this);
					}
				}
			}
		}

		private SafeProcessHandle GetProcessHandle(int access, bool throwIfExited)
		{
			if (this.haveProcessHandle)
			{
				if (throwIfExited)
				{
					ProcessWaitHandle processWaitHandle = null;
					try
					{
						processWaitHandle = new ProcessWaitHandle(this.m_processHandle);
						if (processWaitHandle.WaitOne(0, false))
						{
							if (this.haveProcessId)
							{
								throw new InvalidOperationException(String.Format("ProcessHasExited: {0}", this.processId));
							}
							throw new InvalidOperationException("ProcessHasExitedNoId");
						}
					}
					finally
					{
						if (processWaitHandle != null)
						{
							processWaitHandle.Close();
						}
					}
				}
				return this.m_processHandle;
			}
			this.EnsureState((ProcessEx.State)3);
			SafeProcessHandle safeProcessHandle = SafeProcessHandle.InvalidHandle;
			safeProcessHandle = OpenProcess(this.processId, access, throwIfExited);
			if (throwIfExited && (access & 1024) != 0 && NativeMethods.GetExitCodeProcess(safeProcessHandle, out this.exitCode) && this.exitCode != 259)
			{
				throw new InvalidOperationException(String.Format("ProcessHasExited: {0}", this.processId));
			}
			return safeProcessHandle;
		}
		private SafeProcessHandle GetProcessHandle(int access)
		{
			return this.GetProcessHandle(access, true);
		}
		private SafeProcessHandle OpenProcessHandle()
		{
			return this.OpenProcessHandle(2035711);
		}
		private SafeProcessHandle OpenProcessHandle(int access)
		{
			if (!this.haveProcessHandle)
			{
				if (this.disposed)
				{
					throw new ObjectDisposedException(base.GetType().Name);
				}
				this.SetProcessHandle(this.GetProcessHandle(access));
			}
			return this.m_processHandle;
		}
		private void RaiseOnExited()
		{
			if (!this.raisedOnExited)
			{
				bool flag = false;
				try
				{
					Monitor.Enter(this, ref flag);
					if (!this.raisedOnExited)
					{
						this.raisedOnExited = true;
						this.OnExited();
					}
				}
				finally
				{
					if (flag)
					{
						Monitor.Exit(this);
					}
				}
			}
		}
		private void SetProcessHandle(SafeProcessHandle processHandle)
		{
			this.m_processHandle = processHandle;
			this.haveProcessHandle = true;
			if (this.watchForExit)
			{
				this.EnsureWatchingForExit();
			}
		}
		private void SetProcessId(int processId)
		{
			this.processId = processId;
			this.haveProcessId = true;
		}

		private void SetThreadId(int threadId)
		{
			this.mainThreadId = threadId;
			this.haveMainThreadId = true;
		}

		private static void CreatePipeWithSecurityAttributes(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
		{
			bool flag = NativeMethods.CreatePipe(out hReadPipe, out hWritePipe, lpPipeAttributes, nSize);
			if (!flag || hReadPipe.IsInvalid || hWritePipe.IsInvalid)
			{
				throw new Win32Exception();
			}
		}
		private void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs)
		{
			NativeMethods.SECURITY_ATTRIBUTES sECURITY_ATTRIBUTES = new NativeMethods.SECURITY_ATTRIBUTES();
			sECURITY_ATTRIBUTES.bInheritHandle = true;
			SafeFileHandle safeFileHandle = null;
			try
			{
				if (parentInputs)
				{
					ProcessEx.CreatePipeWithSecurityAttributes(out childHandle, out safeFileHandle, sECURITY_ATTRIBUTES, 0);
				}
				else
				{
					ProcessEx.CreatePipeWithSecurityAttributes(out safeFileHandle, out childHandle, sECURITY_ATTRIBUTES, 0);
				}
				if (!NativeMethods.DuplicateHandle(new HandleRef(this, NativeMethods.GetCurrentProcess()), safeFileHandle, new HandleRef(this, NativeMethods.GetCurrentProcess()), out parentHandle, 0, false, 2))
				{
					throw new Win32Exception();
				}
			}
			finally
			{
				if (safeFileHandle != null && !safeFileHandle.IsInvalid)
				{
					safeFileHandle.Close();
				}
			}
		}
		private static StringBuilder BuildCommandLine(string executableFileName, string arguments)
		{
			StringBuilder stringBuilder = new StringBuilder();
			string text = executableFileName.Trim();
			bool flag = text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal);
			if (!flag)
			{
				stringBuilder.Append("\"");
			}
			stringBuilder.Append(text);
			if (!flag)
			{
				stringBuilder.Append("\"");
			}
			if (!string.IsNullOrEmpty(arguments))
			{
				stringBuilder.Append(" ");
				stringBuilder.Append(arguments);
			}
			return stringBuilder;
		}
		private bool StartWithCreateProcess(ProcessStartInfoEx startInfo)
		{
			if (startInfo.StandardOutputEncoding != null && !startInfo.RedirectStandardOutput)
			{
				throw new InvalidOperationException("StandardOutputEncodingNotAllowed");
			}
			if (startInfo.StandardErrorEncoding != null && !startInfo.RedirectStandardError)
			{
				throw new InvalidOperationException("StandardErrorEncodingNotAllowed");
			}
			if (this.disposed)
			{
				throw new ObjectDisposedException(base.GetType().Name);
			}
			StringBuilder stringBuilder = ProcessEx.BuildCommandLine(startInfo.FileName, startInfo.Arguments);
			NativeMethods.STARTUPINFO sTARTUPINFO = new NativeMethods.STARTUPINFO();
			SafeNativeMethods.PROCESS_INFORMATION pROCESS_INFORMATION = new SafeNativeMethods.PROCESS_INFORMATION();
			SafeProcessHandle safeProcessHandle = new SafeProcessHandle();
			SafeThreadHandle safeThreadHandle = new SafeThreadHandle();
			int num = 0;
			SafeFileHandle handle = null;
			SafeFileHandle handle2 = null;
			SafeFileHandle handle3 = null;
			GCHandle gCHandle = default(GCHandle);
			lock (ProcessEx.s_CreateProcessLock)
			{
				try
				{
					if (startInfo.RedirectStandardInput || startInfo.RedirectStandardOutput || startInfo.RedirectStandardError)
					{
						if (startInfo.RedirectStandardInput)
						{
							this.CreatePipe(out handle, out sTARTUPINFO.hStdInput, true);
						}
						else
						{
							sTARTUPINFO.hStdInput = new SafeFileHandle(NativeMethods.GetStdHandle(-10), false);
						}
						if (startInfo.RedirectStandardOutput)
						{
							this.CreatePipe(out handle2, out sTARTUPINFO.hStdOutput, false);
						}
						else
						{
							sTARTUPINFO.hStdOutput = new SafeFileHandle(NativeMethods.GetStdHandle(-11), false);
						}
						if (startInfo.RedirectStandardError)
						{
							this.CreatePipe(out handle3, out sTARTUPINFO.hStdError, false);
						}
						else
						{
							sTARTUPINFO.hStdError = new SafeFileHandle(NativeMethods.GetStdHandle(-12), false);
						}
						sTARTUPINFO.dwFlags = 256;
					}
					ProcessCreationFlags creationFlags = startInfo.CreationFlags;
					if (startInfo.CreateNoWindow)
					{
						creationFlags |= ProcessCreationFlags.CREATE_NO_WINDOW;
					}
					IntPtr intPtr = (IntPtr)0;
					if (startInfo.environmentVariables != null)
					{
						bool unicode = false;
						creationFlags |= ProcessCreationFlags.CREATE_UNICODE_ENVIRONMENT;
						unicode = true;
						byte[] value = EnvironmentBlock.ToByteArray(startInfo.environmentVariables, unicode);
						gCHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
						intPtr = gCHandle.AddrOfPinnedObject();
					}
					string text = startInfo.WorkingDirectory;
					if (text == string.Empty)
					{
						text = Environment.CurrentDirectory;
					}
					bool flag2;
					RuntimeHelpers.PrepareConstrainedRegions();
					try
					{
					}
					finally
					{
						flag2 = NativeMethods.CreateProcess(null, stringBuilder, null, null, true, (uint)creationFlags, intPtr, text, sTARTUPINFO, pROCESS_INFORMATION);
						if (!flag2)
						{
							num = Marshal.GetLastWin32Error();
						}
						if (pROCESS_INFORMATION.hProcess != (IntPtr)0 && pROCESS_INFORMATION.hProcess != NativeMethods.INVALID_HANDLE_VALUE)
						{
							safeProcessHandle.InitialSetHandle(pROCESS_INFORMATION.hProcess);
						}
						if (pROCESS_INFORMATION.hThread != (IntPtr)0 && pROCESS_INFORMATION.hThread != NativeMethods.INVALID_HANDLE_VALUE)
						{
							safeThreadHandle.InitialSetHandle(pROCESS_INFORMATION.hThread);
						}
					}
					if (!flag2)
					{
						if (num == 193 || num == 216)
						{
							throw new Win32Exception(num, "InvalidApplication");
						}
						throw new Win32Exception(num);
					}
				}
				finally
				{
					if (gCHandle.IsAllocated)
					{
						gCHandle.Free();
					}
					sTARTUPINFO.Dispose();
				}
			}
			if (startInfo.RedirectStandardInput)
			{
				this.standardInput = new StreamWriter(new FileStream(handle, FileAccess.Write, 4096, false), Console.InputEncoding, 4096);
				this.standardInput.AutoFlush = true;
			}
			if (startInfo.RedirectStandardOutput)
			{
				Encoding encoding = (startInfo.StandardOutputEncoding != null) ? startInfo.StandardOutputEncoding : Console.OutputEncoding;
				this.standardOutput = new StreamReader(new FileStream(handle2, FileAccess.Read, 4096, false), encoding, true, 4096);
			}
			if (startInfo.RedirectStandardError)
			{
				Encoding encoding2 = (startInfo.StandardErrorEncoding != null) ? startInfo.StandardErrorEncoding : Console.OutputEncoding;
				this.standardError = new StreamReader(new FileStream(handle3, FileAccess.Read, 4096, false), encoding2, true, 4096);
			}
			bool result = false;
			if (!safeProcessHandle.IsInvalid)
			{
				this.SetProcessHandle(safeProcessHandle);
				this.SetProcessId(pROCESS_INFORMATION.dwProcessId);
				this.SetThreadId(pROCESS_INFORMATION.dwThreadId);
				safeThreadHandle.Close();
				result = true;
			}
			return result;
		}

		private void StopWatchingForExit()
		{
			if (this.watchingForExit)
			{
				bool flag = false;
				try
				{
					Monitor.Enter(this, ref flag);
					if (this.watchingForExit)
					{
						this.watchingForExit = false;
						this.registeredWaitHandle.Unregister(null);
						this.waitHandle.Close();
						this.waitHandle = null;
						this.registeredWaitHandle = null;
					}
				}
				finally
				{
					if (flag)
					{
						Monitor.Exit(this);
					}
				}
			}
		}

		// System.Diagnostics.NtProcessManager
		private static int[] GetProcessIds()
		{
			int[] array = new int[256];
			int num;
			while (NativeMethods.EnumProcesses(array, array.Length * 4, out num))
			{
				if (num != array.Length * 4)
				{
					int[] array2 = new int[num / 4];
					Array.Copy(array, array2, array2.Length);
					return array2;
				}
				array = new int[array.Length * 2];
			}
			throw new Win32Exception();
		}

		private static int GetProcessIdFromHandle(SafeProcessHandle processHandle)
		{
			NativeMethods.NtProcessBasicInfo ntProcessBasicInfo = new NativeMethods.NtProcessBasicInfo();
			int num = NativeMethods.NtQueryInformationProcess(processHandle, 0, ntProcessBasicInfo, Marshal.SizeOf(ntProcessBasicInfo), null);
			if (num != 0)
			{
				throw new InvalidOperationException("CantGetProcessId", new Win32Exception(num));
			}
			return ntProcessBasicInfo.UniqueProcessId.ToInt32();
		}

		private static bool IsProcessRunning(int processId)
		{
			foreach (var proc in GetProcessIds())
			{
				if (proc == processId)
				{
					return true;
				}
			}
			return false;
		}

		private static SafeProcessHandle OpenProcess(int processId, int access, bool throwIfExited)
		{
			SafeProcessHandle safeProcessHandle = NativeMethods.OpenProcess(access, false, processId);
			int lastWin32Error = Marshal.GetLastWin32Error();
			if (!safeProcessHandle.IsInvalid)
			{
				return safeProcessHandle;
			}
			if (processId == 0)
			{
				throw new Win32Exception(5);
			}
			if (IsProcessRunning(processId))
			{
				throw new Win32Exception(lastWin32Error);
			}
			if (throwIfExited)
			{
				throw new InvalidOperationException(String.Format("ProcessHasExited, PID: {0}", processId));
			}
			return SafeProcessHandle.InvalidHandle;
		}

		public void Dispose()
		{
			if (!this.disposed)
			{
				this.Close();
				this.disposed = true;
			}
		}
	}
#pragma warning restore
}
