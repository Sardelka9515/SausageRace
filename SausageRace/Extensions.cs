using Stride.Engine.Processors;
using System;
using System.Threading.Tasks;

namespace SausageRace
{
    public static class Extensions
    {
        public static async Task Wait(this ScriptSystem s, TimeSpan time)
        {
            var end = Environment.TickCount64 + time.TotalMilliseconds;
            while (Environment.TickCount64 < end)
            {
                await s.NextFrame();
            }
        }
        public static async Task Wait(this ScriptSystem s, float t) => await s.Wait(new TimeSpan(0, 0, 0, 0, (int)(t * 1000)));
    }
}
