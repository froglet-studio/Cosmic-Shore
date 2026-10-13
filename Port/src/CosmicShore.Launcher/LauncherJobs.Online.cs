using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Online play (SETTINGS > ONLINE). Every game this launcher starts gets the chosen server and key;
    /// and this PC can host the online server itself: the player run as <c>CosmicShore --server PORT</c>,
    /// the lobby and relay for every game on the network (or, with the port forwarded on the router,
    /// on the internet). It runs while the launcher is open, independent of any game, and games on
    /// the LAN prefer it over the server a game hosts on its own.
    /// </summary>
    public sealed partial class LauncherJobs
    {
        readonly object _serverLock = new();
        Process? _server;
        readonly List<string> _serverAddresses = new();
        string _serverStatus = "";

        /// <summary>The online server and key every game process uses (empty: the branch's server, else automatic).</summary>
        void OnlineEnvironment(ProcessStartInfo psi)
        {
            if (!string.IsNullOrWhiteSpace(_s.NetServer)) psi.Environment["COSMIC_SHORE_NET_SERVER"] = _s.NetServer.Trim();
            if (!string.IsNullOrEmpty(_s.NetKey)) psi.Environment["COSMIC_SHORE_NET_KEY"] = _s.NetKey;
        }

        public bool ServerRunning
        {
            get { lock (_serverLock) return ProcessRunner.IsRunning(_server); }
        }

        /// <summary>The addresses players on this network use, as the running server printed them.</summary>
        public IReadOnlyList<string> ServerAddresses
        {
            get { lock (_serverLock) return _serverAddresses.ToArray(); }
        }

        /// <summary>The server's last status line (players connected, sessions, relay links).</summary>
        public string ServerStatus
        {
            get { lock (_serverLock) return _serverStatus; }
        }

        /// <summary>Starts the online server on this PC (the built player with --server). False when there is no player build yet.</summary>
        public bool StartServer()
        {
            lock (_serverLock)
            {
                if (ProcessRunner.IsRunning(_server)) return true;
                if (!File.Exists(PlayerExe))
                {
                    Log.Add(LogKind.Warn, "Start the game once first: the online server is the game's player run with --server, and it is not built yet.");
                    return false;
                }
                var psi = PlayerStart(PlayerExe, new[] { "--server", _s.HostServerPort.ToString() }, audio: false, network: true, profile: null);
                psi.Environment.Remove("COSMIC_SHORE_NET_SERVER");
                _serverAddresses.Clear();
                _serverStatus = "starting";
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.OutputDataReceived += (_, e) => ServerOutput(e.Data);
                p.ErrorDataReceived += (_, e) => ServerOutput(e.Data);
                p.Exited += (_, _) =>
                {
                    lock (_serverLock) _serverStatus = "stopped";
                    Log.Add(LogKind.Info, "Online server stopped.");
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                _server = p;
                Log.Add(LogKind.Success, $"Online server starting on TCP {_s.HostServerPort}.");
                return true;
            }
        }

        void ServerOutput(string? line)
        {
            if (line == null) return;
            Log.Add(LogKind.Output, line);
            const string lan = "players on this network: server = ";
            lock (_serverLock)
            {
                int i = line.IndexOf(lan, StringComparison.Ordinal);
                if (i >= 0) _serverAddresses.Add(line.Substring(i + lan.Length).Trim());
                else if (line.Contains("player(s) connected", StringComparison.Ordinal)) _serverStatus = line.Replace("[online]", "").Trim();
                else if (line.Contains("cannot listen", StringComparison.Ordinal)) _serverStatus = line.Replace("[online]", "").Trim();
                else if (_serverStatus == "starting" && line.Contains("Ctrl+C stops", StringComparison.Ordinal)) _serverStatus = "running, nobody connected yet";
            }
        }

        public void StopServer()
        {
            lock (_serverLock)
            {
                try { if (_server is { HasExited: false }) _server.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                _server = null;
                _serverAddresses.Clear();
                _serverStatus = "";
            }
        }
    }
}
