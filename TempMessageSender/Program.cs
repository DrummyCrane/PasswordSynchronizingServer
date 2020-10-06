using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace TempMessageSender
{
    class MessageInfo
    {
        public const int BUFFER_SIZE = 1024;
        public const int COMMAND_SIZE = 4;
        public const int LENGTH_SIZE = 4;
        public const int HEADER_SIZE = COMMAND_SIZE + LENGTH_SIZE;
        public const int DATA_SIZE = BUFFER_SIZE - HEADER_SIZE;

        public const int CREDENTIAL_SIZE = 8;

        public const int SERVER_CREATED_NEW_HOST = 1001;
        public const int SERVER_CREATED_NEW_GUEST = 1002;

        public const int SERVER_REQUEST_DATA = 2001;
        public const int SERVER_RECEIVED_FILE_SIZE = 2002;
        public const int SERVER_RECEIVED_PARTIAL_DATA = 2003;
        public const int SERVER_RECEIVED_ALL_DATA = 2004;

        public const int SERVER_SEND_FILE_SIZE = 3001;
        public const int SERVER_SEND_PARTIAL_DATA = 3002;
        public const int SERVER_SEND_ALL_DATA = 3003;

        public const int SERVER_PERMISSION_GRANTED = 6001;
        public const int SERVER_PERMISSION_DENIED = 6002;

        public const int CLIENT_CREATE_NEW_HOST = 0001;
        public const int CLIENT_CREATE_NEW_GUEST = 0002;

        public const int CLIENT_SEND_FILE_SIZE = 4001;
        public const int CLIENT_SEND_PARTIAL_DATA = 4002;
        public const int CLIENT_SEND_ALL_DATA = 4003;

        public const int CLIENT_REQUEST_DATA = 5001;
        public const int CLIENT_RECEIVED_FILE_SIZE = 5002;
        public const int CLIENT_RECEIVED_PARTIAL_DATA = 5003;
        public const int CLIENT_RECEIVED_ALL_DATA = 5004;

        public const int CLIENT_PERMISSION_GRANTED = 7001;
        public const int CLIENT_PERMISSION_DENIED = 7002;
    }
    class Program
    {
        static void Main(string[] args)
        {
            Sender host = new Sender();
            Sender guest = new Sender();
            host.RequestServerToCreateNewHost("123456789");
            guest.RequestServerToCreateNewGuest("123456789");

            host.SendFileSize();
            Thread.Sleep(1000000);
        }
    }

    class Sender
    {
        private const string server_ip = "127.0.0.1";
        private const int server_port = 10024;

        private Socket server;
        private byte[] buffer;
        private byte[] data;
        private int currentFileSize = 0;

        public Sender()
        {
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            server.Connect(new IPEndPoint(IPAddress.Parse(server_ip), server_port));
            buffer = new byte[MessageInfo.BUFFER_SIZE];
        }

        public void RequestServerToCreateNewHost(string key)
        {
            string msg = string.Format("{0:D4}{1:D4}", MessageInfo.CLIENT_CREATE_NEW_HOST, key);
            SendMsg(msg);
        }

        public void RequestServerToCreateNewGuest(string key)
        {
            string msg = string.Format("{0:D4}{1:D4}", MessageInfo.CLIENT_CREATE_NEW_GUEST, key);
            SendMsg(msg);
        }

        private void Receive()
        {
            server.BeginReceive(buffer, 0, MessageInfo.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReceiveCallback), server);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            server.EndReceive(ar);
            string msg = Encoding.Default.GetString(buffer, 0, buffer.Length);
            
            int command = int.Parse(msg.Substring(0, MessageInfo.COMMAND_SIZE));
            switch(command)
            {
                case MessageInfo.SERVER_CREATED_NEW_HOST:
                    Console.WriteLine("New Host Created");
                    HandleNewHost();
                    break;
                case MessageInfo.SERVER_CREATED_NEW_GUEST:
                    Console.WriteLine("New Guest Created");
                    HandleNewGuest();
                    break;
                case MessageInfo.SERVER_REQUEST_DATA:
                    Console.WriteLine("Received data request");
                    AskPermission();
                    break;
                case MessageInfo.SERVER_RECEIVED_FILE_SIZE:
                case MessageInfo.SERVER_RECEIVED_PARTIAL_DATA:
                    Console.WriteLine("Data sent");
                    SendData();
                    break;
                case MessageInfo.SERVER_RECEIVED_ALL_DATA:
                    Console.WriteLine("All data sent");
                    break;
                case MessageInfo.SERVER_PERMISSION_GRANTED:
                    // Do not reach here
                    break;
                case MessageInfo.SERVER_PERMISSION_DENIED:
                    Console.WriteLine("Permission Denied");
                    HandlePermissionDenied();
                    break;
                case MessageInfo.SERVER_SEND_FILE_SIZE:
                    Console.WriteLine("Received file size");
                    PrepareReceiveData(msg);
                    break;
                case MessageInfo.SERVER_SEND_PARTIAL_DATA:
                case MessageInfo.SERVER_SEND_ALL_DATA:
                    Console.WriteLine("Received data");
                    ReceiveData(msg);
                    break;
            }
        }

        private void HandleNewHost()
        {
            // Todo
        }

        private void HandleNewGuest()
        {
            // Todo
            Receive();
        }

        private void AskPermission()
        {
            // Todo;
        }

        public void SendPermissionDenied()
        {
            SendMsg(string.Format("{0:D4}", MessageInfo.CLIENT_PERMISSION_DENIED));
        }

        public void SendFileSize()
        {
            currentFileSize = 0;
            int fileLength = Readfile();
            int msgDataLength = Convert.ToString(data.Length).Length;
            SendMsg(string.Format("{0:D4}{1:D4}{2}", MessageInfo.CLIENT_SEND_FILE_SIZE, msgDataLength, fileLength));
        }

        private int Readfile()
        {
            string fileName = "Generator.dat";
            FileInfo fi = new FileInfo(fileName);
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);

            data = new byte[fi.Length];
            br.Read(data, 0, data.Length);
            br.Close();
            fs.Close();

            return data.Length;
        }

        private void SendData()
        {
            int msgCommand, msgDataSize, msgSize;
            byte[] msg;

            if ((data.Length - currentFileSize) > MessageInfo.DATA_SIZE)
            {
                msgCommand = MessageInfo.CLIENT_SEND_PARTIAL_DATA;
                msgDataSize = MessageInfo.DATA_SIZE;
                msgSize = MessageInfo.BUFFER_SIZE;
            }
            else
            {
                msgCommand = MessageInfo.CLIENT_SEND_ALL_DATA;
                msgDataSize = data.Length - currentFileSize;
                msgSize = MessageInfo.HEADER_SIZE + msgDataSize;
            }

            byte[] msgHeader = Encoding.Default.GetBytes(string.Format("{0:D4}{1:D4}", msgCommand, msgDataSize));

            msg = new byte[msgSize];
            Buffer.BlockCopy(msgHeader, 0, msg, 0, MessageInfo.HEADER_SIZE);
            Buffer.BlockCopy(data, currentFileSize, msg, MessageInfo.HEADER_SIZE, msgDataSize);

            currentFileSize += msgDataSize;

            SendMsg(msg);
        }

        private void HandlePermissionDenied()
        {
            // Todo
        }

        private void PrepareReceiveData(string msg)
        {
            int msgDataSize = int.Parse(msg.Substring(MessageInfo.COMMAND_SIZE, MessageInfo.LENGTH_SIZE)); ;
            int fileSize = int.Parse(msg.Substring(MessageInfo.HEADER_SIZE, msgDataSize));
            data = new byte[fileSize];
            currentFileSize = 0;

            SendMsg(string.Format("{0:D4}", MessageInfo.CLIENT_RECEIVED_FILE_SIZE));
        }

        private void ReceiveData(string msg)
        {
            int msgDataSize;
            byte[] receivedFile;

            msgDataSize = int.Parse(msg.Substring(MessageInfo.COMMAND_SIZE, MessageInfo.LENGTH_SIZE));
            receivedFile = Encoding.Default.GetBytes(msg.Substring(MessageInfo.HEADER_SIZE, msgDataSize));

            Buffer.BlockCopy(receivedFile, 0, data, currentFileSize, msgDataSize);
            currentFileSize += msgDataSize;

            if (data.Length == currentFileSize)
            {
                StoreFile();
                SendMsg(string.Format("{0:D4}", MessageInfo.CLIENT_RECEIVED_ALL_DATA));
            }
            else
            {
                SendMsg(string.Format("{0:D4}", MessageInfo.CLIENT_RECEIVED_PARTIAL_DATA));
            }
        }

        private void StoreFile()
        {
            string fileName = "GeneratorSaved.dat";
            FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(data, 0, data.Length);
            bw.Close();
            fs.Close();
        }

        private void SendMsg(string msg)
        {
            byte[] byteMsg = Encoding.Default.GetBytes(msg);
            server.BeginSend(byteMsg, 0, byteMsg.Length, SocketFlags.None, new AsyncCallback(Callback_SendMsg), server);
            Thread.Sleep(500);
        }

        private void SendMsg(byte[] msg)
        {
            server.BeginSend(msg, 0, msg.Length, SocketFlags.None, new AsyncCallback(Callback_SendMsg), server);
            Thread.Sleep(500);
        }

        private void Callback_SendMsg(IAsyncResult ar)
        {
            server.EndSend(ar);
            Receive();
        }
    }
}
