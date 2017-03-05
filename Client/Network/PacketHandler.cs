using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared;
using Shared.Network;
using Shared.Schema;


namespace Client
{
    public class PacketHandler : PacketHandlerManager<GameClient>
    {
        public readonly Dictionary<long, GameObject> GameObjects;

        public PacketHandler()
        {
            AutoLoad();
            GameObjects = new Dictionary<long, GameObject>();
        }

        [PacketHandler(OpCodes.MoveObject)]
        public void MoveObject(GameClient client, Packet packet)
        {
            packet.Read(out GameObject go);
            GameObjects[go.ID] = go;
        }

        [PacketHandler(OpCodes.Crypter)]
        public void Crypter(GameClient client, Packet packet)
        {
            client.ID = packet.Read<long>();
            GameObjects.Add(client.ID, client);
            client.GetAroundPlayers();
        }

        [PacketHandler(OpCodes.Ping)]
        public void Ping(GameClient client, Packet packet)
        {
            var cur = new DateTime(packet.Read<long>());
            Task.Delay(30).ContinueWith(task => client.Ping());

            Console.Clear();
            Console.Write(" Ping:{0}", (cur - client.LastPingTime).TotalMilliseconds);
            client.LastPingTime = cur;

            foreach (var cell in GameObjects.Values.ToArray())
               Console.MoveBufferArea(cell.X, cell.Y, 1, 1, 0, 0, '@', cell.ID == client.ID ? ConsoleColor.Red : ConsoleColor.Green, ConsoleColor.Black);

        }

        [PacketHandler(999)]
        public void RemoveObject(GameClient client, Packet packet) => GameObjects.Remove(packet.Read<GameObject>().ID);

    }
}
