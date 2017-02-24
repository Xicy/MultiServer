using System.Linq;
using System.Reflection;

namespace Shared
{
    public static class OpCodes
    {
        public const long ServerID = 0;

        public const ushort Crypter = 0;
        public const ushort Ping = 1;
        public const ushort Login = 2;

        public const ushort AddNewUser = 3;
        public const ushort AddNewCharacter = 4;

        public const ushort MoveObject = 918;

        public static string GetName(ushort opcode)
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(filed => filed.FieldType == typeof(ushort)).Where(field => (ushort)field.GetValue(null) == opcode))
            {
                return field.Name;
            }
            return "<NULL>";
        }
    }
}
