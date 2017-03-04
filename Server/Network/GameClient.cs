using System;
using Shared.Network;
using Shared.Schema;
using Shared.Security;

namespace Server
{
    public class GameClient : BaseClient<GameClient>
    {
        public DateTime LastPingTime { set; get; }
        public long ID { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }

        public void Send(Packet packet)
        {
            Send(packet.Build());
        }

        public static implicit operator GameObject(GameClient client)
        {
            return new GameObject(client.ID, client.X, client.Y);
        }
    }
}
