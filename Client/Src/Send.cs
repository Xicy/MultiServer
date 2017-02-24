using System;
using Shared;
using Shared.Network;
using Shared.Schema;

namespace Client
{
    public static class Send
    {
        public static void Crypter(this GameClient client)
        {
            client.Send(new Packet(OpCodes.Crypter, client.ID).Write(true));
        }
        public static void Ping(this GameClient client)
        {
            client.Send(new Packet(OpCodes.Ping, client.ID).Write(DateTime.Now));
        }
        public static void Move(this GameClient client, Directions dir)
        {
            client.Send(new Packet(OpCodes.MoveObject, client.ID).Write((byte)dir));
        }
    }
}
