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
		CompileOutput Compile(CompileInput input);
	}

	[DataContract(Namespace = GeneralSettings.CompilerMessageNamespace)]
	public class CompileStatus
	{
		public CompileStatus(bool success, int exitCode, CompileArtifactCookie[] cookies)
		{
			ExitCode = exitCode;
			Success = success;
			Cookies = cookies;
		}

		[DataMember]
		public bool Success { get; private set; }

		[DataMember]
		public int ExitCode { get; private set; }

		[DataMember]
		public CompileArtifactCookie[] Cookies { get; private set; }
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
		private readonly CompileStatus _status;
		private Stream _resultData;

		public CompileOutput(CompileStatus status, Stream resultData)
		{
			_status = status;
			_resultData = resultData;
		}

		public CompileOutput(
			bool success,
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

			_status = new CompileStatus(success, exitCode, cookies);
		}

		[MessageHeader]
		public CompileStatus Status
		{
			get { return _status; }
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
}
