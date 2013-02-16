using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using DistCL.Utils;

namespace DistCL
{
	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	public interface ILocalCompiler
	{
		[OperationContract]
		LocalCompileOutput LocalCompile(LocalCompileInput input);
	}

	[MessageContract(WrapperNamespace = GeneralSettings.LocalCompilerMessageNamespace)]
	public class LocalCompileInput
	{
		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		public string Arguments { get; set; }

		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		public string Src { get; set; }

		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		public string SrcName { get; set; }
	}

	[MessageContract(WrapperNamespace = GeneralSettings.LocalCompilerMessageNamespace)]
	public class LocalCompileOutput : CompileOutput
	{
		public LocalCompileOutput(CompileStatus status, Stream resultData) : base(status, resultData)
		{
		}

		public LocalCompileOutput(bool success, int exitCode, IDictionary<CompileArtifactDescription, Stream> streams) : base(success, exitCode, streams)
		{
		}
	}
}