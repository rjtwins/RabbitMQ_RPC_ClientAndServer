using System;

namespace RPC.Models
{
    public class RabbitMQRPCMessageEventArgs : EventArgs
    {
        public string Message { get; set; }
        public string CorrelationId { get; set; }
        public string ReplyTo { get; set; }
        public string Queue { get; set; }
    }

    public class RabbitMQRPCMessage
    {
        public string Message { get; set; }
        public string CorrelationId { get; set; }
        public string ReplyTo { get; set; }
        public string Queue { get; set; }
        public ulong DeliveryTag { get; set; }
    }
}
