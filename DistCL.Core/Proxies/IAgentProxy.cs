namespace DistCL.Proxies
{
	internal interface IAgentProxy
	{
		IAgent Description { get; }
		ICompiler GetCompiler();
		ICompileCoordinatorProxy GetCoordinator();
		IAgentPoolProxy GetAgentPool();
	}
}