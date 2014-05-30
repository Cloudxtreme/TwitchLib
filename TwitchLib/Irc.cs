﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    public enum ConnectResult
    {
        Connected,
        NetworkError,
        LoginFailed
    }

    class IrcConnection
    {
        bool m_connected;
        Socket m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        DnsEndPoint m_endpoint;
        string m_user, m_oauth;
        Encoding m_encoding = Encoding.UTF8;
        volatile bool m_shutdown = false;

        object m_sync = new object();
        DateTime m_lastEvent = DateTime.MinValue;

        byte[] m_buffer = new byte[1024];
        Stream m_stream = new CircularBufferStream(0x10000);

        TaskCompletionSource<ConnectResult> m_connectResult;

        TwitchConnection m_twitch;

        BlockingCollection<string> m_queue = new BlockingCollection<string>(new ConcurrentQueue<string>());
        AutoResetEvent m_event = new AutoResetEvent(false);
        Thread m_thread;

        SafeLineReader m_reader;

        public DateTime LastEvent
        {
            get
            {
                DateTime result;
                lock (m_sync)
                    result = m_lastEvent;

                return result;
            }
        }

        public IrcConnection(TwitchConnection twitch)
        {
            m_reader = new SafeLineReader(new StreamReader(m_stream, m_encoding));
            m_twitch = twitch;
        }

        public Task<ConnectResult> ConnectAsync(string hostName, int port, string user, string oauth)
        {
            if (m_connected)
                throw new InvalidOperationException("Already connected to twitch chat.");

            m_endpoint = new DnsEndPoint(hostName, port);
            m_user = user;
            m_oauth = oauth;

            var taskCompletion = new TaskCompletionSource<ConnectResult>();

            m_socket.BeginConnect(m_endpoint, asyncResult =>
                {
                    try
                    {
                        m_connected = true;
                        if (m_thread == null)
                        {
                            m_thread = new Thread(ProcessMessages);
                            m_thread.Name = "Message Processor for User " + m_user;
                            m_thread.Start();
                        }

                        string login = "PASS :{1}\nUSER {0} 0 * :{0}\nNICK :{0}\nTWITCHCLIENT 3\n";
                        login = string.Format(login, m_user, m_oauth);

                        m_connectResult = taskCompletion;

                        SendAsync(login);
                        ReceiveAsync();
                    }
                    catch (Exception e)
                    {
                        taskCompletion.TrySetException(e);
                    }
                }, null);

            return taskCompletion.Task;
        }

        private void SendAsync(string value)
        {
            Debug.Assert(value.EndsWith("\n"));

            byte[] bytes = m_encoding.GetBytes(value);

            var args = new SocketAsyncEventArgs();
            args.SetBuffer(bytes, 0, bytes.Length);

            m_socket.SendAsync(args);
            Log.Instance.LogSend(value);
        }

        private void SendAsync(string format, params object[] args)
        {
            if (args.Length > 0)
                format = string.Format(format, args);

            SendAsync(format);
        }

        private void ReceiveAsync()
        {
            m_socket.BeginReceive(m_buffer, 0, m_buffer.Length, 0, new AsyncCallback(ReceiveCallback), null);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            lock (m_sync)
                m_lastEvent = DateTime.UtcNow;

            try
            {
                int read = m_socket.EndReceive(ar);
                Log.Instance.LogBytesReceived(read);

                if (read > 0)
                {
                    m_stream.Write(m_buffer, 0, read);

                    string line;
                    List<string> result = new List<string>();
                    while ((line = m_reader.ReadLine()) != null)
                    {
                        Log.Instance.LogRecv(line);
                        m_queue.Add(line);
                    }

                    ReceiveAsync();
                }
            }
            catch (SocketException e)
            {
                Log.Instance.LogError(e.ToString());
                Debug.Fail(e.ToString());
            }
        }

        private void ProcessMessages()
        {
            while (!m_shutdown)
            {
                string line = m_queue.Take();

                int userStart;
                int userEnd;
                string command;
                int args;

                if (ParseLine(line, out userStart, out userEnd, out command, out args))
                {
                    if (!ProcessCommand(line, userStart, userEnd, command, args))
                        ReportError(line);
                }
                else
                {
                    ReportError(line);
                }
            }
        }

        public static IEnumerable<string> TestInput(IEnumerable<string> lines, TwitchConnection.ChannelCreatedHandler chanCreated)
        {
            List<string> errors = new List<string>();

            TwitchConnection twitch = new TwitchConnection();
            twitch.ChannelCreated += chanCreated;

            IrcConnection conn = new IrcConnection(twitch);
            
            foreach (var line in lines)
            {
                int userStart;
                int userEnd;
                string command;
                int args;

                if (ParseLine(line, out userStart, out userEnd, out command, out args))
                {
                    if (!conn.ProcessCommand(line, userStart, userEnd, command, args))
                        errors.Add(line);
                }
                else
                {
                    errors.Add(line);
                }
            }

            return errors;
        }

        public static bool ParseLine(string line, out int userStart, out int userEnd, out string command, out int args)
        {
            args = -1;
            command = null;
            userStart = -1;
            userEnd = -1;

            if (line.Length == 0)
                return false;

            int curr = 0;
            if (line[0] == ':')
            {
                userStart = 1;
                if (!GetNameEnd(line, curr, ref userEnd, ref curr))
                    return false;

                curr++;
            }


            if (curr >= line.Length)
                return false;

            string server = "tmi.twitch.tv";
            if (line.StartsWith(server, curr))
                curr += server.Length + 1;

            int commandStart = curr;
            int commandEnd = line.IndexOf(' ', commandStart + 1);

            if (commandStart == -1 || commandEnd == -1)
                return false;

            command = line.Slice(commandStart, commandEnd);
            args = commandEnd + 1;
            return true;
        }

        private bool ProcessCommand(string line, int userStart, int userEnd, string command, int args)
        {
            switch (command)
            {
                case "NOTICE":
                    if (m_connectResult != null && line.EndsWith(":Login unsuccessful", StringComparison.CurrentCultureIgnoreCase))
                    {
                        m_connectResult.SetResult(ConnectResult.LoginFailed);
                        m_connectResult = null;

                        return true;
                    }
                    
                    return false;


                case "PRIVMSG":
                    if (line.StartsWith(":jtv!"))
                        return OnMessage("jtv", line, args);
                    else
                        return OnMessage(line.Slice(userStart, userEnd), line, args);

                case "MODE":
                    return OnMode(line, args);


                case "PING":
                    OnPing(line.Substring(args + 1));
                    return true;

                case "JOIN":
                    m_twitch.NotifyJoined(line.Substring(args + 1));
                    return true;

                case "PART":
                    m_twitch.NotifyPart(line.Substring(args + 1));
                    return true;

                case "001":
                    if (m_connectResult != null)
                        m_connectResult.SetResult(ConnectResult.Connected);

                    m_connectResult = null;
                    return true;


                case "PONG":
                case "002":
                case "003":
                case "004":
                case "375":
                case "372":
                case "376":
                case "353":
                case "366":
                    return true;
            }

            return false;
        }

        private bool OnMessage(string user, string line, int args)
        {
            if (args >= line.Length)
                return false;

            int i = line.IndexOf(' ', args);
            if (i == -1)
                return false;

            string channel = null;
            if (line[args] == '#')
                channel = line.Slice(args+1, i);

            m_twitch.NotifyMessageReceived(channel, user, line, i + 1);
            return true;
        }


        private bool OnMode(string line, int args)
        {
            // #channel +o user

            if (args + 7 >= line.Length)
                return false;

            args++;
            int i = line.IndexOf(' ', args);
            if (i == -1)
                return false;

            string channel = line.Slice(args, i);
            i++;

            if (i+3 >= line.Length)
                return false;

            bool joined = line[i] == '+';
            if (!joined && line[i] != '-')
                return false;

            string user = line.Substring(i + 3);

            if (joined)
                m_twitch.NotifyModeratorJoined(channel, user);
            else
                m_twitch.NotifyModeratorLeft(channel, user);

            return true;
        }

        private void OnPing(string value)
        {
            SendAsync("PONG {0}\n", value);
        }
        
        static bool GetNameEnd(string line, int curr, ref int userEnd, ref int whitespace)
        {
            while (curr < line.Length)
            {
                if (userEnd == -1 && line[curr] == '!')
                {
                    userEnd = curr;
                }
                else if (line[curr] == ' ')
                {
                    if (userEnd == -1)
                        userEnd = curr;

                    whitespace = curr;
                    return true;
                }

                curr++;
            }

            return false;
        }

        private void ReportError(string line)
        {
            Log.Instance.LogError(string.Format("Error: {0}", line));
        }

        internal void Join(string channel)
        {
            Debug.Assert(channel == channel.ToLower());

            SendAsync("JOIN {0}\n", channel);
        }

        internal void Disconnect()
        {
            SendAsync("QUIT\n");

            m_shutdown = true;
        }

        internal void Part(string channel)
        {
            Debug.Assert(channel == channel.ToLower());

            SendAsync("PART {0}\n", channel);
        }

        internal void Send(string message)
        {
            SendAsync(message);
        }

        internal void Ping()
        {
            SendAsync("PING " + m_user + "\n");
        }
    }
}
