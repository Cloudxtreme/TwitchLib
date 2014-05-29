using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    public class TwitchChannel
    {
        readonly string r_ban = ".ban ";
        readonly string r_timeout = ".timeout ";
        readonly string r_action = new string((char)1, 1) + "ACTION";

        TwitchConnection m_twitch;

        HashSet<TwitchUser> m_moderators = new HashSet<TwitchUser>();
        Dictionary<string, TwitchUser> m_users = new Dictionary<string, TwitchUser>();
        string m_prvtMsg;

        public delegate void ChannelEventHandler(TwitchChannel channel);
        public delegate void UserEventHandler(TwitchChannel channel, TwitchUser user);
        public delegate void MessageHandler(TwitchChannel channel, TwitchUser user, string text);
        public delegate void SlowModeHandler(TwitchChannel channel, int time);
        public delegate void StatusMessageHandler(TwitchChannel channel, string text);

        public event StatusMessageHandler StatusMessageReceived;

        public event SlowModeHandler SlowModeBegin;
        public event ChannelEventHandler SlowModeEnd;

        public event ChannelEventHandler SubModeBegin;
        public event ChannelEventHandler SubModeEnd;

        public event MessageHandler MessageReceived;
        public event MessageHandler ActionReceived;

        public event UserEventHandler UserSubscribed;

        public event UserEventHandler ModeratorJoined;
        public event UserEventHandler ModeratorLeft;

        public event UserEventHandler UserChatCleared;
        public event ChannelEventHandler ChatCleared;

        public event MessageHandler MessageSent;

        public TwitchUser[] Moderators
        {
            get
            {
                lock (m_moderators)
                    return m_moderators.ToArray();
            }
        }

        public TwitchConnection Connection { get { return m_twitch; } }

        public bool Connected { get; internal set; }
        public string Name { get; private set; }

        internal TwitchChannel(TwitchConnection twitch, string channel)
        {
            m_twitch = twitch;
            Name = channel;
            m_prvtMsg = string.Format("PRIVMSG #{0} :", channel);
        }

        public override string ToString()
        {
            return Name;
        }

        public void SendMessage(string message)
        {
            SendMessage(message, null);
        }

        public void SendMessage(string format, params object[] parameters)
        {
            int paramCount = parameters != null ? parameters.Length : 0;

            StringBuilder sb = new StringBuilder(m_prvtMsg.Length + format.Length + paramCount * 8 + 1);
            sb.Append(m_prvtMsg);

            if (paramCount == 0)
                sb.Append(format);
            else
                sb.AppendFormat(format, parameters);

            sb.Append('\n');

            string message = sb.ToString();
            m_twitch.Send(message);

            var evt = MessageSent;
            if (evt != null)
                evt(this, GetUser(m_twitch.User), message);
        }

        public void Ban(TwitchUser user)
        {
            StringBuilder sb = new StringBuilder(m_prvtMsg.Length + r_ban.Length + user.Name.Length + 1);
            sb.Append(m_prvtMsg);
            sb.Append(r_ban);
            sb.Append(user.Name);
            sb.Append('\n');

            m_twitch.Send(sb.ToString());
        }

        public void Timeout(TwitchUser user, int duration)
        {
            if (duration <= 0)
                throw new ArgumentException("duration must be 1 or greater");

            StringBuilder sb = new StringBuilder(m_prvtMsg.Length + r_timeout.Length + user.Name.Length + 8);
            sb.Append(m_prvtMsg);
            sb.Append(r_timeout);
            sb.Append(user.Name);
            sb.Append(' ');
            sb.Append(duration);
            sb.Append('\n');

            m_twitch.Send(sb.ToString());
        }


        public TwitchUser GetUser(string name)
        {
            name = name.ToLower();

            lock (m_users)
            {
                TwitchUser result;
                if (m_users.TryGetValue(name, out result))
                    return result;

                TwitchUserData data = m_twitch.GetUserData(name);
                result = m_users[name] = new TwitchUser(data);
                return result;
            }
        }

        public void Leave()
        {
            m_twitch.Leave(Name);
            m_twitch = null;
            Connected = false;
        }

        internal void NotifyMessageReceived(string username, string line, int offset)
        {
            Debug.Assert(username == username.ToLowerInvariant());
            Debug.Assert(offset < line.Length);

            // Twitchnotify is how subscriber messages "Soandso just subscribed!" comes in:
            if (username == "twitchnotify")
            {
                TwitchUser user;
                int i = line.IndexOf(" just", offset);
                if (i > 0)
                {
                    user = GetUser(line.Slice(offset, i));
                    user.IsSubscriber = true;
                    user.UserData.ImageSet = null; // Need to reparse icon set

                    var evt = UserSubscribed;
                    if (evt != null)
                        evt(this, user);

                    return;
                }
            }

            if (line.StartsWith(r_action, offset))
            {
                var evt = ActionReceived;
                if (evt != null)
                    evt(this, GetUser(username), line.Substring(offset));
            }
            else
            {
                var evt = MessageReceived;
                if (evt != null)
                    evt(this, GetUser(username), line.Substring(offset));
            }
        }



        internal void ParseModerators(string text, int offset)
        {
            // This room is now in slow mode. You may send messages every 120 seconds
            //*  The moderators of this room are: mod1, mod2, mod3

            lock (m_moderators)
            {
                TwitchUser streamer = GetUser(Name);

                string[] modList = text.Substring(offset).Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                HashSet<TwitchUser> mods = new HashSet<TwitchUser>(modList.Select(name => GetUser(name)));
                mods.Add(streamer);

                foreach (var mod in mods)
                    mod.IsModerator = true;

                var demodded = from mod in m_moderators
                               where !mods.Contains(mod)
                               select mod;

                foreach (var former in demodded)
                    former.IsModerator = false;
            }
        }


        internal void ParseChatClear(string text, int offset)
        {
            string chatclear = "CLEARCHAT";

            if (!text.StartsWith(chatclear, offset))
            {
                Debug.Fail(string.Format("ParseChatClear failed to parse {0}", text));
                return;
            }

            offset += chatclear.Length;

            if (offset+2 > text.Length)
            {
                var evt = ChatCleared;
                if (evt != null)
                    evt(this);
            }
            else
            {
                var evt = UserChatCleared;
                if (evt == null)
                    return;

                offset++;
                var user = GetUser(text.Substring(offset));
                evt(this, user);
            }
        }

        internal void RawJtvMessage(string text, int offset)
        {
            var evt = StatusMessageReceived;
            if (evt != null)
                evt(this, text.Substring(offset));
        }

        internal void NotifyModeratorJoined(string user)
        {
            var evt = ModeratorJoined;
            if (evt != null)
                evt(this, GetUser(user));
        }

        internal void NotifyModeratorLeft(string user)
        {
            var evt = ModeratorLeft;
            if (evt != null)
                evt(this, GetUser(user));
        }

        internal void ParseSlowMode(string text, int offset)
        {
            var evt = SlowModeBegin;
            if (evt == null)
                return;

            int i = text.IndexOf(' ', offset);
            if (i == -1)
            {
                Debug.Fail(string.Format("ParseSlowMode failed to parse {0}", text));
                return;
            }

            int time;
            if (!int.TryParse(text, out time))
            {
                Debug.Fail(string.Format("ParseSlowMode failed to parse {0}", text));
                return;
            }

            evt(this, time);
        }

        internal void SlowOff()
        {
            var evt = SlowModeEnd;
            if (evt != null)
                evt(this);
        }

        internal void SubMode()
        {
            var evt = SubModeBegin;
            if (evt != null)
                evt(this);
        }

        internal void SubModeOff()
        {
            var evt = SubModeEnd;
            if (evt != null)
                evt(this);
        }
    }
}
