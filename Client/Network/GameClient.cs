using System;
using Shared.Network;
using Shared.Schema;

namespace Client
{
    public class GameClient : BaseClient
    {
        public DateTime LastPingTime { set; get; }
        public long ID { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }

        public static implicit operator GameObject(GameClient client)
        {
            return new GameObject(client.ID, client.X, client.Y);
        }

        public PacketHandlerManager<GameClient> Handlers { set; get; }

        public GameClient()
        {
            Handlers = new PacketHandler();
            HandleBuffer += (c, b) => Handlers.Handle((GameClient)c, new Packet(b, 0));
        }
    }
}
