using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DistCL.Utils.Streams
{
	public abstract class ReadOnlyStream : Stream
	{
		private Logger _logger;

		public Logger Logger
		{
			get { return LazyInitializer.EnsureInitialized(ref _logger, () => new Logger(GetType().Name.ToUpperInvariant())); }
		}

		protected void LogError()
		{
			Logger.Warn(new StackTrace().ToString());
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

		public override bool CanRead
		{
			get { return true; }
		}
		
		public override bool CanWrite
		{
			get { return false; }
		}
	}
}
