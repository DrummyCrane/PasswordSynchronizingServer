using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;
using System.Diagnostics;

namespace PasswordSynchronizingServer
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
    public class Server
    {
        private const int portNum = 10024;

        private Socket listener = null;

        private Hashtable linkTable = new Hashtable();

        public void Initiate()
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            listener.Bind(new IPEndPoint(IPAddress.Any, portNum));
            listener.Listen(100);
        }

        public async Task Listening()
        {

            while (true)
            {
                Socket client = listener.Accept();
                await Task.Run(() =>
                {
                    new Transfer(client, this);
                });
            }
        }

        public bool AddHostSocket(string key, Socket device)
        {
            if (linkTable.ContainsKey(key))
            {
                return false;
            }
            else
            {
                linkTable.Add(key, device);
                return true;
            }
        }

        public Socket GetHostSocket(string key)
        {
            if (linkTable.ContainsKey(key))
            {
                return (Socket)linkTable[key];
            }
            else
            {
                return null;
            }
        }

        public void RemoveHostSocket(Socket target)
        {
            string targetKey = "";
            foreach (DictionaryEntry linkEntry in linkTable)
            {
                if (linkEntry.Value == target)
                {
                    targetKey = (string)linkEntry.Key;
                    break;
                }
            }
            linkTable.Remove(targetKey);
        }
    }

    class Transfer
    {
        private Socket clientHost;
        private Socket clientGuest;
        private Server server;
        private byte[] buffer;
        private byte[] data;
        private int currentFileSize = 0;
        private bool listeningHost = false;

        public Transfer(Socket client, Server server)
        {
            clientGuest = client;
            this.server = server;
            buffer = new byte[MessageInfo.BUFFER_SIZE];
            Receive();
        }

        private void Receive()
        {
            if (listeningHost)
                clientHost.BeginReceive(buffer, 0, MessageInfo.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReceiveHostCallback), clientHost);
            else
                clientGuest.BeginReceive(buffer, 0, MessageInfo.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReceiveGuestCallback), clientGuest);
        }

        private void ReceiveGuestCallback(IAsyncResult ar)
        {
            try
            {
                clientGuest.EndReceive(ar);
                string msg = Encoding.Default.GetString(buffer, 0, buffer.Length);

                int command = int.Parse(msg.Substring(0, MessageInfo.COMMAND_SIZE));
                switch (command)
                {
                    case MessageInfo.CLIENT_CREATE_NEW_HOST:
                        Console.WriteLine("New Host Created");
                        CreateNewLink(true, msg);
                        break;
                    case MessageInfo.CLIENT_CREATE_NEW_GUEST:
                        Console.WriteLine("New Guest Created");
                        CreateNewLink(false, msg);
                        break;
                    case MessageInfo.CLIENT_REQUEST_DATA:
                        Console.WriteLine("Data request sent");
                        RequestPermission();
                        break;
                    case MessageInfo.CLIENT_RECEIVED_FILE_SIZE:
                    case MessageInfo.CLIENT_RECEIVED_PARTIAL_DATA:
                        Console.WriteLine("Data sent");
                        SendData();
                        break;
                    case MessageInfo.CLIENT_RECEIVED_ALL_DATA:
                        Console.WriteLine("All data sent");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Disconnect();
            }
        }

        private void ReceiveHostCallback(IAsyncResult ar)
        {
            try
            {
                clientHost.EndReceive(ar);
                string msg = Encoding.Default.GetString(buffer, 0, buffer.Length);
                int command = int.Parse(msg.Substring(0, MessageInfo.COMMAND_SIZE));

                switch (command)
                {
                    case MessageInfo.CLIENT_PERMISSION_GRANTED:
                        Console.WriteLine("Permission granted");
                        ReplyPermission(true);
                        break;
                    case MessageInfo.CLIENT_PERMISSION_DENIED:
                        Console.WriteLine("Permission denied");
                        ReplyPermission(false);
                        break;
                    case MessageInfo.CLIENT_SEND_FILE_SIZE:
                        Console.WriteLine("File size received");
                        PrepareReceiveData(msg);
                        break;
                    case MessageInfo.CLIENT_SEND_PARTIAL_DATA:
                    case MessageInfo.CLIENT_SEND_ALL_DATA:
                        Console.WriteLine("Data received");
                        ReceiveData(msg);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Disconnect();
            }
        }

        private void CreateNewLink(bool isHost, string msg)
        {
            string key = msg.Substring(MessageInfo.HEADER_SIZE, MessageInfo.CREDENTIAL_SIZE);
            Socket tempHost = server.GetHostSocket(key);

            if (isHost)
            {
                if (tempHost == null)
                {
                    server.AddHostSocket(key, clientGuest);
                    ReplyNewLink(true, true);
                }
                else
                {
                    ReplyNewLink(true, false);
                }
            }
            else
            {
                if (tempHost != null)
                {
                    clientHost = tempHost;
                    listeningHost = true;
                    ReplyNewLink(false, true);
                }
                else
                {
                    ReplyNewLink(false, false);
                }
            }
        }

        private void ReplyNewLink(bool isHost, bool result)
        {
            int replyHeader;
            string replyMessage;

            replyHeader = (isHost ? MessageInfo.SERVER_CREATED_NEW_HOST : MessageInfo.SERVER_CREATED_NEW_GUEST);
            replyMessage = (result ? "succeeded" : "failed");

            SendMsg(false, string.Format("{0:D4}{1:D4}{2}", replyHeader, 0, replyMessage));
        }

        private void ReplyPermission(bool permission)
        {
            int replyCommand = (permission ? MessageInfo.SERVER_PERMISSION_GRANTED : MessageInfo.SERVER_PERMISSION_DENIED);
            SendMsg(false, string.Format("{0:D4}", replyCommand));
        }

        private void RequestPermission()
        {
            listeningHost = true;
            SendMsg(true, string.Format("{0:D4}", MessageInfo.SERVER_REQUEST_DATA));
        }

        private void PrepareReceiveData(string msg)
        {
            int msgDataLength = int.Parse(msg.Substring(MessageInfo.COMMAND_SIZE, MessageInfo.LENGTH_SIZE));
            int fileSize = int.Parse(msg.Substring(MessageInfo.HEADER_SIZE, msgDataLength));
            data = new byte[fileSize];

            SendMsg(true, string.Format("{0:D4}", MessageInfo.SERVER_RECEIVED_FILE_SIZE));
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
                listeningHost = false;
                currentFileSize = 0;
                SendMsg(true, string.Format("{0:D4}", MessageInfo.SERVER_RECEIVED_ALL_DATA));

                int msgDataLength = Convert.ToString(data.Length).Length;
                SendMsg(false, string.Format("{0:D4}{1:D4}{2}", MessageInfo.SERVER_SEND_FILE_SIZE, msgDataLength, data.Length));
            }
            else
            {
                SendMsg(true, string.Format("{0:D4}", MessageInfo.SERVER_RECEIVED_PARTIAL_DATA));
            }
        }

        private void SendData()
        {
            int msgCommand, msgDataSize, msgSize;
            byte[] msg;

            if ((data.Length - currentFileSize) > MessageInfo.DATA_SIZE)
            {
                msgCommand = MessageInfo.SERVER_SEND_PARTIAL_DATA;
                msgDataSize = MessageInfo.DATA_SIZE;
                msgSize = MessageInfo.BUFFER_SIZE;
            }
            else
            {
                msgCommand = MessageInfo.SERVER_SEND_ALL_DATA;
                msgDataSize = data.Length - currentFileSize;
                msgSize = MessageInfo.HEADER_SIZE + msgDataSize;
            }

            byte[] msgHeader = Encoding.Default.GetBytes(string.Format("{0:D4}{1:D4}", msgCommand, msgDataSize));

            msg = new byte[msgSize];
            Buffer.BlockCopy(msgHeader, 0, msg, 0, MessageInfo.HEADER_SIZE);
            Buffer.BlockCopy(data, currentFileSize, msg, MessageInfo.HEADER_SIZE, msgDataSize);

            currentFileSize += msgDataSize;

            SendMsg(false, msg);
        }

        private void SendMsg(bool isHost, string msg)
        {
            Socket destination;
            byte[] byteMsg = Encoding.Default.GetBytes(msg);

            destination = (isHost ? clientHost : clientGuest);
            destination.BeginSend(byteMsg, 0, byteMsg.Length, SocketFlags.None, new AsyncCallback(SendMsgCallback), destination);

            Thread.Sleep(500);
        }

        private void SendMsg(bool isHost, byte[] msg)
        {
            Socket destination;

            destination = (isHost ? clientHost : clientGuest);
            destination.BeginSend(msg, 0, msg.Length, SocketFlags.None, new AsyncCallback(SendMsgCallback), destination);

            Thread.Sleep(500);
        }

        private void SendMsgCallback(IAsyncResult ar)
        {
            try
            {
                Socket destination = (Socket)ar.AsyncState;
                destination.EndSend(ar);

                if (clientHost == null)
                    return;

                Receive();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Disconnect();
            }
        }

        private void Disconnect()
        {
            if (clientHost != null)
                clientHost.Close();
            if (clientGuest != null)
                clientGuest.Close();
            server.RemoveHostSocket(clientHost);
            server.RemoveHostSocket(clientGuest);
            return;
        }
    }
}
