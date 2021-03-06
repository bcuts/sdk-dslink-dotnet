﻿using DSLink.Connection;
using DSLink.Util.Logger;

namespace DSLink.NET
{
    public static class NETPlatform
    {
        public static void Initialize()
        {
            ConnectorManager.SetConnector(typeof(WebSocketSharpConnector));
            BaseLogger.Logger = typeof(NETLogger);
        }
    }
}
