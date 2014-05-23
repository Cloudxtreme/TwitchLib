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
            TwitchConnection twitch = new TwitchConnection();
            twitch.ConnectAsync("darkautumn", "oauth:avhst3mk12tdjvp2nz47x66tfdk0wvh", delegate(TwitchConnection c)
            {
                Thread.Sleep(10000);
                c.Join("darkautumn");
            });


            while (true)
                Thread.Sleep(10000);
        }
    }
}
