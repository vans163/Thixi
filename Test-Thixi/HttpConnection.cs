using System;

using System.Net.Sockets;
using Thixi;

namespace TestThixi
{
	public class HttpConnection : Thixi.Connection
	{
		public HttpConnection (Thixi.Server serv, Socket socket)
			: base (serv, socket)
		{
		}

		public override void OnConnect ()
		{
			Console.WriteLine ("Something connected");
		}

		static byte[] httpResp = null;
		public static byte[] HTTPResp
		{
			get
			{
				if (httpResp == null)
				{
					string resp = 
						"<!DOCTYPE html>"+
						"<html>"+
						"<body>"+
						"<h1>My First Heading</h1>"+
						"<p>My first paragraph.</p>"+
						"</body>"+
						"</html>";
					
					string respHeader = string.Format("HTTP/1.0 200 OK\r\n" +
						"Content-Type: text/html; charset=utf-8\r\n" +
										"Content-Length: {0}\r\n\r\n", resp.Length);
					

					string fullResp = respHeader + resp;
					byte[] bytes = System.Text.Encoding.UTF8.GetBytes (fullResp);
					httpResp = bytes;
				}
				return httpResp;
			}
		}

		byte[] buffer = new byte[4096];
		public override void OnData (byte[] buf, int offset, int bytestrans)
		{
			Console.WriteLine (string.Format ("Received {0} bytes", bytestrans));
			Array.Copy (buf, offset, buffer, 0, bytestrans);

			Console.WriteLine ("Sending sample HTTP Response");
			Send (HTTPResp);
			Disconnect ();
		}
	}
}

