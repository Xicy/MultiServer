using System;
using Shared.Util;
using System.Collections.Generic;

namespace Shared.Network
{
    public class PacketHandlerManager<TClient> where TClient : BaseClient<TClient>
    {
        public delegate void PacketHandlerFunc(TClient client, Packet packet);

        private readonly Dictionary<ushort, PacketHandlerFunc> _handlers;

        public PacketHandlerManager()
        {
            _handlers = new Dictionary<ushort, PacketHandlerFunc>();
        }

        public void Add(ushort op, PacketHandlerFunc handler)
        {
            _handlers[op] = handler;
        }

        public void AutoLoad()
        {
            foreach (var method in GetType().GetMethods())
            {
                foreach (PacketHandlerAttribute attr in method.GetCustomAttributes(typeof(PacketHandlerAttribute), false))
                {
                    var del = (PacketHandlerFunc)Delegate.CreateDelegate(typeof(PacketHandlerFunc), this, method);
                    foreach (var op in attr.Ops)
                        Add(op, del);
                }
            }
        }

        public virtual void Handle(TClient client, Packet packet)
        {
            PacketHandlerFunc handler;
            if (!_handlers.TryGetValue(packet.OpCode, out handler))
            {
                UnknownPacket(client, packet);
                return;
            }
            handler(client, packet);
            packet.Dispose();
        }

        public virtual void UnknownPacket(TClient client, Packet packet)
        {
            Log.Unimplemented(Localization.Get("Shared.Network.PacketHandlerManager.UnknownPacket.UnImplemented"), packet.OpCode.ToString("X4"), OpCodes.GetName(packet.OpCode));
            Log.Debug(packet);
            packet.Dispose();
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class PacketHandlerAttribute : Attribute
    {
        public ushort[] Ops { get; protected set; }

        public PacketHandlerAttribute(params ushort[] ops)
        {
            Ops = ops;
        }
    }
}
