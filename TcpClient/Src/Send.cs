using System;
using Shared;
using Shared.Network;

namespace Client
{
    public static partial class Send
    {
        public static void Crypter(this TestClient client)
        {
            client.Send(new Packet(OpCodes.Crypter, client.ID).Write(true));
        }
        public static void Ping(this TestClient client)
        {
            client.Send(new Packet(OpCodes.Ping, client.ID).Write(DateTime.Now));
        }
        public static void Login(this TestClient client, string username, string password)
        {
            client.Send(new Packet(OpCodes.Login, client.ID).Write(username).Write(password));
        }
    }
}
