using System.Diagnostics;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime;
using System.Security;
using System.Security.Permissions;
using System.Text;

namespace DistCL.Utils.ProcessExtensions

{
	/// <summary>Specifies a set of values that are used when you start a process.</summary>
	/// <filterpriority>2</filterpriority>
	[TypeConverter(typeof(ExpandableObjectConverter))]
	[HostProtection(SecurityAction.LinkDemand, SharedState = true, SelfAffectingProcessMgmt = true), PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
	public sealed class ProcessStartInfoEx
	{
		private string fileName;
		private string arguments;
		private string directory;
		private string verb;
		private ProcessWindowStyle windowStyle;
		private bool errorDialog;
		private IntPtr errorDialogParentHandle;
		private bool useShellExecute = true;
		private string userName;
		private string domain;
		private SecureString password;
		private bool loadUserProfile;
		private bool redirectStandardInput;
		private bool redirectStandardOutput;
		private bool redirectStandardError;
		private Encoding standardOutputEncoding;
		private Encoding standardErrorEncoding;
		private bool createNoWindow;
		private WeakReference weakParentProcess;
		internal StringDictionary environmentVariables;
		/// <summary>Gets or sets the verb to use when opening the application or document specified by the <see cref="P:System.Diagnostics.ProcessStartInfo.FileName" /> property.</summary>
		/// <returns>The action to take with the file that the process opens. The default is an empty string ("").</returns>
		/// <filterpriority>2</filterpriority>
		[DefaultValue(""), NotifyParentProperty(true), TypeConverter("System.Diagnostics.Design.VerbConverter, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"), MonitoringDescription("ProcessVerb")]
		public string Verb
		{
			get
			{
				if (this.verb == null)
				{
					return string.Empty;
				}
				return this.verb;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.verb = value;
			}
		}
		/// <summary>Gets or sets the set of command-line arguments to use when starting the application.</summary>
		/// <returns>File type–specific arguments that the system can associate with the application specified in the <see cref="P:System.Diagnostics.ProcessStartInfo.FileName" /> property. The default is an empty string (""). On Windows Vista and earlier versions of the Windows operating system, the length of the arguments added to the length of the full path to the process must be less than 2080. On Windows 7 and later versions, the length must be less than 32699.</returns>
		/// <filterpriority>1</filterpriority>
		[DefaultValue(""), NotifyParentProperty(true), SettingsBindable(true), TypeConverter("System.Diagnostics.Design.StringValueConverter, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"), MonitoringDescription("ProcessArguments")]
		public string Arguments
		{
			get
			{
				if (this.arguments == null)
				{
					return string.Empty;
				}
				return this.arguments;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.arguments = value;
			}
		}
		/// <summary>Gets or sets a value indicating whether to start the process in a new window.</summary>
		/// <returns>true if the process should be started without creating a new window to contain it; otherwise, false. The default is false.</returns>
		/// <filterpriority>2</filterpriority>
		[DefaultValue(false), NotifyParentProperty(true), MonitoringDescription("ProcessCreateNoWindow")]
		public bool CreateNoWindow
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.createNoWindow;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.createNoWindow = value;
			}
		}
		/// <summary>Gets search paths for files, directories for temporary files, application-specific options, and other similar information.</summary>
		/// <returns>A string dictionary that provides environment variables that apply to this process and child processes. The default is null.</returns>
		/// <filterpriority>1</filterpriority>
		[DefaultValue(null), DesignerSerializationVisibility(DesignerSerializationVisibility.Content), Editor("System.Diagnostics.Design.StringDictionaryEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"), NotifyParentProperty(true), MonitoringDescription("ProcessEnvironmentVariables")]
		public StringDictionary EnvironmentVariables
		{
			get
			{
				if (this.environmentVariables == null)
				{
					this.environmentVariables = new StringDictionary();
					if (this.weakParentProcess == null || !this.weakParentProcess.IsAlive || ((Component)this.weakParentProcess.Target).Site == null || !((Component)this.weakParentProcess.Target).Site.DesignMode)
					{
						foreach (DictionaryEntry dictionaryEntry in Environment.GetEnvironmentVariables())
						{
							this.environmentVariables.Add((string)dictionaryEntry.Key, (string)dictionaryEntry.Value);
						}
					}
				}
				return this.environmentVariables;
			}
		}
		/// <summary>Gets or sets a value indicating whether the input for an application is read from the <see cref="P:System.Diagnostics.Process.StandardInput" /> stream.</summary>
		/// <returns>true if input should be read from <see cref="P:System.Diagnostics.Process.StandardInput" />; otherwise, false. The default is false.</returns>
		/// <filterpriority>2</filterpriority>
		[DefaultValue(false), NotifyParentProperty(true), MonitoringDescription("ProcessRedirectStandardInput")]
		public bool RedirectStandardInput
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.redirectStandardInput;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.redirectStandardInput = value;
			}
		}
		/// <summary>Gets or sets a value that indicates whether the output of an application is written to the <see cref="P:System.Diagnostics.Process.StandardOutput" /> stream.</summary>
		/// <returns>true if output should be written to <see cref="P:System.Diagnostics.Process.StandardOutput" />; otherwise, false. The default is false.</returns>
		/// <filterpriority>2</filterpriority>
		[DefaultValue(false), NotifyParentProperty(true), MonitoringDescription("ProcessRedirectStandardOutput")]
		public bool RedirectStandardOutput
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.redirectStandardOutput;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.redirectStandardOutput = value;
			}
		}
		/// <summary>Gets or sets a value that indicates whether the error output of an application is written to the <see cref="P:System.Diagnostics.Process.StandardError" /> stream.</summary>
		/// <returns>true if error output should be written to <see cref="P:System.Diagnostics.Process.StandardError" />; otherwise, false. The default is false.</returns>
		/// <filterpriority>2</filterpriority>
		[DefaultValue(false), NotifyParentProperty(true), MonitoringDescription("ProcessRedirectStandardError")]
		public bool RedirectStandardError
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.redirectStandardError;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.redirectStandardError = value;
			}
		}
		/// <summary>Gets or sets the preferred encoding for error output.</summary>
		/// <returns>An object that represents the preferred encoding for error output. The default is null.</returns>
		public Encoding StandardErrorEncoding
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.standardErrorEncoding;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.standardErrorEncoding = value;
			}
		}
		/// <summary>Gets or sets the preferred encoding for standard output.</summary>
		/// <returns>An object that represents the preferred encoding for standard output. The default is null.</returns>
		public Encoding StandardOutputEncoding
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.standardOutputEncoding;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.standardOutputEncoding = value;
			}
		}
		/// <summary>Gets or sets a value indicating whether to use the operating system shell to start the process.</summary>
		/// <returns>true if the shell should be used when starting the process; false if the process should be created directly from the executable file. The default is true.</returns>
		/// <filterpriority>2</filterpriority>
		[DefaultValue(true), NotifyParentProperty(true), MonitoringDescription("ProcessUseShellExecute")]
		public bool UseShellExecute
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.useShellExecute;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.useShellExecute = value;
			}
		}
		/// <summary>Gets the set of verbs associated with the type of file specified by the <see cref="P:System.Diagnostics.ProcessStartInfo.FileName" /> property.</summary>
		/// <returns>The actions that the system can apply to the file indicated by the <see cref="P:System.Diagnostics.ProcessStartInfo.FileName" /> property.</returns>
		/// <filterpriority>2</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string[] Verbs
		{
			get
			{
				ArrayList arrayList = new ArrayList();
				RegistryKey registryKey = null;
				string extension = Path.GetExtension(this.FileName);
				try
				{
					if (extension != null && extension.Length > 0)
					{
						registryKey = Registry.ClassesRoot.OpenSubKey(extension);
						if (registryKey != null)
						{
							string str = (string)registryKey.GetValue(string.Empty);
							registryKey.Close();
							registryKey = Registry.ClassesRoot.OpenSubKey(str + "\\shell");
							if (registryKey != null)
							{
								string[] subKeyNames = registryKey.GetSubKeyNames();
								for (int i = 0; i < subKeyNames.Length; i++)
								{
									if (string.Compare(subKeyNames[i], "new", StringComparison.OrdinalIgnoreCase) != 0)
									{
										arrayList.Add(subKeyNames[i]);
									}
								}
								registryKey.Close();
								registryKey = null;
							}
						}
					}
				}
				finally
				{
					if (registryKey != null)
					{
						registryKey.Close();
					}
				}
				string[] array = new string[arrayList.Count];
				arrayList.CopyTo(array, 0);
				return array;
			}
		}
		/// <summary>Gets or sets the user name to be used when starting the process.</summary>
		/// <returns>The user name to use when starting the process.</returns>
		/// <filterpriority>1</filterpriority>
		[NotifyParentProperty(true)]
		public string UserName
		{
			get
			{
				if (this.userName == null)
				{
					return string.Empty;
				}
				return this.userName;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.userName = value;
			}
		}
		/// <summary>Gets or sets a secure string that contains the user password to use when starting the process.</summary>
		/// <returns>The user password to use when starting the process.</returns>
		/// <filterpriority>1</filterpriority>
		public SecureString Password
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.password;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.password = value;
			}
		}
		/// <summary>Gets or sets a value that identifies the domain to use when starting the process. </summary>
		/// <returns>The Active Directory domain to use when starting the process. The domain property is primarily of interest to users within enterprise environments that use Active Directory.</returns>
		/// <filterpriority>1</filterpriority>
		[NotifyParentProperty(true)]
		public string Domain
		{
			get
			{
				if (this.domain == null)
				{
					return string.Empty;
				}
				return this.domain;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.domain = value;
			}
		}
		/// <summary>Gets or sets a value that indicates whether the Windows user profile is to be loaded from the registry. </summary>
		/// <returns>true if the Windows user profile should be loaded; otherwise, false. The default is false.</returns>
		/// <filterpriority>1</filterpriority>
		[NotifyParentProperty(true)]
		public bool LoadUserProfile
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.loadUserProfile;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.loadUserProfile = value;
			}
		}
		/// <summary>Gets or sets the application or document to start.</summary>
		/// <returns>The name of the application to start, or the name of a document of a file type that is associated with an application and that has a default open action available to it. The default is an empty string ("").</returns>
		/// <filterpriority>1</filterpriority>
		[DefaultValue(""), Editor("System.Diagnostics.Design.StartFileNameEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"), NotifyParentProperty(true), SettingsBindable(true), TypeConverter("System.Diagnostics.Design.StringValueConverter, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"), MonitoringDescription("ProcessFileName")]
		public string FileName
		{
			get
			{
				if (this.fileName == null)
				{
					return string.Empty;
				}
				return this.fileName;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.fileName = value;
			}
		}
		/// <summary>Gets or sets the initial directory for the process to be started.</summary>
		/// <returns>The fully qualified name of the directory that contains the process to be started. The default is an empty string ("").</returns>
		/// <filterpriority>1</filterpriority>
		[DefaultValue(""), Editor("System.Diagnostics.Design.WorkingDirectoryEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"), NotifyParentProperty(true), SettingsBindable(true), TypeConverter("System.Diagnostics.Design.StringValueConverter, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"), MonitoringDescription("ProcessWorkingDirectory")]
		public string WorkingDirectory
		{
			get
			{
				if (this.directory == null)
				{
					return string.Empty;
				}
				return this.directory;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.directory = value;
			}
		}
		/// <summary>Gets or sets a value indicating whether an error dialog box is displayed to the user if the process cannot be started.</summary>
		/// <returns>true if an error dialog box should be displayed on the screen if the process cannot be started; otherwise, false. The default is false.</returns>
		/// <filterpriority>2</filterpriority>
		[DefaultValue(false), NotifyParentProperty(true), MonitoringDescription("ProcessErrorDialog")]
		public bool ErrorDialog
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.errorDialog;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.errorDialog = value;
			}
		}
		/// <summary>Gets or sets the window handle to use when an error dialog box is shown for a process that cannot be started.</summary>
		/// <returns>A pointer to the handle of the error dialog box that results from a process start failure.</returns>
		/// <filterpriority>2</filterpriority>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public IntPtr ErrorDialogParentHandle
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.errorDialogParentHandle;
			}
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			set
			{
				this.errorDialogParentHandle = value;
			}
		}
		/// <summary>Gets or sets the window state to use when the process is started.</summary>
		/// <returns>One of the enumeration values that indicates whether the process is started in a window that is maximized, minimized, normal (neither maximized nor minimized), or not visible. The default is normal.</returns>
		/// <exception cref="T:System.ComponentModel.InvalidEnumArgumentException">The window style is not one of the <see cref="T:System.Diagnostics.ProcessWindowStyle" /> enumeration members. </exception>
		/// <filterpriority>2</filterpriority>
		[DefaultValue(ProcessWindowStyle.Normal), NotifyParentProperty(true), MonitoringDescription("ProcessWindowStyle")]
		public ProcessWindowStyle WindowStyle
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.windowStyle;
			}
			set
			{
				if (!Enum.IsDefined(typeof(ProcessWindowStyle), value))
				{
					throw new InvalidEnumArgumentException("value", (int)value, typeof(ProcessWindowStyle));
				}
				this.windowStyle = value;
			}
		}

		public ProcessCreationFlags CreationFlags { get; set; }

		/// <summary>Initializes a new instance of the <see cref="T:System.Diagnostics.ProcessStartInfo" /> class without specifying a file name with which to start the process.</summary>
		public ProcessStartInfoEx()
		{
		}
		internal ProcessStartInfoEx(ProcessEx parent)
		{
			this.weakParentProcess = new WeakReference(parent);
		}
		/// <summary>Initializes a new instance of the <see cref="T:System.Diagnostics.ProcessStartInfo" /> class and specifies a file name such as an application or document with which to start the process.</summary>
		/// <param name="fileName">An application or document with which to start a process. </param>
		public ProcessStartInfoEx(string fileName)
		{
			this.fileName = fileName;
		}
		/// <summary>Initializes a new instance of the <see cref="T:System.Diagnostics.ProcessStartInfo" /> class, specifies an application file name with which to start the process, and specifies a set of command-line arguments to pass to the application.</summary>
		/// <param name="fileName">An application with which to start a process. </param>
		/// <param name="arguments">Command-line arguments to pass to the application when the process starts. </param>
		public ProcessStartInfoEx(string fileName, string arguments)
		{
			this.fileName = fileName;
			this.arguments = arguments;
		}
	}
}
