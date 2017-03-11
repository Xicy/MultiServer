using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared.Network;
using Shared.Util;

namespace Shared.Test.Network
{
    [TestClass]
    public class Packet
    {
        private struct Test
        {
            private bool _a;
            private int _b;

            public Test(bool k, int l)
            {
                _a = k;
                _b = l;
            }
        }

        private byte[] _pack;

        [TestInitialize]
        public void Build()
        {
            _pack = new Shared.Network.Packet(1, 0)
                .Write((byte)1)
                .Write((sbyte)-1)

                .Write(true)
                .Write(false)

                .Write((short)-1)
                .Write((ushort)1)

                .Write(1U)
                .Write(-1)

                .Write(-1L)
                .Write(1UL)

                .Write(1F)
                .Write(-1F)

                .Write(decimal.MaxValue)

                .Write(1D)
                .Write(-1D)

                .Write("Test")
                .Write('C')

                .Write(new byte[] { 0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x02 })

                .Write(new Test(true, 1234))

                .Build();
        }

        [TestMethod]
        public void AutoSizeStructure()
        {
            Assert.ThrowsException<InvalidDataException>(() => new Shared.Network.Packet(0, 0).Write(DateTime.Now));
        }

        [TestMethod]
        public void Read()
        {
            var pck = new Shared.Network.Packet(_pack, 0);
            Assert.AreEqual(pck.OpCode, 1, "OpCode Test");
            Assert.AreEqual(pck.Id, 0, "ID Test");

            Assert.ThrowsException<Exception>(() => pck.Read<short>(), "Throw Error Test");

            Assert.AreEqual(pck.Read<byte>(), (byte)1, "Byte Test");
            Assert.AreEqual(pck.Read<sbyte>(), (sbyte)-1, "SByte Test");

            Assert.AreEqual(pck.Read<bool>(), true, "Boolean Test");
            Assert.AreEqual(pck.Read<bool>(), false, "Boolean Test");

            Assert.AreEqual(pck.Read<short>(), (short)-1, "Int16 Test");
            Assert.AreEqual(pck.Read<ushort>(), (ushort)1, "UInt16 Test");

            Assert.AreEqual(pck.Read<uint>(), 1U, "Int32 Test");
            Assert.AreEqual(pck.Read<int>(), -1, "UInt32 Test");

            Assert.AreEqual(pck.Read<long>(), -1L, "Int64 Test");
            Assert.AreEqual(pck.Read<ulong>(), 1UL, "UInt64 Test");

            Assert.AreEqual(pck.Read<float>(), 1F, "Single Test");
            Assert.AreEqual(pck.Read<float>(), -1F, "Single Test");

            Assert.AreEqual(pck.Read<decimal>(), Decimal.MaxValue, "Decimal Test");

            Assert.AreEqual(pck.Read<double>(), 1D, "Double Test");
            Assert.AreEqual(pck.Read<double>(), -1D, "Double Test");


            Assert.AreEqual(pck.Read<string>(), "Test", "String Test");
            Assert.AreEqual(pck.Read<char>(), 'C', "Char Test");

            CollectionAssert.AreEqual(pck.Read<byte[]>(), new byte[] { 0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x02 }, "Bin Test");

            Assert.AreEqual(pck.Read<Test>(), new Test(true, 1234), "Object Test");

            Assert.AreEqual(pck.Read<object>(), null, "Test null object");

            Assert.AreEqual(pck.Peek(), PacketElementTypes.None, "Finish Test");
        }

        [TestMethod]
        public void Benchmark()
        {
            Console.WriteLine("---Build Benchmark---");
            Helper.Clock.BenchmarkTime(Build, 1000);

            Console.WriteLine("---Read Benchmark---");
            Helper.Clock.BenchmarkTime(Read, 1000);

            Console.WriteLine("---Skip Benchmark---");
            Helper.Clock.BenchmarkTime(Skip, 1000);
        }

        [TestMethod]
        public void Skip()
        {
            var pck = new Shared.Network.Packet(_pack, 0);
            var totalElement = pck.GetField("_elements");
            var i = 0;
            for (; pck.Peek() != PacketElementTypes.None; i++)
            {
                pck.Skip();
            }
            Assert.AreEqual(i, totalElement, "Element Size Test");
        }

        [TestMethod]
        public void ValidTypes()
        {
            var pck = Shared.Network.Packet.Empty();
            foreach (PacketElementTypes pet in Enum.GetValues(typeof(PacketElementTypes)))
            {
                Assert.AreEqual(pck.GetMethod("IsValidType", pet), pet != PacketElementTypes.None, pet.ToString());
            }
        }

        [TestMethod]
        public void Marshal()
        {
            Assert.AreEqual(default(PacketElementTypes).SizeOf(), 1);
            Assert.AreEqual(default(byte).SizeOf(), 1);
            Assert.AreEqual(default(sbyte).SizeOf(), 1);
            Assert.AreEqual(default(int).SizeOf(), 4);
            Assert.AreEqual(default(uint).SizeOf(), 4);
            Assert.AreEqual(default(short).SizeOf(), 2);
            Assert.AreEqual(default(ushort).SizeOf(), 2);
            Assert.AreEqual(default(long).SizeOf(), 8);
            Assert.AreEqual(default(ulong).SizeOf(), 8);
            Assert.AreEqual(default(float).SizeOf(), 4);
            Assert.AreEqual(default(double).SizeOf(), 8);
            Assert.AreEqual(default(char).SizeOf(), 2);
            Assert.AreEqual(default(bool).SizeOf(), 1);
            Assert.AreEqual(default(decimal).SizeOf(), 16);

        }


    }
}
