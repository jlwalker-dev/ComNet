/*
 */
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;


/* 
 * This is the class template that is used to set up and track communications instances
 *
 * If async flag is false, then we expect this to be a send/respond format similar to
 * the FoxWeb project where a client sends a message and waits for the server to
 * respond, using the same msg file for the response.
 *  
 *  This protocol does not currently support the outbound stack... only one outbound 
 *  message is currently supported
 *  
 *  History
 *      11/9/2020
 *          Actual message format of file is not altered by MsgFormat.  That only
 *          affects what is sent to calling program.
 *          
 *          Updated Sync/Async logic so that if a sync message comes while in async
 *          mode, it get's an error msg back and the message is ignored.
 *          
 *  TODO
 *
 *      Login authentication?
 *      
 *      Erase handshake files over 20 min old?
 *      
 *      Support outbound stack for Asynch messages
 *      
 *      Create FIFO queue for inbound messages
 *        - If queue is empty, read in all waiting messages
 *        - Getmessage reads in from queue and deletes semaphore for top message
 *          from FIFO quque and then pops message off the queue
 *     
 *      Sync mode returns an error if you try to get another message before replying
 *      to the current message.
 *      
 *      Sync messages have an error level in the response
 *      
 */
namespace ComPack
{
    public class ComFile : CommunicationChannel
    {
        //private readonly string MessagePrefix = "_MSG-";
        private readonly string MessageExtension = ".MSG";
        private readonly string SendSemaphore = ".SEM";
        private readonly string ReplySemaphore = ".RPL";
        private readonly string HandShakeExtension = ".HSK";

        // Number of waiting messages allowed to be sent out to someone
        private string HSFile;

        public ComFile()
        {
            Type = 1;
            Name = "Send/Respond Text File System";
            ClearOldLogs();
        }

        /*
         * Create a handshake file
         */
        public override int Open()
        {
            int Result = 0;


            if (Server.Length > 2)
            {
                if (UserName.Length > 2)
                {
                    // This filename is unique for the userID and Machine combination, but if the same user
                    // has more than one instance on the same machine, then the two will not play well together
                    //
                    // I could have used instance ID for the name to make everything unique, but then if the instance
                    // goes down and restarts, it would not have access to the old messages upon return and if it went 
                    // down due to failure, then bringing it back up would not remove or reuse the old handshake file
                    //
                    HSFile = UserName.Replace("@", "+").Replace(" ", ".") + "_" + MachineName;

                    if (DebugLevel > 0) WriteDebug("---New Channel created ---");

                    if (DebugLevel > 1)
                    {
                        WriteDebug("   ID  =" + InstanceID);
                        WriteDebug("   Type=" + Type.ToString());
                        WriteDebug("   Name=" + Name.ToString());
                        WriteDebug("   Path=" + DLLPath.ToString());
                        WriteDebug("   HSFile=" + HSFile);
                    }

                    MyInfo.Name = UserName;
                    MyInfo.MachineID = MachineName;
                    MyInfo.Address = InstanceID;

                    Result = (HandShakeFiler() ? 0 : 9);
                }
                else
                {
                    AddError(Result = 1, "Username is not defined (Must be 3 or more characters)", GetType().Name + ".OPEN");
                }
            }
            else
            {
                AddError(Result = 2, "Server is not defined (Must be 3 or more characters)", GetType().Name + ".OPEN");
            }

            return Result;
        }

        /*
         * Create or update the Handshake file
        */
        private bool HandShakeFiler()
        {
            bool Result = true;
            TimeZone localZone = TimeZone.CurrentTimeZone;
            DateTime currentDate = DateTime.Now;
            TimeSpan currentOffset = localZone.GetUtcOffset(currentDate);
            string timeStamp = string.Format("{0} {1}", currentDate, currentOffset);

            try
            {
                File.WriteAllText(Server + HSFile + HandShakeExtension, timeStamp + "\r" + UserName + "\r" + Environment.MachineName + "\r" + InstanceID, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AddError(21, ex.Message, "Failed to create handshake file " + HSFile + HandShakeExtension, GetType().Name + ".HANDSHAKEFLIER");
                Result = false;
            }

            return Connected = Result;
        }


        /*
         * Close the channel and remove the HandShake file
         */
        public override bool Close()
        {
            bool Result = true;
            HandShakeFiler();

            if (Connected)
            {
                // Delete the handshake file
                try
                {
                    System.IO.File.Delete(Server + HSFile + HandShakeExtension);
                }
                catch (Exception ex)
                {
                    AddError(22, ex.Message, "Can't delete file " + Server + HSFile + HandShakeExtension, GetType().Name + ".CLOSE");
                    Result = false;
                }
            }

            return Connected = Result;
        }

        /*
         * Returns count of waiting messages
         * 
         */
        public override int MessagesWaiting()
        {
            int Result = 0;

            HandShakeFiler();

            if (Connected)
            {
                if (async == false)
                {
                    // TODO - look for ReplySemaphor files that are for this instance
                    // by traversing the waiting sent messages list and process the reply
                }

                // get a directory of all SendSemaphor files that are for this instance
                string[] msgFiles = Directory.GetFiles(Server, HSFile + "*" + SendSemaphore);
                Result = msgFiles.Length;
                WriteDebug(Result.ToString() + " messages waiting");
            }
            else
            {
                // Not open error
                AddError(16, "Communications channel is not open", GetType().Name + ".MESSAGEWAITING");
            }

            //return (ReceivedMessages == null ? 0 : ReceivedMessages.Count);
            return Result;
        }


        /*
         * This is used for Ack/Nak protocols.  In the file system, it is 
         * used to send a reply during synch mode
         * 
         * Once a message has a reply, pop it off the FIFO stack
         * 
         * TODO --------------- make sure this actually does something -------------------
         * 
         */
        public override int ACKMessage(int ErrorCode)
        {
            int Result = 2;

            HandShakeFiler();

            if (Connected)
            {
                // switch sender with recipient
                List<CommunicationRecipient> s = new List<CommunicationRecipient> { CurrentMsg.Sender };
                CurrentMsg.Sender = CurrentMsg.Recipient[0];
                CurrentMsg.Recipient = s;
                CurrentMsg.ErrorCode = ErrorCode;

                // send message using reply semephore
                SendMessageOut(true);
            }
            else
            {
                // Not open error
                AddError(Result = 31, "Communications channel is not open", GetType().Name + ".ACKMESSAGE");
            }

            return Result;
        }


        /*
         * Get the message at the top of the stack and toss it if callresponseonly=false
         */
        public override bool GetMessage()
        {
            bool Result = false;
            int i = 0;
            CurrentMsg = null;

            HandShakeFiler();

            if (Connected)
            {
                GetContactList();

                // Get list of waiting messages for this user/machine
                string[] msgFiles = Directory.GetFiles(Server, HSFile + "*" + SendSemaphore);

                if (msgFiles.Length > 0)
                {
                    while (Result == false && i < msgFiles.Length)
                    {
                        // keep reading until we run across a usable message
                        Result = BreakMessage(msgFiles[i++].Replace(SendSemaphore, MessageExtension));
                    }
                }
            }

            return Result;
        }

        /*
         * breaks up the text from the current message and puts it into the currentmsg structure
         */
        private bool BreakMessage(string fName)
        {
            // read in the message file
            //string[] fileParts = msgFiles[0].Replace(SendSemaphore, "").Split('-');
            string text = File.ReadAllText(fName).Replace("\n", "");
            string[] lines = text.Split('\r');
            string body = string.Empty;
            string subject = string.Empty;
            string address = string.Empty;
            string from = string.Empty;
            string wsid = string.Empty;
            string line = string.Empty;
            int e;
            DateTime d;

            CommunicationRecipient rec = new CommunicationRecipient();

            // break up the message file
            CurrentMsg = new CommunicationMessage
            {
                ErrorCode = 1,
                ID = fName
            };

            for (int i = 0; i < lines.Length; i++)
            {
                WriteDebug(string.Format("Line {0} -> {1}", i, lines[i]));

                if (lines[i].Substring(0, 4).Equals("SYNC"))
                {
                    CurrentMsg.Async = false;

                    if (lines[i].Contains("|"))
                    {
                        string[] s = lines[i].Split('|');
                        if (int.TryParse(s[1], out e))
                            CurrentMsg.ErrorCode = e;
                    }
                }
                else if (lines[i].Substring(0, 4).Equals("TIME:"))
                {
                    if (DateTime.TryParse(lines[i], out d))
                        CurrentMsg.MessageTime = d;
                }
                else if (lines[i].Substring(0, 4).Equals("IID:"))
                {
                    rec.Address = lines[i].Substring(4).Trim();
                }
                else if (lines[i].Substring(0, 5).Equals("WSID:"))
                {
                    // break out other addresses
                    rec.MachineID = lines[i].Substring(5).Trim();
                }
                else if (lines[i].Substring(0, 5).Equals("FROM:"))
                {
                    rec.Name = lines[i].Substring(5).Trim();

                    if (rec.Name.Contains("|"))
                    {
                        string[] s = rec.Name.Split('|');
                        rec.NickName = s[0];
                        rec.Name = s[1];
                    }
                }
                else if (lines[i].Substring(0, 8).Equals("SUBJECT:"))
                {
                    CurrentMsg.Subject = lines[i].Substring(8).Trim();
                }
                else if (lines[i].Substring(0, 5).Equals("BODY:"))
                {
                    CurrentMsg.Message = lines[i].Substring(5).Trim();
                    CurrentMsg.ErrorCode = 0;
                }
                else
                {
                    // add to the message
                    CurrentMsg.Message += "\r" + lines[i];
                }
            }

            if (rec.Address.Length > 18 && (rec.Name.Length == 0 || rec.MachineID.Length == 0))
            {
                // Search by address and fill in info
                for (int i = 0; i < Contacts.Count; i++)
                {
                    if (Contacts[i].Address == address)
                    {
                        rec = Contacts[i];
                        break;
                    }
                }
            }

            CurrentMsg.Sender = rec;

            if (CurrentMsg.Async != async && async)
            {
                // Received an Synchronous msg and we're in sync mode
                // so return an error msg immediately and kill the
                // message so that it does not go anywhere
                CurrentMsg.Message = "Recipient is not set for synchronous communications";
                CurrentMsg.ErrorCode = 1001;
                SendMessageOut(true);
                CurrentMsg = null;
            }

            // Remark on what we found
            if (DebugLevel > 4 && CurrentMsg != null)
            {
                WriteDebug("------------------------------------------------------------------------------");
                WriteDebug("GETMESSAGE");
                WriteDebug("  File  =" + fName);
                WriteDebug("  Error  =" + CurrentMsg.ErrorCode.ToString());
                WriteDebug("  Msg ID =" + CurrentMsg.ID);
                WriteDebug("  From   =" + CurrentMsg.Sender.Name);
                WriteDebug("  Inst ID=" + CurrentMsg.Sender.Address);
                WriteDebug("  MachID =" + CurrentMsg.Sender.MachineID);
                WriteDebug("  Subject=" + CurrentMsg.Subject);
                WriteDebug("  Body   =" + CurrentMsg.Message);
                WriteDebug("------------------------------------------------------------------------------");
            }


            // delete the semephore file
            try
            {
                System.IO.File.Delete(fName);

            }
            catch (Exception ex)
            {
                AddError(33, ex.Message, GetType().Name + ".GETMESSAGE", "Can't delete file " + fName);
            }

            // if async delete the message file
            if (async)
            {
                try
                {
                    System.IO.File.Delete(fName.Replace(SendSemaphore, MessageExtension));
                }
                catch (Exception ex)
                {
                    AddError(34, ex.Message, GetType().Name + ".GETEMESSAGE", "Can't delete file " + fName.Replace(SendSemaphore, MessageExtension));
                }
            }

            return CurrentMsg != null;
        }


        /*
         * Create a new message structure and fill it in
         */
        public override int CreateMessage(string address, string body, string subject)
        {
            int Result = 0;
            GetContactList();

            try
            {
                Result = 1;
                CurrentMsg = new CommunicationMessage
                {
                    Message = body,
                    Subject = subject,
                    Sender = MyInfo
                };


                Result = AddRecipient(address);
            }
            catch (Exception ex)
            {
                AddError(Result = 35, ex.Message, GetType().Name + ".CREATEMESSAGE", "Failed to create message -> " + body);
            }

            // any failure during creation kills the message
            if (Result > 0) CurrentMsg = null;

            return Result;
        }

        /*
         * Add a recipient to the most recent outbound message
         */
        public override int AddRecipient(string address)
        {
            int Result = 3;

            if (DebugLevel > 8) WriteDebug("ADDRECIPIENT - Looking for " + address);

            for (int i = 0; i < Contacts.Count; i++)
            {
                if (DebugLevel > 8) WriteDebug(string.Format("Contact {0} - {1}", i, Contacts[i].Address));

                if (address.Equals(Contacts[i].Address))
                {
                    try
                    {
                        WriteDebug("   ADDING " + Contacts[i].Name);
                        CurrentMsg.Recipient.Add(Contacts[i]);
                        Result = 0;
                        break;
                    }
                    catch (Exception ex)
                    {
                        AddError(Result = 35, ex.Message, GetType().Name + ".ADDRECIPIENT");
                    }
                }
            }

            return Result;
        }


        /*
         * Send all waiting messages
         */
        public override int SendMessage()
        {
            return SendMessageOut(false);
        }


        /*
         * This protocol requires fields & IID for async
         */
        public new void SetFormat(byte b)
        {
            MsgFormat = b;
            MsgFormat &= 0b_1000_1111;
            if (async) MsgFormat |= 0b_1000_1000; // bits 7 & 3 required for async
        }

        /*
         * Write a message out to the disk - if sendReply (Sync mode) then return
         * the response with the same file name and use the reply semephore.
         * 
         * MsgFormat only alters what is sent to the calling program.  All messages
         * are transferred in one format which sends pretty much everything.
         * 
         * TODO if too many similar, return message box full
         * 
         */
        public int SendMessageOut(bool sendReply)
        {
            int Result = 3;

            HandShakeFiler();

            if (Connected)
            {

                if (CurrentMsg != null)
                {
                    try
                    {
                        string msg = (async ? "ASYNC" : "SYNC") + "|" + CurrentMsg.ErrorCode + "\r\n"
                                + "TIME: " + DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffffffK") + "\r\n"
                                + "IID:" + InstanceID + "\r\n"
                                + "WSID:" + CurrentMsg.Sender.MachineID + "\r\n"
                                + "IP:" + CurrentMsg.Sender.IP4Address + "\r\n"
                                + "FROM:" + CurrentMsg.Sender.NickName + "|" + CurrentMsg.Sender.Name + "\r\n"
                                + "BODY:" + CurrentMsg.Message;

                        if (sendReply)
                        {
                            // Sending a Reply in same file name and using reply semaphore
                            // A reply is just the body, nothing else
                            File.WriteAllText(CurrentMsg.ID, msg, Encoding.UTF8);
                            File.WriteAllText(CurrentMsg.ID.Replace(MessageExtension, ReplySemaphore), "", Encoding.UTF8);
                        }
                        else
                        {
                            // Sending out a new message to someone
                            MsgCount++; // Update the msg count to create unique file names for messages

                            string fName =
                                Server + CurrentMsg.Recipient[0].Name.Replace("@", "+").Replace(" ", ".") + "_"
                              + CurrentMsg.Recipient[0].MachineID + "-"
                              + (MsgCount.ToString() + "0000000000").Substring(0, 10);

                            // Send normal message extension and semaphore
                            File.WriteAllText(fName + MessageExtension, msg, Encoding.UTF8);
                            File.WriteAllText(fName + SendSemaphore, "", Encoding.UTF8);

                            string rName = fName.Replace(MessageExtension, ReplySemaphore);

                            // if asynch=false then wait for the reply up to the inicated timeout period
                            if (async == false)
                            {
                                DateTime waitUntil = DateTime.Now.AddMilliseconds(TimeOut);

                                while (DateTime.Now < waitUntil && File.Exists(rName) == false) ;

                                if (File.Exists(rName))
                                {
                                    // did it throw an error?
                                    CurrentMsg = null;
                                    Result = (BreakMessage(fName) ? 0 : 6);
                                }
                            }
                            else
                            {
                                CurrentMsg = null;
                                Result = 0;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddError(Result = 2, ex.Message, GetType().Name + ".SENDMESSAGE");
                    }
                }
            }
            else
            {
                // Not open error
                AddError(36, "Communication channel is not open", GetType().Name + ".SENDEMESSAGE");
            }

            return Result;
        }



        /*
         * This protocol doesn't currently support any kind of login
         * but the Username is part of the debug file name so we
         * want them to "login"
         * 
         * TODO - add handshakefiler & connected
         */
        public override bool LogIn(string user, string password)
        {
            bool Result = true;
            string illegalChars = @"?\/:%$#[]{};'" + "\"";
            UserName = user;

            for (int i = 0; i < UserName.Length; i++)
            {
                if ((int)UserName[i] < 32 || (int)UserName[i] > 127)
                {
                    // if any character is outside of 32 - 127 ASCII value
                    // then give the UserName some failing characters
                    UserName += illegalChars;
                }
            }

            for (int i = 0; i < illegalChars.Length; i++)
            {
                UserName.Replace(illegalChars.Substring(i, 1), "");
            }


            if (UserName != user)
            {
                if (DebugLevel > 0) WriteDebug("LOGIN error - Username contains at least one illegal character.");
                Result = false;
                UserName = string.Empty;
            }

            if (UserName.Length < 3)
            {
                if (DebugLevel > 0) WriteDebug("LOGIN error - Username is less than 3 characters.");
                Result = false;
                UserName = string.Empty;
            }

            return Result;
        }


        /*
         * Return a formatted list of current users
         * Info comes from the handshake files
         */
        public override string GetContactList()
        {
            string[] files = Directory.GetFiles(Server, "*" + HandShakeExtension);
            string filetext;
            string contactList = string.Empty;
            Contacts = new List<CommunicationRecipient>();

            if (Connected)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    filetext = File.ReadAllText(files[i]);
                    string[] filelines = (filetext.Replace((char)10, ' ') + "\r\r\r\r").Split((char)13);  // Add several return characters and split on them

                    // Put this users info on top, otherwise add the user info to the bottom of the list
                    // List format is UserName|MachineID|InstanceID (Address)|FileName[|"offline"]
                    if (Server + HSFile + HandShakeExtension == files[i])
                    {
                        // This is the current instance
                        contactList = (filelines[1].Length == 0 ? string.Format("User[{0}]", i) : filelines[1].Trim()) + "|"
                            + (filelines[2].Length == 0 ? string.Format("MachID[{0}]", i) : filelines[2].Trim()) + "|"
                            + (filelines[3].Length == 0 ? string.Format("Instance[{0}]", i) : filelines[3].Trim()) + "|"
                            + files[i].Replace(HandShakeExtension, "").Replace(Server, "") + "\r" + contactList;
                    }
                    else
                    {
                        // TODO - if a handshake file is over a certain age, it should be marked as off-line
                        // This is someone else
                        contactList += (filelines[1].Length == 0 ? string.Format("User[{0}]", i) : filelines[1].Trim()) + "|"
                            + (filelines[2].Length == 0 ? string.Format("MachID[{0}]", i) : filelines[2].Trim()) + "|"
                            + (filelines[3].Length == 0 ? string.Format("Instance[{0}]", i) : filelines[3].Trim()) + "|"
                            + files[i].Replace(HandShakeExtension, "").Replace(Server, "") + "\r";

                        // Add everyone else to a List<> for internal use
                        CommunicationRecipient con = new CommunicationRecipient();
                        con.Name = (filelines[1].Length == 0 ? string.Format("User[{0}]", i) : filelines[1].Trim());
                        con.Address = (filelines[3].Length == 0 ? string.Format("Instance[{0}]", i) : filelines[3].Trim());  // Unique Key
                        con.MachineID = (filelines[2].Length == 0 ? string.Format("MachID[{0}]", i) : filelines[2].Trim());
                        con.IP4Address = files[i].Replace(HandShakeExtension, "").Replace(Server, "");
                        Contacts.Add(con);
                    }
                }
            }

            return contactList;
        }
    }
}
