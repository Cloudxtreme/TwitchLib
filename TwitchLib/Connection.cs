using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        Dictionary<string, TwitchUserData> m_users = new Dictionary<string, TwitchUserData>();

        volatile TwitchUserData m_lastUser;


        public string User { get; private set; }

        public TwitchConnection()
        {
            m_irc = new IrcConnection(this);
        }

        internal TwitchUserData GetUserData(string name)
        {
            Debug.Assert(name == name.ToLower());

            TwitchUserData user = m_lastUser;
            if (user != null && user.Name == name)
                return user;

            lock (m_users)
            {
                if (!m_users.TryGetValue(name, out user))
                    user = m_users[name] = new TwitchUserData(name);

                m_lastUser = user;
                return user;
            }
        }

        public void Disconnect()
        {
            m_irc.Quit();
        }


        internal void Leave(string name)
        {
            m_irc.Part("#" + name);
        }

        internal void Send(string message)
        {
            m_irc.Send(message);
        }

        public TwitchChannel Join(string channel)
        {
            if (string.IsNullOrEmpty(channel) || channel[0] == '#')
                throw new ArgumentException("channel");

            channel = channel.ToLower();

            TwitchChannel twitchChannel;
            lock (m_channels)
            {
                if (!m_channels.TryGetValue(channel, out twitchChannel))
                {
                    twitchChannel = new TwitchChannel(this, channel);
                    m_channels[channel] = twitchChannel;
                }
            }

            m_irc.Join("#" + channel);
            return twitchChannel;
        }

        public async Task<TwitchChannel> JoinAsync(string channel)
        {
            TwitchChannel result = Join(channel);

            if (result.Connected)
                return result;

            await Task.Factory.StartNew(() =>
                {
                    while (!result.Connected)
                        Task.Delay(150);
                });

            return result;
        }


        static volatile TwitchChannel s_lastChannel;
        public TwitchChannel GetChannel(string channel)
        {
            channel = channel.ToLower();

            var twitchChannel = s_lastChannel;
            if (twitchChannel != null && twitchChannel.Name == channel)
                return twitchChannel;

            lock (m_channels)
                m_channels.TryGetValue(channel, out twitchChannel);

            s_lastChannel = twitchChannel;

#if DEBUG
            if (twitchChannel == null && IrcConnection.s_chanCreated != null)
            {
                twitchChannel = new TwitchChannel(this, channel);

                lock (m_channels)
                    m_channels[channel] = twitchChannel;

                IrcConnection.s_chanCreated(twitchChannel);
            }
#endif

            return twitchChannel;
        }


        public ConnectResult Connect(string user, string oauth)
        {
            return ConnectAsync(user, oauth).Result;
        }

        public async Task<ConnectResult> ConnectAsync(string user, string oauth)
        {
            User = user;
            return await m_irc.ConnectAsync("irc.twitch.tv", 6667, user, oauth);
        }



        #region Internal Callbacks
        internal void NotifyJoined(string channel)
        {
            TwitchChannel twitchChannel = GetChannel(channel);
            Debug.Assert(twitchChannel != null);

            if (twitchChannel != null)
                twitchChannel.Connected = true;
        }

        internal void NotifyPart(string channel)
        {
            lock (m_channels)
            {
                TwitchChannel twitchChannel;
                if (!m_channels.TryGetValue(channel, out twitchChannel))
                {
                    Debug.Fail("Parted channel not in dictionary.");
                    return;
                }

                twitchChannel.Connected = false;
                m_channels.Remove(channel);
            }
        }

        internal void NotifyMessageReceived(string channel, string user, string line, int offset)
        {
            TwitchChannel chan = null;
            if (channel != null)
                chan = GetChannel(channel);

            offset++;
            if (user == "jtv")
            {
                HandleJtvMessage(chan, line, offset);
            }
            else if (chan != null)
            {
                Debug.Assert(chan != null);

                if (chan != null)
                    chan.NotifyMessageReceived(user, line, offset);
            }
        }


        private void HandleJtvMessage(TwitchChannel chan, string text, int offset)
        {
            switch (text[offset])
            {
                case 'E':
                    ParseEmoteSet(text, offset);
                    break;

                case 'C':
                    Debug.Assert(chan != null);
                    chan.ParseChatClear(text, offset);
                    break;

                case 'S':
                    ParseSpecialUser(chan, text, offset);
                    break;

                case 'T':
                    Debug.Assert(chan != null);
                    string modMsg = "The moderators of this room are: ";
                    string slowMode = "This room is now in slow mode. You may send messages every ";
                    string slowOff = "This room is no longer in slow mode.";
                    string subMode = "This room is now in subscribers-only mode.";
                    string subModeOff = "This room is no longer in subscribers-only mode.";

                    if (text.StartsWith(modMsg, offset))
                        chan.ParseModerators(text, offset + modMsg.Length);
                    else if (text.StartsWith(slowMode, offset))
                        chan.ParseSlowMode(slowMode, offset + slowMode.Length);
                    else if (text.StartsWith(slowOff, offset))
                        chan.SlowOff();
                    else if (text.StartsWith(subMode, offset))
                        chan.SubMode();
                    else if (text.StartsWith(subModeOff, offset))
                        chan.SubModeOff();
                    else
                        chan.RawJtvMessage(text, offset);
                    break;

                case 'H':
                    Debug.Assert(text.StartsWith("HISTORYEND", offset));
                    break;

                case 'U':
                    ParseUserColor(text, offset);
                    break;

                default:
                    Debug.Assert(chan != null);
                    chan.RawJtvMessage(text, offset);
                    return;
            }
        }

        internal void ParseSpecialUser(TwitchChannel chan, string text, int offset)
        {
            //*  SPECIALUSER username subscriber

            string specialuser = "SPECIALUSER ";
            if (!text.StartsWith(specialuser, offset))
            {
                Debug.Fail("ParseSpecialUser received unexpected string start.");
                return;
            }

            offset += specialuser.Length;

            int i = text.IndexOf(' ', offset);
            string name = text.Slice(offset, i);

            if (text.EndsWith("subscriber"))
            {
                if (chan == null)
                {
                    Debug.Fail("Subscriber message sent to non-channel.");
                    return;
                }

                var user = chan.GetUser(name);
                user.IsSubscriber = true;
            }
            else if (text.EndsWith("turbo"))
            {
                var user = GetUserData(name);
                user.IsTurbo = true;
            }
            else if (text.EndsWith("admin"))
            {
                var user = GetUserData(name);
                user.IsAdmin = true;
            }
            else
            {
                Debug.Fail(string.Format("Parse SpecialMessage could not parse {0}", text));
            }
        }


        private void ParseUserColor(string text, int offset)
        {
            //USERCOLOR username #8A2BE2

            string usercolor = "USERCOLOR ";
            if (!text.StartsWith(usercolor, offset))
            {
                Debug.Fail(string.Format("Could not parse user color {0}", text));
                return;
            }

            offset += usercolor.Length;
            if (offset + 3 >= text.Length)
            {
                Debug.Fail(string.Format("Could not parse user color {0}", text));
                return;
            }

            int i = text.IndexOf(' ', offset);
            if (text[i + 1] != '#')
            {
                Debug.Fail(string.Format("Could not parse user color {0}", text));
                return;
            }

            var user = GetUserData(text.Slice(offset, i));
            user.Color = text.Substring(i + 1);
        }

        private void ParseEmoteSet(string text, int offset)
        {
            string emoteset = "EMOTESET ";
            if (!text.StartsWith(emoteset, offset))
            {
                Debug.Fail(string.Format("Could not parse emote set {0}", text));
                return;
            }

            offset += emoteset.Length;
            int i = text.IndexOf(' ', offset);
            if (i == -1)
            {
                Debug.Fail(string.Format("Could not parse emote set {0}", text));
                return;
            }

            var user = GetUserData(text.Slice(offset, i));
            if (user.ImageSet != null)
                return;

            if (text[i + 1] != '[' || text[text.Length - 1] != ']')
            {
                Debug.Fail(string.Format("Could not parse emote set {0}", text));
                return;
            }

            string items = text.Slice(i + 2, text.Length - 1);

            int[] imageSet = (from str in items.Split(',')
                              let j = int.Parse(str)
                              select j).ToArray();

            Array.Sort(imageSet);
            user.ImageSet = imageSet;
        }


        internal void NotifyModeratorJoined(string channel, string user)
        {
            var chan = GetChannel(channel);
            Debug.Assert(chan != null);

            if (chan != null)
                chan.NotifyModeratorJoined(user);
        }

        internal void NotifyModeratorLeft(string channel, string user)
        {
            var chan = GetChannel(channel);
            Debug.Assert(chan != null);

            if (chan != null)
                chan.NotifyModeratorLeft(user);
        }
        #endregion
    }
}
