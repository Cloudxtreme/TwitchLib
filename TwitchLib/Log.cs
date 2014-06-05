using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    [EventSource(Name="TwitchLib-Irc")]
    class IrcLog : EventSource
    {
        public class Keywords
        {
            public const EventKeywords IrcData = (EventKeywords)0x1;
            public const EventKeywords Status = (EventKeywords)0x2;
            public const EventKeywords Error = (EventKeywords)0x3;
        }

        internal IrcLog()
        {
        }

        [Event(1, Keywords = Keywords.IrcData, Opcode = EventOpcode.Send)]
        public void MessageSent(string value)
        {
            bool isEnabled = IsEnabled();
            WriteEvent(1, value);
        }

        [Event(2, Keywords = Keywords.IrcData, Opcode = EventOpcode.Receive)]
        public void MessageReceived(string value)
        {
            WriteEvent(2, value);
        }

        [Event(3, Keywords = Keywords.Status, Opcode = EventOpcode.Info)]
        public void Timeout()
        {
            WriteEvent(3);
        }

        [Event(4, Keywords = Keywords.Status, Opcode = EventOpcode.Info)]
        public void Disconnected()
        {
            WriteEvent(4);
        }

        [Event(5, Keywords = Keywords.Status, Opcode = EventOpcode.Info)]
        public void Connected()
        {
            WriteEvent(5);
        }

        [Event(6, Keywords = Keywords.Status, Opcode = EventOpcode.Info)]
        public void ConnectionFailed()
        {
            WriteEvent(6);
        }

        [Event(7, Keywords = Keywords.Error, Opcode = EventOpcode.Info)]
        public void ParseFailure(string input)
        {
            WriteEvent(6, input);
        }

        [Event(8, Keywords = Keywords.Error, Opcode = EventOpcode.Info)]
        public void CommandFailure(string command, string line)
        {
            WriteEvent(7, command, line);
        }

        [Event(8, Keywords = Keywords.IrcData, Opcode = EventOpcode.Send)]
        public void LoginSent(string user, int client)
        {
            WriteEvent(8, user, client);
        }
    }

    [EventSource(Name = "TwitchLib-Http")]
    class HttpLog
    {
    }

    static class Log
    {
        public static IrcLog Irc = new IrcLog();
        public static HttpLog Http = new HttpLog();
    }
}
