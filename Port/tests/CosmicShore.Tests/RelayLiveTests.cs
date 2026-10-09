using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CosmicShore.Tests
{
    /// <summary>Runs only when COSMIC_SHORE_RELAY_LIVE=1: these talk to Unity's real services.</summary>
    public sealed class RelayLiveFactAttribute : FactAttribute
    {
        public RelayLiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("COSMIC_SHORE_RELAY_LIVE") != "1")
                Skip = "live Unity Relay test: set COSMIC_SHORE_RELAY_LIVE=1 to run it (docs/RELAY.md)";
        }
    }

    /// <summary>
    /// Two processes, the engine's real netcode, Unity's real Relay (docs/RELAY.md): relay-probe hosts (signs in,
    /// allocates, prints a join code), a second relay-probe joins by that code, and over Relay the host spawns
    /// a NetworkObject, sends 20 KB in one RPC (fragmented), then 100 ClientRpc pings that come back as ServerRpc
    /// pongs while it bumps a NetworkVariable. Each side checks what it saw. Uses the game's UGS project,
    /// "development" environment; the UGS player of each side is cached in its own temp folder.
    /// </summary>
    public class RelayLiveTests
    {
        readonly Xunit.Abstractions.ITestOutputHelper _out;
        public RelayLiveTests(Xunit.Abstractions.ITestOutputHelper output) { _out = output; }

        static string ProbeDll()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CosmicShore.slnx"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("Port/ not found above the test output");
            var candidates = new[] { "Debug", "Release" }
                .Select(c => Path.Combine(dir.FullName, "tests", "CosmicShore.RelayProbe", "bin", c, "net10.0", "relay-probe.dll"))
                .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).ToList();
            return candidates.FirstOrDefault() ?? throw new InvalidOperationException("relay-probe.dll not built (dotnet build tests/CosmicShore.RelayProbe)");
        }

        /// <summary>A probe process whose stdout and stderr are drained on background threads, so a full pipe never stalls it.</summary>
        sealed class Probe : IDisposable
        {
            public readonly Process Process;
            public readonly System.Collections.Concurrent.BlockingCollection<string> Lines = new();
            readonly System.Text.StringBuilder _all = new();
            public Probe(string args)
            {
                var psi = new ProcessStartInfo("dotnet", $"\"{ProbeDll()}\" {args}")
                { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                Process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                Process.OutputDataReceived += (_, e) => { if (e.Data == null) Lines.CompleteAdding(); else { lock (_all) _all.AppendLine(e.Data); Lines.Add(e.Data); } };
                Process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_all) _all.AppendLine("[stderr] " + e.Data); };
                Process.Start();
                Process.BeginOutputReadLine();
                Process.BeginErrorReadLine();
            }
            public string Output { get { lock (_all) return _all.ToString(); } }
            public string Result => Output.Split('\n').Select(l => l.TrimEnd('\r')).LastOrDefault(l => l.StartsWith("RESULT "))?[7..];
            public void Finish(int ms) { if (!Process.WaitForExit(ms)) { try { Process.Kill(true); } catch { } } Process.WaitForExit(); }
            public void Dispose() { try { if (!Process.HasExited) Process.Kill(true); } catch { } Process.Dispose(); }
        }

        void Run(string channelArg)
        {
            string data = Path.Combine(Path.GetTempPath(), "cosmicshore-relay-live");
            using var host = new Probe($"host {channelArg} --data \"{Path.Combine(data, "host")}\" --pings 100");
            string code = null;
            var sw = Stopwatch.StartNew();
            while (code == null && sw.Elapsed.TotalSeconds < 40)
            {
                if (!host.Lines.TryTake(out var line, 1000)) { if (host.Lines.IsCompleted) break; continue; }
                if (line.StartsWith("JOINCODE ")) code = line[9..].Trim();
            }
            Assert.True(code != null, "the host never printed a join code:\n" + host.Output);

            using var join = new Probe($"join {code} {channelArg} --data \"{Path.Combine(data, "join")}\"");
            join.Finish(150000);
            host.Finish(90000);
            foreach (var l in host.Output.Split('\n')) _out.WriteLine("host: " + l.TrimEnd('\r'));
            foreach (var l in join.Output.Split('\n')) _out.WriteLine("join: " + l.TrimEnd('\r'));
            string hostResult = host.Result, joinResult = join.Result;

            Assert.NotNull(hostResult);
            Assert.NotNull(joinResult);
            var h = JsonDocument.Parse(hostResult).RootElement;
            var j = JsonDocument.Parse(joinResult).RootElement;
            Assert.True(j.GetProperty("spawnSeen").GetBoolean(), "the client never saw the NetworkObject spawn");
            Assert.True(j.GetProperty("blob20kOk").GetBoolean(), "the 20 KB RPC did not arrive intact");
            Assert.Equal(j.GetProperty("pingsReceived").GetInt32(), j.GetProperty("finalCounter").GetInt32()); // NetworkVariable kept pace with the RPCs
            Assert.True(h.GetProperty("ok").GetBoolean(), "host: " + hostResult);
            Assert.True(j.GetProperty("ok").GetBoolean(), "join: " + joinResult);
            Assert.Equal(0, host.Process.ExitCode);
            Assert.Equal(0, join.Process.ExitCode);
        }

        [RelayLiveFact]
        public void SpawnRpcAndNetworkVariable_OverRealRelay_Dtls() => Run("");

        [RelayLiveFact]
        public void SpawnRpcAndNetworkVariable_OverRealRelay_PlainUdp() => Run("--udp");
    }
}
