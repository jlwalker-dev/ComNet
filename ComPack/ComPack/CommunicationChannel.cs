/*
 * Basic template for all communication classes
 * 
 * Not all forms of communications will use all of the methods/properties
 * but all forms must support them, even if all they do is throw exceptions
 * or return error codes.
 * 
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ComPack
{
    public abstract class CommunicationChannel
    {
        public int DebugLevel = 9; // change to 1 before putting into production

        //public int MessageCounter = 0;
        public int Type = 0;
        public string Name = string.Empty;
        public string Server = string.Empty;    // Server address (URL or UNC based)
        public string UserName = string.Empty;
        public string UserNick = string.Empty;
        public string UserHost = string.Empty;
        public string Room = string.Empty;      // Room or channel - only one supported per instance
        public bool isOpen = false;
        public bool Connected = false;          // indicates if you're connected to the server
        public byte MsgFormat = 0xFF;           // see docs below
        public long MsgCount = 0L;

        // Encryption support
        public Cryptor cryptor = new Cryptor();
        public string EncryptInfo = string.Empty;
        public bool SetEncrypt = false;
        public bool isEncrypted = false;


        //public List<CommunicationMessage> ReceivedMessages = null;
        //public List<CommunicationMessage> SentMessages = null;
        public CommunicationRecipient MyInfo = new CommunicationRecipient();
        public List<CommunicationRecipient> Contacts = null;
        public CommunicationMessage CurrentMsg = null;
        public List<ErrorInfo> ErrorList = new List<ErrorInfo>();

        public List<CommunicationMessage> InMsgQueue = new List<CommunicationMessage>();
        public List<CommunicationMessage> OutMsgQueue = new List<CommunicationMessage>();


        public readonly string MachineName = Environment.MachineName.Replace(" ", "_");
        public readonly string InstanceID = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("/", "_");
        public readonly string DLLPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";

        // TCP port designations
        public int PortIn = 0;
        public int PortOut = 0;
        public int PortHandShake = 0;
        public int Protocol = 0;

        // other settings
        public bool async = true;
        public long TimeOut = 30000;  // 30 second timeout

        public abstract int Open(); // open the channel
        public abstract bool Close(); // close the channel
        public abstract bool LogIn(string user, string password); // log into the server
        public abstract int MessagesWaiting(); // how many messages are waiting in the queue
        public abstract int ACKMessage(int ErrorCode);  // used by some protocols for acknowledgements
        public abstract bool GetMessage(); // Get the next message in the FIFO stack
        public abstract int SendMessage(); // Send all messages from the outbound stack
        public abstract string GetContactList();  // create a list of contacts for the host program
        public abstract int CreateMessage(string contact, string body, string subject); // Create a new message and place it on the outbound stack
        public abstract int AddRecipient(string contact); // Add a recipient to a message for those protocols that allow it

        /*
        * Need to see if there is some way to extend the functionality
        * of a class method so that whatever is new or different in
        * the subclass is all that ends up being put in.
        * 
        * We accept anything and turn it into a string, then convert the
        * string to other values, if possible.
        * 
        * This then updates the appropriate property for the class
        * and provides an easy to use understand process for programmers.
        * 
        */
        public void Setting(string setting, dynamic value)
        {
            // Fix whatever is sent to be in a usable format for each setting
            string vtype = string.Format("{0}", value.GetType());
            int ivalue;
            long lvalue;
            float fvalue;
            bool bvalue = false;
            string svalue = value.ToString();
            string svaluetest = svalue.ToUpper().Trim();

            int.TryParse(svaluetest, out ivalue);
            long.TryParse(svaluetest, out lvalue);
            float.TryParse(svaluetest, out fvalue);


            if (svaluetest == "TRUE" || svaluetest == "1" || svaluetest == "T" || svaluetest == "Y" || svaluetest == "YES" || ivalue > 0)
                bvalue = true;

            // process the setting
            try
            {
                switch (setting.ToUpper())
                {
                    case "ASYNC":
                        async = bvalue;
                        break;

                    case "CHANNEL":
                        Room = svalue.Trim();
                        break;

                    case "DEBUG":
                        DebugLevel = ivalue;
                        break;

                    case "ENCRYPT":
                        EncryptInfo = svalue.Trim();
                        SetEncrypt = true;
                        break;

                    case "MSGFORMAT":
                        MsgFormat = Convert.ToByte(ivalue.ToString());
                        break;

                    case "NICK":
                        UserNick = (svalue.Length > 2 ? svalue : UserNick).Trim().Replace(" ", ".");
                        break;

                    case "PORTIN":
                        PortIn = ivalue;
                        break;

                    case "PORT":
                    case "PORTOUT":
                        PortOut = ivalue;
                        break;

                    case "PORTHANDSHAKE":
                        PortHandShake = ivalue;
                        break;

                    case "PROTOCOL":
                        Protocol = ivalue;
                        break;

                    case "SERVER":
                        Server = svalue;
                        Server = Server.Trim();

                        // This checks for UNC & drive:path entries and makes sure they end with a backslash
                        if (Server.Length > 1)
                            if (Server.Substring(1, 1).Equals(":") || Server.Substring(0, 2) == @"\\")
                                if (Server.Substring(Server.Length - 1, 1) != @"\") Server += @"\";
                        break;

                    case "TIMEOUT":
                        TimeOut = lvalue;
                        break;

                    case "USERNAME":    // don't let them change user name once set
                        if (UserName.Length == 0)
                        {
                            UserName = (svalue.Length > 2 ? svalue : UserName).Trim();
                            UserNick = (UserNick.Length == 0 ? UserName : UserNick);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (DebugLevel > 0)
                {
                    WriteDebug(string.Format("SETTING error - Failed setting {0} with value {1} of type ({2})", setting, value, GetStaticType(value)));
                    WriteDebug("   Error: " + ex.Message);
                }
            }
        }


        /*
         * Return the setting requested by the calling program
        */
        public dynamic Setting(string setting)
        {
            switch (setting.ToUpper())
            {
                case "ASYNC":
                    return async;

                case "CHANNEL":
                    return Room;

                case "DEBUG":
                    return DebugLevel;

                case "ENCRYPT":
                    return isEncrypted;

                case "MSGCOUNT":
                    return MsgCount;

                case "MSGFORMAT":
                    return MsgFormat.ToString("X2");

                case "NAME":
                    return Name;

                case "NICK":
                    return UserNick;

                case "PORT":
                case "PORTOUT":
                    return PortOut;

                case "PORTIN":
                    return PortIn;

                case "PROTOCOL":
                    return Protocol;

                case "SERVER":
                    return Server;

                case "TYPE":
                    return Type;

                case "USERNAME":
                    return UserName;

                default:
                    return "Uknown property " + setting.ToUpper();
            }
        }

        /*
         * 
         * Output debug to a log file... each class has a different file based on the InstanceID
         * 
         */
        public void WriteDebug(string info)
        {
            // 0 = no output at all
            if (DebugLevel != 0)
            {
                DateTime date = DateTime.Now;
                string now = date.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logFileName = DLLPath + date.ToString("MMdd") + "-" + UserName.Replace("@", "+").Replace(" ", ".") + "_" + MachineName + ".log";
                string infoOut = now + " - ";

                info = info.Replace("\n", String.Empty);

                // if there are leading EOLs then transfer them to before
                // the date and time stamp before saving
                while (info.Length > 0 && info[0] == '\r')
                {
                    infoOut = "\n\r" + infoOut;
                    info = info.Substring(1);
                }

                // why no replace("\r","\n\r")?
                infoOut += info;

                using (StreamWriter file = new StreamWriter(logFileName, true))
                {
                    file.WriteLine(infoOut);
                }
            }
        }


        /*
         * 
         * Delete log files over a certain number of days in age
         * 
         */
        public void ClearOldLogs()
        {
            DateTime date = DateTime.Now;
            // grab all matching files
            string[] files = Directory.GetFiles(DLLPath, "*.log");
            string file;
            int j;

            // is it too old?
            for (int i = 0; i < files.Length; i++)
            {
                j = files[i].IndexOf('\\');

                if (j >= 0)
                {
                    file = files[i].Substring(j + 1);
                }
                else
                {
                    file = files[i];
                }

                if (file.Length > 4 && file.Substring(4, 1) == "-")
                {
                    if (file.Substring(0, 4) != date.ToString("MMdd"))
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            if (DebugLevel > 0)
                                WriteDebug("Can't delete file " + file + "\r\n" + "Exception - " + ex.Message);
                        }
                    }
                }
            }
        }


        // ---------- Error Handling -------------------------------------
        public void AddError(int code)
        {
            AddError(code, "", "", "");
        }

        public void AddError(int code, string message)
        {
            AddError(code, message, "", "");
        }
        public void AddError(int code, string message, string method)
        {
            AddError(code, message, method, "");
        }

        /*
         * Common routine to add an error report to a list and
         * make them available to the host program... also 
         * writes the info to the debug file
         * 
         */
        public void AddError(int code, string message, string method, string explaination)
        {
            ErrorList.Add(new ErrorInfo { Code = code, Message = message, Method = method, Explaination = explaination });

            if (ErrorList.Count > 50)
            {
                // we hold the last 50 errors, nothing more
                ErrorList.RemoveAt(ErrorList.Count - 1);
            }

            if (DebugLevel > 0)
            {
                WriteDebug(string.Format("ERROR: {0}{1}{2}{3}", code,
                    (method.Length > 0 ? " in Method " + method : ""),
                    (message.Length > 0 ? " - " + message : ""),
                    (explaination.Length > 0 ? "\r\n" + explaination : "")));
            }
        }

        // helper type to return type of var
        public Type GetStaticType<T>(T x) { return typeof(T); }

        // Return if bit is set in a byte
        public bool IsBitSet(byte b, byte nPos)
        {
            return new BitArray(new[] { b })[nPos];
        }

        /*
         * When sending multipart fields (ex: Name [{nick}] [<address>]) each
         * part must be pipe delimited, such as the following:
         *
         *      From: Jon.Walker|{JonW}|<JLW61@yahoo.com>
         *
         * Certain charactes are translated in the fields using x99 format as needed:
         *     ex: x20=space  x27= single quote   x24=double quote  |=7C
         *
         * An example would be:
         *
         *      From: Terryx20Ox27yDell {JonW} <JLW61@yahoo.com>
         *
         * bit control
         * 76543210
         * 10000000 = send headers otherwise just end each line with chr(13)
         * x1000000 = 
         * x0100000 =  
         * x0010000 = send info - Error Code | Msg Code
         * x0001000 = send IID  - (instance ID)
         * x0000100 = send WSID - Machine Name | IP4Address | IP6Adddress | MAC
         * x0000010 = send From - Name | nick | address
         * x0000001 = send subject
         * x0000000 = send body of message, nothing else
         * 
         * Typically, if mask = 0b_0000_0000 then just the message is sent
         * 
         */
        public void SetFormat(byte b) { MsgFormat = b; }


        // ---------- Encryption routines ------------------------------------
        /*
         * Set encryption key based on reading in a text file and 
         * grabbing some characters at an offset for length
         * 
         * That way, if two people have the same text file, they 
         * can create a key without transmitting it
         * 
         */
        public bool SetEncryption(string fname, int loc, int len)
        {
            bool Result = false;

            try
            {
                if (File.Exists(fname) && loc >= 0 && len >= 8)
                {
                    string text = File.ReadAllText(fname).Replace("\n", "").Replace("\r", " ");
                    text = Regex.Replace(text, "[^a-zA-Z0-9 .]", string.Empty).Replace(" ", "_");

                    if (text.Length >= loc + len)
                    {
                        string pp = text.Substring(loc, len);
                        WriteDebug("Setting passphrase to " + pp);
                        cryptor.SetPassPhrase(pp);
                        Result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                AddError(902, ex.Message, GetType().Name + ".CREATEMESSAGE", "Failed to set encryption from dictionary " + fname);
            }


            isEncrypted = Result;
            return Result;
        }

        /*
         * Set the key based on a string provided by the user
         * or a config file entry
         */
        public bool SetEncryption(string key)
        {
            try
            {
                cryptor.SetPassPhrase(key);
                isEncrypted = true;
            }
            catch (Exception ex)
            {
                AddError(901, ex.Message, GetType().Name + ".CREATEMESSAGE", "Failed to set encryption");
                isEncrypted = false;
            }
            return isEncrypted;
        }

        /*
        * Try to decrypt a string that starts with [CRYPTOR
        * 
        */
        public string cryptText(string message)
        {
            string Result = message;

            // Get the encrypted part of the message to variable m
            string m = (message.Length > 7 && message.Substring(0, 8).Equals("[CRYPTOR") ? message.Substring(8) : message);  // kill the [CRYPTOR lead-in

            if (m.Length > 1)
            {
                if (m.Substring(0, 1).Equals("]"))
                {
                    m = m.Substring(1);

                    // Decrypt the message
                    try
                    {
                        if (isEncrypted)
                        {
                            try
                            {
                                if (DebugLevel > 8) WriteDebug("Calling decrypt");
                                Result = cryptor.DecryptString(m);
                            }
                            catch (Exception ex)
                            {
                                // failed to decrypt
                                AddError(59, ex.Message, GetType().Name + ".READCHAT", "Failed to decrypt message" + message);
                            }
                        }
                        else
                        {
                            AddError(59, "No encryption set", GetType().Name + ".READCHAT", "No encryption set for message " + message);
                        }
                    }
                    catch (Exception ex)
                    {
                        // error, let user know
                        Result += "\r\nError: " + ex.Message;
                    }
                }
                else if (m.ToUpper().Contains("SET]") && m.Length > 4)
                {
                    // Set the passphrase based on a common ditionary
                    string k = m.Substring(4);
                    int loc;
                    int.TryParse(m, out loc);
                    Result = string.Format("Setting encryption loc {0}", loc);
                    if (DebugLevel > 3) WriteDebug(Result);
                    SetEncryption("IRCrypt.dat", loc, 20);
                }
            }
            else
            {
                AddError(59, "Invalid CRYPTOR message", GetType().Name + ".READCHAT", "Received invalid Cryptor message: " + CurrentMsg.Message);
                Result = message + "\r\nInvalid CRYPTOR message";
            }

            return Result;
        }
    }
}
