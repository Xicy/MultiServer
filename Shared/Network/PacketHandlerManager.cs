using System;
using Shared.Util;
using System.Collections.Generic;

namespace Shared.Network
{
    public abstract class PacketHandlerManager<TClient> where TClient : BaseClient
    {
        public delegate void PacketHandlerFunc(TClient client, Packet packet);

        private readonly Dictionary<ushort, PacketHandlerFunc> _handlers;

        protected PacketHandlerManager()
        {
            _handlers = new Dictionary<ushort, PacketHandlerFunc>();
        }

        public void Add(ushort op, PacketHandlerFunc handler)
        {
            _handlers[op] = handler;
        }

        public void AutoLoad()
        {
            foreach (var method in this.GetType().GetMethods())
            {
                foreach (PacketHandlerAttribute attr in method.GetCustomAttributes(typeof(PacketHandlerAttribute), false))
                {
                    var del = (PacketHandlerFunc)Delegate.CreateDelegate(typeof(PacketHandlerFunc), this, method);
                    foreach (var op in attr.Ops)
                        this.Add(op, del);
                }
            }
        }

        public virtual void Handle(TClient client, Packet packet)
        {
            //new System.Threading.Thread(() =>
            //{
            PacketHandlerFunc handler;
            if (!_handlers.TryGetValue(packet.OpCode, out handler))
            {
                this.UnknownPacket(client, packet);
                return;
            }
            handler(client, packet); packet.Dispose();
            //}).Start(); //TODO:Thread
        }

        public virtual void UnknownPacket(TClient client, Packet packet)
        {
            Log.Unimplemented(Localization.Get("shared.network.packethandlernanager.unknownpacket.unimplemented"), packet.OpCode, OpCodes.GetName(packet.OpCode));
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
            this.Ops = ops;
        }
    }
}
