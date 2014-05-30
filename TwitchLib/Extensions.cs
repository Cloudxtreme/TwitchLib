using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkAutumn.Twitch
{
    public static class Extensions
    {
        public static Task AsTask(this ManualResetEvent handle)
        {
            return AsTask(handle, Timeout.InfiniteTimeSpan);
        }

        public static Task AsTask(this ManualResetEvent handle, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<object>();
            var registration = ThreadPool.RegisterWaitForSingleObject(handle, (state, timedOut) =>
            {
                var localTcs = (TaskCompletionSource<object>)state;
                if (timedOut)
                    localTcs.TrySetCanceled();
                else
                    localTcs.TrySetResult(null);
            }, tcs, timeout, executeOnlyOnce: true);
            tcs.Task.ContinueWith((_, state) => ((RegisteredWaitHandle)state).Unregister(null), registration, TaskScheduler.Default);
            return tcs.Task;
        }

        public static bool StartsWith(this string line, string value, int curr)
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
