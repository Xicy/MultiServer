using System;
using Shared;
using System.Linq;
using Shared.Util;
using Shared.Network;

namespace Server
{
    public partial class PacketHandler : PacketHandlerManager<TestClient>
    {
        public PacketHandler()
        {
            AutoLoad();
        }

        [PacketHandler(OpCodes.Crypter)]
        public void Crypter(TestClient client, Packet packet)
        {
            Log.Debug("Crypter is {1} from {0},{2}", client.Address, packet.GetBool(), client.ID);
            client.Ping();
        }

        [PacketHandler(OpCodes.Ping)]
        public void Ping(TestClient client, Packet packet)
        {
            client.LastPingTime = DateTime.Now;
            //Log.Debug("Request Ping From {0},{2} {1}", client.Address, packet.ToString(), client.ID);
            client.Ping();
        }

        [PacketHandler(OpCodes.Login)]
        public void Login(TestClient client, Packet packet)
        {
            string username = packet.GetString();
            string password = packet.GetString();
            Log.Debug("Request Login From {0},{3} Username:{1} Password:{2}", client.Address, username, password, client.ID);

            client.Login();
        }

    }
}
