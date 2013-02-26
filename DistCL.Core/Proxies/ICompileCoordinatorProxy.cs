using System.Threading.Tasks;

namespace DistCL.Proxies
{
	internal interface ICompileCoordinatorProxy
	{
		string Name { get; }

		void RegisterAgent(IAgentProxy request);
		Task RegisterAgentAsync(IAgentProxy request);

		bool IncreaseErrorCount();
		void ResetErrorCount();

		IAgentProxy Proxy { get; }
		
		IAgent GetDescription();
		Task<IAgent> GetDescriptionAsync();
	}
}