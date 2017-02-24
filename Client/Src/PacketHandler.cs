using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared;
using Shared.Network;
using Shared.Schema;
using Shared.Util;

namespace Client
{
    public class PacketHandler : PacketHandlerManager<GameClient>
    {
        public Dictionary<long, GameObject> GameObjects;

        public PacketHandler()
        {
            AutoLoad();
            GameObjects = new Dictionary<long, GameObject>();
        }

        [PacketHandler(OpCodes.MoveObject)]
        public void MoveObject(GameClient client, Packet packet)
        {
            var go = packet.GetObj<GameObject>();
            if (GameObjects.ContainsKey(go.ID))
            {
                GameObjects[go.ID] = go;
            }
            else
            {
                GameObjects.Add(go.ID, go);
            }
        }

        [PacketHandler(OpCodes.Crypter)]
        public void Crypter(GameClient client, Packet packet)
        {
            client.ID = packet.GetLong();
            GameObjects.Add(client.ID, client);
            string key = packet.GetString();
            client.Crypter = new Shared.Security.Blowfish(key);
            Log.Debug("Crypter changed new key: {0}", key);
            client.Crypter();
        }

        [PacketHandler(OpCodes.Ping)]
        public void Ping(GameClient client, Packet packet)
        {
            client.LastPingTime = DateTime.Now;
            Task.Delay(10).ContinueWith(task => client.Ping());

            Console.Clear();
            foreach (var cell in GameObjects.Values.ToArray())
            {
                Console.MoveBufferArea(cell.X, cell.Y, 1, 1, 0, 0, '@', cell.ID == client.ID ? ConsoleColor.Red : ConsoleColor.Green, ConsoleColor.Black);
            }
        }

        [PacketHandler(999)]
        public void RemoveObject(GameClient client, Packet packet)
        {
            var go = packet.GetObj<GameObject>();
            GameObjects.Remove(go.ID);
        }

    }
}
