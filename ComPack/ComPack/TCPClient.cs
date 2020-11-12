/*
 * 11/9/2020 - Not used by the DLL - remove if no use
 */

using System.IO;
using System.Net.Sockets;
using System.Net;
using System;
using System.CodeDom;

namespace ComPack
{
    class TCPClient2
    {
        private string userName;
        private TcpClient tcpClient;
        private StreamReader inputStream;
        private StreamWriter outputStream;
        private string address = string.Empty;
        private int port = 6667;

        private CommunicationChannel Channel = null;

        /*
         * Constructor to connect to a server and log in
         */
        public TCPClient2(CommunicationChannel chan)
        {
            Channel = chan;

            tcpSet();

            // Set up tcp client
            try
            {
                tcpClient = new TcpClient(address, port);
                address = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Address.ToString();

                if (Channel.DebugLevel > 3)
                    Channel.WriteDebug("Connected to " + address + " on port " + port.ToString());
            }
            catch (Exception ex)
            {
                Channel.AddError(1, ex.Message, GetType().Name + ".TCPHELPER", "Attempted to connect to " + address + ":" + port.ToString());
                tcpClient = null;
                address = null;
            }

            // Set up the streams and set up user/nick
            if (tcpClient != null)
            {
                try
                {
                    inputStream = new StreamReader(tcpClient.GetStream());
                    outputStream = new StreamWriter(tcpClient.GetStream());

                    SendOutput(string.Format("NICK {0}", userName));
                    SendOutput(string.Format("USER {0} 8 * {1}", userName, userName));
                    outputStream.Flush();
                }
                catch (Exception ex)
                {
                    Channel.AddError(2, ex.Message, GetType().Name + ".TCPHELPER", "Attempted to authorize user at " + address + ":" + port.ToString());
                }
            }
        }

        /*
         * Close the TCP client
         */
        public void tcpClose()
        {
            this.tcpClient.Close();
        }

        public void tcpSet()
        {
            this.userName = Channel.UserName.ToUpper().Trim();
            this.address = Channel.Server.Trim();
            this.port = Channel.PortOut;
        }

        /*
         * Return a bool for our connection state
         */
        public bool Connected()
        {
            return tcpClient != null && tcpClient.Connected;
        }


        /*
         * Send a formatted message and then flush()
         */
        public void sendMessage(string message)
        {
            SendOutput(message);
            outputStream.Flush();
        }

        /*
         * Send a chat message from the user and intercept any commands that we might support
         * 
         */
        public void sendChatMessage(string message)
        {
            string cmd = string.Empty;
            string msg = string.Empty;

            if (message.Length > 0)
            {
                // Do we encrypt the message?
                if (Channel.isEncrypted)
                {
                    try
                    {
                        // Create the formatted message
                        message = "[CRYPTOR]" + Channel.cryptor.EncryptString(message);
                    }
                    catch (Exception ex)
                    {
                        message = "<Failed CRYPTOR message>  Error: " + ex.Message;
                        Channel.AddError(101, ex.Message, GetType().Name + ".SENDCHATMESSAGE", "Failed to encrypt message -> " + message);

                    }
                }

                // Put together the formatted chat message
                sendMessage("#" + userName + ":" + message);
            }
        }


        /*
         * Look for some input from the server
         */
        public string readMessage()
        {
            string message = null;

            try
            {
                message = inputStream.ReadLine();
                if (Channel.DebugLevel > 8) Channel.WriteDebug("<-" + message);
            }
            catch (Exception ex)
            {
                if (Channel.DebugLevel > 0) Channel.WriteDebug("Irc ReadMessage failure: " + ex.Message);
            }

            return message;
        }


        /*
         * Send the message to the server without a flush()
         */
        private void SendOutput(string msg)
        {
            try
            {
                if (Channel.DebugLevel > 3) Channel.WriteDebug("-> " + msg);
                outputStream.WriteLine(msg);
            }
            catch (Exception ex)
            {
                Channel.AddError(3, "IrcClient.SENDOUTPUT", ex.Message, "Failed to send message: " + msg);
            }
        }
    }
}
