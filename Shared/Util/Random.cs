using System;
using System.Text;
using System.Linq;

namespace Shared.Util
{
    public static class URandom
    {
        private static readonly Random Rand = new Random((int)DateTime.Now.Ticks);

        public const string PattenLower = "abcdefghijklmnopqrstuvwxyz";
        public const string PattenUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const string PattenInt = "0123456789";
        public const string PattenSpecial = "!\"#$%&'()*+`-./:;<=>?@[\\]^_[]";
        public const string PattenAll = PattenUpper + PattenLower + PattenInt + PattenSpecial;

        public static string String(int length, string patten)
        {
            return new string(Enumerable.Repeat(patten, length).Select(s => s[Rand.Next(s.Length)]).ToArray());
        }
        public static string String(int length)
        {
            var data = new byte[length];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)Rand.Next(32, 127);
            }
            return Encoding.UTF8.GetString(data);
        }
        public static void Byte(byte[] buffer)
        {
            Rand.NextBytes(buffer);
        }
        public static int Int()
        {
            return Rand.Next();
        }
        public static int Int(int max)
        {
            return Rand.Next(max);
        }
        public static int Int(int min, int max)
        {
            return Rand.Next(min, max);
        }
        public static long Long()
        {
            return Long(0, long.MaxValue);
        }
        public static long Long(long max)
        {
            return Long(0, max);
        }
        public static long Long(long min, long max)
        {
            var buf = new byte[8];
            Rand.NextBytes(buf);
            var longRand = BitConverter.ToInt64(buf, 0);
            return (Math.Abs(longRand % (max - min)) + min);
        }
        public static double Double()
        {
            return Rand.NextDouble();
        }
    }
}
