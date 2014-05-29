using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public string Color { get; internal set; }

        public TwitchUserData(string name)
        {
            Name = name;
        }
    }

    public class TwitchUser
    {
        internal TwitchUserData UserData { get; set; }

        public bool IsModerator { get; internal set; }
        public bool IsSubscriber { get; internal set; }

        public string Name { get{ return UserData.Name; } }
        public bool IsAdmin { get{ return UserData.IsAdmin; } }
        public bool IsStaff { get{ return UserData.IsStaff; } }
        public bool IsTurbo { get { return UserData.IsTurbo; } internal set { UserData.IsTurbo = value; } }
        public int[] ImageSet { get{ return UserData.ImageSet; } }
        public string Color { get{ return UserData.Color; } }

        public TwitchUser(TwitchUserData data)
        {
            UserData = data;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
