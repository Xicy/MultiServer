using System;
using Shared.Network;

namespace Client
{
    public class TestClient : BaseClient
    {
        public DateTime LastPingTime { set; get; }
        public long ID { set; get; }
        public PacketHandlerManager<TestClient> Handlers { set; get; }

        public TestClient()
        {
            Handlers = new PacketHandler();
        }
        protected override byte[] BuildPacket(Packet packet)
        {
            return packet.Build();
        }
        protected override void OnHandleBuffer(BaseClient client, byte[] buffer)
        {
            this.Handlers.Handle((TestClient)client, new Packet(buffer, 0));
            base.OnHandleBuffer(client,buffer);
        }
    }
}
