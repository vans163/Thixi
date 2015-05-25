using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Thixi
{
	public class BufferManager
	{
		List<byte[]> Buffers = new List<byte[]>();

		byte BufferBlockId = 0;
		int BufferBlockIndex = 0;
		int BufferBlockSize = 0;
		int saeaSize = 2048;
		object bufferLock = new object();

		public BufferManager(int saeaSize, int blockSize)
		{
			byte[] initBlock = new byte[blockSize];
			Buffers.Add(initBlock);

			this.BufferBlockSize = blockSize;
			this.saeaSize = saeaSize;
		}

		internal bool SetBuffer(SocketAsyncEventArgs args)
		{
			lock (bufferLock)
			{
				//Need to allocate a new buffer?
				if (BufferBlockIndex + saeaSize > BufferBlockSize)
				{
					byte[] newBlock = new byte[BufferBlockSize];
					Buffers.Add(newBlock);
					BufferBlockId++;
					BufferBlockIndex = 0;
				}

				args.SetBuffer(Buffers[BufferBlockId], BufferBlockIndex, saeaSize);
				BufferBlockIndex += saeaSize;
			}
			return true;
		}
	}
}