using Rebex.Net;
using System.Net.Sockets;

namespace VncDeviceProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            // connect to cloud service 
            var webSocketTunnelClient = new WebSocketClient();
            //webSocketTunnelClient.Connect("ws://localhost:5000/ws");

            webSocketTunnelClient.Connect("ws://40.127.108.43/ws");

            // vnc client
            var vncClient = new TcpClient("10.101.100.244", 5900);
            var vncClientStream = vncClient.GetStream();

         

            // pipe em up 
            var p = new Pipe(vncClientStream, webSocketTunnelClient);
            p.TunnelAsync().Join();
        }
    }
}
