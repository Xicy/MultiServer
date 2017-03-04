using System;
using Shared;
using Shared.Network;
using Shared.Schema;

namespace Client
{
    public static class Send
    {
        public static void Crypter(this GameClient client, string key)
        {
            var isDone = !string.IsNullOrEmpty(key);
            var pck = new Packet(OpCodes.Crypter, client.ID).Write(isDone);
            if (isDone) pck.Write(key);
            client.Send(pck);
        }
        public static void Ping(this GameClient client)
        {
            client.Send(new Packet(OpCodes.Ping, client.ID).Write(DateTime.Now.Ticks));
        }
        public static void Move(this GameClient client, Directions dir)
        {
            client.Send(new Packet(OpCodes.MoveObject, client.ID).Write((byte)dir));
        }
        public static void GetAroundPlayers(this GameClient client) => client.Send(new Packet(OpCodes.GetAroundPlayers, client.ID));
    }
}
