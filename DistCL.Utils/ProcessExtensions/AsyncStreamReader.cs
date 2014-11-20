using System;
using System.Collections;
using System.IO;
using System.Runtime;
using System.Text;
using System.Threading;

namespace DistCL.Utils.ProcessExtensions
{
	internal delegate void UserCallBack(string data);

	internal class AsyncStreamReader : IDisposable
	{
		internal const int DefaultBufferSize = 1024;
		private Stream stream;
		private Encoding encoding;
		private Decoder decoder;
		private byte[] byteBuffer;
		private char[] charBuffer;
		private int _maxCharsPerBuffer;
		private ProcessEx process;
		private UserCallBack userCallBack;
		private bool cancelOperation;
		private ManualResetEvent eofEvent;
		private Queue messageQueue;
		private StringBuilder sb;
		private bool bLastCarriageReturn;
		private const int MinBufferSize = 128;
		public virtual Encoding CurrentEncoding
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.encoding;
			}
		}
		public virtual Stream BaseStream
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get
			{
				return this.stream;
			}
		}
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		internal AsyncStreamReader(ProcessEx process, Stream stream, UserCallBack callback, Encoding encoding)
			: this(process, stream, callback, encoding, 1024)
		{
		}
		internal AsyncStreamReader(ProcessEx process, Stream stream, UserCallBack callback, Encoding encoding, int bufferSize)
		{
			this.Init(process, stream, callback, encoding, bufferSize);
			this.messageQueue = new Queue();
		}
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public virtual void Close()
		{
			this.Dispose(true);
		}
		void IDisposable.Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
			if (disposing && this.stream != null)
			{
				this.stream.Close();
			}
			if (this.stream != null)
			{
				this.stream = null;
				this.encoding = null;
				this.decoder = null;
				this.byteBuffer = null;
				this.charBuffer = null;
			}
			if (this.eofEvent != null)
			{
				this.eofEvent.Close();
				this.eofEvent = null;
			}
		}
		internal void BeginReadLine()
		{
			if (this.cancelOperation)
			{
				this.cancelOperation = false;
			}
			if (this.sb == null)
			{
				this.sb = new StringBuilder(1024);
				this.stream.BeginRead(this.byteBuffer, 0, this.byteBuffer.Length, new AsyncCallback(this.ReadBuffer), null);
				return;
			}
			this.FlushMessageQueue();
		}
		internal void CancelOperation()
		{
			this.cancelOperation = true;
		}
		internal void WaitUtilEOF()
		{
			if (this.eofEvent != null)
			{
				this.eofEvent.WaitOne();
				this.eofEvent.Close();
				this.eofEvent = null;
			}
		}
		private void Init(ProcessEx process, Stream stream, UserCallBack callback, Encoding encoding, int bufferSize)
		{
			this.process = process;
			this.stream = stream;
			this.encoding = encoding;
			this.userCallBack = callback;
			this.decoder = encoding.GetDecoder();
			if (bufferSize < 128)
			{
				bufferSize = 128;
			}
			this.byteBuffer = new byte[bufferSize];
			this._maxCharsPerBuffer = encoding.GetMaxCharCount(bufferSize);
			this.charBuffer = new char[this._maxCharsPerBuffer];
			this.cancelOperation = false;
			this.eofEvent = new ManualResetEvent(false);
			this.sb = null;
			this.bLastCarriageReturn = false;
		}
		private void ReadBuffer(IAsyncResult ar)
		{
			int num;
			try
			{
				num = this.stream.EndRead(ar);
			}
			catch (IOException)
			{
				num = 0;
			}
			catch (OperationCanceledException)
			{
				num = 0;
			}
			if (num == 0)
			{
				lock (this.messageQueue)
				{
					if (this.sb.Length != 0)
					{
						this.messageQueue.Enqueue(this.sb.ToString());
						this.sb.Length = 0;
					}
					this.messageQueue.Enqueue(null);
				}
				try
				{
					this.FlushMessageQueue();
					return;
				}
				finally
				{
					this.eofEvent.Set();
				}
			}
			int chars = this.decoder.GetChars(this.byteBuffer, 0, num, this.charBuffer, 0);
			this.sb.Append(this.charBuffer, 0, chars);
			this.GetLinesFromStringBuilder();
			this.stream.BeginRead(this.byteBuffer, 0, this.byteBuffer.Length, new AsyncCallback(this.ReadBuffer), null);
		}
		private void GetLinesFromStringBuilder()
		{
			int i = 0;
			int num = 0;
			int length = this.sb.Length;
			if (this.bLastCarriageReturn && length > 0 && this.sb[0] == '\n')
			{
				i = 1;
				num = 1;
				this.bLastCarriageReturn = false;
			}
			while (i < length)
			{
				char c = this.sb[i];
				if (c == '\r' || c == '\n')
				{
					string obj = this.sb.ToString(num, i - num);
					num = i + 1;
					if (c == '\r' && num < length && this.sb[num] == '\n')
					{
						num++;
						i++;
					}
					lock (this.messageQueue)
					{
						this.messageQueue.Enqueue(obj);
					}
				}
				i++;
			}
			if (this.sb[length - 1] == '\r')
			{
				this.bLastCarriageReturn = true;
			}
			if (num < length)
			{
				this.sb.Remove(0, num);
			}
			else
			{
				this.sb.Length = 0;
			}
			this.FlushMessageQueue();
		}
		private void FlushMessageQueue()
		{
			while (this.messageQueue.Count > 0)
			{
				lock (this.messageQueue)
				{
					if (this.messageQueue.Count > 0)
					{
						string data = (string)this.messageQueue.Dequeue();
						if (!this.cancelOperation)
						{
							this.userCallBack(data);
						}
					}
					continue;
				}
			}
		}
	}
}
