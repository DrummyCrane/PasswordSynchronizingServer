using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TempMessageSender
{
    class Program
    {
        static void Main(string[] args)
        {
            Sender sender = new Sender();
            sender.SendMsg();
        }
    }

    class Sender
    {
        private const string server_ip = "127.0.0.1";
        private const int server_port = 10024;

        private Socket socket;

        public Sender()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            socket.Connect(new IPEndPoint(IPAddress.Parse(server_ip), server_port));
        }

        public void SendMsg()
        {
            byte[] byteMsg = Encoding.Default.GetBytes("msg");
            socket.BeginSend(byteMsg, 0, byteMsg.Length, SocketFlags.None, new AsyncCallback(Callback_SendMsg), socket);
            Thread.Sleep(500);
        }

        private void Callback_SendMsg(IAsyncResult ar)
        {
            socket = (Socket)ar.AsyncState;
            int size = socket.EndSend(ar);
        }
    }
}
