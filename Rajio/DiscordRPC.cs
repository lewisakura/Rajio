using System.Diagnostics;
using DiscordRPC;
using DiscordRPC.Logging;

namespace Rajio
{
    class DiscordRPC
    {
        private readonly string _clientId = "614495728949133322";
        private readonly DiscordRpcClient _client;

        public DiscordRPC()
        {
            _client = new DiscordRpcClient(_clientId) {Logger = new ConsoleLogger {Level = LogLevel.Trace}};
            _client.Initialize();
        }

        ~DiscordRPC()
        {
            _client.Dispose();
            Debug.WriteLine("discordrpc disposed");
        }

        public void SetPresence(RichPresence presence)
        {
            if (_client.IsInitialized)
            {
                _client.SetPresence(presence);
            }
        }
    }
}
