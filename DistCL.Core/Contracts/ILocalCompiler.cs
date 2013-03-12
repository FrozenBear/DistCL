using System;
using System.Collections.Generic;
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
		}
	}

	[DataContract(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
	public class CompilerNotFoundFaultContract
	{
		[DataMember]
		public string CompilerVersion { get; set; }
	}
}