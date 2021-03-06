using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSLink.Connection;
using DSLink.Crypto;
using DSLink.Util;
using DSLink.Util.Logger;
using Mono.Options;
using PCLStorage;

namespace DSLink
{
    /// <summary>
    /// Stores generic information about the DSLink.
    /// </summary>
    public class Configuration
    {
        public static IFolder StorageBaseFolder;

        /// <summary>
        /// Gets the Storage Folder.
        /// </summary>
        public static async Task<IFolder> GetStorageFolder()
        {
            if (StorageBaseFolder == null)
            {
                StorageBaseFolder = await FileSystem.Current.GetFolderFromPathAsync(".");
            }
            return StorageBaseFolder;
        }

        /// <summary>
        /// SHA256 cryptography instance
        /// </summary>
        private readonly SHA256 _sha256;

        /// <summary>
        /// Name of the DSLink
        /// </summary>
        public string Name;

        /// <summary>
        /// True when requester features are enabled.
        /// </summary>
        public readonly bool Requester;

        /// <summary>
        /// True when responder features are enabled.
        /// </summary>
        public readonly bool Responder;

        /// <summary>
        /// True when nodes.json loading is enabled.
        /// </summary>
        public bool LoadNodesJson;

        /// <summary>
        /// Full URL to the Broker.
        /// </summary>
        public string BrokerUrl;

        /// <summary>
        /// Location to store generated keypair.
        /// </summary>
        public readonly string KeysLocation;

        /// <summary>
        /// Communication format(internal use only)
        /// </summary>
        internal string _communicationFormat;

        /// <summary>
        /// Bouncy Castle KeyPair abstraction
        /// </summary>
        public KeyPair KeyPair { get; private set; }

        /// <summary>
        /// Authentication string
        /// </summary>
        public string Authentication => UrlBase64.Encode(_sha256.ComputeHash(Encoding.UTF8.GetBytes(RemoteEndpoint.salt).Concat(SharedSecret).ToArray()));

        /// <summary>
        /// Communication format
        /// </summary>
        public string CommunicationFormat => (string.IsNullOrEmpty(_communicationFormat) ? RemoteEndpoint.format : _communicationFormat);

        /// <summary>
        /// Shared secret string
        /// </summary>
        public byte[] SharedSecret => string.IsNullOrEmpty(RemoteEndpoint.tempKey) ? new byte[0] : KeyPair.GenerateSharedSecret(RemoteEndpoint.tempKey);

        /// <summary>
        /// DSLink ID
        /// </summary>
        public string DsId => Name + "-" + UrlBase64.Encode(_sha256.ComputeHash(KeyPair.EncodedPublicKey));

        /// <summary>
        /// Remote Endpoint object, stores data that came from the /conn endpoint
        /// </summary>
        public RemoteEndpoint RemoteEndpoint
        {
            internal set;
            get;
        }

        /// <summary>
        /// Highest log level that the logger will output to the console.
        /// </summary>
        public LogLevel LogLevel
        {
            private set;
            get;
        }

        /// <summary>
        /// Represents the amount of times the DSLink can attempt to connect
        /// to the broker. -1 will allow infinite attempts.
        /// </summary>
        public int ConnectionAttemptLimit
        {
            private set;
            get;
        }

        /// <summary>
        /// Represents the amount of time in seconds that the DSLink will
        /// delay after a connection failure.
        /// </summary>
        public int MaxConnectionCooldown
        {
            private set;
            get;
        }

        /// <summary>
        /// Configuration constructor
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="name">DSLink Name</param>
        /// <param name="requester">Enable requester features</param>
        /// <param name="responder">Enable responder features</param>
        /// <param name="keysLocation">Location to store keys</param>
        /// <param name="communicationFormat">Communication format (json, msgpack)</param>
        /// <param name="brokerUrl">Full URL of broker to connect to</param>
        /// <param name="logLevel">Log Level</param>
        /// <param name="connectionAttemptLimit">Limit of connection attempts (-1 means unlimited)</param>
        /// <param name="maxConnectionCooldown">Maximum Connection Cooldown</param>
        /// <param name="loadNodesJson">Enable loading of the nodes.json</param>
        public Configuration(IEnumerable<string> args, string name, bool requester = false, bool responder = false,
                             string keysLocation = ".keys", string communicationFormat = "",
                             string brokerUrl = "http://localhost:8080/conn", LogLevel logLevel = null,
                             int connectionAttemptLimit = -1, int maxConnectionCooldown = 60,
                             bool loadNodesJson = false)
        {
            if (logLevel == null)
            {
                logLevel = LogLevel.Info;
            }

            _sha256 = new SHA256();

            Name = name;
            Requester = requester;
            Responder = responder;
            KeysLocation = keysLocation;
            LoadNodesJson = loadNodesJson;
            _communicationFormat = communicationFormat;

            var options = new OptionSet
            {
                {
                    "broker=", val => { brokerUrl = val; }
                },
                {
                    "log=", val => { logLevel = LogLevel.ParseLogLevel(val); }
                }
            };
            options.Parse(args);

            BrokerUrl = brokerUrl;
            LogLevel = logLevel;
            ConnectionAttemptLimit = connectionAttemptLimit;
            if (maxConnectionCooldown < 1)
            {
                throw new InvalidOperationException("Cooldown must be greater than 0");
            }
            MaxConnectionCooldown = maxConnectionCooldown;

            Task.Run(async () =>
            {
                KeyPair = new KeyPair(await GetStorageFolder(), KeysLocation);
                await KeyPair.Load();
            }).Wait();
        }
    }
}
