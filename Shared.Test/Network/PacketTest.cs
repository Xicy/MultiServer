using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared.Network;

namespace Shared.Test.Network
{
    [TestClass]
    public class PacketTest
    {
        struct Test
        {
            public bool a;
            public int b;

            public Test(bool k, int l)
            {
                a = k;
                b = l;
            }
        }

        private byte[] pack;

        [TestInitialize]
        public void Build()
        {
            pack = new Packet(1, 0)
                .Write((byte)1)
                .Write(unchecked((byte)-1))

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

                .Write(Decimal.MaxValue)

                .Write(1D)
                .Write(-1D)

                .Write("Test")
                .Write('C')

                .Write(new byte[] { 0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x02 })

                .Write(new Test(true, 1234))

                .Build();
        }

        [TestMethod]
        public void Read()
        {
            var pck = new Packet(pack, 0);
            Assert.AreEqual(pck.OpCode, 1, "OpCode Test");
            Assert.AreEqual(pck.Id, 0, "ID Test");

            Assert.ThrowsException<Exception>(() => pck.Read<short>(), "Throw Error Test");

            Assert.AreEqual(pck.Read<byte>(), (byte)1, "Byte Test");
            Assert.AreEqual((sbyte)pck.Read<byte>(), (sbyte)-1, "SByte Test");

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

            var a = pck.ToString();
        }

        [TestMethod]
        public void Skip()
        {
            int i = 0;
            for (Packet pck = new Packet(pack, 0); pck.Peek() != PacketElementTypes.None; i++)
            {
                pck.Skip();
            }
            Assert.AreEqual(i, 19, "Element Size Test");
        }

    }
}
