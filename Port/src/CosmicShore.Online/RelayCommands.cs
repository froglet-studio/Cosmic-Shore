using System;
using System.Threading.Tasks;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Online
{
    /// <summary>
    /// Host or join a Relay game directly, without the party/session flow - the player's --relay-host and
    /// --relay-join CODE flags and its <c>do relay ...</c> verb (docs/RELAY.md):
    ///   relay              status: signed-in player, region, join code, relay ping
    ///   relay host         restart this game's NetworkManager as a host behind a new Relay slot; prints the join code
    ///   relay join CODE    restart it as a client of the host behind CODE
    /// The NetworkManager the game already runs (the menu's own host) is shut down first, as a party join does.
    /// </summary>
    public static class RelayCommands
    {
        static string s_autoMode, s_autoCode;
        static double s_readySince = -1;
        static bool s_busy;

        /// <summary>Hosts (<paramref name="mode"/> "host") or joins ("join", with <paramref name="code"/>) once the game's own host is up.</summary>
        public static void ArmAutoStart(string mode, string code = null)
        {
            s_autoMode = mode;
            s_autoCode = code;
        }

        /// <summary>Once per frame (main thread): starts an armed --relay-host / --relay-join when the game is ready for it.</summary>
        public static void Tick()
        {
            if (s_autoMode == null) return;
            var nm = NetworkManager.Singleton;
            // Ready = the game brought its own NetworkManager up (the menu's host); a few seconds later the menu has settled.
            bool ready = nm != null && nm.IsListening && nm.IsServer;
            double now = Environment.TickCount64 / 1000.0;
            if (!ready) { s_readySince = -1; return; }
            if (s_readySince < 0) { s_readySince = now; return; }
            if (now - s_readySince < 3) return;
            string mode = s_autoMode, code = s_autoCode;
            s_autoMode = null;
            // With --relay the game's own party host already went through Relay: keep it (and its code)
            // instead of replacing it with a second slot and a second, confusing code.
            if (mode == "host" && NetRelay.InUse && !string.IsNullOrEmpty(NetRelay.JoinCode))
            {
                Console.WriteLine($"[relay] already hosting through Unity Relay - JOIN CODE: {NetRelay.JoinCode}");
                return;
            }
            Console.WriteLine(Command(mode == "join" ? "join " + code : "host"));
        }

        public static string Command(string arg)
        {
            var parts = (arg ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var backend = NetRelay.Backend;
            if (parts.Length == 0 || parts[0] == "status")
                return backend == null ? "[relay] off - start the player with --relay (or COSMIC_SHORE_NET_TRANSPORT=relay)" : "[relay] " + backend.Status;
            if (backend == null) return "[relay] off - start the player with --relay (or COSMIC_SHORE_NET_TRANSPORT=relay)";
            if (s_busy) return "[relay] busy with the previous command";
            switch (parts[0].ToLowerInvariant())
            {
                case "host": Run(() => backend.PrepareHostAsync(15), host: true); return "[relay] reserving a Relay slot...";
                case "join" when parts.Length > 1: Run(() => backend.PrepareJoinAsync(parts[1]), host: false); return $"[relay] joining {parts[1].ToUpperInvariant()}...";
                default: return "[relay] usage: relay [status] | relay host | relay join CODE";
            }
        }

        static async void Run(Func<Task> prepare, bool host)
        {
            s_busy = true;
            try
            {
                var nm = NetworkManager.Singleton;
                if (nm == null) { Console.WriteLine("[relay] no NetworkManager in this scene yet"); return; }
                await prepare(); // resumes on the main thread (the game loop's synchronization context)
                if (nm.IsListening) nm.Shutdown();
                bool ok = host ? nm.StartHost() : nm.StartClient();
                Console.WriteLine(ok ? $"[relay] {(host ? "hosting" : "connecting")} through Unity Relay" : "[relay] the NetworkManager would not start");
            }
            catch (Exception e) { Console.WriteLine($"[relay] {(host ? "host" : "join")} failed: {e.Message}"); }
            finally { s_busy = false; }
        }
    }
}
