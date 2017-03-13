using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Util
{
    public static class Helper
    {
        public static bool IsNullOrWhiteSpace(this string value)
        {
            return value == null || value.All(char.IsWhiteSpace);
        }

        public static bool TryParse(Type _enum, string value, bool ignoreCase, out object result)
        {
            if (Enum.IsDefined(_enum, value))
            {
                result = Enum.Parse(_enum, value, ignoreCase);
                return true;
            }

            result = null;
            return false;
        }

        public static bool HasFlag(ulong value, ulong flag)
        {
            return (value & flag) > 0;
        }



        private static readonly Type Bct = typeof(BitConverter);
        private static readonly Encoding Encoding = Encoding.UTF8;

        internal static void CopyTo(this Array srcArray, Array dstArray, ref int dstOffset)
        {
            Buffer.BlockCopy(srcArray, 0, dstArray, dstOffset, srcArray.Length);
            dstOffset += srcArray.Length;
        }

        public static int SizeOf(this object obj)
        {
            return obj.GetType().SizeOf();
        }
        public static int SizeOf(this Type type)
        {
            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            int length;

            if (type == typeof(bool)) length = 1;
            else if (type == typeof(char)) length = 2;
            else length = Marshal.SizeOf(type);

            return length;
        }

        internal static T ToObjectWithLenght<T>(ref byte[] buffer, ref int position, int length = 0, bool skipPosition = true)
        {
            var type = typeof(T);
            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            object ret;

            length = length <= 0 ? type.SizeOf() : length;

            if (position + length > buffer.Length) throw new IndexOutOfRangeException(Localization.Get("Shared.Network.Packet.IsReadable.Exception"));

            if (!type.IsArray)
            {
                if (type == typeof(byte))
                {
                    ret = buffer[position];
                }
                else if (type == typeof(sbyte))
                {
                    ret = unchecked((sbyte)buffer[position]);
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
                    ret = Bct.GetMethod("To" + type.Name).Invoke(null, new object[] { buffer, position });
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
        internal static T ToObject<T>(ref byte[] buffer, ref int position, bool skipPosition = true)
        {
            return ToObjectWithLenght<T>(ref buffer, ref position, 0, skipPosition);
        }
        internal static void ToObject<T>(out T ret, ref byte[] buffer, ref int position, bool skipPosition = true)
        {
            ret = ToObjectWithLenght<T>(ref buffer, ref position, 0, skipPosition);
        }

        internal static byte[] GetBytes(object obj)
        {
            var type = obj.GetType();
            if (type.IsArray && (type.GetElementType() == typeof(byte) || type.GetElementType() == typeof(sbyte))) return (byte[])obj;
            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            byte[] bytesRet;
            if (type == typeof(byte)) bytesRet = new[] { (byte)obj };
            else if (type == typeof(sbyte)) bytesRet = new[] { unchecked((byte)(sbyte)obj) }; //Find better solition
            else if (type == typeof(decimal))
            {
                var bits = decimal.GetBits((decimal)obj);
                bytesRet = new byte[16];
                for (var i = 0; i < 4; i++)
                    BitConverter.GetBytes(bits[i]).CopyTo(bytesRet, i * 4);
            }
            else if (type == typeof(string)) bytesRet = Encoding.GetBytes((string)obj);
            else if (type.IsPrimitive) bytesRet = (byte[])Bct.GetMethod("GetBytes", new[] { type }).Invoke(null, new[] { obj });
            else if (type.StructLayoutAttribute != null && type.StructLayoutAttribute.Value != LayoutKind.Auto)
            {
                var size = obj.SizeOf();//Marshal.SizeOf(obj);
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
            else throw new InvalidDataException("Object Not Recognized");
            return bytesRet;
        }
    }
}