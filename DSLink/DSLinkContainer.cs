using System;
using System.Threading.Tasks;
using DSLink.Connection;
using DSLink.Connection.Serializer;
using DSLink.Container;
using DSLink.Util.Logger;
using Newtonsoft.Json.Linq;

namespace DSLink
{
    /// <summary>
    /// DSLink implementation of a container.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class DSLinkContainer : AbstractContainer
    {
        /// <summary>
        /// Task used to send pings across the communication layer.
        /// </summary>
        private readonly Task _pingTask;

        /// <summary>
        /// SerializationManager handles serialization for communications.
        /// </summary>
        internal SerializationManager SerializationManager;

        /// <summary>
        /// Handshake object, which contains data from the initial connection handshake.
        /// </summary>
        protected Handshake Handshake;

        /// <summary>
        /// Whether to reconnect to the broker. 
        /// </summary>
        internal bool Reconnect;

        /// <summary>
        /// Flag for when this DSLink Container is initialized.
        /// Used to prevent duplicate initializations.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// DSLinkContainer constructor.
        /// </summary>
        /// <param name="config">Configuration for the DSLink</param>
        public DSLinkContainer(Configuration config) : base(config)
        {
            CreateLogger("DSLink");

            Reconnect = true;
            Connector = ConnectorManager.Create(this);

            // Events
            Connector.OnMessage += OnTextMessage;
            Connector.OnBinaryMessage += OnBinaryMessage;
            Connector.OnWrite += OnWrite;
            Connector.OnBinaryWrite += OnBinaryWrite;
            Connector.OnOpen += OnOpen;
            Connector.OnClose += OnClose;

            // Overridable events for DSLink writers
            Connector.OnOpen += OnConnectionOpen;
            Connector.OnClose += OnConnectionClosed;

            _pingTask = Task.Factory.StartNew(OnPingElapsed);
        }

        /// <summary>
        /// Initializes the DSLink.
        /// </summary>
        public async Task Initialize()
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;

            if (Config.Responder)
            {
                var loaded = await LoadNodes();
                if (!loaded)
                {
                    InitializeDefaultNodes();
                }
            }
        }

        /// <summary>
        /// Called when loading a the nodes.json is not successful.
        /// </summary>
        public virtual void InitializeDefaultNodes()
        {}

        /// <summary>
        /// Connect to the broker.
        /// </summary>
        public async Task Connect()
        {
            await Initialize();

            Reconnect = true;
            Handshake = new Handshake(this);
            var attemptsLeft = Config.ConnectionAttemptLimit;
            var attempts = 1;
            while (attemptsLeft == -1 || attemptsLeft > 0)
            {
                var handshakeStatus = await Handshake.Shake();
                if (handshakeStatus)
                {
                    SerializationManager = new SerializationManager(Config.CommunicationFormat);
                    Connector.Serializer = SerializationManager.Serializer;
                    await Connector.Connect();
                    return;
                }

                var delay = attempts;
                if (delay > Config.MaxConnectionCooldown)
                {
                    delay = Config.MaxConnectionCooldown;
                }
                Logger.Warning($"Failed to connect, delaying for {delay} seconds");
                await Task.Delay(TimeSpan.FromSeconds(delay));

                if (attemptsLeft > 0)
                {
                    attemptsLeft--;
                }
                attempts++;
            }
            Logger.Warning("Failed to connect within the allotted connection attempt limit.");
            OnConnectionFailed();
        }

        /// <summary>
        /// Disconnect from the broker.
        /// </summary>
        public void Disconnect()
        {
            Reconnect = false;
            Connector.Disconnect();
        }

        /// <summary>
        /// Loads the saved nodes from the filesystem.
        /// </summary>
        public async Task<bool> LoadNodes()
        {
            return await Responder.Deserialize();
        }

        /// <summary>
        /// Saves the nodes to the filesystem.
        /// </summary>
        /// <returns>Loading task</returns>
        public async Task SaveNodes()
        {
            await Responder.Serialize();
        }

        /// <summary>
        /// Event that fires when the connection to the broker is complete.
        /// </summary>
        private void OnOpen()
        {
            Connector.Flush();
        }

        /// <summary>
        /// Event that fires when the connection is closed to the broker.
        /// </summary>
        private async void OnClose()
        {
            Responder.SubscriptionManager.ClearAll();
            Responder.StreamManager.ClearAll();
            if (Reconnect)
            {
                await Connect();
            }
        }

        /// <summary>
        /// Called when the connection is opened to the broker.
        /// Override when you need to do something after connection opens.
        /// </summary>
        protected virtual void OnConnectionOpen() {}

        /// <summary>
        /// Called when the connection is closed to the broker.
        /// Override when you need to do something after connection closes.
        /// </summary>
        protected virtual void OnConnectionClosed() {}

        /// <summary>
        /// Called when the connection fails to connect to the broker.
        /// Override when you need to detect a failure to connect.
        /// </summary>
        protected virtual void OnConnectionFailed() {}

        /// <summary>
        /// Event that fires when a plain text message is received from the broker.
        /// This deserializes the message and hands it off to OnMessage.
        /// </summary>
        /// <param name="messageEvent">Text message event</param>
        private async void OnTextMessage(MessageEvent messageEvent)
        {
            if (Logger.ToPrint.DoesPrint(LogLevel.Debug))
            {
                Logger.Debug("Text Received: " + messageEvent.Message);
            }

            await OnMessage(SerializationManager.Serializer.Deserialize(messageEvent.Message));
        }

        /// <summary>
        /// Event that fires when a binary message is received from the broker.
        /// This deserializes the message and hands it off to OnMessage.
        /// </summary>
        /// <param name="messageEvent">Binary message event</param>
        private async void OnBinaryMessage(BinaryMessageEvent messageEvent)
        {
            if (Logger.ToPrint.DoesPrint(LogLevel.Debug))
            {
                if (messageEvent.Message.Length < 5000)
                {
                    Logger.Debug("Binary Received: " + BitConverter.ToString(messageEvent.Message));
                }
                else
                {
                    Logger.Debug("Binary Received: (over 5000 bytes)");
                }
            }

            await OnMessage(SerializationManager.Serializer.Deserialize(messageEvent.Message));
        }

        /// <summary>
        /// Called when a message is received from the server, and is passed in deserialized data.
        /// </summary>
        /// <param name="message">Deserialized data</param>
        private async Task OnMessage(JObject message)
        {
            var response = new JObject();
            if (message["msg"] != null)
            {
                response["ack"] = message["msg"].Value<int>();
            }

            bool write = false;

            if (message["requests"] != null)
            {
                JArray responses = await Responder.ProcessRequests(message["requests"].Value<JArray>());
                if (responses.Count > 0)
                {
                    response["responses"] = responses;
                }
                write = true;
            }

            if (message["responses"] != null)
            {
                JArray requests = await Requester.ProcessResponses(message["responses"].Value<JArray>());
                if (requests.Count > 0)
                {
                    response["requests"] = requests;
                }
                write = true;
            }

            if (write)
            {
                await Connector.Write(response);
            }
        }

        /// <summary>
        /// Event that is fired when a plain text message is sent to the broker.
        /// </summary>
        /// <param name="messageEvent">Text message event</param>
        private void OnWrite(MessageEvent messageEvent)
        {
            if (Logger.ToPrint.DoesPrint(LogLevel.Debug))
            {
                Logger.Debug("Text Sent: " + messageEvent.Message);
            }
        }

        /// <summary>
        /// Event that is fired when a binary message is sent to the broker.
        /// </summary>
        /// <param name="messageEvent">Binary message event</param>
        private void OnBinaryWrite(BinaryMessageEvent messageEvent)
        {
            if (Logger.ToPrint.DoesPrint(LogLevel.Debug))
            {
                if (messageEvent.Message.Length < 5000)
                {
                    Logger.Debug("Binary Sent: " + BitConverter.ToString(messageEvent.Message));
                }
                else
                {
                    Logger.Debug("Binary Sent: (over 5000 bytes)");
                }
            }
        }

        /// <summary>
        /// Task used to send pings occasionally to keep the connection alive.
        /// </summary>
        private async void OnPingElapsed()
        {
            while (_pingTask.Status != TaskStatus.Canceled)
            {
                if (Connector.Connected())
                {
                    // Write a blank message containing no responses/requests.
                    Logger.Debug("Sent ping");
                    await Connector.Write(new JObject(), false);
                }
                // TODO: Extract the amount of time to the configuration object.
                await Task.Delay(30000);
            }
        }
    }
}
