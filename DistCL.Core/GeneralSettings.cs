using System;
using System.ServiceModel.Description;

namespace DistCL
{
	internal static class GeneralSettings
	{
		internal const string Namespace = "urn:distcl";

		private const string CoordinatorNamespace = Namespace + ":agents";
		internal const string CoordinatorMessageNamespace = CoordinatorNamespace + ":messages";

		private const string AgentPoolNamespace = CoordinatorNamespace + ":pool";
		internal const string AgentPoolMessageNamespace = AgentPoolNamespace + ":messages";

		private const string CompilerNamespace = Namespace + ":compiler";
		internal const string CompilerMessageNamespace = CompilerNamespace + ":messages";

		private const string LocalCompilerNamespace = CompilerNamespace + ":local";
		internal const string LocalCompilerMessageNamespace = LocalCompilerNamespace + ":messages";

		internal const string BindingNamespace = Namespace + ":bindings";
	}

	public class BindingNamespaceBehaviorAttribute : Attribute, IServiceBehavior
	{
		public void AddBindingParameters(
			ServiceDescription serviceDescription,
			System.ServiceModel.ServiceHostBase serviceHostBase,
			System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints,
			System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
		{
		}

		public void ApplyDispatchBehavior(
			ServiceDescription serviceDescription,
			System.ServiceModel.ServiceHostBase serviceHostBase)
		{
		}

		public void Validate(ServiceDescription serviceDescription, System.ServiceModel.ServiceHostBase serviceHostBase)
		{
			foreach (var endpoint in serviceHostBase.Description.Endpoints)
			{
				endpoint.Binding.Namespace = GeneralSettings.BindingNamespace;
			}
		}
	}
}