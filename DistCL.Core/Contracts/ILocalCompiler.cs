using System.ServiceModel;

namespace DistCL
{
	[ServiceContract]
	public interface ILocalCompiler
	{
		[OperationContract]
		CompileOutput LocalCompile(LocalCompileInput input);
	}

	[MessageContract]
	public class LocalCompileInput : ICompileInput<string>
	{
		[MessageBodyMember]
		public string Arguments { get; set; }

		[MessageBodyMember]
		public string Src { get; set; }
	}
}