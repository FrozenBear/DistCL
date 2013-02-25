using System.Collections.Generic;
using System.Threading.Tasks;

namespace DistCL
{
	internal interface IAgentPoolInternal : ICompileCoordinatorInternal
	{
		IEnumerable<IAgent> GetAgents();
		Task<IEnumerable<IAgent>> GetAgentsAsync();
	}
}