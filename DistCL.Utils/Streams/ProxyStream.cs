using System;
using System.Diagnostics;
using System.IO;

namespace DistCL.Utils.Streams
{
	public interface IStreamedMessage
	{
		long StreamLength { get; }
		Stream StreamBody { get; }
	}

	public class ProxyStream : Stream
	{
		private readonly IStreamedMessage _streamedMessage;

		public ProxyStream(IStreamedMessage streamedMessage)
		{
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

		public override bool CanRead
		{
			get { return true; }
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		internal static void LogError()
		{
			using (
				var writer =
					new StreamWriter(Path.Combine(Path.GetDirectoryName(typeof (ProxyStream).Assembly.Location), "error.log"), true))
			{
				writer.WriteLine(new StackTrace());
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			LogError();
			throw new InvalidOperationException();
		}

		public override void SetLength(long value)
		{
			LogError();
			throw new InvalidOperationException();
		}

		public override void Flush()
		{
			LogError();
			throw new InvalidOperationException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			LogError();
			throw new InvalidOperationException();
		}
	}
}