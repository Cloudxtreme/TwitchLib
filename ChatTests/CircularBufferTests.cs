using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DarkAutumn.Twitch;
using System.IO;
using System.Text;

namespace DarkAutumn.Twitch.Tests
{
    [TestClass]
    public class CircularBufferTests
    {
        [TestMethod]
        public void TestPartialRead()
        {
            var encoding = Encoding.UTF8;

            CircularBufferStream stream = new CircularBufferStream(0x10000);
            var reader = new SafeLineReader(new StreamReader(stream, encoding));

            byte[] bytes = encoding.GetBytes("one\ntwo\nthree");
            stream.Write(bytes, 0, bytes.Length);

            Assert.AreEqual("one", reader.ReadLine());
            Assert.AreEqual("two", reader.ReadLine());
            Assert.AreEqual(null, reader.ReadLine());
        }

        [TestMethod]
        public void TestFullRead()
        {
            var encoding = Encoding.UTF8;

            CircularBufferStream stream = new CircularBufferStream(0x10000);
            var reader = new SafeLineReader(new StreamReader(stream, encoding));

            byte[] bytes = encoding.GetBytes("one\ntwo\nthree\n");
            stream.Write(bytes, 0, bytes.Length);

            Assert.AreEqual("one", reader.ReadLine());
            Assert.AreEqual("two", reader.ReadLine());
            Assert.AreEqual("three", reader.ReadLine());
        }
    }
}
