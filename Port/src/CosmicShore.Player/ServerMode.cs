using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Player
{
    /// <summary>
    /// <c>CosmicShore --server [PORT] [--key KEY] [--name NAME] [--no-discovery]</c>: Amoebius's online
    /// server on its own - the lobby and relay every game on the network (or, port-forwarded or on a
    /// machine with a public address, on the internet) plays through. No window, no game content.
    /// A server started this way is standalone: games on the LAN find it and prefer it over the
    /// one a game hosts by itself. The key (or COSMIC_SHORE_NET_KEY) keeps strangers out.
    /// </summary>
    static class ServerMode
    {
        public static bool Requested(string[] args) => args.Contains("--server");

        public static int Run(string[] args)
        {
            var options = new OnlineServerOptions { Standalone = true, Key = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_KEY") };
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--server" when i + 1 < args.Length && int.TryParse(args[i + 1], out var p): options.Port = p; i++; break;
                    case "--port" when i + 1 < args.Length && int.TryParse(args[i + 1], out var p2): options.Port = p2; i++; break;
                    case "--key" when i + 1 < args.Length: options.Key = args[++i]; break;
                    case "--name" when i + 1 < args.Length: options.Name = args[++i]; break;
                    case "--no-discovery": options.Discovery = false; break;
                }
            }

            OnlineServer server;
            try { server = OnlineServer.Start(options); }
            catch (SocketException e)
            {
                Console.WriteLine($"[online] cannot listen on TCP {options.Port}: {e.Message}. Is another server already running? Pick another port: --server PORT");
                return 1;
            }

            Console.WriteLine($"[online] Amoebius online server {server.Id} on TCP {server.Port}{(server.DiscoveryActive ? ", announced on the LAN" : "")}.");
            foreach (var ip in LocalAddresses())
                Console.WriteLine($"[online]   players on this network: server = {ip}:{server.Port}");
            Console.WriteLine($"[online]   players elsewhere: forward TCP {server.Port} on the router to this machine, then server = <your public address>:{server.Port}");
            if (!string.IsNullOrEmpty(options.Key)) Console.WriteLine("[online]   a key is required (the launcher's Server key, or COSMIC_SHORE_NET_KEY)");
            Console.WriteLine("[online] Ctrl+C stops the server.");

            using var done = new ManualResetEventSlim();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.Set(); };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => done.Set();
            string last = null;
            while (!done.Wait(30000))
            {
                var status = server.Status;
                if (status != last) Console.WriteLine("[online] " + status);
                last = status;
            }
            server.Dispose();
            Console.WriteLine("[online] server stopped");
            return 0;
        }

        static string[] LocalAddresses()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                    .Select(a => a.Address.ToString())
                    .Distinct()
                    .ToArray();
            }
            catch (Exception) { return Array.Empty<string>(); }
        }
    }
}
