using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    class JoinRateLimiter
    {
        LinkedList<DateTime> m_timestamps = new LinkedList<DateTime>();
        const int Timelimit = 30;
        const int Limit = 20;

        public bool CanJoin(out int delay)
        {
            delay = 0;

            lock (m_timestamps)
            {
                if (m_timestamps.Count + 1 >= Limit)
                {
                    var now = DateTime.UtcNow;
                    int count = m_timestamps.Count;
                    while (count-- > 0 && (now - m_timestamps.First.Value).TotalSeconds > Timelimit)
                        m_timestamps.RemoveFirst();

                    if (m_timestamps.Count == 0)
                        return true;

                    delay = 20 - (int)(now - m_timestamps.First.Value).TotalSeconds;
                    if (m_timestamps.Count >= Limit)
                        return false;
                }

                m_timestamps.AddLast(DateTime.UtcNow);
            }

            return true;
        }
    }
    

    class MessageRateLimiter
    {
        LinkedList<SendTime> m_timestamps = new LinkedList<SendTime>();
        int m_nonModMessages = 0;
        const int Timelimit = 30;
        const int ModLimit = 100;
        const int UserLimit = 20;


        public bool CanSendMessage(bool isMod)
        {
            lock (m_timestamps)
            {
                if (!isMod)
                    m_nonModMessages++;

                int limit = m_nonModMessages > 0 ? UserLimit : ModLimit;
                if (m_timestamps.Count + 1 >= limit)
                {
                    var now = DateTime.UtcNow;
                    int count = m_timestamps.Count;
                    while (count-- > 0 && (now - m_timestamps.First.Value.Timestamp).TotalSeconds > Timelimit)
                    {
                        if (!m_timestamps.First.Value.Moderator)
                            m_nonModMessages--;

                        m_timestamps.RemoveFirst();
                    }

                    if (m_timestamps.Count >= limit)
                        return false;
                }

                m_timestamps.AddLast(new SendTime(isMod));
            }

            return true;
        }

        struct SendTime
        {
            public bool Moderator;
            public DateTime Timestamp;

            public SendTime(bool mod)
            {
                Moderator = mod;
                Timestamp = DateTime.UtcNow;
            }
        }
    }
}
