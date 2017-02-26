using System;
using System.Text;
using Shared.Util;
using System.Threading;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

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
        Decimal,
        Double,
        String,
        Char,
        Bin,
        Bool
    }

    public class Packet : IDisposable
    {
        private const ushort DefaultBufferSize = 1024;
        private const ushort AddSize = 512;
        private readonly ReaderWriterLockSlim _readWriteLock = new ReaderWriterLockSlim();
        protected byte[] Buffer;
        protected int Position;
        protected int BodyStart;
        private int _elements;
        private int _bodyLength;

        public ushort OpCode { set; get; }
        public long Id { set; get; }

        public Packet(ushort opCode, long id)
        {
            using (_readWriteLock.Write())
            {
                OpCode = opCode;
                Id = id;
                Buffer = new byte[DefaultBufferSize];
            }
        }
        public Packet(byte[] buffer, int offset)
        {
            using (_readWriteLock.Write())
            {
                Buffer = buffer;
                Position = offset;

                OpCode = BitConverter.ToUInt16(buffer, Position);
                Id = BitConverter.ToInt64(buffer, Position + sizeof(ushort));
                Position += sizeof(ushort) + sizeof(long);

                _bodyLength = ReadVarInt(Buffer, ref Position);
                _elements = ReadVarInt(Buffer, ref Position);

                Position++; //0x00

                BodyStart = Position;
            }
        }

        public Packet Clear(ushort opCode, long id)
        {
            using (_readWriteLock.Write())
            {
                OpCode = opCode;
                Id = id;

                Array.Clear(Buffer, 0, Buffer.Length);
                Position = 0;
                BodyStart = 0;
                _elements = 0;
                _bodyLength = 0;
            }
            return this;
        }
        public static Packet Empty()
        {
            return new Packet(0, 0);
        }
        public PacketElementTypes Peek()
        {
            using (_readWriteLock.Read())
            {
                if (Position + sizeof(PacketElementTypes) + 1 > Buffer.Length) { return PacketElementTypes.None; }
                return (PacketElementTypes)Buffer[Position];
            }
        }
        public bool NextIs(PacketElementTypes type)
        {
            return (Peek() == type);
        }
        protected bool IsRequireSize(int required)
        {
            if (Position + required < Buffer.Length) return false;
            Array.Resize(ref Buffer, Buffer.Length + Math.Max(AddSize, required * 2));
            return true;
        }
        private void IsReadable(int byteCount)
        {
            if (byteCount <= 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
            if (Position + byteCount > Buffer.Length)
                throw new IndexOutOfRangeException(Localization.Get("Shared.Network.Packet.IsReadable.Exception"));
        }

        #region Write
        protected Packet WriteSimple(PacketElementTypes type, params byte[] val)
        {
            using (_readWriteLock.Write())
            {
                var length = sizeof(PacketElementTypes) + val.Length;
                IsRequireSize(length);

                Buffer[Position] = (byte)type;//TODO:Spesfic
                Position += sizeof(PacketElementTypes);

                val.CopyTo(Buffer, Position);
                Position += val.Length;
                _elements++;
                _bodyLength += length;
            }
            return this;
        }
        protected Packet WriteWithLength(PacketElementTypes type, byte[] val)
        {
            Array.Resize(ref val, sizeof(ushort) + val.Length);
            Array.Copy(val, 0, val, sizeof(ushort), val.Length - sizeof(ushort));
            BitConverter.GetBytes((ushort)(val.Length - sizeof(ushort))).CopyTo(val, 0);
            return WriteSimple(type, val);
        }

        public Packet Write() { return Write((byte)0); }
        public Packet Write(byte val) { return WriteSimple(PacketElementTypes.Byte, val); }
        public Packet Write(sbyte val) { return WriteSimple(PacketElementTypes.SByte, (byte)val); }
        public Packet Write(bool val) { return WriteSimple(PacketElementTypes.Bool, BitConverter.GetBytes(val)); }
        public Packet Write(short val) { return WriteSimple(PacketElementTypes.Short, BitConverter.GetBytes(val)); }
        public Packet Write(ushort val) { return WriteSimple(PacketElementTypes.UShort, BitConverter.GetBytes(val)); }
        public Packet Write(int val) { return WriteSimple(PacketElementTypes.Int, BitConverter.GetBytes(val)); }
        public Packet Write(uint val) { return WriteSimple(PacketElementTypes.UInt, BitConverter.GetBytes(val)); }
        public Packet Write(long val) { return WriteSimple(PacketElementTypes.Long, BitConverter.GetBytes(val)); }
        public Packet Write(ulong val) { return WriteSimple(PacketElementTypes.ULong, BitConverter.GetBytes(val)); }
        public Packet Write(float val) { return WriteSimple(PacketElementTypes.Float, BitConverter.GetBytes(val)); }
        public Packet Write(decimal val) { return WriteSimple(PacketElementTypes.Decimal, PacketConverter.GetBytes(val)); }
        public Packet Write(double val) { return WriteSimple(PacketElementTypes.Double, BitConverter.GetBytes(val)); }
        public Packet Write(string val) { return WriteWithLength(PacketElementTypes.String, Encoding.UTF8.GetBytes(val)); }
        public Packet Write(char val) { return WriteSimple(PacketElementTypes.Char, BitConverter.GetBytes(val)); }
        public Packet Write(string format, params object[] args) { return Write(string.Format(format ?? string.Empty, args)); }
        public Packet Write(byte[] val) { return WriteWithLength(PacketElementTypes.Bin, val); }
        public Packet Write(object val)
        {
            var type = val.GetType();
            if (!type.IsValueType || type.IsPrimitive)
                throw new Exception(Localization.Get("Shared.Network.Packet.Write.Exception"));

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

            return Write(arr);
        }
        #endregion

        #region Read
        protected T Get<T>(Func<byte[], int, T> converter)
        {
            using (_readWriteLock.Read())
            {
                var len = Marshal.SizeOf(typeof(T));
                if (typeof(T) == typeof(bool)) len = 1;
                else if (typeof(T) == typeof(char)) len = 2;
                IsReadable(sizeof(PacketElementTypes) + len);
                Position += sizeof(PacketElementTypes);
                var val = converter(Buffer, Position);
                Position += len;
                return val;
            }
        }
        protected T Get<T>(Func<byte[], int, int, T> converter)
        {
            var len = Get(BitConverter.ToUInt16);
            using (_readWriteLock.Read())
            {
                IsReadable(len);
                var val = converter(Buffer, Position, len);
                Position += len;
                return val;
            }
        }

        public byte GetByte()
        {
            if (!NextIs(PacketElementTypes.Byte))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetByte.Exception"), Peek()));
            return Get(PacketConverter.ToByte);
        }
        public sbyte GetSByte()
        {
            if (!NextIs(PacketElementTypes.SByte))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetSByte.Exception"), Peek()));
            return Get(PacketConverter.ToSByte);
        }
        public bool GetBool()
        {
            if (!NextIs(PacketElementTypes.Bool))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetBool.Exception"), Peek()));
            return Get(PacketConverter.ToBoolean);
        }
        public short GetShort()
        {
            if (!NextIs(PacketElementTypes.Short))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetShort.Exception"), Peek()));
            return Get(PacketConverter.ToInt16);
        }
        public ushort GetUShort()
        {
            if (!NextIs(PacketElementTypes.UShort))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetUShort.Exception"), Peek()));
            return Get(PacketConverter.ToUInt16);
        }
        public int GetInt()
        {
            if (!NextIs(PacketElementTypes.Int))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetInt.Exception"), Peek()));
            return Get(PacketConverter.ToInt32);
        }
        public uint GetUInt()
        {
            if (!NextIs(PacketElementTypes.UInt))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetUInt.Exception"), Peek()));
            return Get(PacketConverter.ToUInt32);
        }
        public long GetLong()
        {
            if (!NextIs(PacketElementTypes.Long))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetLong.Exception"), Peek()));
            return Get(PacketConverter.ToInt64);
        }
        public ulong GetULong()
        {
            if (!NextIs(PacketElementTypes.ULong))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetULong.Exception"), Peek()));
            return Get(PacketConverter.ToUInt64);
        }
        public float GetFloat()
        {
            if (!NextIs(PacketElementTypes.Float))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetFloat.Exception"), Peek()));
            return Get(PacketConverter.ToSingle);
        }
        public decimal GetDecimal()
        {
            if (!NextIs(PacketElementTypes.Decimal))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetFloat.Exception"), Peek()));
            return Get(PacketConverter.ToDecimal);
        }
        public double GetDouble()
        {
            if (!NextIs(PacketElementTypes.Double))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetFloat.Exception"), Peek()));
            return Get(PacketConverter.ToDouble);
        }
        public string GetString()
        {
            if (!NextIs(PacketElementTypes.String))
                throw new ArgumentException(string.Format(Localization.Get("Shared.Network.Packet.GetString.Exception"), Peek()));
            return Get(PacketConverter.ToString);
        }
        public char GetChar()
        {
            if (!NextIs(PacketElementTypes.Char))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetByte.Exception"), Peek()));
            return Get(PacketConverter.ToChar);
        }
        public byte[] GetBin()
        {
            if (!NextIs(PacketElementTypes.Bin))
                throw new ArgumentException(string.Format(Localization.Get("Shared.Network.Packet.GetBin.Exception"), Peek()));
            return Get(PacketConverter.ToBin);
        }
        public T GetObj<T>() where T : new()
        {
            var type = typeof(T);
            if (!type.IsValueType || type.IsPrimitive)
                throw new Exception(Localization.Get("Shared.Network.Packet.GetObj.Exception"));
            return Get(PacketConverter.ToObject<T>);
        }
        public void Skip(int num = 1)
        {
            for (int i = 0; i < num; ++i)
            {
                switch (Peek())
                {
                    case PacketElementTypes.Byte: GetByte(); break;
                    case PacketElementTypes.Short: GetShort(); break;
                    case PacketElementTypes.Int: GetInt(); break;
                    case PacketElementTypes.Long: GetLong(); break;
                    case PacketElementTypes.Float: GetFloat(); break;
                    case PacketElementTypes.String: GetString(); break;
                    case PacketElementTypes.Bin: GetBin(); break;
                    case PacketElementTypes.SByte: GetSByte(); break;
                    case PacketElementTypes.UShort: GetUShort(); break;
                    case PacketElementTypes.UInt: GetUInt(); break;
                    case PacketElementTypes.ULong: GetULong(); break;
                    case PacketElementTypes.Bool: GetBool(); break;
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
            var i = sizeof(ushort) + sizeof(long);// op + id + body

            int n = _bodyLength; // + body len
            do { i++; n >>= 7; } while (n != 0);

            n = _elements; // + number of elements
            do { i++; n >>= 7; } while (n != 0);

            i++; // + zero
            i += _bodyLength; // + body

            return i;
        }

        public byte[] Build()
        {
            var result = new byte[GetSize()];
            Build(ref result, 0);

            return result;
        }
        public void Build(ref byte[] buffer, int offset)
        {
            using (_readWriteLock.UpgradeableRead())
            {
                if (buffer.Length < offset + GetSize())
                    throw new Exception(Localization.Get("Shared.Network.Packet.Build.Exception"));

                {
                    Array.Copy(BitConverter.GetBytes(OpCode), 0, buffer, offset, sizeof(ushort));
                    Array.Copy(BitConverter.GetBytes(Id), 0, buffer, offset + sizeof(ushort), sizeof(long));
                    offset += sizeof(ushort) + sizeof(long);

                    WriteVarInt(_bodyLength, buffer, ref offset);
                    WriteVarInt(_elements, buffer, ref offset);

                    buffer[offset++] = 0;
                }

                Array.Copy(Buffer, BodyStart, buffer, offset, _bodyLength);
            }
        }

        private bool IsValidType(PacketElementTypes type)
        {
            return (type >= PacketElementTypes.Byte && type <= PacketElementTypes.Bool);
        }
        public override string ToString()
        {
            var result = new StringBuilder();
            var prevPtr = Position;
            Position = BodyStart;

            result.AppendLine();
            result.AppendFormat("Op: {0:X04} {2}, Id: {1:X16}" + Environment.NewLine, OpCode, Id, OpCodes.GetName(OpCode));

            PacketElementTypes type;
            for (int i = 1; (IsValidType(type = Peek()) && Position < Buffer.Length); ++i)
            {
                switch (type)
                {
                    case PacketElementTypes.Byte:
                        {
                            var data = GetByte();
                            result.AppendFormat("{0:000} [{1}] Byte    : {2}", i, data.ToString("X2").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.SByte:
                        {
                            var data = GetSByte();
                            result.AppendFormat("{0:000} [{1}] SByte   : {2}", i, data.ToString("X2").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Bool:
                        {
                            var data = GetBool();
                            result.AppendFormat("{0:000} [{1}] Bool    : {2}", i, (data ? "01" : "00").PadLeft(32, '.'), data ? "True" : "False");
                        }
                        break;
                    case PacketElementTypes.Short:
                        {
                            var data = GetShort();
                            result.AppendFormat("{0:000} [{1}] Short   : {2}", i, data.ToString("X4").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.UShort:
                        {
                            var data = GetUShort();
                            result.AppendFormat("{0:000} [{1}] UShort  : {2}", i, data.ToString("X4").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Int:
                        {
                            var data = GetInt();
                            result.AppendFormat("{0:000} [{1}] Int     : {2}", i, data.ToString("X8").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.UInt:
                        {
                            var data = GetUInt();
                            result.AppendFormat("{0:000} [{1}] UInt    : {2}", i, data.ToString("X8").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Long:
                        {
                            var data = GetLong();
                            result.AppendFormat("{0:000} [{1}] Long    : {2}", i, data.ToString("X16").PadLeft(32,'.'), data);
                        }
                        break;
                    case PacketElementTypes.ULong:
                        {
                            var data = GetULong();
                            result.AppendFormat("{0:000} [{1}] ULong   : {2}", i, data.ToString("X16").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Float:
                        {
                            var data = GetFloat();

                            var hex = (BitConverter.DoubleToInt64Bits(data) >> 32).ToString("X8");
                            if (hex.Length > 8)
                                hex = hex.Substring(8);

                            result.AppendFormat("{0:000} [{1}] Float   : {2}", i, hex.PadLeft(32, '.'), data.ToString("0.0####", CultureInfo.InvariantCulture));
                        }
                        break;
                    case PacketElementTypes.String:
                        {
                            var data = GetString();
                            result.AppendFormat("{0:000} [{1}] String  : {2}", i, "".PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Bin:
                        {
                            var data = BitConverter.ToString(GetBin());
                            var splitted = data.Split('-');

                            result.AppendFormat("{0:000} [{1}] Bin     : ", i, "".PadLeft(32, '.'));
                            for (var j = 1; j <= splitted.Length; ++j)
                            {
                                result.Append(splitted[j - 1]);
                                if (j < splitted.Length)
                                    if (j % 12 == 0)
                                        result.Append(Environment.NewLine.PadRight(51, ' '));
                                    else
                                        result.Append(' ');
                            }
                        }
                        break;
                    case PacketElementTypes.Decimal:
                        {
                            var data = GetDecimal();
                            var p = decimal.GetBits(data);
                            result.AppendFormat("{0:000} [{1}] Decimal : {2}", i, (p[0].ToString("X8") + p[1].ToString("X8") + p[2].ToString("X8") + p[3].ToString("X8")).PadLeft(32, '.'), data.ToString("0.0####", CultureInfo.InvariantCulture));
                        }
                        break;
                    case PacketElementTypes.Double:
                        {
                            var data = GetDouble();
                            var hex = (BitConverter.DoubleToInt64Bits(data) >> 64).ToString("X8");
                            result.AppendFormat("{0:000} [{1}] Double  : {2}", i, hex.PadLeft(32, '.'), data.ToString("0.0####", CultureInfo.InvariantCulture));
                        }
                        break;
                    case PacketElementTypes.Char:
                        {
                            var data = GetChar();
                            result.AppendFormat("{0:000} [{1}] Char    : {2}", i, ((byte)data).ToString("X2").PadLeft(32, '.'), data);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (_elements != i) { result.AppendLine(); }
            }

            Position = prevPtr;

            return result.ToString();
        }

        public void Dispose()
        {
            using (_readWriteLock.Write())
            {
                Array.Clear(Buffer, 0, Buffer.Length);
                Buffer = null;
                Position = 0;
                BodyStart = 0;
                _bodyLength = 0;
                _elements = 0;
                OpCode = 0;
                Id = 0;
            }
            GC.SuppressFinalize(this);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Packet)) return false;
            var a = ((Packet)obj).Build();
            return Build().SequenceEqual(a);
        }
    }
}
