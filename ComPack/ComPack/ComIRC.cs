/*
 * The code for this project was taken from a number of websites including 
 *      www.c-sharpcorner.com/article/C-Sharp-irc-bot
 *      liquid.fish/current/twitch-irc-bot-c-console-application
 *      
 *      
 * TODO
 *      Passwords
 *      Register Usernames
 *      Multiple IRC channel support
 *      Keep login info on local drive (encrypted of course)
 *      Someday look at public/private key encryption
 *      Someday think about multiple ComChannel support
 *      Bots?
 *      Bot language?
 *      
 * History
 *      11/3/2020 
 *          Basic processing and commands fleshed out and basic VFP interface program created
 *          Lots of excess debug statements need to be removed or set up for debug levels
 *          Need to finish commenting everything to death
 * 
 *      11/4/2020
 *          First success on encrypted communications... way cool!
 *          Going to slow down a bit now and start cleaning things up.
 *          
 */
using System;
using System.Threading;

namespace ComPack
{
    class ComIRC : CommunicationChannel
    {
        private IrcClient irc = null;  // helper class
        private static long pings = 0; // keeping track of # of pings

        private Thread chatThread = null;   // Thread for reading incoming messages
        private bool chatThreadStop = false;// Flag to tell thread when it's time to stop
        private bool inThread = false;

        /*
         * Constructor to fix basic settings for Channel settings
         */
        public ComIRC()
        {
            Type = 2;
            MsgFormat = 2; // default message format returned to calling program is <FROM>\r\n<BODY>
            Name = "IRC Client";
            ClearOldLogs();
        }

        /*
         * ComIRC does not use this method
         */
        public override int Open()
        {
            int Result = 0;
            try
            {
                // is there a server entry?
                if (Server.Length > 0 && Room.Length > 0)
                {
                    if (UserName.Length > 2)
                    {
                        // Try to connect to the server and send in login info
                        irc = new IrcClient(this);

                        if (irc != null)
                        {
                            WriteDebug("Connected to " + Server + " and joining IRC Channel " + Room);
                            irc.joinRoom(Room);
                            GetMessage();
                        }
                    }
                    else
                    {
                        AddError(Result = 17, "User must be more than 2 characters", GetType().Name + ".LOGIN", "Invalid user name -> '" + UserName + "'");
                    }
                }

                // If successful IRC start and chatThread is not yet running
                if (irc != null && chatThread == null)
                {
                    if (DebugLevel > 5) WriteDebug("Calling for a start of the readChat thread");
                    chatThreadStop = false;
                    chatThread = new Thread(() => readChat(irc));
                    chatThread.Start();
                    if (DebugLevel > 5) WriteDebug("   call completed");
                }
            }
            catch (Exception ex)
            {
                AddError(Result = 18, ex.Message, GetType().Name + ".LOGIN", "Failed to log into server " + Server);
            }

            return Result;
        }


        /*
         * Shut things down so we can start a new connection with
         * a different IRC server
         * 
         * TODO - look this over, do we tcpclose before quitting thread???
         * 
         */
        public override bool Close()
        {
            bool Result = true;

            try
            {
                // If we're still connected to the server, say goodbye
                if (Connected)
                {
                    irc.sendChatMessage("/PART");  // leave the channel
                    irc.sendChatMessage("/QUIT");  // quit the server
                    irc.tcpClose();
                }

                // close down chatThread
                if (chatThread != null) chatThreadStop = true;
                while (inThread) ;

                // null out the IRC connection
                irc = null;
            }
            catch (Exception ex)
            {
                AddError(15, ex.Message, GetType().Name + ".CLOSE", "Error closing IRC instance");
            }

            return Result;
        }


        /*
         * Connect to an IRC server, log in, and join a channel
         * 
         * TODO - passwords?
         * 
         */
        public override bool LogIn(string user, string pw)
        {
            UserName = (user.Length > 2 ? user : UserName);
            UserNick = UserName;

            return (irc.Connected() ? irc.IrcLogIn(user, pw) == 0 : false);
        }



        /*
         * Returns count of waiting messages
         * 
         */
        public override int MessagesWaiting()
        {
            return InMsgQueue.Count;
        }




        /*
         * Pop the message at the top of the FIFO stack
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
         * Create a message for outbound and add it to the out queue
         * 
         * THOUGHTS
         *      If address len>0 then it's private message
         *      If subject len>0 then it's a message to another channel?  Is that allowed?
         *      
         */
        public override int CreateMessage(string address, string body, string subject)
        {
            int Result = 0;

            try
            {
                CommunicationMessage msg = new CommunicationMessage();
                CommunicationRecipient r = new CommunicationRecipient();

                msg.Message = body;
                r.Name = address;

                msg.Recipient.Add(r);
                OutMsgQueue.Add(msg);
            }
            catch (Exception ex)
            {
                AddError(Result = 16, ex.Message, GetType().Name + ".CREATEMESSAGE", "Failed to create message -> " + body);
            }

            return Result;
        }


        /*
         * Send everything from the outbound message queue
         * 
         */
        public override int SendMessage()
        {
            int Result = 0;

            for (int i = 0; i < OutMsgQueue.Count; i++)
            {
                try
                {
                    string m = OutMsgQueue[0].Message;
                    if (m != null && m.Length > 0)
                    {
                        irc.sendChatMessage(m);
                    }
                    else
                    {
                        WriteDebug(GetType().Name + ".SENDMESSAGE - OutMsgQueue[0].Message is " + (m == null ? "null" : "empty"));
                    }
                }
                catch (Exception ex)
                {
                    AddError(Result = 12, ex.Message, GetType().Name + ".SENDMESSAGE");
                    break;
                }

                // if the message was sent, remove the old message
                OutMsgQueue.RemoveAt(0);
                MsgCount++;
            }

            return Result;
        }


        /*
         * TODO: collect names from channel and put into list for reporting
         */
        public override string GetContactList()
        {
            return "";
        }

        /*
         * Msg format is only used here for creating a message to
         * return to the calling program.  IRC protocols are
         * followed when formatting outgoing messages.
         * 
         */
        public new void SetFormat(byte b)
        {
            MsgFormat = b;
            MsgFormat &= 0b_1000_1111;
            if (async) MsgFormat |= 0b_0000_0010; // must have FROM & BODY at a minimum
        }



        /*
         * When a message comes in, put it into the inbound message queue
         * which is why it's continuously looping in a thread
         * 
         * TODO: Add automation to handle various things and to allow ease of
         *       making a simple bot to bounce flooders and other such common
         *       issues for an IRC channel
         * 
         */
        void readChat(IrcClient irc)
        {
            string message = null;
            inThread = true;

            if (DebugLevel > 8) WriteDebug("readChat has started");

            while (chatThreadStop == false)
            {
                // Are you connected?
                try
                {
                    Connected = irc.Connected();
                    chatThreadStop = (!Connected);
                }
                catch (Exception ex)
                {
                    AddError(50, ex.Message, GetType().Name + ".READCHAT", "Failure checking connection");
                }

                // Get the next message
                try
                {
                    if (DebugLevel > 8) WriteDebug("ComIRC.readChat - Waiting for message");
                    message = irc.readMessage();
                }
                catch (Exception ex)
                {
                    AddError(50, ex.Message, GetType().Name + ".READCHAT", "readMessage() failure");
                }

                // Is there a message?
                if (message != null)
                {
                    try
                    {
                        // Is it a ping?
                        if (message.Contains("PING "))
                        {
                            if (DebugLevel > 5)
                                WriteDebug(string.Format("{0}", message));

                            irc.sendIrcMessage(message.Replace("PING", "PONG"));
                            pings++;
                        }
                        else
                        {
                            // parse the message
                            CommunicationMessage msg = new CommunicationMessage();

                            if (message.IndexOf(':') == 0)
                            {
                                // This is a message from someone or something
                                int prv = message.IndexOf(" PRIVMSG ");
                                int xpt = message.IndexOf('!');
                                int xpt2 = message.IndexOf('!', xpt + 1);
                                int spc = message.IndexOf(' ');
                                int cln2 = message.IndexOf(':', 1);
                                int nk = message.IndexOf(" " + UserNick + " ");
                                int nkmsg = message.IndexOf(" " + UserNick + " :");

                                if (xpt > 0 && xpt < spc)
                                {
                                    // there's a nick/name combo such as 
                                    //    :JWVFPDev1!JWVFPDev1@localhost 
                                    //       or
                                    //    :JWVFPDev1!~JWVFPDev1@localhost 
                                    msg.Sender.NickName = message.Substring(1, xpt - 1);
                                    msg.Sender.Name = message.Substring(xpt + 1, spc - xpt).Replace("~", "").Trim();
                                }
                                else
                                {
                                    // must be an address like
                                    //      :nonstop.ix.me.dal.net 372 JWVFPDev4 
                                    msg.Sender.Address = message.Substring(1, spc).Trim();
                                }

                                if (prv > 0)
                                {
                                    // Actual PRIVMSG from someone
                                    // Example-> :JWVFPDev1!JWVFPDev1@localhost PRIVMSG #Test1234 :hello
                                    //            ^Nick^^^^ ^Name@host^^^^^^^^^         ^Channel^  ^Msg^
                                    msg.Sender.Address = (message.IndexOf(':', prv) > 0 ? message.Substring(prv + 7, message.IndexOf(':', prv) - prv + 7) : "").Trim();  // Channel
                                    message = (message.IndexOf(':', prv) > 0 ? message.Substring(message.IndexOf(':', prv) + 1) : ""); // message

                                    if (DebugLevel > 8) WriteDebug(string.Format("Code 1 msg - '{0}", message));

                                    try
                                    {
                                        // Encryption tie-in
                                        if (message.Length > 8 && message.Substring(0, 8).Equals("[CRYPTOR"))
                                        {
                                            if (DebugLevel > 8) WriteDebug(string.Format("Decryption of '{0}'", message));
                                            message = cryptText(message);
                                            if (DebugLevel > 8) WriteDebug(string.Format("   returns '{0}'", message));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        AddError(61, ex.Message, GetType().Name + ".READCHAT", string.Format("Failed with message {0}", message));
                                    }

                                    msg.Message = message;
                                    msg.Code = 1;

                                    // TODO: Keep track of users on the channel - is there a way to see when people leave?
                                }
                                else
                                {
                                    if (message.Substring(0, UserNick.Length + 1).Equals(":" + UserNick))
                                    {
                                        // Expecting a user control message like
                                        //     :JWVFPDev1 MODE JWVFPDev1 :+iw
                                        //     :JWVFPDev1!~JWVFPDev1@168.sub-174-193-15.myvzw.com JOIN :#Test1234
                                        msg.Subject = message.Substring(message.IndexOf(' ')).Trim();

                                        // expect a : but have a backup plan
                                        if (msg.Subject.Contains(":"))
                                            msg.Message = msg.Subject.Substring(msg.Subject.IndexOf(':') + 1).Trim();
                                        else
                                            msg.Message = msg.Subject.Substring(msg.Subject.IndexOf(' ') + 1).Trim();

                                        msg.Subject = msg.Subject.Substring(0, msg.Subject.IndexOf(' ')).Trim();
                                        msg.Code = 2;
                                    }
                                    else
                                        if (nkmsg > spc)
                                    {
                                        //            "Nick :" is just like PRIVMSG
                                        //            :nonstop.ix.me.dal.net 372 JWVFPDev4 :- * We reserve the right to deny access to this server without *
                                        //            :^Address^^^^^^^^^^^^^     ^Nick^^^^  ^Message^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                                        msg.Message = (message.IndexOf(':', nkmsg) > 0 ? message.Substring(message.IndexOf(':', nkmsg) + 1) : ""); // message
                                        msg.Code = 3;
                                    }
                                    else
                                    {
                                        if (nk > spc)
                                        {
                                            // _Nick_ after first space in raw message
                                            // Example-> :lion.tx.us.dal.net 005 JWVFPDev2 CASEMAPPING=ascii WATCH=128 SILENCE=10 ELIST=cmntu EXCEPTS INVEX CHANMODES=beI,k,jl,ciPAmMnOprRsSt
                                            msg.Subject = message.Substring(nk).Trim(); // command
                                            msg.Code = 4;
                                        }
                                        else
                                        {
                                            // TODO Break command and misc out from message
                                            // Message from system
                                            // Examples-> :JWVFPDev2!~JWVFPDev2@168.sub-174-193-15.myvzw.com JOIN :#Test1234
                                            //             ^Nick^^^^ ^Name@host^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ ^CMD  ^Channel^
                                            if (cln2 > 0 && cln2 > spc)
                                            {
                                                msg.Subject = message.Substring(spc + 1, cln2 - spc - 1).Trim();
                                                msg.Message = message.Substring(cln2 + 1).Trim();
                                                msg.Code = 5;
                                            }
                                            else
                                            {
                                                // it's a command
                                                msg.Message = message.Substring(spc + 1).Trim();
                                                msg.Code = 6;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Weird message
                                msg.Code = 99;
                                msg.Message = message;
                            }

                            if (DebugLevel > 5)
                                WriteDebug(string.Format("<-{0}\r\n           Nick: {1}\r\n           Name: {2}\r\n           Address: {3}\r\n           Subject: {4}\r\n           Code: {5}\r\n           Message: {6}\r\n", message, msg.Sender.NickName, msg.Sender.Name, msg.Sender.Address, msg.Subject, msg.Code, msg.Message));

                            // Code 4 - User commands from Server
                            if (msg.Code == 2)
                            {
                                // Possible command
                                if (msg.Subject.Equals("JOIN") && UserHost.Length == 0)
                                {
                                    if (msg.Sender.Name.Contains("@"))
                                    {
                                        UserHost = msg.Sender.Name.Substring(msg.Sender.Name.IndexOf("@") + 1);
                                        irc.SetHost(UserHost);

                                        //if (DebugLevel > 3) WriteDebug("Setting UserHost to " + UserHost);
                                    }
                                }
                            }

                            InMsgQueue.Add(msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddError(51, ex.Message, GetType().Name + ".READCHAT");
                    }
                }
            }

            inThread = false;
        }


        //ComIRC does not use -----------------------------------------
        public override int AddRecipient(string address)
        {
            int Result = 999;
            return Result;
        }

        public override int ACKMessage(int ErrorCode)
        {
            int Result = 999;
            return Result;
        }
    }
}
