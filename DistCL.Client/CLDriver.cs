using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DistCL.Utils;

namespace DistCL.Client
{

	//public interface ICompilerDriver
	//{
	//    public IList<string> SourceFiles { get; }
	//    public IList<string> SourceFiles { get; }

	//}

	public struct TOutputArtefact
	{
		private CompileArtifactType _type;
		private string _path;

		public CompileArtifactType Type
		{
			get { return _type; }
		}
		
		public string Path
		{
			get { return _path; }
		}

		public TOutputArtefact(CompileArtifactType type, string path)
		{
			_type = type;
			_path = path;
		}
	}

	/// <summary>
	/// Parser for cl.exe command line. We have the following requirements:
	///     1. PCH will be turning off (generation and using, i.e we could process this artefacts locally or simply ignore it)
	///     2. Only one PDB-file format is supported: /Z7 - embedded into the obj. All /Zi /ZI options will be replaced with /Z7
	///     3. Do we need/can support SBR files generation?
	///     4. Response @file is not supported
	///     5. Many input source files are not supported. One Source => One Target with /C option always (compile only mode)
	///     6. All linker options must be thrown error becuase it isn't supported.
	///     7. /FU is not supported
	///     8. /Tc and /Tp are supported but separated input source should be checked
	///     9. All preprocessor options like /E /EP should be lead to simple local cl.exe call with preprocessor output
	///     10. Additional output files like: *.map, *.sbr, *.lst will be supported in future
	/// </summary>
	public class CLDriver
	{
		private const string CLFilename = "cl.exe";

		private List<string> _sourceFiles = new List<string>();
		private List<TOutputArtefact> _outputFiles = new List<TOutputArtefact>();
		private string _remoteCmdLine;
		private string _localCmdLine;
		private bool _localCompileOnly = false;
		private bool _pchCreation = false;

		private const char ArgSeparator = ' ';

		private readonly HashSet<string> _sourceExtensions = new HashSet<string>() { ".cpp", ".cc", ".c" };

		private HashSet<string> _optionsForIgnore = new HashSet<string>() { "Fm", "Fd", "Fm", "Fe", "Fo", "Fr", "Fp", "FR", "doc", "FU", };
		private HashSet<string> _optionsWithArgs = new HashSet<string>() { "Fa", "Fd", "Fm", "Fp", "FR", "FA", "Fe", "Fo", "Fr", "doc" };

		public CLDriver(string[] args)
		{
			Parse(args);
		}

		public IList<string> SourceFiles
		{
			get
			{
				return _sourceFiles;
			}
		}

		public IList<TOutputArtefact> OutputFiles
		{
			get
			{
				return _outputFiles;
			}
		}

		public string LocalCommandLine
		{
			get
			{
				return _localCmdLine;
			}
		}

		public string RemoteCommandLine
		{
			get
			{
				return _remoteCmdLine;
			}
		}

		public bool LocalCompilationOnly
		{
			get { return _localCompileOnly; }
		}

		public bool PchCreationRequest
		{
			get { return _pchCreation; }
		}

		private void Parse(string[] args)
		{
			StringBuilder remoteCmdLine = new StringBuilder(" ");
			StringBuilder localCmdLine = new StringBuilder(" ");

			int idx = 0;
			while (idx < args.Length)
			{
				if (String.IsNullOrEmpty(args[idx]))
					continue;

				string arg = args[idx];

				if (arg.IndexOfAny(new char[] { '/', '-' }, 0, 1) != -1)
				{
					if (arg.StartsWith("/D") || arg.StartsWith("/I"))
					{
						if (arg.Length <= 2)
						{
							++idx;
							if (idx >= args.Length)
							{
								throw new ApplicationException(String.Format("Parameter '{0}' requires argument", arg));
							}
							localCmdLine.Append(arg);
						}

						localCmdLine.Append(StringUtils.QuoteString(arg) + ArgSeparator);
					}
					else if (arg.StartsWith("/V"))
					{
						// depracated
					}
					else if (arg.StartsWith("/FI") || arg.StartsWith("/Yu") || arg.StartsWith("/Fp") || arg.StartsWith("/Fd"))
					{
						// skip it
					}
					else if (arg.StartsWith("/Yc"))
					{
						Logger.Warn("Pre-compiled headers are not supported for distributed builds. TheThese options will be ignored.");
						_pchCreation = true;
					}
					else if (arg.StartsWith("/Zi") || arg.StartsWith("/ZI"))
					{
						// replace with /Z7
						localCmdLine.Append("/Z7" + ArgSeparator);
						remoteCmdLine.Append("/Z7" + ArgSeparator);
					}
					else if (arg.StartsWith("/Fo"))
					{
						// output object file name
						_outputFiles.Add(new TOutputArtefact(CompileArtifactType.Obj, arg.Substring(3)));
					}
					else if (arg.StartsWith("/EP") || arg.StartsWith("/P") || arg.StartsWith("/E"))
					{
						// only local compile is needed
						localCmdLine.Append(arg + ArgSeparator);
						_localCompileOnly = true;
					}
					else if (arg.StartsWith("/LD") || arg.StartsWith("/LDd") || arg.StartsWith("/LN") || arg.StartsWith("/link"))
					{
						throw new NotSupportedException(String.Format("Linker option '{0}' is not supported for distributed builds", arg));
					}
					else
					{
						localCmdLine.Append(StringUtils.QuoteString(arg) + ArgSeparator);
						remoteCmdLine.Append(StringUtils.QuoteString(arg) + ArgSeparator);
					}
				}
				else if (arg.StartsWith("@"))
				{
					// Currently we do not support file lists
					throw new ApplicationException("@filelist argument doesn't supported by DistCL");
				}
				else
				{
					// filename
					// cl.exe analize files by extension. For sources it supports: *.cpp, *.c, *.cc
					if (!File.Exists(arg))
						throw new FileNotFoundException("Source file not found", arg);

					if (!_sourceExtensions.Contains(Path.GetExtension(arg)))
					{
						Logger.Warn(String.Format("Unrecognized source file type. File '{0}' will be ignored.", arg));
						continue;
					}

					// TODO Check /Fo flag too
					if (_sourceFiles.Count > 0)
						throw new NotSupportedException("Several input source files aren't supported yet");

					_sourceFiles.Add(args[idx]);
					localCmdLine.Append(StringUtils.QuoteString(arg) + ArgSeparator);

				}

				++idx;
			} // while
			_localCmdLine = localCmdLine.ToString();
			_remoteCmdLine = remoteCmdLine.ToString();
		}

	}
}
