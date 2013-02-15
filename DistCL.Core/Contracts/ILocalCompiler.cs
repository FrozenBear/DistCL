using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using DistCL.Utils;

namespace DistCL
{
	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	//[ServiceContract]
	public interface ILocalCompiler
	{
		[OperationContract]
		LocalCompileOutput LocalCompile(LocalCompileInput input);
	}

	[MessageContract(WrapperNamespace = GeneralSettings.LocalCompilerMessageNamespace)]
	//[MessageContract]
	public class LocalCompileInput : ICompileInput<string>
	{
		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		//[MessageBodyMember]
		public string Arguments { get; set; }

		[MessageBodyMember(Namespace = GeneralSettings.LocalCompilerMessageNamespace)]
		//[MessageBodyMember]
		public string Src { get; set; }
	}

	[MessageContract(WrapperNamespace = GeneralSettings.LocalCompilerMessageNamespace)]
	//[MessageContract]
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