using System.Runtime.InteropServices;

namespace Shared.Schema
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GameObject
    {
        public long ID { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }

        public GameObject(long id, byte x, byte y)
        {
            ID = id;
            X = x;
            Y = y;
        }
    }
}