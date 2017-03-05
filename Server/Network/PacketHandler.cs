using System;
using System.Linq;
using Shared;
using Shared.Util;
using Shared.Network;
using Shared.Schema;

namespace Server
{
    public class PacketHandler : PacketHandlerManager<GameClient>
    {
        private readonly GameServer _server;
        public PacketHandler(GameServer server)
        {
            _server = server;
            AutoLoad();
        }

        [PacketHandler(OpCodes.Crypter)]
        public void Crypter(GameClient client, Packet packet)
        {
            var crypterStatus = packet.Read<bool>();
            if (!crypterStatus)
            {
                client.Crypter(URandom.Long());
                byte x = (byte)URandom.Int(40), y = (byte)URandom.Int(20);

                client.X = x;
                client.Y = y;
            }
            Log.Debug("Crypter is {1} from {0},{2}", client.Address, crypterStatus, client.ID);
        }

        [PacketHandler(OpCodes.Ping)]
        public void Ping(GameClient client, Packet packet)
        {
            client.LastPingTime = new DateTime(packet.Read<long>());
            client.Ping();
        }

        [PacketHandler(OpCodes.GetAroundPlayers)]
        public void GetAroundPlayers(GameClient client, Packet packet)
        {
            _server.Clients.ToList().ForEach(c => { client.Move(c); c.Move(client); });
        }

        [PacketHandler(OpCodes.MoveObject)]
        public void Move(GameClient client, Packet packet)
        {
            var direction = packet.Read<Directions>();
            byte x = client.X, y = client.Y;
            switch (direction)
            {
                case Directions.Up:
                    y--; break;
                case Directions.Down:
                    y++; break;
                case Directions.Left:
                    x--; break;
                case Directions.Right:
                    x++; break;
            }

            if (x < 40)
                client.X = x;
            if (y < 20)
                client.Y = y;

            _server.Clients.ForEach(c => c.Move(client));
        }

    }
}
