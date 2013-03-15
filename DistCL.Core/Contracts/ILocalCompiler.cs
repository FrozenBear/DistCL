using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using DistCL.Utils;

namespace DistCL
{
	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	public interface ILocalCompiler
	{
		[OperationContract]
		[FaultContract(typeof(CompilerNotFoundFaultContract), Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		PreprocessToken GetPreprocessToken(string name, string compilerVersion);

		[OperationContract]
		[FaultContract(typeof(CompilerNotFoundFaultContract), Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		LocalCompileOutput LocalCompile(LocalCompileInput input);
	}

	[MessageContract(WrapperNamespace = GeneralSettings.LocalCompilerMessageNamespace)]
	public class LocalCompileInput
	{
		public LocalCompileInput(
			string compilerVersion,
			string arguments,
			string src,
			string srcName,
			PreprocessToken preprocessToken)
		{
			Contract.Requires(! string.IsNullOrEmpty(compilerVersion));
			Contract.Requires(! string.IsNullOrEmpty(arguments));
			Contract.Requires(! string.IsNullOrEmpty(src));
			Contract.Requires(! string.IsNullOrEmpty(srcName));
			Contract.Requires(preprocessToken != null);

			CompilerVersion = compilerVersion;
			Arguments = arguments;
			Src = src;
			SrcName = srcName;
			PreprocessToken = preprocessToken;
		}

		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		public string CompilerVersion { get; set; }

		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		public string Arguments { get; set; }

		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		public string Src { get; set; }

		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		public string SrcName { get; set; }

		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		public PreprocessToken PreprocessToken { get; set; }

		[ContractInvariantMethod]
		private void CheckInvariant()
		{
			Contract.Invariant(!string.IsNullOrEmpty(CompilerVersion));
			Contract.Invariant(!string.IsNullOrEmpty(Arguments));
			Contract.Invariant(!string.IsNullOrEmpty(Src));
			Contract.Invariant(!string.IsNullOrEmpty(SrcName));
			Contract.Invariant(PreprocessToken != null);
		}
	}

	[DataContract(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
	public class PreprocessToken
	{
		public PreprocessToken(Guid guid, string accountName, DateTime requested, DateTime created)
		{
			Guid = guid;
			AccountName = accountName;
			Requested = requested;
			Created = created;
		}

		[DataMember]
		public DateTime Requested { get; set; }

		[DataMember]
		public DateTime Created { get; set; }

		[DataMember]
		public Guid Guid { get; set; }

		[DataMember]
		public string AccountName { get; set; }
	}

	[MessageContract(WrapperNamespace = GeneralSettings.LocalCompilerMessageNamespace)]
	public class LocalCompileOutput : CompileOutput
	{
		public LocalCompileOutput(CompileStatus status, Stream resultData) : base(status, resultData)
		{
		}

		public LocalCompileOutput(
			bool success,
			int exitCode,
			IDictionary<CompileArtifactDescription, Stream> streams,
			IEnumerable<CompileArtifactDescription> artifacts)
			: base(success, exitCode, streams, artifacts)
		{
			Contract.Requires(streams != null);
		}
	}

	[DataContract(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
	public class CompilerNotFoundFaultContract
	{
		public CompilerNotFoundFaultContract(string compilerVersion)
		{
			CompilerVersion = compilerVersion;
		}

		[DataMember]
		public string CompilerVersion { get; private set; }
	}
}