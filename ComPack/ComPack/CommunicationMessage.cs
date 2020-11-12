/*
 */
using System;
using System.Collections.Generic;

namespace ComPack
{
    public class CommunicationMessage
    {
        private int priority = 0; // 0=no priority, 1=low priority, 2=normal priority, 3=high priority, other priority values are user defined
        private string id = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("/", "_");  // default GUID, may be replaced as needed
        private string subject = string.Empty;
        private string message = string.Empty;

        private CommunicationRecipient sender = new CommunicationRecipient();
        private List<CommunicationRecipient> recipient = new List<CommunicationRecipient>();

        private int errorcode = 0; // 1=channel not open, 2=transmission failure, 3=checksum failure, 999=internal failure
        private int type = 0; // 0=inbound, 1=outbound, 2=internal
        private int code = 0;

        public DateTime messagetime = DateTime.Now;  // time the msg was created
        public readonly DateTime TransactionTime = DateTime.Now;  // time the msg entry was created

        private bool async = true;

        public int Priority { get => priority; set => priority = value; }
        public string ID { get => id; set => id = value; }
        public string Subject { get => subject; set => subject = value; }
        public string Message { get => message; set => message = value; }

        public CommunicationRecipient Sender { get => sender; set => sender = value; }
        public List<CommunicationRecipient> Recipient { get => recipient; set => recipient = value; }

        public int ErrorCode { get => errorcode; set => errorcode = value; }
        public int Type { get => type; set => type = value; }
        public int Code { get => code; set => code = value; }
        public DateTime MessageTime { get => messagetime; set => messagetime = value; }
        public bool Async { get => async; set => async = value; }
    }
}
