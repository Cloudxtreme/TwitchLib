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
        static void Main(string[] a)
        {
            Task<TwitchChannel> t = MainAsync();
            t.Wait();

            TwitchChannel chan = t.Result;
            chan.ChatCleared += chan_ChatCleared;
            chan.ActionReceived += chan_ActionReceived;
            chan.MessageReceived += chan_ActionReceived;
            chan.ModeratorJoined += chan_ModeratorJoined;
            chan.ModeratorLeft += chan_ModeratorLeft;
            chan.UserChatCleared += chan_UserChatCleared;
            chan.UserSubscribed += chan_UserSubscribed;

            while (Console.ReadKey().KeyChar != 'q')
                Thread.Sleep(250);

            var connection = chan.Connection;
            chan.Leave();
            connection.Disconnect();

            while (true)
                Thread.Sleep(1000);
        }

        static void chan_UserSubscribed(TwitchChannel channel, TwitchUser user)
        {
            Console.WriteLine("[{0}] {1} subscribed", channel.Name, user.Name);
        }

        static void chan_UserChatCleared(TwitchChannel channel, TwitchUser user)
        {
            Console.WriteLine("[{0}] {1} chat clear", channel.Name, user.Name);
        }

        static void chan_ModeratorJoined(TwitchChannel channel, TwitchUser user)
        {
            Console.WriteLine("[{0}] {1} joined", channel.Name, user.Name);
        }
        static void chan_ModeratorLeft(TwitchChannel channel, TwitchUser user)
        {
            Console.WriteLine("[{0}] {1} left", channel.Name, user.Name);
        }

        static void chan_ActionReceived(TwitchChannel channel, TwitchUser user, string text)
        {
            Console.WriteLine("[{0}] {1}: {2}", channel.Name, user.Name, text);
        }

        static void chan_ChatCleared(TwitchChannel channel)
        {
            Console.WriteLine("Channel cleared: {0}", channel);
        }

        static async Task<TwitchChannel> MainAsync()
        {
            TwitchConnection twitch = new TwitchConnection();

            string[] loginData = File.ReadLines("login.txt").Take(3).ToArray();

            var result = await twitch.ConnectAsync(loginData[0], loginData[1]);
            if (result != ConnectResult.Connected)
            {
                Console.WriteLine("Failed to login.");
                return null;
            }


            var channel = twitch.Join(loginData[2]);
            while (!channel.Connected)
                await Task.Delay(1000);


            return channel;
        }
    }
}
