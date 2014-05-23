using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DarkAutumn.Twitch;
using System.Linq;
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

        [TestMethod]
        public void TestMultipleWrites()
        {
            var encoding = Encoding.UTF8;

            CircularBufferStream stream = new CircularBufferStream(0x10000);
            var reader = new SafeLineReader(new StreamReader(stream, encoding));

            byte[] bytes = encoding.GetBytes(m_line1);
            stream.Write(bytes, 0, bytes.Length);


            bytes = encoding.GetBytes(m_line2);
            stream.Write(bytes, 0, bytes.Length);

            string[] lines = m_line2.Split('\n');

            Assert.AreEqual(m_line1.TrimEnd('\n'), reader.ReadLine());
            foreach (var line in lines.Take(lines.Length - 1))
            {
                var actual = reader.ReadLine();
                Assert.AreEqual(line.TrimEnd('\r'), actual);
            }


             //:tmi.twitch.tv 001 darkautumn :Welcome, GLHF!

        }

        [TestMethod]
        public void TestMultipleWrites2()
        {
            var encoding = Encoding.UTF8;

            CircularBufferStream stream = new CircularBufferStream(0x10000);
            var reader = new SafeLineReader(new StreamReader(stream, encoding));

            byte[] bytes = encoding.GetBytes("one\n");
            stream.Write(bytes, 0, bytes.Length);

            Assert.AreEqual("one", reader.ReadLine());

            bytes = encoding.GetBytes("two\n");
            stream.Write(bytes, 0, bytes.Length);


            bytes = encoding.GetBytes("three\n");
            stream.Write(bytes, 0, bytes.Length);

            Assert.AreEqual("two", reader.ReadLine());
            Assert.AreEqual("three", reader.ReadLine());
            Assert.IsNull(reader.ReadLine());
        }

        string m_line1 = ":tmi.twitch.tv 001 darkautumn :Welcome, GLHF!\n";
        string m_line2 = @":tmi.twitch.tv 002 darkautumn :Your host is tmi.twitch.tv
:tmi.twitch.tv 003 darkautumn :This server is rather new
:tmi.twitch.tv 004 darkautumn :-
:tmi.twitch.tv 375 darkautumn :-
:tmi.twitch.tv 372 darkautumn :You are in a maze of twisty passages, all alike.
:tmi.twitch.tv 376 darkautumn :>
";
    }
}
