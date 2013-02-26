using System.Collections.Generic;
using System.Threading.Tasks;

namespace DistCL.Proxies
{
	internal interface IAgentPoolProxy : ICompileCoordinatorProxy
	{
		IEnumerable<IAgent> GetAgents();
		Task<IEnumerable<IAgent>> GetAgentsAsync();
	}
}