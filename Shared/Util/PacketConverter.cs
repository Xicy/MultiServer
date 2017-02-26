using Shared.Network;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Util
{
    public static class PacketConverter
    {
        internal static byte ToByte(byte[] buffer, int position) => buffer[position];
        internal static sbyte ToSByte(byte[] buffer, int position) => (sbyte)ToByte(buffer, position);
        internal static bool ToBoolean(byte[] buffer, int position) => BitConverter.ToBoolean(buffer, position);
        internal static short ToInt16(byte[] buffer, int position) => BitConverter.ToInt16(buffer, position);
        internal static ushort ToUInt16(byte[] buffer, int position) => BitConverter.ToUInt16(buffer, position);
        internal static char ToChar(byte[] buffer, int position) => BitConverter.ToChar(buffer, position);
        internal static int ToInt32(byte[] buffer, int position) => BitConverter.ToInt32(buffer, position);
        internal static uint ToUInt32(byte[] buffer, int position) => BitConverter.ToUInt32(buffer, position);
        internal static float ToSingle(byte[] buffer, int position) => BitConverter.ToSingle(buffer, position);
        internal static long ToInt64(byte[] buffer, int position) => BitConverter.ToInt64(buffer, position);
        internal static ulong ToUInt64(byte[] buffer, int position) => BitConverter.ToUInt64(buffer, position);
        internal static double ToDouble(byte[] buffer, int position) => BitConverter.ToDouble(buffer, position);

        public static decimal ToDecimal(byte[] buffer, int position)
        {
            var bits = new int[4];
            for (var i = 0; i <= 15; i += 4)
            {
                bits[i / 4] = BitConverter.ToInt32(buffer, position + i);
            }
            return new decimal(bits);
        }

        internal static string ToString(byte[] buffer, int position, int length) => Encoding.UTF8.GetString(buffer, position, length);
        internal static byte[] ToBin(byte[] buffer, int position, int length) { var val = new byte[length]; Array.Copy(buffer, position, val, 0, length); return val; }

        internal static T ToObject<T>(byte[] buffer, int position, int length)
        {
            object val;
            var intPtr = IntPtr.Zero;
            try
            {
                intPtr = Marshal.AllocHGlobal(length);
                Marshal.Copy(buffer, position, intPtr, length);
                val = Marshal.PtrToStructure(intPtr, typeof(T));
            }
            finally
            {
                if (intPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(intPtr);
            }
            return (T)val;
        }

        public static byte[] GetBytes(decimal dec)
        {
            var bits = decimal.GetBits(dec);
            var bytes = new byte[16];
            for (var i = 0; i < 4; i++)
                BitConverter.GetBytes(bits[i]).CopyTo(bytes, i * 4);
            return bytes;
        }

    }
}