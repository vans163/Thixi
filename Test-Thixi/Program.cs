using System;

using System.Net.Sockets;
using System.Net;
using Thixi;

namespace TestThixi
{
	class MainClass
	{
		static Thixi.Connection OnNewUserConnection(Thixi.Server serv, Socket socket)
		{
			var newconn = new HttpConnection(serv, socket);
			return newconn;
		}

		public static void Main (string[] args)
		{
			ushort port = 8010;
			Console.WriteLine("Creating 2000 initial client server with receive buffer size of 4096");
			Console.WriteLine("Creating 20 Accept Args");
			IPEndPoint server_ep = new IPEndPoint(IPAddress.Any, port);
			var userserver = new Thixi.Server(2000, 4096, 20);

			Console.WriteLine("Setting delegated OnNewConnection");
			userserver.OnNewConnection = OnNewUserConnection;

			Console.WriteLine("Starting TCP Server with NoDelay option set on port "+port);
			userserver.Start(server_ep, SocketOptionName.NoDelay);

			Console.ReadLine ();
		}
	}
}
