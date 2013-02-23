using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DistCL.Utils.Streams
{
    internal class MultiStream : Stream
    {
        private readonly List<Stream> _streams;
        long _position;

        public MultiStream(params Stream[] streams) : this((IEnumerable<Stream>)streams){}

        public MultiStream(IEnumerable<Stream> streams)
        {
            _streams = new List<Stream>(streams);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
	        if (count == 0)
		        return 0;

            var localPosition = 0L;
            var result = 0;
            var bufPos = offset;

            foreach (var stream in _streams)
            {
                if (Position < localPosition + stream.Length)
                {
                    var streamRead = 0L;

					stream.Position = Position - localPosition;
	                localPosition += stream.Position;

                    while (count > 0 && stream.Position < stream.Length)
                    {
                        int bytesRead = stream.Read(buffer, bufPos, count);
                        
                        result += bytesRead;
                        streamRead += bytesRead;
                        
                        bufPos += bytesRead;
                        _position += bytesRead;

                        count -= bytesRead;
                    }

                    localPosition += streamRead;
                }
                else
                {
                    localPosition += stream.Length;
                }

	            if (count == 0)
		            break;
            }

            return result;
        }

        public override void Close()
        {
            foreach (var stream in _streams)
            {
                stream.Close();
            }
        }

        public override bool CanRead
        {
            get { return true; }
        }
        public override bool CanSeek
        {
            get { return true; }
        }
        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get
            {
                return _streams.Sum(stream => stream.Length);
            }
        }
        public override long Position
        {
            get { return _position; }
            set
            {
                if (_position != value)
                {
                    Seek(value, SeekOrigin.Begin);
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var len = Length;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    _position = len - offset;
                    break;
            }

            if (_position > len)
            {
                _position = len;
            }
            else if (_position < 0)
            {
                _position = 0;
            }

            return _position;
        }

        public override void SetLength(long value)
        {
            ProxyStream.LogError();
            throw new InvalidOperationException();
        }
        public override void Flush()
        {
            ProxyStream.LogError();
            throw new InvalidOperationException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            ProxyStream.LogError();
            throw new InvalidOperationException();
        }
    }
}
