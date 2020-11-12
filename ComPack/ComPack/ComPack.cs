/*
 * Communications DLL designed to provide communications with other
 * applications on the workstaion, network, internet.
 * 
 * The IRC protocol is for internet communications.
 * Workstation and network communications are typically set up
 * with a configuration file.  
 * 
 * An XML file will provide basic configuration.  If it doesn't
 * exist, the configuration must be set by the calling exe.
 * 
 * Some communication methods will allow you to simply set
 * up via the Settings command.
 * 
 * Each module identifies itself as follows:
 *      Type    Class       Name
 *      1       ComFile     Send/Respond Text File System
 *      2       ComIRC      IRC Client
 *      3       ComChat     Chat Client 2 ports
 *      4       ComTCP      Chat Client 1 port
 *      5       ComSMS      SMS Client (dream on, baby)
 * 
 *      
 * To Do
 *      Add CRC class
 *      Add dictionary auto exchange class
 *      
 *      ========== REMOVE SUBJECT PARAMETER ==========
 *            
 */
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;

namespace ComPack
{
    [ComVisible(true)]  // This is mandatory.exs
    [ClassInterface(ClassInterfaceType.None)]
    public class ComPack
    {
        public CommunicationChannel Channel = null;
        private readonly string LocalPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        private readonly string InstanceID = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("/", "_");
        private bool startupDebug = true;

        // Constructor to set up DLL
        public ComPack()
        {
            startupDebug = File.Exists(LocalPath + "Startup.log");  // If it exists, we're adding to it
            if (startupDebug) dbug("Starting " + GetType().Name);
        }

        /*
         * Public method that sets up the system.  If called after inital setup then
         * everything is closed out and restarted from scratch as if it was first
         * time startup.
         */
        public int InitChannel(string ConfigFileUNC)
        {
            int Result = 0;

            if (startupDebug) dbug(GetType().Name + ".InitChannel");

            // Close everything up
            //if (Channel != null) CloseAll();

            if (Result == 0)
            {
                // Is there a supplied Configuration File Name and does it exist?
                if (ConfigFileUNC != null && File.Exists(ConfigFileUNC))
                {
                    try
                    {
                        if (startupDebug) dbug("InitChannel - setup " + ConfigFileUNC);

                        // found the file, open it up and process the [COMFILE] section
                        SetUpChannel(ConfigFileUNC, "Properties");
                    }
                    catch (Exception ex)
                    {
                        Result = -2;
                        if (startupDebug) dbug("InitChannel Error: -2 - " + ex.Message + "\r\nFrom " + ex.Source);
                    }
                }
                else
                {
                    // If no config name supplied or found, look for ComPack_Config.xml and set the globals
                    if (File.Exists(LocalPath + "ComPack_Config.xml"))
                    {
                        try
                        {
                            // Look for [COMFILE] section and set things up
                            SetUpChannel(LocalPath + "ComPack_Config.xml", "Properties");
                        }
                        catch (Exception ex)
                        {
                            Result = -3;
                            if (startupDebug) dbug("InitChannel Error: -3 - " + ex.Message + "\r\nFrom " + ex.Source);
                        }
                    }
                    else
                    {
                        try
                        {
                            if (ConfigFileUNC != null && ConfigFileUNC.Length > 0)
                                Result = 4; // Config file not found
                        }
                        catch (Exception ex)
                        {
                            Result = -4;
                            if (startupDebug) dbug("InitChannel Error: -4 - " + ex.Message + "\r\nFrom " + ex.Source);
                        }
                    }
                }
            }
            return Result;
        }


        /*
         * Set up the channel by simply traversing the XML node and calling
         * the Settings method for each entry.  Example XML file will look
         * like the following:
         * 
         * <Table>
         *   <Channel>
         *     <Type>1</Type>
         *   </Channel>
         *   <Setup>
         *     <Server>c:\temp</Server>
         *     <Debug>9</Debug>
         *   </Setup>
         * </Table>
         * 
         */
        private void SetUpChannel(string ConfigFileUNC, string SectionName)
        {
            Channel = null;

            XmlDocument xmldoc = new XmlDocument();
            XmlNodeList xmlnode;
            FileStream fs = new FileStream(ConfigFileUNC, FileMode.Open, FileAccess.Read);

            if (startupDebug) dbug("In SetUpChannel");

            xmldoc.Load(fs);

            xmlnode = xmldoc.GetElementsByTagName("Channel");
            for (int i = 0; i <= xmlnode.Count - 1; i++)
            {
                for (int j = 0; j < xmlnode[i].ChildNodes.Count; j++)
                {
                    try
                    {
                        // Fill it up
                        switch (xmlnode[i].ChildNodes.Item(j).Name.Trim().ToUpper())
                        {
                            case "TYPE":
                                switch (xmlnode[i].ChildNodes.Item(j).InnerText.Trim().ToUpper())
                                {
                                    case "COMFILE":
                                        Channel = null;
                                        Channel = new ComFile();
                                        break;

                                    case "COMIRC":
                                        Channel = null;
                                        Channel = new ComIRC();
                                        break;

                                    case "COMCHAT":
                                        Channel = null;
                                        Channel = new ComChat();
                                        break;

                                    case "COMTCP":
                                        Channel = null;
                                        Channel = new ComTCP();
                                        break;
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        dbug("SetUpChannel Error: " + ex.Message + "\r\nFrom " + ex.Source);
                    }
                }
            }

            if (Channel != null)
            {
                xmlnode = xmldoc.GetElementsByTagName(SectionName);
                for (int i = 0; i <= xmlnode.Count - 1; i++)
                {
                    for (int j = 0; j < xmlnode[i].ChildNodes.Count; j++)
                    {
                        if (startupDebug) dbug(string.Format("Setting {0} to {1}", xmlnode[i].ChildNodes.Item(j).Name.Trim(), xmlnode[i].ChildNodes.Item(j).InnerText.Trim()));
                        Channel.Setting(xmlnode[i].ChildNodes.Item(j).Name.Trim(), xmlnode[i].ChildNodes.Item(j).InnerText.Trim());
                    }
                }

                if (Channel.SetEncrypt)
                {
                    SetChannelEncryption(Channel.EncryptInfo);
                }

                if (startupDebug) dbug("Running " + Channel.Name + "\r\n-----------------------------------------------------------------------\r\n");
            }

            fs.Close();
        }


        public bool SetChannelEncryption(string pinfo)
        {
            bool Results = false;
            int locinfo = -1;
            string setinfo = string.Empty;
            string einfo = pinfo;

            try
            {
                if (einfo.Length > 4)
                {

                    if (einfo.Substring(0, 4).Equals("KEY:"))
                    {
                        setinfo = einfo.Substring(4).Trim();
                        Results = Channel.SetEncryption(setinfo);
                    }
                    else if (einfo.Substring(0, 4).Equals("LOC:"))
                    {
                        setinfo = einfo.Substring(4).Trim();
                        int.TryParse(setinfo, out locinfo);
                        Results = Channel.SetEncryption(setinfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Channel.AddError(903, ex.Message, GetType().Name + ".SETCHANNELENCRYPTTION", "Failed to set encryption from parameter " + pinfo);
            }

            Channel.WriteDebug(string.Format("SetChannelEncryption('{0}') set encryption using {1}", pinfo, setinfo));
            Channel.SetEncrypt = false;
            Channel.EncryptInfo = string.Empty;
            Channel.isEncrypted = Results;
            return Results;
        }

        /*
         */
        public int Open() { return Channel.Open(); }
        public bool Close() { return Channel.Close(); }
        public dynamic GetProperty(string setting) { return Channel.Setting(setting); }
        public void SetProperty(string setting, dynamic value) { Channel.Setting(setting, value); }
        public string GetContactList() { return Channel.GetContactList(); }
        public bool LogIn(string user, string pw) { return Channel.LogIn(user, pw); }

        /*
         * Ask for recipient and message, subject is not accepted and 
         * is being saved for future expansion.
         * 
         */
        public int CreateMessage(string address, string msg)
        {
            int Result;

            if (address.Length == 0 || address.Length > 2)
            {
                Result = Channel.CreateMessage(address, msg, "");
            }
            else
            {
                Channel.AddError(Result = 107, "Recipient address too short", GetType().Name + ".SETCHANNELENCRYPTTION");
            }
            return Result;
        }

        public int SendMessage() { return Channel.SendMessage(); }

        /*
         * Get the oldest message from the FIFO inbound message queue
         * and format it according the the MsgFormat byte
         */
        public string NextMessage()
        {
            string Result = string.Empty;

            if (Channel.CurrentMsg.Message.Length > 0 || Channel.GetMessage())
            {
                /*
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
                 */
                bool fld0 = Channel.IsBitSet(Channel.MsgFormat, 0); // subject
                bool fld1 = Channel.IsBitSet(Channel.MsgFormat, 1); // FROM
                bool fld2 = Channel.IsBitSet(Channel.MsgFormat, 2); // WSID
                bool fld3 = Channel.IsBitSet(Channel.MsgFormat, 3); // IID
                bool fld4 = Channel.IsBitSet(Channel.MsgFormat, 4); // Info
                bool flds = Channel.IsBitSet(Channel.MsgFormat, 7); // fields

                if (Channel.DebugLevel > 8) Channel.WriteDebug(string.Format("NextMessage FLDs={0}  0={1}  1={2}  2={3}  3={4}  4={5}", flds, fld0, fld1, fld2, fld3, fld4));

                if (Channel.MsgFormat == 0)
                {
                    // if Msgformat is 0 then we only want the message
                    Result = Channel.CurrentMsg.Message;
                }
                else if (flds)
                {
                    // Only return that which is requested.  So if we just want FROM and BODY, it will return a string
                    // with the format of "FROM: "<FROM><CR>"BODY: "<BODY>
                    Result =
                            (fld4 ? "INFO: " + (Channel.async ? "ASYNC" : "SYNC") + "|" + Channel.CurrentMsg.ErrorCode.ToString() + "|" + Channel.CurrentMsg.Code.ToString() + "\r" : "") +
                             (fld3 ? "IID: " + Channel.CurrentMsg.ID + "\r" : "") +
                             (fld2 ? "WSID: " + Channel.CurrentMsg.Sender.MachineID + "\r" : "") +
                             (fld1 ? "FROM: " + Channel.CurrentMsg.Sender.Name + "|" + Channel.CurrentMsg.Sender.NickName + "|" + Channel.CurrentMsg.Sender.Address + "\r" : "") +
                             (fld0 ? "SUBJECT: " + Channel.CurrentMsg.Subject + "\r" : "") +
                             "BODY: " + Channel.CurrentMsg.Message;
                }
                else
                {
                    // Only return that which is requested.  So if we just want FROM and BODY, it will return a string
                    // with the format of <FROM><CR><BODY>
                    Result =
                            (fld4 ? (Channel.async ? "ASYNC" : "SYNC") + "|" + Channel.CurrentMsg.ErrorCode.ToString() + "|" + Channel.CurrentMsg.Code.ToString() + "\r" : "") +
                            (fld3 ? Channel.CurrentMsg.ID + "\r" : "") +
                            (fld2 ? Channel.CurrentMsg.Sender.MachineID + "\r" : "") +
                            (fld1 ? Channel.CurrentMsg.Sender.Name + "|" + Channel.CurrentMsg.Sender.NickName + "|" + Channel.CurrentMsg.Sender.Address + "\r" : "") +
                            (fld0 ? Channel.CurrentMsg.Subject + "\r" : "") +
                            Channel.CurrentMsg.Message;
                }

                Channel.CurrentMsg.Message = string.Empty;
            }
            else
            {
                // Was an error returned?
                if (Channel.CurrentMsg == null)
                {
                    // null current msg
                    Result = "<Channel Error: 99 - null msg>";
                }
                else
                {
                    Result = (Channel.CurrentMsg.ErrorCode != 0 ? "<Channel Error: " + Channel.CurrentMsg.ErrorCode.ToString() + ">\r" : "");
                    Channel.CurrentMsg.Message = string.Empty;
                }
            }

            if (Channel.DebugLevel > 8) Channel.WriteDebug("ComPack.NEXTMESSAGE result ->" + Result);
            return Result;
        }

        // return number of waiting messages
        public int MessagesWaiting() { return Channel.MessagesWaiting(); }

        // return number of 
        public int ErrorCount() { return Channel.ErrorList.Count; }

        /* 
         * Return the error in the format Code|Method|Message|Explaination
         * and pop it off the stack if desired
         */
        public string GetError()
        {
            string errmsg = string.Empty;
            if (Channel.ErrorList.Count > 0)
                errmsg = string.Format("{0}|{1}|{2}|{3}", Channel.ErrorList[0].Code,
                    (Channel.ErrorList[0].Method.Length > 0 ? Channel.ErrorList[0].Method : ""),
                    (Channel.ErrorList[0].Message.Length > 0 ? Channel.ErrorList[0].Message : ""),
                    (Channel.ErrorList[0].Explaination.Length > 0 ? Channel.ErrorList[0].Explaination : ""));

            Channel.ErrorList.RemoveAt(0);
            return errmsg;
        }

        /*
         * Return a nicely formatted error message, pop it off if desired
         */
        public string GetErrorAt(int idx, bool remove)
        {
            string errmsg = string.Empty;

            if (Channel.ErrorList.Count > idx)
                errmsg = string.Format("Error #{0}{1}{2}{3}", Channel.ErrorList[idx].Code,
                    (Channel.ErrorList[idx].Method.Length > 0 ? " in Method " + Channel.ErrorList[idx].Method : ""),
                    (Channel.ErrorList[idx].Message.Length > 0 ? " -> " + Channel.ErrorList[idx].Message : ""),
                    (Channel.ErrorList[idx].Explaination.Length > 0 ? "\r\n" + Channel.ErrorList[idx].Explaination : ""));

            if (remove) Channel.ErrorList.RemoveAt(0);
            return errmsg;
        }

        public bool Connected() { return Channel.Connected; }

        // ------------ COMFILE ONLY METHODS ------------
        public int AddRecipient(string address) { return Channel.AddRecipient(address); }

        public int MessageReply(string body, int errCode)
        {
            // this is only for ComFile
            if (Channel.Type != 1) throw new NotImplementedException();

            // returns message in comfile
            Channel.CurrentMsg.Message = body;
            return Channel.ACKMessage(errCode);
        }

        private void dbug(string txt)
        {
            using (StreamWriter file = new StreamWriter(LocalPath + "Startup.log", true))
            {
                file.WriteLine(string.Format("{0} - {1}", DateTime.Now, txt));
            }
        }
    }
}
