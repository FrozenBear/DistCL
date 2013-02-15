using System.ServiceModel;

namespace DistCL
{
	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	//[ServiceContract]
	public interface ICompileManager : ICompiler, IAgentPool
	{

	}
}