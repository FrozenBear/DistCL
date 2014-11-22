using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using DistCL.Utils;
using DistCL.Utils.Streams;

namespace DistCL
{
	[ServiceContract(Namespace = GeneralSettings.Namespace)]
	public interface ICompiler
	{
		[OperationContract]
		bool IsReady();

		[OperationContract]
		[FaultContract(typeof (CompilerNotFoundFaultContract), Namespace = GeneralSettings.CompilerMessageNamespace)]
		CompileOutput Compile(CompileInput input);
	}

	[MessageContract(WrapperNamespace = GeneralSettings.CompilerMessageNamespace)]
	public class CompileInput : IStreamedMessage
	{
		public CompileInput()
		{
		}

		public CompileInput(IStreamedMessage message)
		{
			SrcLength = message.StreamLength;
			Src = new ProxyStream(message);
		}

		[MessageHeader]
		public string CompilationToken { get; set; }

		[MessageHeader]
		public string CompilerVersion { get; set; }

		[MessageHeader]
		public string Arguments { get; set; }

		[MessageBodyMember]
		public Stream Src { get; set; }

		[MessageHeader]
		public string SrcName { get; set; }

		[MessageHeader]
		public long SrcLength { get; set; }

		#region IStreamedMessage

		Stream IStreamedMessage.StreamBody
		{
			get { return Src; }
		}

		public long StreamLength
		{
			get { return SrcLength; }
		}

		#endregion
	}

	[MessageContract(WrapperNamespace = GeneralSettings.CompilerMessageNamespace)]
	public class CompileOutput : IDisposable
	{
		private readonly CompileResult _result;
		private Stream _resultData;

		public CompileOutput(string compilationToken, string requiredFileName)
		{
			_result = new SourceFileRequest(compilationToken, requiredFileName);
		}

		public CompileOutput(CompileResult result, Stream resultData)
		{
			_result = result;
			_resultData = resultData;
		}

		public CompileOutput(
			int exitCode,
			IDictionary<CompileArtifactDescription, Stream> streams,
			IEnumerable<CompileArtifactDescription> artifacts)
		{
			CompileArtifactCookie[] cookies;
			_resultData = CompileResultHelper.Pack(streams, out cookies);

			// TODO remove redundant collections copy
			if (artifacts != null)
			{
				var list = new List<CompileArtifactCookie>(cookies);
				list.AddRange(artifacts.Select(artifact => new CompileArtifactCookie(artifact, -1)));
				cookies = list.ToArray();
			}

			_result = new FinishedCompileResult(exitCode, cookies);
		}

		[MessageHeader]
		public CompileResult Result
		{
			get { return _result; }
		}

		[MessageBodyMember]
		public Stream ResultData
		{
			get { return _resultData; }
		}

		public void Dispose()
		{
			if (_resultData != null)
			{
				_resultData.Dispose();
				_resultData = null;
			}
		}
	}

	[DataContract(Namespace = GeneralSettings.CompilerMessageNamespace)]
	[KnownType(typeof(FinishedCompileResult))]
	[KnownType(typeof(SourceFileRequest))]
	public abstract class CompileResult
	{
		protected CompileResult(bool finished)
		{
			Finished = finished;
		}

		[DataMember]
		public bool Finished { get; private set; }
	}

	[DataContract(Namespace = GeneralSettings.CompilerMessageNamespace)]
	public class FinishedCompileResult : CompileResult
	{
		public FinishedCompileResult(int exitCode, CompileArtifactCookie[] cookies)
			: base(true)
		{
			ExitCode = exitCode;
			Cookies = cookies;
		}

		[DataMember]
		public int ExitCode { get; private set; }

		[DataMember]
		public CompileArtifactCookie[] Cookies { get; private set; }
	}

	[DataContract(Namespace = GeneralSettings.CompilerMessageNamespace)]
	public class SourceFileRequest : CompileResult
	{
		public SourceFileRequest(string compilationToken, string fileName)
			: base(false)
		{
			CompilationToken = compilationToken;
			FileName = fileName;
		}

		[DataMember]
		public string FileName { get; private set; }

		[DataMember]
		public string CompilationToken { get; private set; }
	}
}
