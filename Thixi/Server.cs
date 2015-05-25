using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

using System.Net.Sockets;
using System.Threading;
using System.Net;

namespace Thixi
{
	public class Server
	{
		int NAcceptArgs = 0;

		List<SocketAsyncEventArgs> acceptPool = new List<SocketAsyncEventArgs>();
		SocketAsyncEventArgsPool m_readPool, m_writePool;

		ConcurrentStack<Socket> m_reuseableSockets = new ConcurrentStack<Socket>();

		internal BufferManager bufferMan;
		Socket listenSocket;

		public ConcurrentDictionary<long, Connection> Connected = new ConcurrentDictionary<long, Connection>();
		static System.Diagnostics.Stopwatch _Timer;

		public static long Tick
		{
			get
			{
				if (_Timer == null)
				{
					_Timer = new System.Diagnostics.Stopwatch();
					_Timer.Start();
				}
				return _Timer.ElapsedMilliseconds;
			}
		}

		public virtual Server.newconnection OnNewConnection { get; set; }
		public delegate Connection newconnection(Server server,Socket socket);

		public Server(int initNumConnections, int saeaBuffSize, int NAcceptArgs)
		{
			this.NAcceptArgs = NAcceptArgs;
			bufferMan = new BufferManager(saeaBuffSize, saeaBuffSize * initNumConnections * 1); //Only alloc Reads
			m_readPool = new SocketAsyncEventArgsPool(this);
			m_writePool = new SocketAsyncEventArgsPool(this);
			this.Init(initNumConnections);
		}

		void Init(int initNumConnections)
		{
			for (int index = 0; index < NAcceptArgs; ++index)
			{
				SocketAsyncEventArgs args = new SocketAsyncEventArgs();
				args.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
				acceptPool.Add(args);
			}
			for (int index = 0; index < initNumConnections; ++index)
			{
				m_readPool.Push(CreateSAEA());
			}
			for (int index = 0; index < initNumConnections; ++index)
			{
				m_writePool.Push(CreateSAEA(false));
			}
		}

		internal SocketAsyncEventArgs CreateSAEA(bool setBuffer = true)
		{
			SocketAsyncEventArgs args = new SocketAsyncEventArgs();
			args.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
			if (setBuffer)
				bufferMan.SetBuffer(args);
			return args;
		}

		public void Start(IPEndPoint localEndPoint, params SocketOptionName[] options)
		{
			listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			//UDP
			//listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);


			foreach (SocketOptionName optionName in options)
				listenSocket.SetSocketOption(SocketOptionLevel.Socket, optionName, true);

			listenSocket.Bind(localEndPoint);
			listenSocket.Listen(128);

			foreach (var Asaea in acceptPool)
			{
				StartAccept(Asaea);
			}

			//Start timer
			var tick = Tick;
		}

		internal void IO_Completed(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
			case SocketAsyncOperation.Accept:
				ProcessAccept(e);
				break;
			case SocketAsyncOperation.Receive:
				ProcessReceive(e);
				break;
			case SocketAsyncOperation.Send:
				ProcessSend(e);
				break;
			case SocketAsyncOperation.Disconnect:
				ProcessDisconnect(e);
				break;
			default:
				throw new ArgumentException("The last operation completed on the socket was not an accept, disconnect, receive or send");
			}
		}

		void StartAccept(SocketAsyncEventArgs acceptEventArg)
		{
			Socket reuse;
			if (m_reuseableSockets.TryPop(out reuse))
				acceptEventArg.AcceptSocket = reuse;

			if (!listenSocket.AcceptAsync(acceptEventArg))
				ProcessAccept(acceptEventArg);
		}

		void ProcessAccept(SocketAsyncEventArgs e)
		{
			if (e.SocketError == SocketError.Success)
			{
				try
				{
					//Microsoft needs to fix this to not throw when null
					if (e.AcceptSocket.RemoteEndPoint == null)
						throw new Exception("RemoteEndPoint IS NULL ?");
				}
				catch (SocketException)
				{
					goto EndAccept;
				}

				var conn = this.OnNewConnection(this, e.AcceptSocket);
				conn.Socket = e.AcceptSocket;

				SocketAsyncEventArgs readEventArgs = m_readPool.Pop();
				readEventArgs.UserToken = conn;

				Connected[conn.conId] = conn;

				conn.OnConnect();

				if (!e.AcceptSocket.ReceiveAsync(readEventArgs))
					ProcessReceive(readEventArgs);
			}

			EndAccept:
			e.AcceptSocket = null;
			this.StartAccept(e);
		}

		void ProcessReceive(SocketAsyncEventArgs e)
		{
			Connection conn = e.UserToken as Connection;

			//TODO: Currently .NET does not handle half-open sockets
			//Shutting down Send on client makes server recieve SocketError.Success and 0 Transfered
			//Server should receive SocketError.Shutdown
			if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
			{
				conn.OnData(e.Buffer, e.Offset, e.BytesTransferred);

				//Deref to ensure atomicity
				var socket = conn.Socket;
				if (socket != null)
				{
					if (!socket.ReceiveAsync(e))
						ProcessReceive(e);
				}
			}
			else
			{ //Disconnected or socket error
				conn.Disconnect();
				e.UserToken = null;
				m_readPool.Push(e);
			}
		}

		internal void Send(Connection conn, byte[] buffer)
		{
			SocketAsyncEventArgs sendArg = m_writePool.Pop(false);
			sendArg.UserToken = conn;
			sendArg.SetBuffer(buffer, 0, buffer.Length);

			var socket = conn.Socket;
			if (socket != null)
			{
				if (!socket.SendAsync(sendArg))
					this.ProcessSend(sendArg);
			}
		}

		void ProcessSend(SocketAsyncEventArgs e)
		{
			Connection conn = e.UserToken as Connection;
			bool cleanup = true;

			if (e.SocketError == SocketError.Success)
			{
				//Dont check for BytesTransferred <= 0 because SocketError wont be success in that case
				if (e.BytesTransferred < e.Buffer.Length)
				{
					cleanup = false;
					e.SetBuffer(e.BytesTransferred, e.Buffer.Length - e.BytesTransferred);

					//Deref to ensure atomicity
					var socket = conn.Socket;
					if (socket != null)
					{
						if (!socket.SendAsync(e))
							ProcessSend(e);
					}
				}
			}
			else
			{
				conn.Disconnect();
			}

			if (cleanup)
			{
				e.UserToken = null;
				e.SetBuffer(null, 0, 0);
				m_writePool.Push(e);
			}
		}

		internal void DisconnectClient(Connection conn)
		{
			//Use the write pool why not
			var e = m_writePool.Pop(false);
			e.UserToken = conn;

			//Reuse socket, Windows handle count will be glad??
			e.DisconnectReuseSocket = true;

			try
			{
				conn.Socket.Shutdown(SocketShutdown.Both);
			}
			catch
			{
			}

			var socket = conn.Socket;
			if (socket != null)
			{
				if (!socket.DisconnectAsync(e))
					ProcessDisconnect(e);
			}

			Connection outv;
			if (!Connected.TryRemove(conn.conId, out outv))
				new Exception("Failed to remove a connection from dict");
		}

		internal void ProcessDisconnect(SocketAsyncEventArgs e)
		{
			Connection conno = (e.UserToken as Connection);

			m_reuseableSockets.Push(conno.Socket);

			conno.OnDisconnect();
			conno.Socket = null;

			//Dont forget to return our arg
			e.DisconnectReuseSocket = false;
			e.UserToken = null;
			m_writePool.Push(e);
		}
	}
}
