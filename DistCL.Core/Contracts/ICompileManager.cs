using System.ServiceModel;

namespace DistCL
{
	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	public interface ICompileManager : ICompiler, IAgentPool
	{

	}
}