using System.Collections.Generic;
using Shared.Network;
using Shared.Schema;
using Shared.Util;

namespace Server
{
    public class GameServer : BaseServer<GameClient>
    {
        public Dictionary<long, GameObject> GameObjects;

        public GameServer()
        {
            GameObjects = new Dictionary<long, GameObject>();
            this.Handlers = new PacketHandler();
        }

        protected override void HandleBuffer(GameClient client, byte[] buffer)
        {
            this.Handlers.Handle(client, new Packet(buffer, 0));
        }

        protected override void OnClientConnected(GameClient client)
        {
            client.Crypter(URandom.Long());

            GameObjects.Add(client.ID, client);

            client.X = 10;
            client.Y = 10;
            Clients.ForEach(c => { client.Move(c); c.Move(client); });

            client.Ping();

            base.OnClientConnected(client);
        }

        protected override void OnClientDisconnected(GameClient client)
        {
            GameObjects.Remove(client.ID);
            Clients.ForEach(c => c.RemoveObject(client));
            base.OnClientDisconnected(client);
        }
    }
}
