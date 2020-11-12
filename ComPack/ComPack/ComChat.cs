/*
 * Taken from https://www.c-sharpcorner.com/UploadFile/97ec13/how-to-make-a-chat-application-in-C-Sharp/
 * along with ideas from other pages
 * 
 * TODO - Make alterations for chat server support
 *        Allow for confirmation of msg receipt
 *        
 *
 * This is a simple TCP socket chat client
 * 
 * History
 *  11/7/2020
 *      First brush with a solution which includes encrypted
 *      communications and one-to-one connection.  Some work
 *      done to prepare for a chat server.
 *      
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ComPack
{
    class ComChat : CommunicationChannel
    {
        private readonly Socket sck = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private byte[] buffer;

        string LocalIP;
        string RemoteIP;

        string LocalPort;
        string RemotePort;

        IPEndPoint epLocal;
        EndPoint epRemote;

        public ComChat()
        {
            Type = 3;
            MsgFormat = 2;
            Name = "Chat Client";
            CurrentMsg = new CommunicationMessage();
            Contacts = new List<CommunicationRecipient>();
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


        /*
         * Open the sockets and make the connection
         */
        public override int Open()
        {
            int Result = 0;

            RemoteIP = Server;
            RemotePort = PortOut.ToString();
            LocalPort = PortIn.ToString();

            try
            {
                sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                LocalIP = GetLocalIP();
            }
            catch (Exception ex)
            {
                AddError(Result = 801, ex.Message, GetType().Name + ".SocketClient", "Failed to set socket");
            }

            try
            {
                // binding socket
                epLocal = new IPEndPoint(IPAddress.Parse(LocalIP), Convert.ToInt32(LocalPort));
                sck.Bind(epLocal);

                // connect to remote IP and port
                epRemote = new IPEndPoint(IPAddress.Parse(RemoteIP), Convert.ToInt32(RemotePort));
                sck.Connect(epRemote);

                Connected = true;

                if (DebugLevel > 5)
                    WriteDebug("Waiting...");

                // starts to listen to an specific port
                buffer = new byte[1500];
                sck.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref epRemote, new AsyncCallback(MessageCallBack), buffer);
            }
            catch (Exception ex)
            {
                AddError(Result = 802, ex.Message, GetType().Name + ".SocketClient", "Failed to open socket");
            }

            return Result;
        }


        /*
         * 
         * Receiving a message
         * 
         */
        private void MessageCallBack(IAsyncResult aResult)
        {
            if (DebugLevel > 8)
                WriteDebug("In MessageCallBack");

            try
            {
                int size = sck.EndReceiveFrom(aResult, ref epRemote);

                // check if theres actually information if (size > 0) { // used to help us on getting the data
                byte[] receivedData = new byte[1464];

                // getting the message data
                receivedData = (byte[])aResult.AsyncState;

                // converts message data byte array to string
                ASCIIEncoding eEncoding = new ASCIIEncoding();
                string receivedMessage = eEncoding.GetString(receivedData);
                receivedMessage = receivedMessage.Replace("\0", string.Empty).Trim();

                if (DebugLevel > 5)
                    WriteDebug("<-" + receivedMessage);

                // Save the message to the InMsgQueue list
                CommunicationMessage msg = new CommunicationMessage();
                if (receivedMessage.IndexOf("#") > 0)
                {
                    if (DebugLevel > 8)
                        WriteDebug("Breaking out name");

                    string[] nameInfo = receivedMessage.Substring(0, receivedMessage.IndexOf("#")).Split('|');
                    msg.Sender.NickName = nameInfo[0];

                    if (nameInfo.Length > 1) msg.Sender.Name = nameInfo[1];
                    if (nameInfo.Length > 2) msg.Sender.IP4Address = nameInfo[2];
                    if (nameInfo.Length > 3)
                    {
                        msg.Sender.MachineID = nameInfo[3];

                        // add to contacts list if not already
                    }

                    if (nameInfo.Length > 5)
                    {
                        // private message
                        string recipient = nameInfo[4].Replace("[", "").Replace("]", "");

                        // datetime it was sent
                        DateTime dtm;
                        if (DateTime.TryParse(nameInfo[5].Replace("{", "").Replace("}", ""), out dtm) == false)
                            dtm = DateTime.Now;

                        msg.MessageTime = dtm;

                        if (recipient.Length > 0)
                        {
                            if (recipient.ToUpper().Equals(UserName.ToUpper()) == false)
                            {
                                // Received a server message, so trash it
                                AddError(805, "Received private message", GetType().Name + ".MESSAGECALLBACK", "Received message meant for " + (recipient.Equals("?") ? "Chat Server" : recipient));
                                msg = null;
                            }
                        }
                    }
                    else
                    {
                        // This goes to a debug file no matter what
                        int dbl = DebugLevel;
                        DebugLevel = 1;
                        WriteDebug(string.Format(">>>> BAD MESSAGE FORMAT <<<<\r\n    {0}\r\n\r\n", receivedMessage));
                        DebugLevel = dbl;
                    }

                    if (msg != null)
                    {
                        string m = receivedMessage.Substring(receivedMessage.IndexOf("#") + 1).Trim();

                        // handle any received cryptor messages
                        if (m.Substring(0, 8).Equals("[CRYPTOR"))
                        {
                            if (DebugLevel > 5)
                                WriteDebug(string.Format("Calling Cryptor with {0}", m));

                            m = cryptText(m);
                        }

                        msg.Message = m;
                    }
                }
                else
                {
                    msg.Message = receivedMessage;
                }

                // was the message tossed (is null) or do we save it to the queue?
                if (msg != null)
                {
                    if (DebugLevel > 5)
                        WriteDebug(string.Format("<-{0}\r\n     Nick: {1}\r\n     Name: {2}\r\n     Address: {3}\r\n     Machine: {4}\r\n     Message: {5}\r\n",
                            receivedMessage, msg.Sender.NickName, msg.Sender.Name, msg.Sender.Address, msg.Sender.MachineID, msg.Message));

                    InMsgQueue.Add(msg);
                }

                if (DebugLevel > 5) WriteDebug("Waiting...");

                // starts listening to the socket again
                buffer = new byte[1500];
                sck.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref epRemote, new AsyncCallback(MessageCallBack), buffer);
            }
            catch (Exception ex)
            {
                AddError(803, ex.Message, GetType().Name + ".MESSAGECALLBACK");
            }
        }

        /*
         * Close the socket
         */
        public override bool Close()
        {
            sck.Close();
            return true;
        }

        /*
         * Used for authorizing through the server
         * Errors are placed into the Error list
         * Server messages are placed into the inbound Messages list as a normal message
         */
        public override bool LogIn(string user, string password)
        {
            bool Result = true;

            if (DebugLevel > 5) WriteDebug(GetType().Name + ".LOGIN starting");

            if (user.Length > 2)
            {
                string msg = string.Format("/LOGIN {0} | {1}", user, password);

                msg = password;  // TODO - need to encrypt this, or something - perhaps 1 way hash?
                UserName = user;

                try
                {
                    // Send a login msg because we're trying to talk to a server
                    CreateMessage("?", msg, "");
                    SendMessage();
                }
                catch (Exception ex)
                {
                    AddError(801, ex.Message, GetType().Name + ".LOGIN", "Failed to send login to server");
                    Result = false;
                }
            }
            else
            {
                // blank user & pw is ok because we're not trying to log
                // into a server, but attempting a 1 on 1 connection
                if (user.Length > 0 || password.Length > 0)
                {
                    AddError(800, "User name must be 3 or more characters", GetType().Name + ".LOGIN");
                    Result = false;
                }
            }

            return Result;
        }


        /*
         * How many messages waiting in the InMsgQueue list
         */
        public override int MessagesWaiting()
        {
            return InMsgQueue.Count;
        }


        /*
         * Get the most recent message and pop it off the FIFO stack
         */
        public override bool GetMessage()
        {
            CurrentMsg = new CommunicationMessage();

            if (InMsgQueue.Count > 0)
            {
                CurrentMsg = InMsgQueue[0];
                InMsgQueue.RemoveAt(0);
                CurrentMsg.ErrorCode = 0;
                CurrentMsg.ID = string.Empty;
            }

            return CurrentMsg.Message.Length > 0;
        }

        /*
         * Pop the oldest message off the FIFO stack and
         * send it out to the receiving IP
         */
        public override int SendMessage()
        {
            int Result = 0;

            for (int i = 0; i < OutMsgQueue.Count; i++)
            {
                try
                {
                    // converts from string to byte[]
                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                    byte[] msg = new byte[1500];

                    string m =
                          OutMsgQueue[0].Sender.NickName + "|"
                        + OutMsgQueue[0].Sender.Name + "|"
                        + OutMsgQueue[0].Sender.IP4Address + "|"
                        + OutMsgQueue[0].Sender.MachineID + "|"
                        + (OutMsgQueue[0].Recipient.Count > 0 ? "[" + OutMsgQueue[0].Recipient[0].Name + "]" : "") + "|"
                        + "{" + DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffffffK") + "}"
                        + "# " + OutMsgQueue[0].Message;  // FROM: MSG

                    msg = enc.GetBytes(m);
                    if (DebugLevel > 5) WriteDebug(string.Format("-> {0}", m));

                    // sending the message string
                    sck.Send(msg);
                }
                catch (Exception ex)
                {
                    AddError(Result = 802, ex.Message, GetType().Name + ".SENDMESSAGE", string.Format("Failed to send message: {0}", OutMsgQueue[0].Message));
                }
                finally
                {
                    OutMsgQueue.RemoveAt(0);
                }
            }
            return Result;
        }


        /*
         * Create a message and put it onto the FIFO OutMsgQueue
         */
        public override int CreateMessage(string contact, string body, string subject)
        {
            CommunicationMessage msg = new CommunicationMessage();
            msg.Sender.Name = UserName;
            msg.Sender.NickName = UserNick;
            msg.Sender.IP4Address = LocalIP;
            msg.Sender.MachineID = MachineName;

            body = body.Trim();

            if (isEncrypted)
            {
                // we need to encrypt the message
                body = "[CRYPTOR]" + cryptor.EncryptString(body);
            }

            msg.Message = body.Trim();

            if (contact.Length > 0)
            {
                CommunicationRecipient s = new CommunicationRecipient();
                s.Name = UserName;
                msg.Recipient.Add(s);
            }

            OutMsgQueue.Add(msg);

            if (DebugLevel > 8) WriteDebug(string.Format("Saving Name '{0}' and body '{1}'", msg.Sender.Name, msg.Message));
            return 0;
        }


        // TODO - when server is finalized, make this work
        public override string GetContactList()
        {
            return null;
        }

        // ----------------- Not used by this protocol --------------------------------
        public override int AddRecipient(string contact)
        {
            return 1000;
        }

        public override int ACKMessage(int ErrorCode)
        {
            return 1000;
        }
    }
}
