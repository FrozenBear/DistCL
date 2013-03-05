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
		Guid GetPreprocessToken(string name, string compilerVersion, out string accountName);

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
		public Guid PreprocessToken { get; set; }
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

	[DataContract]
	public class CompilerNotFoundFaultContract
	{
		[DataMember]
		public string CompilerVersion { get; set; }
	}
}