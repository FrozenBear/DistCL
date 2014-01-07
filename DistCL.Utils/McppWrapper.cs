using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DistCL.Utils
{
	public class McppWrapper
	{
		private const string McppX64Library = "MCPP_DLLx64.dll";
		private const string McppX86Library = "MCPP_DLLx86.dll";
		private static bool isX64 = Environment.Is64BitProcess;

		//public List<string> GetDependencies()
		//{
		//	Run()
		//}

		public static int Run(int argc, string[] argv)
		{
			return isX64 ? Run64bit(argc, argv) : Run32bit(argc, argv);
		}

		// extern DLL_DECL int     mcpp_lib_main( int argc, char ** argv);
		[DllImport(McppX86Library, EntryPoint = "mcpp_lib_main", CallingConvention = CallingConvention.Cdecl)]
		private static extern Int32 Run32bit(Int32 argc, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, ArraySubType = UnmanagedType.LPStr)] string[] argv);

		[DllImport(McppX64Library, EntryPoint = "mcpp_lib_main", CallingConvention = CallingConvention.Cdecl)]
		private static extern Int32 Run64bit(Int32 argc, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, ArraySubType = UnmanagedType.LPStr)] string[] argv);

	}
}
