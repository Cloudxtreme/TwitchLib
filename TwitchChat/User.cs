using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    public class TwitchUserData
    {
        public string Name { get; internal set; }
        public bool IsAdmin { get; internal set; }
        public bool IsStaff { get; internal set; }
        public bool IsTurbo { get; internal set; }
        public int[] ImageSet { get; internal set; }
    }

    public class TwitchUser
    {
        public bool IsModerator { get; internal set; }
        public bool IsSubscriber { get; internal set; }
    }
}
