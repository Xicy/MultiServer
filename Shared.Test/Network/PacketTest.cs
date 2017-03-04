using System;
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

        private readonly Packet tPacket = Packet.Empty();

        [TestInitialize]
        public void Packet_Build()
        {
            pack = new Packet(1, 0)
                .Write()
                .Write((byte)1)
                //.Write((sbyte)-1)

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
                .Write("{0} test", "format")

                .Write(new byte[] { 0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x02 })

                .Write(new Test(true, 1234))

                .Build();
        }

        [TestMethod]
        public void Packet_Read()
        {
            var pck = new Packet(pack, 0);
            Assert.AreEqual(pck.OpCode, 1, "OpCode Test");
            Assert.AreEqual(pck.Id, 0, "ID Test");

            Assert.AreEqual(pck.GetByte(), (byte)0, "Zero Byte Test");
            Assert.AreEqual(pck.GetByte(), (byte)1, "Byte Test");
            //Assert.AreEqual(pck.GetSByte(), (sbyte)-1, "SByte Test");

            Assert.AreEqual(pck.GetBool(), true, "Bool Test");
            Assert.AreEqual(pck.GetBool(), false, "Bool Test");

            Assert.AreEqual(pck.GetShort(), (short)-1, "Short Test");
            Assert.AreEqual(pck.GetUShort(), (ushort)1, "UShort Test");

            Assert.AreEqual(pck.GetUInt(), 1U, "Int Test");
            Assert.AreEqual(pck.GetInt(), -1, "UInt Test");

            Assert.AreEqual(pck.GetLong(), -1L, "Long Test");
            Assert.AreEqual(pck.GetULong(), 1UL, "ULong Test");

            Assert.AreEqual(pck.GetFloat(), 1F, "Float Test");
            Assert.AreEqual(pck.GetFloat(), -1F, "Float Test");

            Assert.AreEqual(pck.GetDecimal(), Decimal.MaxValue, "Decimal Test");

            Assert.AreEqual(pck.GetDouble(), 1D, "Double Test");
            Assert.AreEqual(pck.GetDouble(), -1D, "Double Test");


            Assert.AreEqual(pck.GetString(), "Test", "String Test");
            Assert.AreEqual(pck.GetChar(), 'C', "Char Test");
            Assert.AreEqual(pck.GetString(), "format test", "Formated String Test");

            CollectionAssert.AreEqual(pck.GetBin(), new byte[] { 0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x02 }, "Bin Test");

            Assert.AreEqual(pck.GetObj<Test>(), new Test(true, 1234), "Object Test");

            Assert.AreEqual(pck.Peek(), PacketElementTypes.None, "Finish Test");

            var a = pck.ToString();
        }

        [TestMethod]
        public void Packet_Skip()
        {
            int i = 0;
            for (Packet pck = new Packet(pack, 0); pck.Peek() != PacketElementTypes.None; i++)
            {
                pck.Skip();
            }
            Assert.AreEqual(i, 21, "Element Size Test");
        }
    }
}
