/*
 * Single socket TCP communications to talk to a chat server
 * 
 */
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ComPack
{
    class ComTCP : CommunicationChannel
    {
        System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();
        NetworkStream serverStream = default(NetworkStream);
        string readData = null;
        string localIP;

        public ComTCP()
        {
            Type = 4;
            MsgFormat = 2;
            Name = "Chat Client for Server";
            CurrentMsg = new CommunicationMessage();
            Contacts = new List<CommunicationRecipient>();
        }

        public override int Open()
        {
            int Result = 0;

            WriteDebug("Open");
            //msg();
            clientSocket.Connect(Server, PortOut);
            serverStream = clientSocket.GetStream();

            Connected = clientSocket.Connected;
            localIP = GetLocalIP();

            WriteDebug(string.Format("Leaving open - Connected={0}", clientSocket.Connected));
            return Result;
        }

        private void getMessage()
        {
            while (true)
            {
                try
                {
                    Connected = clientSocket.Connected;
                    WriteDebug(string.Format("Getmessage - Connected={0}", clientSocket.Connected));
                    serverStream = clientSocket.GetStream();
                    int buffSize = 0;
                    byte[] inStream = new byte[1024];
                    buffSize = clientSocket.ReceiveBufferSize;
                    serverStream.Read(inStream, 0, inStream.Length);
                    string returndata = System.Text.Encoding.ASCII.GetString(inStream);

                    if (returndata != null && returndata.Length > 0)
                    {
                        readData = returndata.Replace("\0", string.Empty).Trim();
                        msg();
                    }
                }
                catch (Exception ex)
                {
                    AddError(701, ex.Message, "GETMESSAGE");
                }
            }
        }

        // Process the incoming messsage
        private void msg()
        {
            try
            {
                WriteDebug(string.Format("Msg - Connected={0}", clientSocket.Connected));
                CommunicationMessage Msg = new CommunicationMessage();
                Msg.Message = readData;
                InMsgQueue.Add(Msg);
                readData = null;
            }
            catch (Exception ex)
            {
                AddError(702, ex.Message, "GETMESSAGE");
            }
        }


        public override bool LogIn(string user, string password)
        {
            bool Result = true;


            UserName = user.Trim();
            UserNick = UserName.Replace(" ", "."); // nicks can't have spaces

            // clear the buffers
            InMsgQueue = new List<CommunicationMessage>();
            OutMsgQueue = new List<CommunicationMessage>();

            // worry about passwords later
            if (Connected)
            {
                byte[] outStream = System.Text.Encoding.ASCII.GetBytes(string.Format("[HELLO:{0}|{1}|{2}|{3}|{4}]", InstanceID, UserName, UserNick, localIP, MachineName));
                serverStream.Write(outStream, 0, outStream.Length);
                serverStream.Flush();

                Thread ctThread = new Thread(getMessage);
                ctThread.Start();
            }
            else
            {
                AddError(901, "Not connected", "OPEN", "Failed to connect to " + Server);
                Result = false;
            }


            return Result;
        }

        /*
        * Return the local IP
        */
        private string GetLocalIP()
        {
            string myIP = "127.0.0.1";

            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    myIP = ip.ToString();
                    break;
                }
            }

            return myIP;
        }


        public override bool Close()
        {
            //WriteDebug(string.Format("Close - Connected={0}", clientSocket.Connected));
            return true;
        }

        public override int CreateMessage(string contact, string body, string subject)
        {
            try
            {
                //WriteDebug(string.Format("Createmsg - Connected={0}", clientSocket.Connected));
                CommunicationMessage Msg = new CommunicationMessage();
                Msg.Message =  body;
                OutMsgQueue.Add(Msg);
            }
            catch (Exception ex)
            {
                AddError(703, ex.Message, "GETMESSAGE");
            }
            return 0;
        }

        public override int MessagesWaiting()
        {
            return InMsgQueue.Count;
        }

        public override bool GetMessage()
        {
            bool Result = false;

            if (MessagesWaiting() > 0)
            {
                try
                {
                    //WriteDebug(string.Format("getmessage - Connected={0}", clientSocket.Connected));
                    CurrentMsg = new CommunicationMessage();
                    CurrentMsg = InMsgQueue[0];
                    InMsgQueue.RemoveAt(0);

                    // Is it an action message?

                    Result = true;
                }
                catch (Exception ex)
                {
                    AddError(904, ex.Message, ".GETMESSAGE", "Failed to retrieve message from inbound queue");
                }
            }

            return Result;
        }


        public override int SendMessage()
        {
            while (OutMsgQueue.Count > 0)
            {
                try
                {
                    //WriteDebug(string.Format("sendmsg - Connected={0}", clientSocket.Connected));
                    byte[] outStream = System.Text.Encoding.ASCII.GetBytes(OutMsgQueue[0].Message);
                    OutMsgQueue.RemoveAt(0);
                    serverStream.Write(outStream, 0, outStream.Length);
                    serverStream.Flush();
                }
                catch (Exception ex)
                {
                    AddError(905, ex.Message, ".SENDMESSAGE", "Failed to send message from outbound queue");
                }
            }

            return 0;
        }


        // --------------------- Not used -----------------------
        public override string GetContactList()
        {
            return null;
        }

        public override int ACKMessage(int ErrorCode)
        {
            return 1000;
        }

        public override int AddRecipient(string contact)
        {
            return 1000;
        }
    }
}
