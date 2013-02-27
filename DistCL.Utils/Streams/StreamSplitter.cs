using System;
using System.IO;

namespace DistCL.Utils.Streams
{
	internal class StreamSplitter
	{
		private readonly Stream _stream;

		public StreamSplitter(Stream stream)
		{
			_stream = stream;
		}

		public Stream GetStream(long length)
		{
			return new StreamSplitterPart(_stream.Position, length, _stream);
		}

		private class StreamSplitterPart : Stream
		{
			private readonly long _startPosition;
			private readonly long _length;
			private readonly Stream _stream;

			public StreamSplitterPart(long startPosition, long length, Stream stream)
			{
				_startPosition = startPosition;
				_length = length;
				_stream = stream;
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				var bytesRead = 0;

				var canRead = (int) (Length - Position);

				if (canRead > 0)
				{
					bytesRead = _stream.Read(buffer, offset, Math.Min(count, canRead));
				}

				return bytesRead;
			}

			public override long Length
			{
				get { return _length; }
			}

			public override long Position
			{
				get { return _stream.Position - _startPosition; }
				set { Seek(value, SeekOrigin.Begin); }
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

			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new InvalidOperationException();
			}

			public override void SetLength(long value)
			{
				throw new InvalidOperationException();
			}

			public override void Flush()
			{
				throw new InvalidOperationException();
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				throw new InvalidOperationException();
			}
		}
	}
}