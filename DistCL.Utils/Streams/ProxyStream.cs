using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace DistCL.Utils.Streams
{
	public interface IStreamedMessage
	{
		long StreamLength { get; }
		Stream StreamBody { get; }
	}

	public class ProxyStream : ReadOnlyStream
	{
		private readonly IStreamedMessage _streamedMessage;

		public ProxyStream(IStreamedMessage streamedMessage)
		{
			Contract.Requires<ArgumentNullException>(streamedMessage != null);

			_streamedMessage = streamedMessage;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return _streamedMessage.StreamBody.Read(buffer, offset, count);
		}

		public override long Length
		{
			get { return _streamedMessage.StreamLength; }
		}

		public override long Position
		{
			get { return _streamedMessage.StreamBody.Position; }
			set
			{
				if (_streamedMessage.StreamBody.Position != value)
				{
					Seek(value, SeekOrigin.Begin);
				}
			}
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			LogError();
			throw new InvalidOperationException();
		}
	}
}