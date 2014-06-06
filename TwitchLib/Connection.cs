using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace DarkAutumn.Twitch
{
    public class TwitchConnection
    {
        IrcConnection m_irc;
        Dictionary<string, TwitchChannel> m_channels = new Dictionary<string, TwitchChannel>();
        Dictionary<string, TwitchUserData> m_users = new Dictionary<string, TwitchUserData>();
        HashSet<TwitchUser> m_activeUsers = new HashSet<TwitchUser>();

        MessageRateLimiter m_messageLimiter = new MessageRateLimiter();
        JoinRateLimiter m_joinLimiter = new JoinRateLimiter();

        volatile TwitchUserData m_lastUser;
        volatile TwitchChannel m_lastChannel;

        public delegate void ChannelCreatedHandler(TwitchConnection connection, TwitchChannel channel);
        public delegate void ConnectionHandler();

        public event ChannelCreatedHandler ChannelCreated;
        public event ConnectionHandler Connected;
        public event ConnectionHandler Disconnected;

        public DateTime LastEvent { get { return m_irc.LastEvent; } }

        public string User { get; private set; }

        public TwitchConnection()
        {
            m_irc = new IrcConnection();
            m_irc.UserJoined += NotifyJoined;
            m_irc.UserParted += NotifyPart;
            m_irc.MessageReceived += NotifyMessageReceived;
            m_irc.ModeratorJoined += NotifyModeratorJoined;
            m_irc.ModeratorLeft += NotifyModeratorLeft;
            m_irc.Disconnected += m_irc_disconnected;
            m_irc.Connected += m_irc_connected;
        }

        private void m_irc_connected()
        {
            var evt = Connected;
            if (evt != null)
                evt();
        }

        private void m_irc_disconnected()
        {
            var evt = Disconnected;
            if (evt != null)
                evt();
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

        public void Quit()
        {
            m_irc.Disconnect();
        }


        public void Ping()
        {
            m_irc.Ping();
        }



        internal void Leave(string name)
        {
            m_irc.Part("#" + name);
        }



        internal bool Send(TwitchUser user, string message)
        {
            if (m_messageLimiter.CanSendMessage(user.IsModerator))
            {
                m_irc.Send(message);
                return true;
            }

            return false;
        }


        internal void Join(string name)
        {
            int delay;
            if (m_joinLimiter.CanJoin(out delay))
                m_irc.Join(name);
            else
                ThreadPool.QueueUserWorkItem(JoinLater, new Tuple<string, int>(name, delay));
        }

        private void JoinLater(object state)
        {
            var param = (Tuple<string, int>)state;
            int delay = param.Item2 >= 1 ? param.Item2 : 1;

            Thread.Sleep(delay);
            Join(param.Item1);
        }

        public TwitchChannel Create(string channel)
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

            return twitchChannel;
        }

        public TwitchChannel GetChannel(string channel)
        {
            channel = channel.ToLower();

            var twitchChannel = m_lastChannel;
            if (twitchChannel != null && twitchChannel.Name == channel)
                return twitchChannel;

            bool created = false;
            lock (m_channels)
            {
                if (!m_channels.TryGetValue(channel, out twitchChannel))
                {
                    twitchChannel = m_channels[channel] = new TwitchChannel(this, channel);
                    created = true;
                }
            }

            if (created)
            {
                var evt = ChannelCreated;
                if (evt != null)
                    evt(this, twitchChannel);
            }

            m_lastChannel = twitchChannel;
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
        internal void NotifyJoined(string user, string channel)
        {
            TwitchChannel twitchChannel = GetChannel(channel);
            Debug.Assert(twitchChannel != null);

            if (user == User)
            {
                if (twitchChannel != null)
                    twitchChannel.NotifyJoined();
            }
            else
            {
                twitchChannel.UserJoined(user);
            }
        }

        internal void NotifyPart(string user, string channel)
        {
            lock (m_channels)
            {
                TwitchChannel twitchChannel;
                if (!m_channels.TryGetValue(channel, out twitchChannel))
                {
                    Debug.Fail("Parted channel not in dictionary.");
                    return;
                }
                
                if (user == User)
                {
                    twitchChannel.IsJoined = false;
                    m_channels.Remove(channel);
                }
                else
                {
                    twitchChannel.UserParted(user);
                }
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
                        chan.ParseModerators(text, offset, offset + modMsg.Length);
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
            else if (text.EndsWith("staff"))
            {
                var user = GetUserData(name);
                user.IsStaff = true;
            }
            else
            {
                Debug.Fail(string.Format("Parse SpecialMessage could not parse {0}", text));
            }
        }


        private bool ParseUserColor(string text, int offset)
        {
            //USERCOLOR username #8A2BE2

            string usercolor = "USERCOLOR ";
            if (!text.StartsWith(usercolor, offset))
            {
                Debug.Fail(string.Format("Could not parse user color {0}", text));
                return false;
            }

            offset += usercolor.Length;
            if (offset + 3 >= text.Length)
            {
                Debug.Fail(string.Format("Could not parse user color {0}", text));
                return false;
            }

            int i = text.IndexOf(' ', offset);
            var user = GetUserData(text.Slice(offset, i));
            if (text[i + 1] == '#')
            {
                user.Color = text.Substring(i + 1);
                return true;
            }

            string color = text.Substring(i + 1);
            switch (color)
            {
                case "black":
                    user.Color = "#000000";
                    return true;
            }

            Debug.Fail(string.Format("Could not parse user color {0}", text));
            return false;
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
