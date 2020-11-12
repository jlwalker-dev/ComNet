using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace WPFChatServer
{
    class ChatServer
    {
        MainWindow mw;

        public Hashtable clientsList = new Hashtable();
        public List<Users> users = new List<Users>();
        public List<HandleClient> clientThreads = new List<HandleClient>();
        Thread ctThread = null;
        Thread ccThread = null;
        TcpListener serverSocket;

        public readonly string LocalPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        public bool keepRunning = false;
        public bool serverRunning = false;

        // System settings
        private string LocalIP;
        private int LocalPort = 6667;
        private bool autoRun = false;  // Startup server upon app start
        public int byteBuffer = 1024;


        public class Users
        {
            public string GUID = string.Empty;
            public string Name = string.Empty;
            public string NickName = string.Empty;
            public string IPAddress = string.Empty;
            public string ComputerName = string.Empty;
            public bool Admin = false;

            public DateTime Created = DateTime.Now;
            public DateTime LastMsg = DateTime.Now;

            public Users(string g, string n, string nn, string i, string c)
            {
                GUID = g;
                Name = n;
                NickName = nn;
                IPAddress = i;
                ComputerName = c;
            }

            public string getID() { return GUID; }
            public string getNick() { return NickName; }
        }

        public ChatServer(MainWindow m)
        {
            mw = m;
            LocalIP = GetLocalIP();
        }


        public void startServer()
        {
            if (keepRunning)
            {
                // it's already running
                mw.Display("Error: Server is already running.");
            }
            else
            {
                mw.Display("Starting server...");
                keepRunning = true;

                ctThread = null;
                ctThread = new Thread(DoServer);
                ctThread.Start();

                ccThread = null;
                ccThread = new Thread(CheckConnections);
                ccThread.Start();
            }
        }

        public void StopServer()
        {
            if (keepRunning)
            {
                mw.Display("Stopping server...");
                keepRunning = false;
                serverSocket.Stop();
                users = new List<Users>();
                clientsList = new Hashtable();
            }
            else
            {
                mw.Display("Error: Server is not running.");
            }
        }


        /*
         * Look at all connections and make sure they
         * are all connected, otherwise kill ones
         * that are not connected
         */
        public void CheckConnections()
        {
            while (keepRunning)
            {
                int i = 0;
                while (i < clientThreads.Count)
                {
                    if (clientThreads[i].destroyMe)
                    {
                        // kill this client
                        string guid = clientThreads[i].GetID();

                        clientThreads.RemoveAt(i); // remove from list

                        clientsList.Remove(guid);  // remove from hash table

                        Users u = users.Find(x => x.getID() == guid);
                        string n = u.NickName;

                        users.Remove(u); // remove from list

                        mw.Display("Removed user " + n + " / client " + guid);
                    }
                    else
                    {
                        // still active, so increment
                        i++;
                    }
                }
                Thread.Sleep(500);
            }
        }

        /*
         * This is the heart of the server and it kicks
         * off client threads as they connect
         */
        public void DoServer()
        {
            serverRunning = true;

            IPAddress localAddr = IPAddress.Parse(LocalIP);
            serverSocket = new TcpListener(localAddr, LocalPort);
            TcpClient clientSocket = default(TcpClient);
            int counter = 0;

            serverSocket.Start();
            mw.Display("Chat Server Started ....");

            while (keepRunning)
            {
                counter += 1;

                string dataFromClient = null;
                byte[] bytesFrom = null;

                try
                {
                    clientSocket = serverSocket.AcceptTcpClient();
                    bytesFrom = new byte[1024];
                    NetworkStream networkStream = clientSocket.GetStream();
                    networkStream.Read(bytesFrom, 0, bytesFrom.Length);
                }
                catch (Exception ex)
                {
                    if (keepRunning)
                        mw.Display("Socket read error: " + ex.Message);
                    else
                        mw.Display("Closing ServerSocket");
                }

                if (bytesFrom != null)
                {
                    dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                    dataFromClient = dataFromClient.Replace("\0", string.Empty).Trim();
                }

                if (dataFromClient != null && dataFromClient.Substring(0, 7).Equals("[HELLO:"))
                {
                    dataFromClient = dataFromClient.Replace("[HELLO:", "").Replace("]", "").Trim();
                    string[] data = dataFromClient.Split('|');

                    if (data.Length > 4)
                    {
                        dataFromClient = data[0]; //guid
                        data[2] = (data[2].Length > 20 ? data[2].Substring(0, 20) : data[2]);  // 20 char nicks max

                        // names and nicks can't have () in them - it's reserved for deduping, so there!
                        data[1] = data[1].Trim().Replace("(", "").Replace(")", "");
                        data[2] = data[2].Trim().Replace("(", "").Replace(")", "").Replace(" ", ""); // and no spaces in the Nick!

                        if (users.Count > 0)
                        {
                            mw.Display(string.Format("{0} users", users.Count));

                            // make sure we don't have any dupes
                            if (NameExists(data[1]) >= 0)
                            {
                                mw.Display("Updating name");
                                data[1] += string.Format("({0})", (users.Count));
                            }

                            // make sure we don't have any dupes
                            if (NickExists(data[2]) >= 0)
                            {
                                mw.Display("Updating nickname");
                                data[2] += string.Format("({0})", (users.Count));
                            }
                        }

                        //                  GUID            Name     Nick     IP              Computer Name
                        Users c = new Users(data[0].Trim(), data[1], data[2], data[3].Trim(), data[4].Trim());

                        users.Add(c);

                        clientsList.Add(dataFromClient, clientSocket);  // GUID and thread for client
                        Broadcast(string.Format(data[2].Trim() + " joined the chat room"), dataFromClient);

                        mw.Display(string.Format("{0} with nickname of {1} on {2}", c.Name, c.NickName, c.ComputerName) + " joined chat room");

                        // Create and start a new thread for this client connection
                        clientThreads.Add(new HandleClient());
                        clientThreads[clientThreads.Count - 1].StartClient(mw, this, clientSocket, dataFromClient, c.NickName);
                    }
                    else
                    {
                        mw.Display(string.Format(">>> Received badly formed hello message {0}\r\n", dataFromClient));
                    }
                }
            }

            serverRunning = false;
            mw.Display("Server stopped.");
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
         * Mark the last time a user sent a message
         */
        public void LastMsgAt(string guid)
        {
            int i = users.FindIndex(x => x.GUID.Equals(guid));

            if (i < 0)
            {
                // did not find entry
                mw.Display(string.Format(">>> Could not locate user with guid= {0}", guid));
            }
            else
            {
                users[i].LastMsg = DateTime.Now;
            }
        }

        /*
         * Look to see if nick name exists
         */
        public int NickExists(string nick)
        {
            return users.FindIndex(x => x.NickName.Equals(nick, StringComparison.OrdinalIgnoreCase));
        }

        /*
         * Look to see if name exists
         */
        public int NameExists(string name)
        {
            return users.FindIndex(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        //Users u = users.Find(x => x.NickName.Equals(nick, StringComparison.OrdinalIgnoreCase));

        /*
        * Send a message out to everyone
        */
        public void Broadcast(string msg, string uName)
        {
            foreach (DictionaryEntry Item in clientsList)
            {
                TcpClient broadcastSocket;
                broadcastSocket = (TcpClient)Item.Value;

                if (broadcastSocket.Connected)
                {
                    try
                    {
                        NetworkStream broadcastStream = broadcastSocket.GetStream();
                        Byte[] broadcastBytes = Encoding.ASCII.GetBytes(msg);
                        broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                        broadcastStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        mw.Display("BroadCast error: " + ex.Message);
                    }
                }
            }
        }  //end broadcast function

        /*
         * Send a private message to recipient and sender
         */
        public void SendTo(string from, string toUser, string msg)
        {
            Users u = users.Find(x => x.NickName.ToUpper() == toUser.ToUpper());

            if (u != null)
            {
                // found it!  Now compare GUID with the key for the match
                foreach (DictionaryEntry Item in clientsList)
                {
                    if (Item.Key.Equals(u.GUID))
                    {
                        SendIt(Item, "Private message from " + from + ": " + msg);
                    }
                }
            }
        }  //end sendTo function


        // Send a message out to the thread
        public void SendIt(DictionaryEntry Item, string msg)
        {
            // and send it!
            TcpClient broadcastSocket;
            broadcastSocket = (TcpClient)Item.Value;

            {
                try
                {
                    NetworkStream broadcastStream = broadcastSocket.GetStream();
                    Byte[] broadcastBytes = Encoding.ASCII.GetBytes(msg);
                    broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                    broadcastStream.Flush();
                }
                catch (Exception ex)
                {
                    mw.Display("BroadCast error: " + ex.Message);
                }
            }
        }


        // Set out a message to one user
        public void sendOut(string toUser, string msg)
        {
            // found it!  Now compare GUID with the key for the match
            foreach (DictionaryEntry Item in clientsList)
            {
                if (Item.Key.Equals(toUser))
                {
                    SendIt(Item, msg);
                    break;
                }
            }
        }  //end sendTo function

        /*
         * Send the help file out to the user requesting
         *
         * TODO - sends so fast it comes in to client as multi-line transmits
         * 
         */
        public void Help(string from)
        {
            // found it!  Now compare GUID with the key for the match
            foreach (DictionaryEntry Item in clientsList)
            {
                if (Item.Key.Equals(from))
                {
                    SendIt(Item, "/HELP\r\n");

                    if (File.Exists(LocalPath + "Help.txt"))
                    {
                        string line;

                        // open the file
                        System.IO.StreamReader file = new System.IO.StreamReader(LocalPath + "Help.txt");
                        while ((line = file.ReadLine()) != null)
                        {
                            // send it out line by line
                            SendIt(Item, line + "\r\n");
                        }

                        file.Close();
                    }
                    break;
                }
            }
        }
    }
}
