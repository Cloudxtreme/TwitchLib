using DarkAutumn.Twitch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestApplication
{
    class Program
    {
        static Dictionary<TwitchChannel, StreamWriter> m_files = new Dictionary<TwitchChannel, StreamWriter>();

        static void Main(string[] a)
        {
            Task<TwitchChannel[]> t = ConnectAsync("darkautumn");
            t.Wait();

            TwitchChannel[] chans = t.Result;

            foreach (var chan in chans)
                m_files[chan] = File.AppendText(chan.Name + ".txt");

            foreach (var chan in chans)
            {
                Console.WriteLine("Connected to {0}", chan.Name);

                chan.ChatCleared += chan_ChatCleared;
                chan.ActionReceived += chan_ActionReceived;
                chan.MessageReceived += chan_ActionReceived;
                chan.ModeratorJoined += chan_ModeratorJoined;
                chan.ModeratorLeft += chan_ModeratorLeft;
                chan.UserChatCleared += chan_UserChatCleared;
                chan.UserSubscribed += chan_UserSubscribed;
            }

            var connection = chans.First().Connection;
            connection.Connected += () => Console.WriteLine("Connected");
            connection.Disconnected += () => Console.WriteLine("Disconnected");

            DateTime lastSave = DateTime.Now;
            Console.WriteLine("Started loop");
            while (Console.ReadKey().KeyChar != 'q')
            {
                Thread.Sleep(250);

                if ((DateTime.Now - lastSave).TotalSeconds > 30)
                {
                    lastSave = DateTime.Now;

                    foreach (var file in m_files.Values)
                        lock (file)
                            file.Flush();
                }
            }

            foreach (var chan in chans)
                chan.Leave();

            connection.Quit();

            foreach (var file in m_files.Values)
                lock (file)
                    file.Flush();

            Thread.Sleep(2000);
        }

        static async Task<TwitchChannel[]> ConnectAsync(params string[] channelNames)
        {
            TwitchConnection twitch = new TwitchConnection();

            string[] loginData = File.ReadLines("login.txt").Take(2).ToArray();

            var connectResult = await twitch.ConnectAsync(loginData[0], loginData[1]);
            if (connectResult != ConnectResult.Connected)
            {
                Console.WriteLine("Failed to login.");
                return null;
            }

            //TwitchChannel result = Create(channel);
            //await result.JoinAsync();
            //return result;



            TwitchChannel[] channels = (from channelName in channelNames select twitch.Create(channelName)).ToArray();
            Task[] channelTasks = (from channel in channels select channel.JoinAsync()).ToArray();

            var result = new TwitchChannel[channelTasks.Length];

            for (int i = 0; i < result.Length; ++i)
                await channelTasks[i];

            return channels;
        }

        static void chan_UserSubscribed(TwitchChannel channel, TwitchUser user)
        {
            WriteLine(channel, "[{0}] {1} subscribed", channel.Name, user.Name);
        }

        static void chan_UserChatCleared(TwitchChannel channel, TwitchUser user)
        {
            WriteLine(channel, "[{0}] {1} chat clear", channel.Name, user.Name);
        }

        static void chan_ModeratorJoined(TwitchChannel channel, TwitchUser user)
        {
            WriteLine(channel, "[{0}] {1} joined", channel.Name, user.Name);
        }
        static void chan_ModeratorLeft(TwitchChannel channel, TwitchUser user)
        {
            WriteLine(channel, "[{0}] {1} left", channel.Name, user.Name);
        }

        static void chan_ActionReceived(TwitchChannel channel, TwitchUser user, string text)
        {
            WriteLine(channel, "[{0}] {1}: {2}", channel.Name, user.Name, text);
        }

        static void chan_ChatCleared(TwitchChannel channel)
        {
            WriteLine(channel, "Channel cleared: {0}", channel);
        }

        private static void WriteLine(TwitchChannel channel, string fmt, params object[] args)
        {
            if (args.Length > 0)
                fmt = string.Format(fmt, args);

            var file = m_files[channel];
            lock (file)
                file.WriteLine("[{0}] {1}", DateTime.Now, fmt);

            Console.WriteLine(fmt);
        }
    }
}
