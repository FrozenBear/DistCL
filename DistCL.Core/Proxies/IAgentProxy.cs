namespace DistCL
{
	internal interface IAgentProxy
	{
		IAgent Description { get; }
		ICompiler GetCompiler();
		ICompileCoordinatorInternal GetCoordinator();
		IAgentPoolInternal GetAgentPool();
	}
}