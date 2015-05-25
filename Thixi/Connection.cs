using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;

namespace Thixi
{
	public class Connection
	{
		internal int DisconnectedFlag = 0;
		internal Socket Socket;
		Server serverRef;

		public long conId;
		static long idpool = 0;

		internal IPAddress endPointIP;

		public Connection(Server server, Socket acceptSocket)
		{
			conId = System.Threading.Interlocked.Increment(ref Connection.idpool);
			Socket = acceptSocket;
			serverRef = server;

			IPEndPoint ipEndPoint = acceptSocket.RemoteEndPoint as IPEndPoint;
			this.endPointIP = ipEndPoint.Address;
		}

		public virtual void RefDispose()
		{
			serverRef = null;
			endPointIP = null;
		}

		public virtual void OnData(byte[] buf, int offset, int bytestrans)
		{
		}

		public virtual void OnConnect()
		{
		}

		public virtual void OnDisconnect()
		{
			//Atomic cleanup here as Disconnect can be called multiple times
			if (System.Threading.Interlocked.CompareExchange(ref DisconnectedFlag, 2, 1) == 1)
			{
				RefDispose();
			}
		}

		public void Send(byte[] buffer)
		{
			var servRef = serverRef;
			if (servRef != null)
				serverRef.Send(this, buffer);
		}

		public void Disconnect()
		{
			if (System.Threading.Interlocked.CompareExchange(ref DisconnectedFlag, 1, 0) == 0)
				serverRef.DisconnectClient(this);
		}
	}
}