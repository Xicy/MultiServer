using System;
using Shared;
using Shared.Util;
using Shared.Network;

namespace Server
{
    public static partial class Send
    {
        public static void Crypter(this TestClient client, long clientID)
        {
            string key = URandom.String(32, URandom.PattenUpper + URandom.PattenInt);
            client.ID = clientID;
            client.Send(new Packet(OpCodes.Crypter, OpCodes.ServerID).Write(clientID).Write(key));
            client.Crypter = new Shared.Security.Blowfish(key);
        }
        public static void Ping(this TestClient client)
        {
            client.Send(new Packet(OpCodes.Ping, OpCodes.ServerID).Write(DateTime.Now));
        }
        public static void Login(this TestClient client)
        {
            client.Send(new Packet(OpCodes.Login, OpCodes.ServerID).Write(true));
        }
    }
}
