﻿using Stride.Engine;
using Stride.Engine.Processors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}