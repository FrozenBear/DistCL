﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18034
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DistCL.RemoteCompilerService {
    using System.Runtime.Serialization;
    using System;
    
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="CompileStatus", Namespace="urn:distcl:compiler:messages")]
    [System.SerializableAttribute()]
    public partial class CompileStatus : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private DistCL.Utils.CompileArtifactCookie[] CookiesField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int ExitCodeField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private bool SuccessField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public DistCL.Utils.CompileArtifactCookie[] Cookies {
            get {
                return this.CookiesField;
            }
            set {
                if ((object.ReferenceEquals(this.CookiesField, value) != true)) {
                    this.CookiesField = value;
                    this.RaisePropertyChanged("Cookies");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int ExitCode {
            get {
                return this.ExitCodeField;
            }
            set {
                if ((this.ExitCodeField.Equals(value) != true)) {
                    this.ExitCodeField = value;
                    this.RaisePropertyChanged("ExitCode");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public bool Success {
            get {
                return this.SuccessField;
            }
            set {
                if ((this.SuccessField.Equals(value) != true)) {
                    this.SuccessField = value;
                    this.RaisePropertyChanged("Success");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="Agent", Namespace="urn:distcl:agents:messages")]
    [System.SerializableAttribute()]
    public partial class Agent : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private System.Uri[] AgentPoolUrlsField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int CPUUsageField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private System.Uri[] CompilerUrlsField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int CoresField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private System.Guid GuidField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string NameField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public System.Uri[] AgentPoolUrls {
            get {
                return this.AgentPoolUrlsField;
            }
            set {
                if ((object.ReferenceEquals(this.AgentPoolUrlsField, value) != true)) {
                    this.AgentPoolUrlsField = value;
                    this.RaisePropertyChanged("AgentPoolUrls");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int CPUUsage {
            get {
                return this.CPUUsageField;
            }
            set {
                if ((this.CPUUsageField.Equals(value) != true)) {
                    this.CPUUsageField = value;
                    this.RaisePropertyChanged("CPUUsage");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public System.Uri[] CompilerUrls {
            get {
                return this.CompilerUrlsField;
            }
            set {
                if ((object.ReferenceEquals(this.CompilerUrlsField, value) != true)) {
                    this.CompilerUrlsField = value;
                    this.RaisePropertyChanged("CompilerUrls");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int Cores {
            get {
                return this.CoresField;
            }
            set {
                if ((this.CoresField.Equals(value) != true)) {
                    this.CoresField = value;
                    this.RaisePropertyChanged("Cores");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public System.Guid Guid {
            get {
                return this.GuidField;
            }
            set {
                if ((this.GuidField.Equals(value) != true)) {
                    this.GuidField = value;
                    this.RaisePropertyChanged("Guid");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Name {
            get {
                return this.NameField;
            }
            set {
                if ((object.ReferenceEquals(this.NameField, value) != true)) {
                    this.NameField = value;
                    this.RaisePropertyChanged("Name");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="urn:distcl", ConfigurationName="RemoteCompilerService.ILocalCompiler")]
    public interface ILocalCompiler {
        
        // CODEGEN: Generating message contract since the wrapper namespace (urn:distcl:compiler:local:messages) of message LocalCompileInput does not match the default value (urn:distcl)
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ILocalCompiler/LocalCompile", ReplyAction="urn:distcl/ILocalCompiler/LocalCompileResponse")]
        DistCL.RemoteCompilerService.LocalCompileOutput LocalCompile(DistCL.RemoteCompilerService.LocalCompileInput request);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ILocalCompiler/LocalCompile", ReplyAction="urn:distcl/ILocalCompiler/LocalCompileResponse")]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.LocalCompileOutput> LocalCompileAsync(DistCL.RemoteCompilerService.LocalCompileInput request);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ILocalCompiler/GetPreprocessToken", ReplyAction="urn:distcl/ILocalCompiler/GetPreprocessTokenResponse")]
        System.Guid GetPreprocessToken(string name);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ILocalCompiler/GetPreprocessToken", ReplyAction="urn:distcl/ILocalCompiler/GetPreprocessTokenResponse")]
        System.Threading.Tasks.Task<System.Guid> GetPreprocessTokenAsync(string name);
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(WrapperName="LocalCompileInput", WrapperNamespace="urn:distcl:compiler:local:messages", IsWrapped=true)]
    public partial class LocalCompileInput {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="urn:distcl:compiler:local:messages", Order=0)]
        public string Arguments;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="urn:distcl:compiler:local:messages", Order=1)]
        public System.Guid PreprocessToken;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="urn:distcl:compiler:local:messages", Order=2)]
        public string Src;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="urn:distcl:compiler:local:messages", Order=3)]
        public string SrcName;
        
        public LocalCompileInput() {
        }
        
        public LocalCompileInput(string Arguments, System.Guid PreprocessToken, string Src, string SrcName) {
            this.Arguments = Arguments;
            this.PreprocessToken = PreprocessToken;
            this.Src = Src;
            this.SrcName = SrcName;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(WrapperName="LocalCompileOutput", WrapperNamespace="urn:distcl:compiler:local:messages", IsWrapped=true)]
    public partial class LocalCompileOutput {
        
        [System.ServiceModel.MessageHeaderAttribute(Namespace="urn:distcl")]
        public DistCL.RemoteCompilerService.CompileStatus Status;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="urn:distcl", Order=0)]
        public System.IO.Stream ResultData;
        
        public LocalCompileOutput() {
        }
        
        public LocalCompileOutput(DistCL.RemoteCompilerService.CompileStatus Status, System.IO.Stream ResultData) {
            this.Status = Status;
            this.ResultData = ResultData;
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface ILocalCompilerChannel : DistCL.RemoteCompilerService.ILocalCompiler, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class LocalCompilerClient : System.ServiceModel.ClientBase<DistCL.RemoteCompilerService.ILocalCompiler>, DistCL.RemoteCompilerService.ILocalCompiler {
        
        public LocalCompilerClient() {
        }
        
        public LocalCompilerClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public LocalCompilerClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public LocalCompilerClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public LocalCompilerClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        DistCL.RemoteCompilerService.LocalCompileOutput DistCL.RemoteCompilerService.ILocalCompiler.LocalCompile(DistCL.RemoteCompilerService.LocalCompileInput request) {
            return base.Channel.LocalCompile(request);
        }
        
        public DistCL.RemoteCompilerService.CompileStatus LocalCompile(string Arguments, System.Guid PreprocessToken, string Src, string SrcName, out System.IO.Stream ResultData) {
            DistCL.RemoteCompilerService.LocalCompileInput inValue = new DistCL.RemoteCompilerService.LocalCompileInput();
            inValue.Arguments = Arguments;
            inValue.PreprocessToken = PreprocessToken;
            inValue.Src = Src;
            inValue.SrcName = SrcName;
            DistCL.RemoteCompilerService.LocalCompileOutput retVal = ((DistCL.RemoteCompilerService.ILocalCompiler)(this)).LocalCompile(inValue);
            ResultData = retVal.ResultData;
            return retVal.Status;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.LocalCompileOutput> DistCL.RemoteCompilerService.ILocalCompiler.LocalCompileAsync(DistCL.RemoteCompilerService.LocalCompileInput request) {
            return base.Channel.LocalCompileAsync(request);
        }
        
        public System.Threading.Tasks.Task<DistCL.RemoteCompilerService.LocalCompileOutput> LocalCompileAsync(string Arguments, System.Guid PreprocessToken, string Src, string SrcName) {
            DistCL.RemoteCompilerService.LocalCompileInput inValue = new DistCL.RemoteCompilerService.LocalCompileInput();
            inValue.Arguments = Arguments;
            inValue.PreprocessToken = PreprocessToken;
            inValue.Src = Src;
            inValue.SrcName = SrcName;
            return ((DistCL.RemoteCompilerService.ILocalCompiler)(this)).LocalCompileAsync(inValue);
        }
        
        public System.Guid GetPreprocessToken(string name) {
            return base.Channel.GetPreprocessToken(name);
        }
        
        public System.Threading.Tasks.Task<System.Guid> GetPreprocessTokenAsync(string name) {
            return base.Channel.GetPreprocessTokenAsync(name);
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="urn:distcl", ConfigurationName="RemoteCompilerService.ICompiler")]
    public interface ICompiler {
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompiler/IsReady", ReplyAction="urn:distcl/ICompiler/IsReadyResponse")]
        bool IsReady();
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompiler/IsReady", ReplyAction="urn:distcl/ICompiler/IsReadyResponse")]
        System.Threading.Tasks.Task<bool> IsReadyAsync();
        
        // CODEGEN: Generating message contract since the wrapper namespace (urn:distcl:compiler:messages) of message CompileInput does not match the default value (urn:distcl)
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompiler/Compile", ReplyAction="urn:distcl/ICompiler/CompileResponse")]
        DistCL.RemoteCompilerService.CompileOutput Compile(DistCL.RemoteCompilerService.CompileInput request);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompiler/Compile", ReplyAction="urn:distcl/ICompiler/CompileResponse")]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.CompileOutput> CompileAsync(DistCL.RemoteCompilerService.CompileInput request);
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(WrapperName="CompileInput", WrapperNamespace="urn:distcl:compiler:messages", IsWrapped=true)]
    public partial class CompileInput {
        
        [System.ServiceModel.MessageHeaderAttribute(Namespace="urn:distcl")]
        public string Arguments;
        
        [System.ServiceModel.MessageHeaderAttribute(Namespace="urn:distcl")]
        public long SrcLength;
        
        [System.ServiceModel.MessageHeaderAttribute(Namespace="urn:distcl")]
        public string SrcName;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="urn:distcl", Order=0)]
        public System.IO.Stream Src;
        
        public CompileInput() {
        }
        
        public CompileInput(string Arguments, long SrcLength, string SrcName, System.IO.Stream Src) {
            this.Arguments = Arguments;
            this.SrcLength = SrcLength;
            this.SrcName = SrcName;
            this.Src = Src;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(WrapperName="CompileOutput", WrapperNamespace="urn:distcl:compiler:messages", IsWrapped=true)]
    public partial class CompileOutput {
        
        [System.ServiceModel.MessageHeaderAttribute(Namespace="urn:distcl")]
        public DistCL.RemoteCompilerService.CompileStatus Status;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="urn:distcl", Order=0)]
        public System.IO.Stream ResultData;
        
        public CompileOutput() {
        }
        
        public CompileOutput(DistCL.RemoteCompilerService.CompileStatus Status, System.IO.Stream ResultData) {
            this.Status = Status;
            this.ResultData = ResultData;
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface ICompilerChannel : DistCL.RemoteCompilerService.ICompiler, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class CompilerClient : System.ServiceModel.ClientBase<DistCL.RemoteCompilerService.ICompiler>, DistCL.RemoteCompilerService.ICompiler {
        
        public CompilerClient() {
        }
        
        public CompilerClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public CompilerClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public CompilerClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public CompilerClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        public bool IsReady() {
            return base.Channel.IsReady();
        }
        
        public System.Threading.Tasks.Task<bool> IsReadyAsync() {
            return base.Channel.IsReadyAsync();
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        DistCL.RemoteCompilerService.CompileOutput DistCL.RemoteCompilerService.ICompiler.Compile(DistCL.RemoteCompilerService.CompileInput request) {
            return base.Channel.Compile(request);
        }
        
        public DistCL.RemoteCompilerService.CompileStatus Compile(string Arguments, long SrcLength, string SrcName, System.IO.Stream Src, out System.IO.Stream ResultData) {
            DistCL.RemoteCompilerService.CompileInput inValue = new DistCL.RemoteCompilerService.CompileInput();
            inValue.Arguments = Arguments;
            inValue.SrcLength = SrcLength;
            inValue.SrcName = SrcName;
            inValue.Src = Src;
            DistCL.RemoteCompilerService.CompileOutput retVal = ((DistCL.RemoteCompilerService.ICompiler)(this)).Compile(inValue);
            ResultData = retVal.ResultData;
            return retVal.Status;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.CompileOutput> DistCL.RemoteCompilerService.ICompiler.CompileAsync(DistCL.RemoteCompilerService.CompileInput request) {
            return base.Channel.CompileAsync(request);
        }
        
        public System.Threading.Tasks.Task<DistCL.RemoteCompilerService.CompileOutput> CompileAsync(string Arguments, long SrcLength, string SrcName, System.IO.Stream Src) {
            DistCL.RemoteCompilerService.CompileInput inValue = new DistCL.RemoteCompilerService.CompileInput();
            inValue.Arguments = Arguments;
            inValue.SrcLength = SrcLength;
            inValue.SrcName = SrcName;
            inValue.Src = Src;
            return ((DistCL.RemoteCompilerService.ICompiler)(this)).CompileAsync(inValue);
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="urn:distcl", ConfigurationName="RemoteCompilerService.IAgentPool")]
    public interface IAgentPool {
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompileCoordinator/GetDescription", ReplyAction="urn:distcl/ICompileCoordinator/GetDescriptionResponse")]
        DistCL.RemoteCompilerService.Agent GetDescription();
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompileCoordinator/GetDescription", ReplyAction="urn:distcl/ICompileCoordinator/GetDescriptionResponse")]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent> GetDescriptionAsync();
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="urn:distcl/ICompileCoordinator/RegisterAgent")]
        void RegisterAgent(DistCL.RemoteCompilerService.Agent request);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="urn:distcl/ICompileCoordinator/RegisterAgent")]
        System.Threading.Tasks.Task RegisterAgentAsync(DistCL.RemoteCompilerService.Agent request);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/IAgentPool/GetAgents", ReplyAction="urn:distcl/IAgentPool/GetAgentsResponse")]
        DistCL.RemoteCompilerService.Agent[] GetAgents();
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/IAgentPool/GetAgents", ReplyAction="urn:distcl/IAgentPool/GetAgentsResponse")]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent[]> GetAgentsAsync();
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IAgentPoolChannel : DistCL.RemoteCompilerService.IAgentPool, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class AgentPoolClient : System.ServiceModel.ClientBase<DistCL.RemoteCompilerService.IAgentPool>, DistCL.RemoteCompilerService.IAgentPool {
        
        public AgentPoolClient() {
        }
        
        public AgentPoolClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public AgentPoolClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public AgentPoolClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public AgentPoolClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        public DistCL.RemoteCompilerService.Agent GetDescription() {
            return base.Channel.GetDescription();
        }
        
        public System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent> GetDescriptionAsync() {
            return base.Channel.GetDescriptionAsync();
        }
        
        public void RegisterAgent(DistCL.RemoteCompilerService.Agent request) {
            base.Channel.RegisterAgent(request);
        }
        
        public System.Threading.Tasks.Task RegisterAgentAsync(DistCL.RemoteCompilerService.Agent request) {
            return base.Channel.RegisterAgentAsync(request);
        }
        
        public DistCL.RemoteCompilerService.Agent[] GetAgents() {
            return base.Channel.GetAgents();
        }
        
        public System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent[]> GetAgentsAsync() {
            return base.Channel.GetAgentsAsync();
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="urn:distcl", ConfigurationName="RemoteCompilerService.ICompileCoordinator")]
    public interface ICompileCoordinator {
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompileCoordinator/GetDescription", ReplyAction="urn:distcl/ICompileCoordinator/GetDescriptionResponse")]
        DistCL.RemoteCompilerService.Agent GetDescription();
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompileCoordinator/GetDescription", ReplyAction="urn:distcl/ICompileCoordinator/GetDescriptionResponse")]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent> GetDescriptionAsync();
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="urn:distcl/ICompileCoordinator/RegisterAgent")]
        void RegisterAgent(DistCL.RemoteCompilerService.Agent request);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="urn:distcl/ICompileCoordinator/RegisterAgent")]
        System.Threading.Tasks.Task RegisterAgentAsync(DistCL.RemoteCompilerService.Agent request);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface ICompileCoordinatorChannel : DistCL.RemoteCompilerService.ICompileCoordinator, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class CompileCoordinatorClient : System.ServiceModel.ClientBase<DistCL.RemoteCompilerService.ICompileCoordinator>, DistCL.RemoteCompilerService.ICompileCoordinator {
        
        public CompileCoordinatorClient() {
        }
        
        public CompileCoordinatorClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public CompileCoordinatorClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public CompileCoordinatorClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public CompileCoordinatorClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        public DistCL.RemoteCompilerService.Agent GetDescription() {
            return base.Channel.GetDescription();
        }
        
        public System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent> GetDescriptionAsync() {
            return base.Channel.GetDescriptionAsync();
        }
        
        public void RegisterAgent(DistCL.RemoteCompilerService.Agent request) {
            base.Channel.RegisterAgent(request);
        }
        
        public System.Threading.Tasks.Task RegisterAgentAsync(DistCL.RemoteCompilerService.Agent request) {
            return base.Channel.RegisterAgentAsync(request);
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="urn:distcl", ConfigurationName="RemoteCompilerService.ICompileManager")]
    public interface ICompileManager {
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompiler/IsReady", ReplyAction="urn:distcl/ICompiler/IsReadyResponse")]
        bool IsReady();
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompiler/IsReady", ReplyAction="urn:distcl/ICompiler/IsReadyResponse")]
        System.Threading.Tasks.Task<bool> IsReadyAsync();
        
        // CODEGEN: Generating message contract since the wrapper namespace (urn:distcl:compiler:messages) of message CompileInput does not match the default value (urn:distcl)
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompiler/Compile", ReplyAction="urn:distcl/ICompiler/CompileResponse")]
        DistCL.RemoteCompilerService.CompileOutput Compile(DistCL.RemoteCompilerService.CompileInput request);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompiler/Compile", ReplyAction="urn:distcl/ICompiler/CompileResponse")]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.CompileOutput> CompileAsync(DistCL.RemoteCompilerService.CompileInput request);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompileCoordinator/GetDescription", ReplyAction="urn:distcl/ICompileCoordinator/GetDescriptionResponse")]
        DistCL.RemoteCompilerService.Agent GetDescription();
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/ICompileCoordinator/GetDescription", ReplyAction="urn:distcl/ICompileCoordinator/GetDescriptionResponse")]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent> GetDescriptionAsync();
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="urn:distcl/ICompileCoordinator/RegisterAgent")]
        void RegisterAgent(DistCL.RemoteCompilerService.Agent request);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="urn:distcl/ICompileCoordinator/RegisterAgent")]
        System.Threading.Tasks.Task RegisterAgentAsync(DistCL.RemoteCompilerService.Agent request);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/IAgentPool/GetAgents", ReplyAction="urn:distcl/IAgentPool/GetAgentsResponse")]
        DistCL.RemoteCompilerService.Agent[] GetAgents();
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:distcl/IAgentPool/GetAgents", ReplyAction="urn:distcl/IAgentPool/GetAgentsResponse")]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent[]> GetAgentsAsync();
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface ICompileManagerChannel : DistCL.RemoteCompilerService.ICompileManager, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class CompileManagerClient : System.ServiceModel.ClientBase<DistCL.RemoteCompilerService.ICompileManager>, DistCL.RemoteCompilerService.ICompileManager {
        
        public CompileManagerClient() {
        }
        
        public CompileManagerClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public CompileManagerClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public CompileManagerClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public CompileManagerClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        public bool IsReady() {
            return base.Channel.IsReady();
        }
        
        public System.Threading.Tasks.Task<bool> IsReadyAsync() {
            return base.Channel.IsReadyAsync();
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        DistCL.RemoteCompilerService.CompileOutput DistCL.RemoteCompilerService.ICompileManager.Compile(DistCL.RemoteCompilerService.CompileInput request) {
            return base.Channel.Compile(request);
        }
        
        public DistCL.RemoteCompilerService.CompileStatus Compile(string Arguments, long SrcLength, string SrcName, System.IO.Stream Src, out System.IO.Stream ResultData) {
            DistCL.RemoteCompilerService.CompileInput inValue = new DistCL.RemoteCompilerService.CompileInput();
            inValue.Arguments = Arguments;
            inValue.SrcLength = SrcLength;
            inValue.SrcName = SrcName;
            inValue.Src = Src;
            DistCL.RemoteCompilerService.CompileOutput retVal = ((DistCL.RemoteCompilerService.ICompileManager)(this)).Compile(inValue);
            ResultData = retVal.ResultData;
            return retVal.Status;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<DistCL.RemoteCompilerService.CompileOutput> DistCL.RemoteCompilerService.ICompileManager.CompileAsync(DistCL.RemoteCompilerService.CompileInput request) {
            return base.Channel.CompileAsync(request);
        }
        
        public System.Threading.Tasks.Task<DistCL.RemoteCompilerService.CompileOutput> CompileAsync(string Arguments, long SrcLength, string SrcName, System.IO.Stream Src) {
            DistCL.RemoteCompilerService.CompileInput inValue = new DistCL.RemoteCompilerService.CompileInput();
            inValue.Arguments = Arguments;
            inValue.SrcLength = SrcLength;
            inValue.SrcName = SrcName;
            inValue.Src = Src;
            return ((DistCL.RemoteCompilerService.ICompileManager)(this)).CompileAsync(inValue);
        }
        
        public DistCL.RemoteCompilerService.Agent GetDescription() {
            return base.Channel.GetDescription();
        }
        
        public System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent> GetDescriptionAsync() {
            return base.Channel.GetDescriptionAsync();
        }
        
        public void RegisterAgent(DistCL.RemoteCompilerService.Agent request) {
            base.Channel.RegisterAgent(request);
        }
        
        public System.Threading.Tasks.Task RegisterAgentAsync(DistCL.RemoteCompilerService.Agent request) {
            return base.Channel.RegisterAgentAsync(request);
        }
        
        public DistCL.RemoteCompilerService.Agent[] GetAgents() {
            return base.Channel.GetAgents();
        }
        
        public System.Threading.Tasks.Task<DistCL.RemoteCompilerService.Agent[]> GetAgentsAsync() {
            return base.Channel.GetAgentsAsync();
        }
    }
}
