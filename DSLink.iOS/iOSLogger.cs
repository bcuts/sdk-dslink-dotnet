﻿using System;
using DSLink.Util.Logger;

namespace DSLink.iOS
{
    public class iOSLogger : BaseLogger
    {
        public iOSLogger(string name, LogLevel toPrint) : base(name, toPrint)
        {
        }

        public override void Print(LogLevel logLevel, string message)
        {
            if (logLevel.DoesPrint(ToPrint))
            {
                Console.WriteLine(Format(logLevel, message));
            }
        }
    }
}

