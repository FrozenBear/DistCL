using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using DistCL.Utils;

namespace DistCL.Client
{

	//public interface ICompilerDriver
	//{
	//    public IList<string> SourceFiles { get; }
	//    public IList<string> SourceFiles { get; }

	//}

	public struct OutputArtefact
	{
		private readonly CompileArtifactType _type;
		private readonly string _path;

		public CompileArtifactType Type
		{
			get { return _type; }
		}
		
		public string Path
		{
			get { return _path; }
		}

		public OutputArtefact(CompileArtifactType type, string path)
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
		private readonly Logger _logger = new Logger("CL_DRIVER");
		private readonly List<string> _sourceFiles = new List<string>();
		private readonly List<OutputArtefact> _outputFiles = new List<OutputArtefact>();
		private string _remoteCmdLine;
		private string _localCmdLine;
		private bool _localCompileOnly;
		private bool _pchCreation;

		private const char ArgSeparator = ' ';

		private readonly HashSet<string> _sourceExtensions = new HashSet<string> { ".cpp", ".cc", ".c" };

		//private HashSet<string> _optionsForIgnore = new HashSet<string>() { "Fm", "Fd", "Fm", "Fe", "Fo", "Fr", "Fp", "FR", "doc", "FU", };
		//private HashSet<string> _optionsWithArgs = new HashSet<string>() { "Fa", "Fd", "Fm", "Fp", "FR", "FA", "Fe", "Fo", "Fr", "doc" };

		public Logger Logger
		{
			get { return _logger; }
		}

		public CLDriver(string[] args)
		{
			Contract.Requires(args != null);

			Parse(args);
		}

		public IList<string> SourceFiles
		{
			get
			{
				return _sourceFiles;
			}
		}

		public IList<OutputArtefact> OutputFiles
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
			Contract.Requires(args != null);
			Contract.Requires(Contract.ForAll(args, s => s != null));

			var remoteCmdLine = new StringBuilder(" ");
			var localCmdLine = new StringBuilder(" ");

			var idx = 0;
			while (idx < args.Length)
			{
				if (String.IsNullOrEmpty(args[idx]))
					continue;

				var arg = args[idx];

				if (arg.IndexOfAny(new[] { '/', '-' }, 0, 1) != -1)
				{
					if (arg.Length < 2)
					{
						Logger.WarnFormat("ignoring unknown option '{0}'", arg[0]);
						++idx;
						continue;
					}

					if (arg.StartsWith("D", 1) || arg.StartsWith("I", 1))
					{
						if (arg.Length <= 2)
						{
							++idx;
							if (idx >= args.Length)
							{
								throw new ApplicationException(String.Format("Parameter '{0}' requires argument", arg));
							}
							localCmdLine.Append(arg);
							arg = args[idx];
						}

						localCmdLine.Append(arg.QuoteString() + ArgSeparator);
					}
					else if (arg.StartsWith("V", 1))
					{
						// depracated
					}
					else if (arg.StartsWith("FI", 1) || arg.StartsWith("Yu", 1) || arg.StartsWith("Fp", 1) || arg.StartsWith("Fd", 1))
					{
						// To opimize disributed compilation we will exclude whole PCH handling and forced include (for POA it is pch-server.h file)
						// TODO: We need to make this customizible in future
					}
					else if (arg.StartsWith("Yc", 1))
					{
						Logger.Warn("Pre-compiled headers are not supported for distributed builds. These options will be ignored.");
						_pchCreation = true;
					}
					else if (arg.StartsWith("Zi", 1) || arg.StartsWith("ZI", 1))
					{
						// replace with /Z7
						localCmdLine.Append("/Z7" + ArgSeparator);
						remoteCmdLine.Append("/Z7" + ArgSeparator);
					}
					else if (arg.StartsWith("Fo", 1))
					{
						// output object file name
						_outputFiles.Add(new OutputArtefact(CompileArtifactType.Obj, arg.Substring(3)));
					}
					else if ((arg.Length == 3 && arg.EndsWith("EP", StringComparison.Ordinal)) || (arg.Length == 2 && (arg.EndsWith("P", StringComparison.Ordinal) || arg.EndsWith("E", StringComparison.Ordinal))))
					{
						// only local compile is needed
						localCmdLine.Append(arg + ArgSeparator);
						_localCompileOnly = true;
					}
					// TODO fix '-' option flag here
					else if (arg.Equals("/LD", StringComparison.Ordinal) || arg.Equals("/LDd", StringComparison.Ordinal) || arg.Equals("/LN", StringComparison.Ordinal) || arg.StartsWith("/link"))
					{
						throw new NotSupportedException(String.Format("Linker option '{0}' is not supported for distributed builds", arg));
					}
					else
					{
						localCmdLine.Append(arg.QuoteString() + ArgSeparator);
						remoteCmdLine.Append(arg.QuoteString() + ArgSeparator);
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
						Logger.WarnFormat("Unrecognized source file type. File '{0}' will be ignored.", arg);
						continue;
					}

					// TODO Check /Fo flag too
					if (_sourceFiles.Count > 0)
						throw new NotSupportedException("Several input source files aren't supported yet");

					_sourceFiles.Add(args[idx]);
					localCmdLine.Append(arg.QuoteString() + ArgSeparator);

				}

				++idx;
			} // while

			if (!_localCompileOnly)
				localCmdLine.Append("/E");

			_localCmdLine = localCmdLine.ToString();
			_remoteCmdLine = remoteCmdLine.ToString();
		}

	}
}
