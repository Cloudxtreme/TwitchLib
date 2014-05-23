using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    public class TwitchChannel
    {
        public bool Connected { get; internal set; }
        public string Name { get; private set; }

        internal TwitchChannel(string channel)
        {
            Name = channel;
        }
    }
}
