using System;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Runtime.InteropServices;
using Shared.Util;

namespace Shared.Network
{
    public enum PacketElementTypes : byte
    {
        None,
        Byte,
        SByte,
        Short,
        UShort,
        Int,
        UInt,
        Long,
        ULong,
        Float,
        String,
        Bin,
        Bool,
        DateTime,
        Packet
    }

    public class Packet : IDisposable
    {
        private const int DefaultBufferSize = 2 * 1024;
        private const int AddSize = 1024;
        private ReaderWriterLockSlim ReadWriteLock = new ReaderWriterLockSlim();
        protected byte[] Buffer;
        protected int Position;
        protected int BodyStart;
        private int Elements;
        private int BodyLength;

        public ushort OpCode { set; get; }
        public long ID { set; get; }

        public Packet(ushort opCode, long id)
        {
            ReadWriteLock.EnterWriteLock();
            try
            {
                OpCode = opCode;
                ID = id;
                Buffer = new byte[DefaultBufferSize];
            }
            finally { ReadWriteLock.ExitWriteLock(); }
        }
        public Packet(byte[] buffer, int offset)
        {
            ReadWriteLock.EnterWriteLock();
            try
            {
                Buffer = buffer;
                Position = offset;

                this.OpCode = BitConverter.ToUInt16(buffer, Position);
                this.ID = BitConverter.ToInt64(buffer, Position + sizeof(ushort));
                Position += sizeof(ushort) + sizeof(long);

                BodyLength = this.ReadVarInt(Buffer, ref Position);
                Elements = this.ReadVarInt(Buffer, ref Position);

                Position++; //0x00

                BodyStart = Position;
            }
            finally { ReadWriteLock.ExitWriteLock(); }
        }

        public Packet Clear(ushort opCode, long id)
        {
            ReadWriteLock.EnterWriteLock();
            try
            {
                this.OpCode = opCode;
                this.ID = id;

                Array.Clear(Buffer, 0, Buffer.Length);
                Position = 0;
                BodyStart = 0;
                Elements = 0;
                BodyLength = 0;
            }
            finally { ReadWriteLock.ExitWriteLock(); }
            return this;
        }
        public static Packet Empty()
        {
            return new Packet(0, 0);
        }
        public PacketElementTypes Peek()
        {
            ReadWriteLock.EnterReadLock();
            try
            {
                if (Position + 2 > Buffer.Length) { return PacketElementTypes.None; }
                return (PacketElementTypes)Buffer[Position];
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public bool NextIs(PacketElementTypes type)
        {
            return (this.Peek() == type);
        }
        protected bool IsRequireSize(int required)
        {
            if (Position + required >= Buffer.Length)
            {
                Array.Resize(ref Buffer, Buffer.Length + Math.Max(AddSize, required * 2));
                return true;
            }
            return false;
        }
        private void IsReadable(int byteCount)
        {
            if (Position + byteCount > Buffer.Length)
                throw new IndexOutOfRangeException(Localization.Get("shared.network.packet.isreadable.exception"));
        }


        #region Write
        protected Packet WriteSimple(PacketElementTypes type, params byte[] val)
        {
            ReadWriteLock.EnterWriteLock();
            try
            {
                var Length = 1 + val.Length;
                this.IsRequireSize(Length);

                Buffer[Position++] = (byte)type;
                val.CopyTo(Buffer, Position);
                Position += val.Length;
                Elements++;
                BodyLength += Length;

                return this;
            }
            finally { ReadWriteLock.ExitWriteLock(); }
        }
        protected Packet WriteWithLength(PacketElementTypes type, byte[] val)
        {
            ReadWriteLock.EnterWriteLock();
            try
            {
                var Length = 1 + sizeof(ushort) + val.Length;
                this.IsRequireSize(Length);

                Buffer[Position++] = (byte)type;
                BitConverter.GetBytes((ushort)val.Length).CopyTo(Buffer, Position);
                Position += sizeof(ushort);
                val.CopyTo(Buffer, Position);
                Position += val.Length;
                Elements++;
                BodyLength += Length;
                return this;
            }
            finally { ReadWriteLock.ExitWriteLock(); }
        }

        public Packet Write() { return Write((byte)0); }
        public Packet Write(byte val) { return WriteSimple(PacketElementTypes.Byte, val); }
        public Packet Write(sbyte val) { return WriteSimple(PacketElementTypes.SByte, (byte)val); }
        public Packet Write(bool val) { return WriteSimple(PacketElementTypes.Bool, val ? new byte[] { 0x01 } : new byte[] { 0x00 }); }
        public Packet Write(short val) { return WriteSimple(PacketElementTypes.Short, BitConverter.GetBytes(val)); }
        public Packet Write(ushort val) { return WriteSimple(PacketElementTypes.UShort, BitConverter.GetBytes(val)); }
        public Packet Write(int val) { return WriteSimple(PacketElementTypes.Int, BitConverter.GetBytes(val)); }
        public Packet Write(uint val) { return WriteSimple(PacketElementTypes.UInt, BitConverter.GetBytes(val)); }
        public Packet Write(long val) { return WriteSimple(PacketElementTypes.Long, BitConverter.GetBytes(val)); }
        public Packet Write(ulong val) { return WriteSimple(PacketElementTypes.ULong, BitConverter.GetBytes(val)); }
        public Packet Write(DateTime val) { return WriteSimple(PacketElementTypes.DateTime, BitConverter.GetBytes(val.Ticks)); }
        public Packet Write(float val) { return WriteSimple(PacketElementTypes.Float, BitConverter.GetBytes(val)); }
        public Packet Write(double val) { return Write((float)val); }
        public Packet Write(string val) { return WriteWithLength(PacketElementTypes.String, Encoding.UTF8.GetBytes(val)); }
        public Packet Write(string format, params object[] args) { return Write(string.Format((format != null ? format : string.Empty), args)); }
        public Packet Write(byte[] val) { return WriteWithLength(PacketElementTypes.Bin, val); }
        public Packet Write(object val)
        {
            var type = val.GetType();
            if (!type.IsValueType || type.IsPrimitive)
                throw new Exception("Write object only takes byte[] and structs.");

            var size = Marshal.SizeOf(val);
            var arr = new byte[size];

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(val, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }

            return this.Write(arr);
        }
        public Packet Write(Packet val)
        {
            return WriteWithLength(PacketElementTypes.Packet, val.Build());
        }
        #endregion

        //TODO:Localization
        #region Read
        public byte GetByte()
        {
            if (this.Peek() != PacketElementTypes.Byte)
                throw new Exception("Expected Byte, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(byte));

                Position += 1;
                return Buffer[Position++];
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public sbyte GetSByte()
        {
            if (this.Peek() != PacketElementTypes.SByte)
                throw new Exception("Expected SByte, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(sbyte));

                Position += 1;
                return (sbyte)Buffer[Position++];
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public bool GetBool()
        {
            if (this.Peek() != PacketElementTypes.Bool)
                throw new Exception("Expected Bool, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(byte));

                Position += 1;
                return Buffer[Position++] == 0x01;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public short GetShort()
        {
            if (this.Peek() != PacketElementTypes.Short)
                throw new Exception("Expected Short, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(short));

                Position += 1;
                var val = BitConverter.ToInt16(Buffer, Position);
                Position += sizeof(short);

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public ushort GetUShort()
        {
            if (this.Peek() != PacketElementTypes.UShort)
                throw new Exception("Expected UShort, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(ushort));

                Position += 1;
                var val = BitConverter.ToUInt16(Buffer, Position);
                Position += sizeof(ushort);

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public int GetInt()
        {
            if (this.Peek() != PacketElementTypes.Int)
                throw new Exception("Expected Int, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(int));

                Position += 1;
                var val = BitConverter.ToInt32(Buffer, Position);
                Position += sizeof(int);

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public uint GetUInt()
        {
            if (this.Peek() != PacketElementTypes.UInt)
                throw new Exception("Expected UInt, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(uint));

                Position += 1;
                var val = BitConverter.ToUInt32(Buffer, Position);
                Position += sizeof(uint);

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public long GetLong()
        {
            if (this.Peek() != PacketElementTypes.Long)
                throw new Exception("Expected Long, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(long));

                Position += 1;
                var val = BitConverter.ToInt64(Buffer, Position);
                Position += sizeof(long);

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public ulong GetULong()
        {
            if (this.Peek() != PacketElementTypes.ULong)
                throw new Exception("Expected ULong, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(ulong));

                Position += 1;
                var val = BitConverter.ToUInt64(Buffer, Position);
                Position += sizeof(ulong);

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public DateTime GetDateTime()
        {
            if (this.Peek() != PacketElementTypes.DateTime)
                throw new Exception("Expected DateTime, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(long));

                Position += 1;
                var val = BitConverter.ToInt64(Buffer, Position);
                Position += sizeof(long);

                return new DateTime(val);
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public float GetFloat()
        {
            if (this.Peek() != PacketElementTypes.Float)
                throw new Exception("Expected Float, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(float));

                Position += 1;
                var val = BitConverter.ToSingle(Buffer, Position);
                Position += sizeof(float);

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public string GetString()
        {
            if (this.Peek() != PacketElementTypes.String)
                throw new ArgumentException("Expected String, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(short));

                Position += 1;
                var len = BitConverter.ToInt16(Buffer, Position);
                Position += sizeof(short);

                this.IsReadable(len);

                var val = Encoding.UTF8.GetString(Buffer, Position, len);
                Position += len;

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public byte[] GetBin()
        {
            if (this.Peek() != PacketElementTypes.Bin)
                throw new ArgumentException("Expected Bin, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(short));

                Position += 1;
                var len = BitConverter.ToInt16(Buffer, Position);
                Position += sizeof(short);

                this.IsReadable(len);
                var val = new byte[len];
                Array.Copy(Buffer, Position, val, 0, len);
                Position += len;

                return val;
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public T GetObj<T>() where T : new()
        {
            var type = typeof(T);
            if (!type.IsValueType || type.IsPrimitive)
                throw new Exception("GetObj can only marshal to structs.");

            var buffer = this.GetBin();
            object val;

            IntPtr intPtr = IntPtr.Zero;
            try
            {
                intPtr = Marshal.AllocHGlobal(buffer.Length);
                Marshal.Copy(buffer, 0, intPtr, buffer.Length);
                val = Marshal.PtrToStructure(intPtr, typeof(T));
            }
            finally
            {
                if (intPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(intPtr);
            }

            return (T)val;
        }
        public Packet GetPacket()
        {
            if (this.Peek() != PacketElementTypes.Packet)
                throw new ArgumentException("Expected Packet, got " + this.Peek() + ".");

            ReadWriteLock.EnterReadLock();
            try
            {
                this.IsReadable(1 + sizeof(short));

                Position += 1;
                var len = BitConverter.ToInt16(Buffer, Position);
                Position += sizeof(short);

                this.IsReadable(len);
                var val = new byte[len];
                Array.Copy(Buffer, Position, val, 0, len);
                Position += len;

                return new Packet(val, 0);
            }
            finally { ReadWriteLock.ExitReadLock(); }
        }
        public void Skip(int num = 1)
        {
            for (int i = 0; i < num; ++i)
            {
                switch (this.Peek())
                {
                    case PacketElementTypes.Byte: this.GetByte(); break;
                    case PacketElementTypes.Short: this.GetShort(); break;
                    case PacketElementTypes.Int: this.GetInt(); break;
                    case PacketElementTypes.Long: this.GetLong(); break;
                    case PacketElementTypes.Float: this.GetFloat(); break;
                    case PacketElementTypes.String: this.GetString(); break;
                    case PacketElementTypes.Bin: this.GetBin(); break;
                }
            }
        }
        #endregion

        private int ReadVarInt(byte[] buffer, ref int ptr)
        {
            int result = 0;

            for (int i = 0; ; ++i)
            {
                result |= (buffer[ptr] & 0x7f) << (i * 7);

                if ((buffer[ptr++] & 0x80) == 0)
                    break;
            }

            return result;
        }
        private void WriteVarInt(int value, byte[] buffer, ref int ptr)
        {
            do
            {
                buffer[ptr++] = (byte)(value > 0x7F ? (0x80 | (value & 0xFF)) : value & 0xFF);
            }
            while ((value >>= 7) != 0);
        }

        public int GetSize()
        {
            var i = 2 + 8; // op + id + body

            int n = BodyLength; // + body len
            do { i++; n >>= 7; } while (n != 0);

            n = Elements; // + number of elements
            do { i++; n >>= 7; } while (n != 0);

            ++i; // + zero
            i += BodyLength; // + body

            return i;
        }

        public byte[] Build()
        {
            var result = new byte[this.GetSize()];
            this.Build(ref result, 0);

            return result;
        }
        public void Build(ref byte[] buffer, int offset)
        {
            ReadWriteLock.EnterUpgradeableReadLock();
            try
            {
                if (buffer.Length < offset + this.GetSize())
                    throw new Exception(Localization.Get("shared.network.packet.build.exception"));

                var length = BodyLength;

                {
                    Array.Copy(BitConverter.GetBytes(this.OpCode), 0, buffer, offset, sizeof(ushort));
                    Array.Copy(BitConverter.GetBytes(this.ID), 0, buffer, offset + sizeof(ushort), sizeof(long));
                    offset += 10;

                    this.WriteVarInt(BodyLength, buffer, ref offset);
                    this.WriteVarInt(Elements, buffer, ref offset);

                    buffer[offset++] = 0;

                    length += offset;
                }

                Array.Copy(Buffer, BodyStart, buffer, offset, BodyLength);
            }
            finally { ReadWriteLock.ExitUpgradeableReadLock(); }
        }

        private bool IsValidType(PacketElementTypes type)
        {
            return (type >= PacketElementTypes.Byte && type <= PacketElementTypes.Packet);
        }
        public override string ToString()
        {
            var result = new StringBuilder();
            var prevPtr = Position;
            Position = BodyStart;

            result.AppendLine();
            result.AppendFormat("Op: {0:X04} {2}, Id: {1:X16}" + Environment.NewLine, this.OpCode, this.ID, OpCodes.GetName(this.OpCode));

            PacketElementTypes type;
            for (int i = 1; (this.IsValidType(type = this.Peek()) && Position < Buffer.Length); ++i)
            {
                if (type == PacketElementTypes.Byte)
                {
                    var data = this.GetByte();
                    result.AppendFormat("{0:000} [{1}] Byte    : {2}", i, data.ToString("X2").PadLeft(16, '.'), data);
                }
                else if (type == PacketElementTypes.SByte)
                {
                    var data = this.GetSByte();
                    result.AppendFormat("{0:000} [{1}] SByte   : {2}", i, data.ToString("X2").PadLeft(16, '.'), data);
                }
                else if (type == PacketElementTypes.Bool)
                {
                    var data = this.GetBool();
                    result.AppendFormat("{0:000} [..............{1}] Bool    : {2}", i, data ? "01" : "00", data ? "True" : "False");
                }
                else if (type == PacketElementTypes.Short)
                {
                    var data = this.GetShort();
                    result.AppendFormat("{0:000} [{1}] Short   : {2}", i, data.ToString("X4").PadLeft(16, '.'), data);
                }
                else if (type == PacketElementTypes.UShort)
                {
                    var data = this.GetUShort();
                    result.AppendFormat("{0:000} [{1}] UShort  : {2}", i, data.ToString("X4").PadLeft(16, '.'), data);
                }
                else if (type == PacketElementTypes.Int)
                {
                    var data = this.GetInt();
                    result.AppendFormat("{0:000} [{1}] Int     : {2}", i, data.ToString("X8").PadLeft(16, '.'), data);
                }
                else if (type == PacketElementTypes.UInt)
                {
                    var data = this.GetUInt();
                    result.AppendFormat("{0:000} [{1}] UInt    : {2}", i, data.ToString("X8").PadLeft(16, '.'), data);
                }
                else if (type == PacketElementTypes.Long)
                {
                    var data = this.GetLong();
                    result.AppendFormat("{0:000} [{1}] Long    : {2}", i, data.ToString("X16"), data);
                }
                else if (type == PacketElementTypes.ULong)
                {
                    var data = this.GetULong();
                    result.AppendFormat("{0:000} [{1}] ULong   : {2}", i, data.ToString("X16"), data);
                }
                else if (type == PacketElementTypes.DateTime)
                {
                    var data = this.GetDateTime();
                    result.AppendFormat("{0:000} [{1}] DateTime: {2} {3}", i, data.Ticks.ToString("X16"), data.ToShortDateString(), data.ToShortTimeString());
                }
                else if (type == PacketElementTypes.Float)
                {
                    var data = this.GetFloat();

                    var hex = (BitConverter.DoubleToInt64Bits(data) >> 32).ToString("X8");
                    if (hex.Length > 8)
                        hex = hex.Substring(8);

                    result.AppendFormat("{0:000} [{1}] Float   : {2}", i, hex.PadLeft(16, '.'), data.ToString("0.0####", CultureInfo.InvariantCulture));
                }
                else if (type == PacketElementTypes.String)
                {
                    var data = this.GetString();
                    result.AppendFormat("{0:000} [................] String  : {1}", i, data);
                }
                else if (type == PacketElementTypes.Packet)
                {
                    var data = this.GetPacket();
                    result.AppendFormat("{0:000} [................] Packet  : {1}", i, data.ToString().Replace("\n", "\n "));
                }
                else if (type == PacketElementTypes.Bin)
                {
                    var data = BitConverter.ToString(this.GetBin());
                    var splitted = data.Split('-');

                    result.AppendFormat("{0:000} [................] Bin     : ", i);
                    for (var j = 1; j <= splitted.Length; ++j)
                    {
                        result.Append(splitted[j - 1]);
                        if (j < splitted.Length)
                            if (j % 12 == 0)
                                result.Append(Environment.NewLine.PadRight(35, ' '));
                            else
                                result.Append(' ');
                    }
                }

                if (this.Elements != i) { result.AppendLine(); }
            }

            Position = prevPtr;

            return result.ToString();
        }

        public void Dispose()
        {
            Array.Clear(Buffer, 0, Buffer.Length);
            Buffer = null;
            Position = 0;
            BodyStart = 0;
            BodyLength = 0;
            Elements = 0;
            OpCode = 0;
            ID = 0;
            GC.SuppressFinalize(this);
        }
    }
}
