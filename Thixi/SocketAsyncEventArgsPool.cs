using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Thixi
{
	public class SocketAsyncEventArgsPool
	{
		ConcurrentStack<SocketAsyncEventArgs> Conc_Pool = new ConcurrentStack<SocketAsyncEventArgs> ();
		Server serverRef;

		public SocketAsyncEventArgsPool (Server serverRef)
		{
			this.serverRef = serverRef;
		}

		internal SocketAsyncEventArgs Pop (bool setBuffer = true)
		{
			SocketAsyncEventArgs outv;
			if (!Conc_Pool.TryPop(out outv))
				return serverRef.CreateSAEA(setBuffer);
			return outv;
		}

		internal void Push (SocketAsyncEventArgs arg)
		{
			Conc_Pool.Push(arg);
		}
	}
}