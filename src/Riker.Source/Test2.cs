﻿using System;

namespace Riker
{
    internal static class Test2
    {
        internal static readonly Action<Action> Action = x => Device.Run(x);
    }
}