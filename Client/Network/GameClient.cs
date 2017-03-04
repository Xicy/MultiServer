using System;
using Shared.Network;
using Shared.Schema;

namespace Client
{
    public class GameClient : BaseClient<GameClient>
    {
        public PacketHandler Handlers;
        public DateTime LastPingTime { set; get; }
        public long ID { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }

        public static implicit operator GameObject(GameClient client)
        {
            return new GameObject(client.ID, client.X, client.Y);
        }

        public void Send(Packet packet)
        {
            Send(packet.Build());
        }

        protected override void OnHandleBuffer(GameClient client, byte[] buffer)
        {
            Handlers.Handle(client, new Packet(buffer, 0));
        }

        public GameClient()
        {
            Handlers = new PacketHandler();
            Disconnected += c => Handlers.GameObjects.Clear();
            Connected += c => { c.Ping(); c.Crypter(""); };
        }
    }
}
