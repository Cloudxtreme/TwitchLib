using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    public class TwitchConnection
    {
        IrcConnection m_irc;
        Dictionary<string, TwitchChannel> m_channels = new Dictionary<string, TwitchChannel>();

        public TwitchConnection()
        {
            m_irc = new IrcConnection(this);
        }

        public void Join(string channel)
        {
            m_channels[channel] = new TwitchChannel(channel);

            m_irc.Join("#" + channel);
        }


        internal void NotifyJoined(string channel)
        {
            Debug.Assert(m_channels.ContainsKey(channel));

            TwitchChannel twitchChannel;
            if (m_channels.TryGetValue(channel, out twitchChannel))
                twitchChannel.Connected = true;
        }

        internal void NotifyMessageReceived(string channel, string user, string text)
        {
            Console.WriteLine("{0} {1}: {2}", channel, user, text);
        }

        internal void NotifyModeratorJoined(string channel, string user)
        {
            Console.WriteLine("{0} {1}: JOINED", channel, user);
        }

        internal void NotifyModeratorLeft(string channel, string user)
        {
            Console.WriteLine("{0} {1}: LEFT", channel, user);
        }



        public void ConnectAsync(string user, string oauth, Action<TwitchConnection> callback)
        {
            m_irc.ConnectAsync("irc.twitch.tv", 6667, user, oauth, callback);
        }
    }
}
