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

namespace PasswordSynchronizingServer
{
    class MessageInfo
    {
        public const int BUFFER_SIZE = 1024;
        public const int HEADER_COMMAND_SIZE = 4;
        public const int HEADER_CREDENTIAL_SIZE = 4;
        public const int HEADER_DATA_LENGTH_SIZE = 4;
        public const int DATA_SIZE = BUFFER_SIZE - HEADER_COMMAND_SIZE - HEADER_CREDENTIAL_SIZE
            - HEADER_DATA_LENGTH_SIZE;
        public const int HEADER_SIZE = HEADER_COMMAND_SIZE + HEADER_CREDENTIAL_SIZE
            + HEADER_DATA_LENGTH_SIZE;

        public const int SERVER_SEND_FILE_SIZE = 8001;
        public const int SERVER_SEND_FILE = 8002;
        public const int SERVER_SEND_NO_FILE = 8003;
        public const int SERVER_RECEIVED_FILE_SIZE = 8500;
        public const int SERVER_RECEIVED_FILE = 8502;

        public const int CLIENT_SEND_FILE = 9002;
        public const int CLIENT_RECEIVED_FILE_SIZE = 9500;
        public const int CLIENT_RECEIVED_FILE = 9501;

        public const int CLIENT_REQUEST_UPLOAD_FILE = 9900;
        public const int CLIENT_REQUEST_READ_FILE = 9901;
        public const int CLIENT_REQUEST_DELETE_FILE = 9902;
    }
    public class Server
    {
        private const int portNum = 10024;

        private Socket listener = null;
        
        Hashtable hashtable = new Hashtable();

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
                    new Transfer(client);
                });
            }
        }
    }

    class Transfer
    {
        private Socket client;
        private byte[] buffer;
        private byte[] data;
        private string fileName;
        private int currentFileSize = 0;

        public Transfer(Socket socket)
        {
            client = socket;
            buffer = new byte[MessageInfo.BUFFER_SIZE];
            Receive();
        }

        private void Receive()
        {
            client.BeginReceive(buffer, 0, MessageInfo.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(EndReceive), buffer);
        }

        private void EndReceive(IAsyncResult ar)
        {
            byte[] buff = (byte[])ar.AsyncState;
            int size = client.EndReceive(ar);
            string msg = Encoding.Default.GetString(buff, 0, size);
            int command = int.Parse(msg.Substring(0, MessageInfo.HEADER_COMMAND_SIZE));

            switch(command)
            {
                case MessageInfo.CLIENT_REQUEST_UPLOAD_FILE:
                    FileUploadInitiate(msg);
                    break;
                case MessageInfo.CLIENT_SEND_FILE:
                    UploadFile(msg);
                    break;
                case MessageInfo.CLIENT_REQUEST_READ_FILE:
                    ReadFile(msg);
                    break;
                case MessageInfo.CLIENT_RECEIVED_FILE_SIZE:
                case MessageInfo.CLIENT_RECEIVED_FILE:
                    SendFile();
                    break;
            }
        }

        private void FileUploadInitiate(string msg)
        {
            fileName = msg.Substring(MessageInfo.HEADER_COMMAND_SIZE, MessageInfo.HEADER_CREDENTIAL_SIZE);

            int fileSize = int.Parse(msg.Substring(MessageInfo.HEADER_SIZE, msg.Length));
            data = new byte[fileSize];
            currentFileSize = 0;

            string reply = string.Format("{0:D4}{1:D4}{2:D4}{3}", MessageInfo.SERVER_RECEIVED_FILE_SIZE, 0, 0, "");
            SendMsg(reply);
        }

        private void UploadFile(string msg)
        {
            int fileSize = int.Parse(msg.Substring(MessageInfo.HEADER_COMMAND_SIZE + MessageInfo.HEADER_CREDENTIAL_SIZE, MessageInfo.HEADER_DATA_LENGTH_SIZE));
            string receivedFile = msg.Substring(MessageInfo.HEADER_SIZE, fileSize);
            byte[] bytes = Encoding.Default.GetBytes(receivedFile);
            Buffer.BlockCopy(bytes, 0, data, currentFileSize, fileSize);

            currentFileSize += fileSize;
            if (data.Length == currentFileSize)
            {
                FileStream fs = new FileStream("./usedat.dat", FileMode.OpenOrCreate, FileAccess.Write);
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data, 0, data.Length);
                bw.Close();
                fs.Close();
            }

            string reply = string.Format("{0:D4}{1:D4}{2:D4}{3}", MessageInfo.SERVER_RECEIVED_FILE, 0, 0, "");
            SendMsg(reply);
        }

        private void ReadFile(string msg)
        {
            fileName = msg.Substring(MessageInfo.HEADER_COMMAND_SIZE, MessageInfo.HEADER_CREDENTIAL_SIZE);

            FileInfo fi = new FileInfo(fileName);
            string reply;
            if (!(fi.Exists))
            {
                reply = string.Format("{0:D4}{1:D4}{2:D4}{3}", MessageInfo.SERVER_SEND_NO_FILE, 0, 0, "");
                SendMsg(reply);
                return;
            }

            data = new byte[fi.Length];
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            br.Read(data, 0, data.Length);
            br.Close();
            fs.Close();

            currentFileSize = 0;

            reply = string.Format("{0:D4}{1:D4}{2:D4}{3}", MessageInfo.SERVER_SEND_FILE_SIZE, 0, data.Length, "");
            SendMsg(reply);
        }

        private void SendFile()
        {
            string strCommand = string.Format("{0:D4}", MessageInfo.SERVER_SEND_FILE);
            byte[] byteCommand = new byte[MessageInfo.HEADER_COMMAND_SIZE];
            byteCommand = Encoding.Default.GetBytes(strCommand);

            string strCredential = string.Format("{0:D4}", 0);
            byte[] byteCredential = new byte[MessageInfo.HEADER_CREDENTIAL_SIZE];
            byteCredential = Encoding.Default.GetBytes(strCredential);

            int dataLength;
            if ((data.Length - currentFileSize) > MessageInfo.BUFFER_SIZE)
            {
                dataLength = MessageInfo.BUFFER_SIZE;
            }
            else
            {
                dataLength = data.Length - currentFileSize;
            }
            string strDataLength = string.Format("{0:D4}", dataLength);
            byte[] byteDataLength = new byte[MessageInfo.HEADER_DATA_LENGTH_SIZE];
            byteDataLength = Encoding.Default.GetBytes(strDataLength);

            byte[] bytes = new byte[MessageInfo.BUFFER_SIZE];
        }

        private void SendMsg(string msg)
        {
            byte[] byteMsg = Encoding.Default.GetBytes(msg);
            client.BeginSend(byteMsg, 0, byteMsg.Length, SocketFlags.None, new AsyncCallback(Callback_SendMsg), client);
            Thread.Sleep(500);
        }

        private void Callback_SendMsg(IAsyncResult ar)
        {
            client = (Socket)ar.AsyncState;
            Receive();
        }
    }
}
