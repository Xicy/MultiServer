using System.Collections.Generic;
using System.Linq;
using Shared.Network;
using Shared.Schema;
using Shared.Util;

namespace Server
{
    public class GameServer : BaseServer<GameClient>
    {
        public GameServer()
        {
            Handlers = new PacketHandler();
        }

        protected override void HandleBuffer(GameClient client, byte[] buffer)
        {
            Handlers.Handle(client, new Packet(buffer, 0));
        }

        protected override void OnClientConnected(GameClient client)
        {
            client.Crypter(URandom.Long());
            client.Ping();

            byte x = (byte)URandom.Int(40), y = (byte)URandom.Int(20);

            client.X = x;
            client.Y = y;
            Clients.ToList().ForEach(c => { client.Move(c); c.Move(client); });

            base.OnClientConnected(client);
        }

        protected override void OnClientDisconnected(GameClient client)
        {
            Clients.ToList().ForEach(c => c.RemoveObject(client));
            base.OnClientDisconnected(client);
        }
    }
}
