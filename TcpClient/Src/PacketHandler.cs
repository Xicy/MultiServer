using System;
using Shared;
using Shared.Network;
using Shared.Util;

namespace Client
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
            client.ID = packet.GetLong();
            string key = packet.GetString();
            client.Crypter = new Shared.Security.Blowfish(key);
            Log.Debug("Crypter changed new key: {0}", key);
            client.Crypter();
        }

        [PacketHandler(OpCodes.Ping)]
        public void Ping(TestClient client, Packet packet)
        {
            client.LastPingTime = DateTime.Now;
            //Log.Debug("Request Ping From {0} {1}", client.Address, packet.ToString());
            System.Threading.Thread.Sleep(2000);
            client.Ping();
        }

        [PacketHandler(OpCodes.Login)]
        public void Login(TestClient client, Packet packet)
        {
            client.LastPingTime = DateTime.Now;
            bool isLogin = packet.GetBool();
            Log.Info("Request Login From {0} {1}", client.Address, isLogin);
        }

    }
}
