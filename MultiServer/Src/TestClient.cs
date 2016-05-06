using System;
using Shared.Network;

namespace Server
{
    public class TestClient : BaseClient
    {
        public DateTime LastPingTime { set; get; }
        public long ID { set; get; }
        
        protected override byte[] BuildPacket(Packet packet)
        {
            return packet.Build();
        }
    }
}
