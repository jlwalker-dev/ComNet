/*
 * Used by ComIRC to talk to an IRC server
 * 
 * TODO
 *      Learn more about IRC commands
 *      What's the deal with flush()?
 *      Password support
 *      Register usernames
 *      
 */
using System.IO;
using System.Net.Sockets;
using System.Net;
using System;

namespace ComPack
{
    class IrcClient
    {
        private string userName;
        private string userHost = "LocalHost";
        private string ircChannel;
        private TcpClient tcpClient;
        private StreamReader inputStream;
        private StreamWriter outputStream;
        private string address = string.Empty;
        private int port = 6667;
        private CommunicationChannel Channel = null;

        /*
         * Constructor to connect to a server and log in
         */
        public IrcClient(CommunicationChannel chan)
        {
            Channel = chan;
            this.userName = Channel.UserName;
            this.address = Channel.Server;
            this.port = Channel.PortOut;

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
                Channel.AddError(1, "IrcClient.IRCCLIENT", ex.Message, "Attempted to connect to " + address + ":" + port.ToString());
            }

            // Set up the streams and set up user/nick
            try
            {
                inputStream = new StreamReader(tcpClient.GetStream());
                outputStream = new StreamWriter(tcpClient.GetStream());
            }
            catch (Exception ex)
            {
                Channel.AddError(2, "IrcClient.IRCCLIENT", ex.Message, "Attempted to authorize user at " + address + ":" + port.ToString());
            }
        }


        /*
         * TODO - learn about IRC login w/ password and implement
         * 
         */
        public int IrcLogIn(string user, string pw)
        {
            int Result = 0;

            if (Connected())
            {
                SendOutput(string.Format("NICK {0}", userName));
                SendOutput(string.Format("USER {0} 8 * {1}", userName, userName));
                outputStream.Flush();
            }
            else
            {
                Channel.AddError(Result = 1, "IRC Channel not connected", GetType().Name + ".IRCLOGIN");
            }

            return Result;
        }

        /*
         * Close the TCP client
         */
        public void tcpClose()
        {
            this.tcpClient.Close();
        }

        /*
         * Set the right had part of UserName@HostName which is plucked from 
         * what the server sends back to us when we're logging in
         * 
         */
        public void SetHost(string h)
        {
            userHost = h;
        }

        /*
         * Return a bool for our connection state
         */
        public bool Connected()
        {
            return tcpClient.Connected;
        }

        /*
         * Code to join an IRC channel
         * Also called from outside code so need this small piece
         * instead of putting it into the send logic
         */
        public void joinRoom(string channel)
        {
            this.ircChannel = channel.Replace("#", "");
            SendOutput("JOIN #" + channel);
            outputStream.Flush();
        }

        /*
         * Send a formatted message and then flush()
         */
        public void sendIrcMessage(string message)
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

            if (message.Substring(0, 1).Equals("/") && message.Length > 1)
            {
                if (Channel.DebugLevel > 0) Channel.WriteDebug("Processing command " + message);

                if (message.IndexOf(' ') > 0)
                {
                    cmd = message.Substring(1, message.IndexOf(' ')).Trim().ToUpper();
                    msg = message.Substring(message.IndexOf(' ')).Trim();
                }
                else
                {
                    cmd = message.Substring(1).Trim().ToUpper();
                }

                message = string.Empty;

                switch (cmd)
                {
                    case "ACTION":
                        message = ((char)1).ToString() + "ACTION " + msg + ((char)1).ToString();
                        break;

                    case "JOIN":
                        joinRoom(msg);
                        break;

                    case "KICK":
                        SendOutput("KICK #" + this.ircChannel + " " + msg);
                        outputStream.Flush();
                        break;

                    case "MODE":
                        SendOutput("MODE :" + msg);
                        break;

                    case "NAMES":
                        SendOutput("NAMES");
                        outputStream.Flush();
                        break;

                    case "NICK":
                        SendOutput("NICK :" + msg);
                        outputStream.Flush();
                        break;

                    case "PART":
                        SendOutput("PART #" + this.ircChannel);
                        outputStream.Flush();
                        break;

                    case "QUIT":
                        SendOutput("QUIT " + msg);
                        outputStream.Flush();
                        break;
                }
            }

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
                    }
                }

                // Put together the formatted chat message
                sendIrcMessage(":" + userName + "!" + userName + "@" + Channel.UserHost + " PRIVMSG #" + ircChannel + " :" + message);
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
         * Return the server IP
         */
        public string getIP()
        {
            return address;
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
