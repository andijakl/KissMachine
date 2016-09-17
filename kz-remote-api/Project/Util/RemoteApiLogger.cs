﻿using System;
using System.Diagnostics;

namespace Kazyx.RemoteApi.Util
{
    public class RemoteApiLogger
    {
        internal static void Log(string msg)
        {
            Logger?.Invoke(msg);
        }

        public static Action<string> Logger { set; get; } = (s) => Debug.WriteLine(s);

        internal static void VerboseLog(string msg)
        {
            VerboseLogger?.Invoke(msg);
        }

        public static Action<string> VerboseLogger { set; get; }
    }
}
