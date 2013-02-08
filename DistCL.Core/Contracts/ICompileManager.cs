using System.ServiceModel;

namespace DistCL
{
	[ServiceContract]
	public interface ICompileManager : ICompiler, IAgentPool
	{

	}
}