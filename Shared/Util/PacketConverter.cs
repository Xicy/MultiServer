using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Util
{
    public static class PacketConverter
    {
        private static readonly Type Bct = typeof(BitConverter);
        private static readonly Encoding Encoding = Encoding.UTF8;

        public delegate T PacketConverterEvent<out T>(ref byte[] buffer, ref int position, bool skipPosition);
        public delegate T PacketConverterWithLenghtEvent<out T>(ref byte[] buffer, ref int position, int length, bool skipPosition);

        internal static T ToObjectWithLenght<T>(ref byte[] buffer, ref int position, int length, bool skipPosition)
        {
            var type = typeof(T);
            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            object ret;

            if (length == 0)
            {
                length = Marshal.SizeOf(type);
                if (type == typeof(bool)) length = 1;
                else if (type == typeof(char)) length = 2;
            }
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (position + length > buffer.Length) throw new IndexOutOfRangeException(Localization.Get("Shared.Network.Packet.IsReadable.Exception"));

            if (!type.IsArray)
            {
                if (type == typeof(byte))
                {
                    ret = buffer[position];
                }
                else if (type == typeof(decimal))
                {
                    var bits = new int[4];
                    for (var i = 0; i <= 15; i += 4)
                    {
                        bits[i / 4] = BitConverter.ToInt32(buffer, position + i);
                    }
                    ret = new decimal(bits);
                }
                else if (type == typeof(string))
                {
                    ret = Encoding.GetString(buffer, position, length);
                }
                else if (type.IsPrimitive)
                {
                    var miGetBytes = Bct.GetMethod("To" + type.Name);
                    if (miGetBytes == null)
                        throw new InvalidOperationException("Method: GetBytes on BitConverter does not have an overload accepting one paramter of type: " + type.FullName);
                    ret = miGetBytes.Invoke(null, new object[] { buffer, position });
                }
                else
                {
                    var intPtr = IntPtr.Zero;
                    try
                    {
                        intPtr = Marshal.AllocHGlobal(length);
                        Marshal.Copy(buffer, position, intPtr, length);
                        ret = Marshal.PtrToStructure(intPtr, typeof(T));
                    }
                    finally
                    {
                        if (intPtr != IntPtr.Zero)
                            Marshal.FreeHGlobal(intPtr);
                    }
                }
            }
            else if (type.GetElementType() == typeof(byte))
            {
                ret = new byte[length];
                Buffer.BlockCopy(buffer, position, (byte[])ret, 0, length);
            }
            else throw new InvalidDataException("Data not resolved.");

            if (skipPosition) position += length;
            return (T)ret;
        }
        internal static T ToObject<T>(ref byte[] buffer, ref int position, bool skipPosition)
        {
            return ToObjectWithLenght<T>(ref buffer, ref position, 0, skipPosition);
        }
        internal static void ToObject<T>(out T ret, byte[] buffer, ref int position, bool skipPosition)
        {
            ret = ToObjectWithLenght<T>(ref buffer, ref position, 0, skipPosition);
        }

        internal static long DoubleToInt64Bits(double value) => BitConverter.DoubleToInt64Bits(value);
        internal static string ToStringBinary(byte[] value) => BitConverter.ToString(value);


        internal static byte[] GetBytes(object obj)
        {
            var type = obj.GetType();
            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            byte[] bytesRet;
            if (type == typeof(byte)) bytesRet = new[] { (byte)obj };
            else if (type == typeof(decimal))
            {
                var bits = decimal.GetBits((decimal)obj);
                bytesRet = new byte[16];
                for (var i = 0; i < 4; i++)
                    BitConverter.GetBytes(bits[i]).CopyTo(bytesRet, i * 4);
            }
            else if (type == typeof(string)) bytesRet = Encoding.GetBytes((string)obj);
            else if (type.IsPrimitive)
            {
                var miGetBytes = Bct.GetMethod("GetBytes", new[] { type });
                if (miGetBytes == null)
                    throw new InvalidOperationException(
                        "Method: GetBytes on BitConverter does not have an overload accepting one paramter of type: " +
                        type.FullName);
                bytesRet = (byte[])miGetBytes.Invoke(null, new object[] { obj });
            }
            else
            {
                var size = Marshal.SizeOf(obj);
                var ptr = IntPtr.Zero;
                bytesRet = new byte[size];

                try
                {
                    ptr = Marshal.AllocHGlobal(size);
                    Marshal.StructureToPtr(obj, ptr, true);
                    Marshal.Copy(ptr, bytesRet, 0, size);
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                        Marshal.FreeHGlobal(ptr);
                }
            }

            return bytesRet;
        }

    }
}