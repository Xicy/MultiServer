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
        SByte,
        Boolean,
        Char,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Decimal,
        Double,
        String,
        Bin
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

        private readonly int _sizeOfOpCode;
        private readonly int _sizeOfId;
        private struct DataLenght
        {
            private long _lenght;
            public static implicit operator long(DataLenght dl) => dl._lenght;
            public static implicit operator DataLenght(long dl) => new DataLenght() { _lenght = dl };

            public static implicit operator int(DataLenght dl) => (int)dl._lenght;
        }
        private readonly int _sizeOfLenght;
        private readonly int _sizeOfPacketElementType;

        private ushort _opCode;
        public ushort OpCode
        {
            set { _opCode = value; }
            get { return _opCode; }
        }
        private long _id;
        public long Id
        {
            set { _id = value; }
            get { return _id; }
        }

        public Packet(ushort opCode, long id) : this()
        {
            using (_rwLock.Write())
            {
                OpCode = opCode;
                Id = id;
                Buffer = new byte[DefaultBufferSize];
            }
        }
        public Packet(byte[] buffer, int offset) : this()
        {
            using (_rwLock.Write())
            {
                Buffer = buffer;
                Position = offset;

                Helper.ToObject(out _opCode, ref Buffer, ref Position);
                Helper.ToObject(out _id, ref Buffer, ref Position);

                _bodyLength = ReadVarInt(Buffer, ref Position);
                _elements = ReadVarInt(Buffer, ref Position);

                BodyStart = Position;
            }
        }
        protected Packet()
        {
            _sizeOfOpCode = OpCode.SizeOf();
            _sizeOfId = Id.SizeOf();
            _sizeOfLenght = typeof(DataLenght).SizeOf();
            _sizeOfPacketElementType = typeof(PacketElementTypes).SizeOf();
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
                return Position + _sizeOfPacketElementType + 1 > Buffer.Length ? PacketElementTypes.None : Helper.ToObject<PacketElementTypes>(ref Buffer, ref Position, false);

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

        private static bool IsValidType(PacketElementTypes type)
        {
            return type >= PacketElementTypes.Byte && type <= PacketElementTypes.Bin;
        }
        private static PacketElementTypes TypeToElement<T>()
        {
            var type = typeof(T);
            if (type.IsArray && type.GetElementType() == typeof(byte)) return PacketElementTypes.Bin;

            object ret;
            if (Helper.TryParse(typeof(PacketElementTypes), type.Name, true, out ret)) return (PacketElementTypes)ret;

            return type.IsValueType ? PacketElementTypes.Bin : PacketElementTypes.None;
        }

        public Packet Write<T>(T obj)
        {
            var pType = TypeToElement<T>();
            var bytes = Helper.GetBytes(obj);
            if (pType == PacketElementTypes.String || pType == PacketElementTypes.Bin)
            {
                Array.Resize(ref bytes, _sizeOfLenght + bytes.Length);
                System.Buffer.BlockCopy(bytes, 0, bytes, _sizeOfLenght, bytes.Length - _sizeOfLenght);
                Helper.GetBytes((DataLenght)(bytes.Length - _sizeOfLenght)).CopyTo(bytes, 0);
            }
            var length = _sizeOfPacketElementType + bytes.Length;
            using (_rwLock.Write())
            {
                IsRequireSize(length);

                Helper.GetBytes(pType).CopyTo(Buffer, ref Position);

                bytes.CopyTo(Buffer, ref Position);
                _elements++;
                _bodyLength += length;
            }
            return this;
        }
        public T Read<T>()
        {
            var type = TypeToElement<T>();
            var checkType = Peek();
            if (type != checkType) throw new Exception(string.Format("Expected {0}, got {1}.", type, checkType));
            if (type == PacketElementTypes.None) return default(T);
            using (_rwLock.Read())
            {
                Helper.ToObject<PacketElementTypes>(ref Buffer, ref Position);
                DataLenght len = 0;
                if (type == PacketElementTypes.String || type == PacketElementTypes.Bin) Helper.ToObject(out len, ref Buffer, ref Position);
                return Helper.ToObjectWithLenght<T>(ref Buffer, ref Position, len);
            }
        }
        public Packet Read<T>(out T o)
        {
            o = Read<T>();
            return this;
        }

        public void Skip(int num = 1)
        {
            for (var i = 0; i < num; ++i)
            {
                switch (Peek())
                {
                    case PacketElementTypes.Byte: Read<byte>(); break;
                    case PacketElementTypes.SByte: Read<sbyte>(); break;
                    case PacketElementTypes.Int16: Read<short>(); break;
                    case PacketElementTypes.Int32: Read<int>(); break;
                    case PacketElementTypes.Int64: Read<long>(); break;
                    case PacketElementTypes.Single: Read<float>(); break;
                    case PacketElementTypes.String: Read<string>(); break;
                    case PacketElementTypes.Bin: Read<byte[]>(); break;
                    case PacketElementTypes.UInt16: Read<ushort>(); break;
                    case PacketElementTypes.UInt32: Read<uint>(); break;
                    case PacketElementTypes.UInt64: Read<ulong>(); break;
                    case PacketElementTypes.Boolean: Read<bool>(); break;
                    case PacketElementTypes.Decimal: Read<decimal>(); break;
                    case PacketElementTypes.Double: Read<double>(); break;
                    case PacketElementTypes.Char: Read<char>(); break;
                }
            }
        }

        private int ReadVarInt(byte[] buffer, ref int ptr)
        {
            int result = 0;

            for (int i = 0; ; ++i)
            {
                result |= (buffer[ptr] & 0x7F) << (i * 7);
                if ((buffer[ptr++] & 0x80) == 0) break;
            }

            return result;
        }
        private void WriteVarInt(int value, byte[] buffer, ref int ptr)
        {
            do
            {
                buffer[ptr++] = (byte)(value > 0x7F ? 0x80 | (value & 0xFF) : value & 0xFF);
            }
            while ((value >>= 7) != 0);
        }

        public int GetSize()
        {
            var i = _sizeOfOpCode + _sizeOfId; //sizeof(ushort) + sizeof(long);// op + id + body

            int n = _bodyLength; // + body len
            do { i++; n >>= 7; } while (n != 0);

            n = _elements; // + number of elements
            do { i++; n >>= 7; } while (n != 0);

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
            using (_rwLock.UpgradeableRead())
            {
                if (buffer.Length < offset + GetSize())
                    throw new Exception(Localization.Get("Shared.Network.Packet.Build.Exception"));

                {
                    Helper.GetBytes(OpCode).CopyTo(buffer, ref offset);
                    Helper.GetBytes(Id).CopyTo(buffer, ref offset);

                    WriteVarInt(_bodyLength, buffer, ref offset);
                    WriteVarInt(_elements, buffer, ref offset);
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
            result.AppendFormat("Op: {0:X04}, Id: {1:X16}" + Environment.NewLine, OpCode, Id);

            PacketElementTypes type;
            for (int i = 1; (IsValidType(type = Peek()) && Position < Buffer.Length); ++i)
            {
                switch (type)
                {
                    case PacketElementTypes.Byte:
                        {
                            var data = Read<byte>();
                            result.AppendFormat("{0:000} [{1}] Byte    : {2}", i, data.ToString("X2").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.SByte:
                        {
                            var data = Read<sbyte>();
                            result.AppendFormat("{0:000} [{1}] SByte   : {2}", i, data.ToString("X2").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Boolean:
                        {
                            var data = Read<bool>();
                            result.AppendFormat("{0:000} [{1}] Boolean    : {2}", i, (data ? "01" : "00").PadLeft(32, '.'), data ? "True" : "False");
                        }
                        break;
                    case PacketElementTypes.Int16:
                        {
                            var data = Read<short>();
                            result.AppendFormat("{0:000} [{1}] Int16   : {2}", i, data.ToString("X4").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.UInt16:
                        {
                            var data = Read<ushort>();
                            result.AppendFormat("{0:000} [{1}] UInt16  : {2}", i, data.ToString("X4").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Int32:
                        {
                            var data = Read<int>();
                            result.AppendFormat("{0:000} [{1}] Int32     : {2}", i, data.ToString("X8").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.UInt32:
                        {
                            var data = Read<uint>();
                            result.AppendFormat("{0:000} [{1}] UInt32    : {2}", i, data.ToString("X8").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Int64:
                        {
                            var data = Read<long>();
                            result.AppendFormat("{0:000} [{1}] Int64    : {2}", i, data.ToString("X16").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.UInt64:
                        {
                            var data = Read<ulong>();
                            result.AppendFormat("{0:000} [{1}] UInt64   : {2}", i, data.ToString("X16").PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Single:
                        {
                            var data = Read<float>();

                            var hex = (BitConverter.DoubleToInt64Bits(data) >> 32).ToString("X8");
                            if (hex.Length > 8)
                                hex = hex.Substring(8);

                            result.AppendFormat("{0:000} [{1}] Single   : {2}", i, hex.PadLeft(32, '.'), data.ToString("0.0####", CultureInfo.InvariantCulture));
                        }
                        break;
                    case PacketElementTypes.String:
                        {
                            var data = Read<string>();
                            result.AppendFormat("{0:000} [{1}] String  : {2}", i, "".PadLeft(32, '.'), data);
                        }
                        break;
                    case PacketElementTypes.Bin:
                        {
                            var data = BitConverter.ToString(Read<byte[]>());
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
                            var data = Read<decimal>();
                            var p = decimal.GetBits(data);
                            result.AppendFormat("{0:000} [{1}] Decimal : {2}", i, (p[0].ToString("X8") + p[1].ToString("X8") + p[2].ToString("X8") + p[3].ToString("X8")).PadLeft(32, '.'), data.ToString("0.0####", CultureInfo.InvariantCulture));
                        }
                        break;
                    case PacketElementTypes.Double:
                        {
                            var data = Read<double>();
                            var hex = (BitConverter.DoubleToInt64Bits(data) >> 64).ToString("X8");
                            result.AppendFormat("{0:000} [{1}] Double  : {2}", i, hex.PadLeft(32, '.'), data.ToString("0.0####", CultureInfo.InvariantCulture));
                        }
                        break;
                    case PacketElementTypes.Char:
                        {
                            var data = Read<char>();
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
