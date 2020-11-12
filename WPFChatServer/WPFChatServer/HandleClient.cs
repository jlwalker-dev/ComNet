using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;


namespace WPFChatServer
{
    class HandleClient
    {
        public bool destroyMe = false;

        private TcpClient clientSocket;
        private string clNo;
        private string NickName;
        private MainWindow mw;
        private ChatServer cs;

        public void StartClient(MainWindow m, ChatServer c, TcpClient inClientSocket, string clineNo, string nick)
        {
            this.cs = c;
            this.mw = m;
            this.clientSocket = inClientSocket;
            this.clNo = clineNo;
            this.NickName = nick;
            Thread ctThread = new Thread(DoChat);
            ctThread.Start();
        }

        // Get the client ID for this thread
        public string GetID() { return clNo; }

        /*
         * Loop that listens and reacts to all input for this connection
         */
        private void DoChat()
        {
            int requestCount = 0;
            byte[] bytesFrom;
            string dataFromClient;
            string rCount;

            while (this.clientSocket.Connected)
            {
                try
                {
                    requestCount++;
                    NetworkStream networkStream = clientSocket.GetStream();

                    // clear the buffer and wait for input
                    try
                    {
                        bytesFrom = new byte[1024];
                        networkStream.Read(bytesFrom, 0, bytesFrom.Length);

                        dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                        dataFromClient = dataFromClient.Replace("\0", string.Empty).Trim();
                        mw.Display("From client - " + clNo + " -> " + dataFromClient);
                        rCount = Convert.ToString(requestCount);
                    }
                    catch (Exception ex)
                    {
                        if (this.clientSocket.Connected)
                        {
                            // read error
                            mw.Display("DOCHAT - Read error: " + ex.Message);
                        }
                        else
                        {
                            // Client has disconnected
                            mw.Display("DOCHAT - Client disconnected ->" + clNo);
                        }

                        dataFromClient = string.Empty;
                    }

                    // break out server command and add space to protect the code
                    string msg = dataFromClient.Trim();

                    //mw.Display("from=" + NickName + "\r\nmsg=" + msg);

                    // Is there a command (starts with /)
                    if (msg.Length > 1)
                    {
                        cs.LastMsgAt(clNo); // update time of last message

                        if (msg.Substring(0, 1).Equals("/"))
                        {
                            // Process the command
                            msg += " ";
                            string command = msg.Substring(1, msg.IndexOf(' ') - 1).Trim().ToUpper();
                            msg = msg.Substring(msg.IndexOf(' ')).Trim();
                            dataFromClient = string.Empty;

                            //Console.WriteLine("Processing command " + command);
                            switch (command)
                            {
                                case "NAMES": // return user list - LIST
                                    string names = "/NAMES\r\n\r\nNames List\r\n--------------------\r\n";
                                    for (int i = 0; i < cs.users.Count; i++)
                                    {
                                        names += string.Format("{0} @ {1}", cs.users[i].NickName, cs.users[i].LastMsg.ToString("HH:mm")) + "\r\n";
                                    }
                                    names += "\r\n--------------------\r\n";

                                    cs.sendOut(clNo, names);
                                    break;

                                case "KICK": // kick nick - KICK usernick
                                    break;

                                case "NICK": // Change the nickname
                                    // no spaces, no parens, 20 char max - dems da rules
                                    msg = msg.Replace("(", "").Replace(")", "").Replace(" ","");
                                    msg = (msg.Length > 20 ? msg.Substring(0, 20) : msg);

                                    if (cs.NickExists(msg) >= 0)
                                    {
                                        dataFromClient = "-> " + NickName + " has changed their nickname to " + msg;
                                        NickName = msg.Trim();
                                    }
                                    else
                                    {
                                        cs.sendOut(clNo, "Nickname already exists");
                                    }
                                    break;

                                case "REG": // register user - REG username pw
                                    break;

                                case "PASS": // change password - PASS old new
                                    break;

                                case "MSG":  // private msg - MGS usernick msg
                                    string toUser = msg.Substring(0, msg.IndexOf(' ')).Trim();

                                    // must have a space or there is no message
                                    if (msg.Contains(' '))
                                    {
                                        cs.SendTo(NickName, toUser, msg.Substring(msg.IndexOf(' ')).Trim());
                                    }
                                    break;

                                case "HELP": // return help screen - HELP
                                    cs.Help(clNo);
                                    break;

                                default: // unknown command - respond with error to user
                                    mw.Display("DOCHAT - Unknown command: " + command);
                                    break;
                            }
                        }
                        else
                        {
                            // normal message
                            dataFromClient = NickName + ": " + msg;
                        }
                    }

                    // If there's a message, send the info out to all connections
                    if (dataFromClient.Length > 0)
                        cs.Broadcast(dataFromClient, clNo);
                }
                catch (Exception ex)
                {
                    mw.Display(string.Format("DOCHAT - ID {0} - Error: {1}", clNo, ex.ToString()));
                }
            }//end while

            mw.Display(string.Format("DOCHAT - Lost Connection {0}", clNo));
            destroyMe = true;
        }//end doChat
    }
}
