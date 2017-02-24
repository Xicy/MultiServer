using System;
using Shared;
using System.Linq;
using Shared.Util;
using Shared.Network;
using Shared.Schema;

namespace Server
{
    public partial class PacketHandler : PacketHandlerManager<GameClient>
    {
        public PacketHandler()
        {
            AutoLoad();
        }

        [PacketHandler(OpCodes.Crypter)]
        public void Crypter(GameClient client, Packet packet)
        {
            Log.Debug("Crypter is {1} from {0},{2}", client.Address, packet.GetBool(), client.ID);
            client.Ping();
        }

        [PacketHandler(OpCodes.Ping)]
        public void Ping(GameClient client, Packet packet)
        {
            client.LastPingTime = DateTime.Now;
            //Log.Debug("Request Ping From {0},{2} {1}", client.Address, packet.ToString(), client.ID);
            client.Ping();
        }

        [PacketHandler(OpCodes.MoveObject)]
        public void Move(GameClient client, Packet packet)
        {
            Directions direction = (Directions)packet.GetByte();
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

            if (x > 0 && x < 40)
                client.X = x;
            if(y > 0 && y < 20)
                client.Y = y;

            Program.Server.Clients.ForEach(c => c.Move(client));
        }

    }
}
