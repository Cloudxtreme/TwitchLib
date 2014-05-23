using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    class Log
    {
        public static Log Instance = new Log();

        StreamWriter m_file;

        Log()
        {
            m_file = File.AppendText("log.txt");
        }

        public void LogSend(string value)
        {
            lock (m_file)
            {
                m_file.Write("[{0}] <<< {1}", DateTime.Now, value);
                m_file.Flush();
            }
        }

        public void LogRecv(string value)
        {
            if (!value.EndsWith("\n"))
                value += "\n";

            lock (m_file)
            {
                m_file.Write("[{0}, t={1}] >>> {2}", DateTime.Now, Thread.CurrentThread.ManagedThreadId, value);
                m_file.Flush();
            }
        }

        public void LogBytesReceived(byte[] bytes, int len)
        {
            lock (m_file)
            {
                string s = Encoding.UTF8.GetString(bytes, 0, len);
                m_file.WriteLine("[{0}, t={1}] bytes {2}: {3}", DateTime.Now, Thread.CurrentThread.ManagedThreadId, len, s);
                m_file.Flush();
            }
        }

        internal void LogError(string str)
        {
            lock (m_file)
            {
                m_file.Write("[{0}, t={1}] ERROR {2}", DateTime.Now, Thread.CurrentThread.ManagedThreadId, str);
                m_file.Flush();
            }
        }
    }
}
