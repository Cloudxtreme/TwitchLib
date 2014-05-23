using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    public static class Extensions
    {
        public static bool StartsWith(this string line, int curr, string value)
        {
            if (line.Length + curr < value.Length)
                return false;

            for (int i = 0; i < value.Length; ++i)
                if (line[i + curr] != value[i])
                    return false;

            return true;
        }

        public static string Slice(this string self, int start, int end)
        {
            if (end < 0)
                end = self.Length + end;

            return self.Substring(start, end - start);
        }
    }
}
