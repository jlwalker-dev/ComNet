/*
 */
using System;
using System.Dynamic;

namespace ComPack
{
    public class CommunicationRecipient
    {
        private string name = string.Empty;
        private string nickname = string.Empty;
        private string address = string.Empty;
        private string type = string.Empty;
        private int transport = 0;
        private string machineid = string.Empty;
        private string ip4address = string.Empty;
        private string ip6address = string.Empty;
        private string macaddress = string.Empty;
        private int portin = 0;
        private int portout = 0;
        private DateTime lastcontact = DateTime.Now;
        private DateTime online = DateTime.Now;
        private long msgcount = 0;
        private bool offline = false;

        public string Name { get => name; set => name = value; } // user name
        public string NickName { get => nickname; set => nickname = value; } // user nick name (needs to be unique to user)
        public string Address { get => address; set => address = value; } // address (needs to be unique to user)
        public string Type { get => type; set => type = value; } // blank=To:, "C"=CC:, "B"=BCC:
        public int Transport { get => transport; set => transport = value; } // 0=ComFile, 1=IRC, 2=EMail, 3=TelChat, 4=TCPDirect
        public string MachineID { get => machineid; set => machineid = value; } // machine they are currently on
        public string IP4Address { get => ip4address; set => ip4address = value; } // IP address of machine
        public string IP6Address { get => ip6address; set => ip6address = value; } // IP address of machine
        public string MACAddress { get => macaddress; set => macaddress = value; } // IP address of machine
        public int PortIn { get => portin; set => portin = value; } // TCP port negotiated for receiving from other machine
        public int PortOut { get => portout; set => portout = value; } // TCP port negotiated for sending to other machine
        public DateTime LastContact { get => lastcontact; set => lastcontact = value; }  // last message received at
        public DateTime OnLine { get => online; set => online = value; } // first noticed at
        public long MsgCount { get => msgcount; set => msgcount = value; } // number of msgs received from
        public bool OffLine { get => offline; set => offline = value; } // current offline flag

    }
}
