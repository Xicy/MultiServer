using System;
using System.Text;
using Shared.Util;
using System.Threading;
using System.Globalization;
using System.Linq;

namespace Shared.Network
{
    public enum PacketElementTypes : byte
    {
        None,
        Byte,
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
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        protected byte[] Buffer;
        protected int Position;
        protected int BodyStart;
        private int _elements;
        private int _bodyLength;
        private ushort _opCode;
        private long _id;

        public ushort OpCode
        {
            set { _opCode = value; }
            get { return _opCode; }
        }
        public long Id
        {
            set { _id = value; }
            get { return _id; }
        }

        public Packet(ushort opCode, long id)
        {
            using (_rwLock.Write())
            {
                OpCode = opCode;
                Id = id;
                Buffer = new byte[DefaultBufferSize];
            }
        }
        public Packet(byte[] buffer, int offset)
        {
            using (_rwLock.Write())
            {
                Buffer = buffer;
                Position = offset;

                PacketConverter.ToObject(out _opCode, Buffer, ref Position, true);
                PacketConverter.ToObject(out _id, Buffer, ref Position, true);

                _bodyLength = ReadVarInt(Buffer, ref Position);
                _elements = ReadVarInt(Buffer, ref Position);

                Position++; //0x00

                BodyStart = Position;
            }
        }

        public Packet Clear(ushort opCode, long id)
        {
            using (_rwLock.Write())
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
            using (_rwLock.Read())
            {
                if (Position + sizeof(PacketElementTypes) + 1 > Buffer.Length) { return PacketElementTypes.None; }
                return PacketConverter.ToObject<PacketElementTypes>(ref Buffer, ref Position, false);
            }
        }
        public bool NextIs(PacketElementTypes type)
        {
            return Peek() == type;
        }
        protected bool IsRequireSize(int required)
        {
            if (Position + required < Buffer.Length) return false;
            Array.Resize(ref Buffer, Buffer.Length + Math.Max(AddSize, required * 2));
            return true;
        }
        private bool IsValidType(PacketElementTypes type)
        {
            return type >= PacketElementTypes.Byte && type <= PacketElementTypes.Bool;
        }

        #region Write
        //TODO: write and write with lenght use with packet converter class
        protected Packet WriteSimple(PacketElementTypes type, params byte[] val)
        {
            using (_rwLock.Write())
            {
                var length = sizeof(PacketElementTypes) + val.Length;
                IsRequireSize(length);

                PacketConverter.GetBytes(type).CopyTo(Buffer, Position);
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
            System.Buffer.BlockCopy(val, 0, val, sizeof(ushort), val.Length - sizeof(ushort));
            PacketConverter.GetBytes((ushort)(val.Length - sizeof(ushort))).CopyTo(val, 0);
            return WriteSimple(type, val);
        }

        public Packet Write() { return Write((byte)0); }
        public Packet Write(byte val) { return WriteSimple(PacketElementTypes.Byte, val); }
        public Packet Write(bool val) { return WriteSimple(PacketElementTypes.Bool, PacketConverter.GetBytes(val)); }
        public Packet Write(short val) { return WriteSimple(PacketElementTypes.Short, PacketConverter.GetBytes(val)); }
        public Packet Write(ushort val) { return WriteSimple(PacketElementTypes.UShort, PacketConverter.GetBytes(val)); }
        public Packet Write(int val) { return WriteSimple(PacketElementTypes.Int, PacketConverter.GetBytes(val)); }
        public Packet Write(uint val) { return WriteSimple(PacketElementTypes.UInt, PacketConverter.GetBytes(val)); }
        public Packet Write(long val) { return WriteSimple(PacketElementTypes.Long, PacketConverter.GetBytes(val)); }
        public Packet Write(ulong val) { return WriteSimple(PacketElementTypes.ULong, PacketConverter.GetBytes(val)); }
        public Packet Write(float val) { return WriteSimple(PacketElementTypes.Float, PacketConverter.GetBytes(val)); }
        public Packet Write(decimal val) { return WriteSimple(PacketElementTypes.Decimal, PacketConverter.GetBytes(val)); }
        public Packet Write(double val) { return WriteSimple(PacketElementTypes.Double, PacketConverter.GetBytes(val)); }
        public Packet Write(string val) { return WriteWithLength(PacketElementTypes.String, PacketConverter.GetBytes(val)); }
        public Packet Write(char val) { return WriteSimple(PacketElementTypes.Char, PacketConverter.GetBytes(val)); }
        public Packet Write(string format, params object[] args) { return Write(string.Format(format ?? string.Empty, args)); }
        public Packet Write(byte[] val) { return WriteWithLength(PacketElementTypes.Bin, val); }
        public Packet Write(object val) { return Write(PacketConverter.GetBytes(val)); }
        #endregion

        #region Read
        protected T Get<T>(PacketConverter.PacketConverterEvent<T> converter)
        {
            using (_rwLock.Read())
            {
                PacketConverter.ToObject<PacketElementTypes>(ref Buffer, ref Position, true);
                var val = converter(ref Buffer, ref Position, true);
                return val;
            }
        }
        protected T Get<T>(PacketConverter.PacketConverterWithLenghtEvent<T> converter)
        {
            var len = Get(PacketConverter.ToObject<ushort>);
            using (_rwLock.Read())
            {
                var val = converter(ref Buffer, ref Position, len, true);
                return val;
            }
        }

        public byte GetByte()
        {
            if (!NextIs(PacketElementTypes.Byte))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetByte.Exception"), Peek()));
            return Get(PacketConverter.ToObject<byte>);
        }
        public bool GetBool()
        {
            if (!NextIs(PacketElementTypes.Bool))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetBool.Exception"), Peek()));
            return Get(PacketConverter.ToObject<bool>);
        }
        public short GetShort()
        {
            if (!NextIs(PacketElementTypes.Short))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetShort.Exception"), Peek()));
            return Get(PacketConverter.ToObject<short>);
        }
        public ushort GetUShort()
        {
            if (!NextIs(PacketElementTypes.UShort))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetUShort.Exception"), Peek()));
            return Get(PacketConverter.ToObject<ushort>);
        }
        public int GetInt()
        {
            if (!NextIs(PacketElementTypes.Int))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetInt.Exception"), Peek()));
            return Get(PacketConverter.ToObject<int>);
        }
        public uint GetUInt()
        {
            if (!NextIs(PacketElementTypes.UInt))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetUInt.Exception"), Peek()));
            return Get(PacketConverter.ToObject<uint>);
        }
        public long GetLong()
        {
            if (!NextIs(PacketElementTypes.Long))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetLong.Exception"), Peek()));
            return Get(PacketConverter.ToObject<long>);
        }
        public ulong GetULong()
        {
            if (!NextIs(PacketElementTypes.ULong))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetULong.Exception"), Peek()));
            return Get(PacketConverter.ToObject<ulong>);
        }
        public float GetFloat()
        {
            if (!NextIs(PacketElementTypes.Float))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetFloat.Exception"), Peek()));
            return Get(PacketConverter.ToObject<float>);
        }
        public decimal GetDecimal()
        {
            if (!NextIs(PacketElementTypes.Decimal))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetFloat.Exception"), Peek()));
            return Get(PacketConverter.ToObject<decimal>);
        }
        public double GetDouble()
        {
            if (!NextIs(PacketElementTypes.Double))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetFloat.Exception"), Peek()));
            return Get(PacketConverter.ToObject<double>);
        }
        public string GetString()
        {
            if (!NextIs(PacketElementTypes.String))
                throw new ArgumentException(string.Format(Localization.Get("Shared.Network.Packet.GetString.Exception"), Peek()));
            return Get(PacketConverter.ToObjectWithLenght<string>);
        }
        public char GetChar()
        {
            if (!NextIs(PacketElementTypes.Char))
                throw new Exception(string.Format(Localization.Get("Shared.Network.Packet.GetByte.Exception"), Peek()));
            return Get(PacketConverter.ToObject<char>);
        }
        public byte[] GetBin()
        {
            if (!NextIs(PacketElementTypes.Bin))
                throw new ArgumentException(string.Format(Localization.Get("Shared.Network.Packet.GetBin.Exception"), Peek()));
            return Get(PacketConverter.ToObjectWithLenght<byte[]>);
        }
        public T GetObj<T>() where T : new() => Get(PacketConverter.ToObjectWithLenght<T>);
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
                    case PacketElementTypes.UShort: GetUShort(); break;
                    case PacketElementTypes.UInt: GetUInt(); break;
                    case PacketElementTypes.ULong: GetULong(); break;
                    case PacketElementTypes.Bool: GetBool(); break;
                    case PacketElementTypes.Decimal: GetDecimal(); break;
                    case PacketElementTypes.Double: GetDouble(); break;
                    case PacketElementTypes.Char: GetChar(); break;
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
        //TODO: opcode and id do spesific
        public void Build(ref byte[] buffer, int offset)
        {
            using (_rwLock.UpgradeableRead())
            {
                if (buffer.Length < offset + GetSize())
                    throw new Exception(Localization.Get("Shared.Network.Packet.Build.Exception"));

                {
                    System.Buffer.BlockCopy(PacketConverter.GetBytes(OpCode), 0, buffer, offset, sizeof(ushort));
                    System.Buffer.BlockCopy(PacketConverter.GetBytes(Id), 0, buffer, offset + sizeof(ushort), sizeof(long));
                    offset += sizeof(ushort) + sizeof(long);

                    WriteVarInt(_bodyLength, buffer, ref offset);
                    WriteVarInt(_elements, buffer, ref offset);

                    buffer[offset++] = 0;
                }

                System.Buffer.BlockCopy(Buffer, BodyStart, buffer, offset, _bodyLength);
            }
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
                            result.AppendFormat("{0:000} [{1}] Long    : {2}", i, data.ToString("X16").PadLeft(32, '.'), data);
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

                            var hex = (PacketConverter.DoubleToInt64Bits(data) >> 32).ToString("X8");
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
                            var data = PacketConverter.ToStringBinary(GetBin());
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
                            var hex = (PacketConverter.DoubleToInt64Bits(data) >> 64).ToString("X8");
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
            using (_rwLock.Write())
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
            return Build().SequenceEqual(((Packet)obj).Build());
        }
    }
}
