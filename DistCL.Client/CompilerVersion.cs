using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DistCL.Client
{
	internal sealed class CompilerVersion
	{
		private static string _compilerVersion;
		private static string _compilerVersionForDefine;

		static void RefreshVersionInfo()
		{
			string envPathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
			foreach (var folder in new[] { "." }.Concat(envPathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)))
			{
				var clPath = Path.Combine(folder, Utils.CompilerSettings.CLExeFilename);
				if (!File.Exists(clPath))
					continue;

				var verInfo = FileVersionInfo.GetVersionInfo(clPath);
				_compilerVersion = verInfo.FileVersion;
				_compilerVersionForDefine = verInfo.FileMajorPart.ToString("D2") + verInfo.FileMinorPart.ToString("D2");
				break;
			}

			if (String.IsNullOrEmpty(_compilerVersion) || String.IsNullOrEmpty(_compilerVersionForDefine))
				throw new Exception("Compiler not found");
		}

		public static string VersionString
		{
			get
			{
				if (_compilerVersion == null)
				{
					RefreshVersionInfo();
				}

				return _compilerVersion;
			}
		}
		public static string VersionStringForDefine
		{
			get
			{
				if (_compilerVersionForDefine == null)
				{
					RefreshVersionInfo();
				}

				return _compilerVersionForDefine;
			}
		}
	}
}