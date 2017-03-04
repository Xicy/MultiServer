using System.Linq;
using Shared.Network;

namespace Server
{
    public sealed class GameServer : BaseServer<GameClient>
    {
        public readonly PacketHandler Handlers;
        public GameServer()
        {
            Handlers = new PacketHandler(this);
        }

        protected override void HandleBuffer(GameClient client, byte[] buffer)
        {
            Handlers.Handle(client, new Packet(buffer, 0));
        }

        protected override void OnClientDisconnected(GameClient client)
        {
            Clients.ToList().ForEach(c => c.RemoveObject(client));
            base.OnClientDisconnected(client);
        }
    }
}
