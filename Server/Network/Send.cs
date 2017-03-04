using System;
using Shared;
using Shared.Util;
using Shared.Network;
using Shared.Schema;

namespace Server
{
    public static class Send
    {
        public static void Crypter(this GameClient client, long clientID)
        {
            //string key = URandom.String(32, URandom.PattenUpper + URandom.PattenInt);
            client.ID = clientID;
            client.Send(new Packet(OpCodes.Crypter, OpCodes.ServerID).Write(clientID));
        }
        public static void Ping(this GameClient client)
        {
            client.Send(new Packet(OpCodes.Ping, OpCodes.ServerID).Write(DateTime.Now.Ticks));
        }

        public static void Move(this GameClient client, GameObject obj)
        {
            GameObject go = obj;
            client.Send(new Packet(OpCodes.MoveObject, OpCodes.ServerID).Write(go));
        }
        public static void Move(this GameClient client)
        {
            client.Send(new Packet(OpCodes.MoveObject, OpCodes.ServerID).Write(client));
        }

        public static void RemoveObject(this GameClient client, GameObject obj)
        {
            client.Send(new Packet(999, OpCodes.ServerID).Write(obj));
        }

    }
}
