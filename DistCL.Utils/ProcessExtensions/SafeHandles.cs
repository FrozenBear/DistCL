using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;

namespace DistCL.Utils.ProcessExtensions
{
	[SecurityCritical]
	public sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal static SafeTokenHandle InvalidHandle
		{
			get
			{
				return new SafeTokenHandle(IntPtr.Zero);
			}
		}
		private SafeTokenHandle()
			: base(true)
		{
		}
		internal SafeTokenHandle(IntPtr handle)
			: base(true)
		{
			base.SetHandle(handle);
		}
		[SecurityCritical]
		protected override bool ReleaseHandle()
		{
			return SafeNativeMethods.CloseHandle(this.handle);
		}
	}

	[SuppressUnmanagedCodeSecurity]
	public sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal static SafeProcessHandle InvalidHandle = new SafeProcessHandle(IntPtr.Zero);
		internal SafeProcessHandle()
			: base(true)
		{
		}
		internal SafeProcessHandle(IntPtr handle)
			: base(true)
		{
			base.SetHandle(handle);
		}
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		internal static extern SafeProcessHandle OpenProcess(int access, bool inherit, int processId);
		internal void InitialSetHandle(IntPtr h)
		{
			this.handle = h;
		}
		protected override bool ReleaseHandle()
		{
			return SafeNativeMethods.CloseHandle(this.handle);
		}
	}

	internal sealed class SafeThreadHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal SafeThreadHandle()
			: base(true)
		{
		}
		internal void InitialSetHandle(IntPtr h)
		{
			base.SetHandle(h);
		}
		protected override bool ReleaseHandle()
		{
			return SafeNativeMethods.CloseHandle(this.handle);
		}
	}

	[SuppressUnmanagedCodeSecurity]
	[HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
	internal sealed class SafeLocalMemHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal SafeLocalMemHandle()
			: base(true)
		{
		}
		[SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
		internal SafeLocalMemHandle(IntPtr existingHandle, bool ownsHandle)
			: base(ownsHandle)
		{
			base.SetHandle(existingHandle);
		}
		[DllImport("advapi32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
		internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string StringSecurityDescriptor, int StringSDRevision, out SafeLocalMemHandle pSecurityDescriptor, IntPtr SecurityDescriptorSize);
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[DllImport("kernel32.dll")]
		private static extern IntPtr LocalFree(IntPtr hMem);
		protected override bool ReleaseHandle()
		{
			return SafeLocalMemHandle.LocalFree(this.handle) == IntPtr.Zero;
		}
	}

}
