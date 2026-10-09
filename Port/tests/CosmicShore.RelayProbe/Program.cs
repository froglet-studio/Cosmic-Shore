using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Online;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.RelayProbe
{
    /// <summary>
    /// The object the host spawns: a NetworkVariable it bumps, a ClientRpc ping the client answers with a
    /// ServerRpc pong (the RPC round trip), and a ClientRpc carrying a 20 KB string (fragmentation over Relay).
    /// The RPC bodies start with <see cref="NetRpc.Intercept"/> by hand - what the source sync weaves into game code.
    /// </summary>
    public sealed class ProbeBehaviour : NetworkBehaviour
    {
        public NetworkVariable<int> Counter = new(0);
        public static Action<int, double> Pinged, Ponged;
        public static Action<string> Blob;
        public static ProbeBehaviour Spawned;

        public override void OnNetworkSpawn() { Spawned = this; }

        [ClientRpc]
        public void PingClientRpc(int seq, double sentAt)
        {
            if (NetRpc.Intercept(this, nameof(PingClientRpc), new object[] { seq, sentAt })) return;
            Pinged?.Invoke(seq, sentAt);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PongServerRpc(int seq, double sentAt)
        {
            if (NetRpc.Intercept(this, nameof(PongServerRpc), new object[] { seq, sentAt })) return;
            Ponged?.Invoke(seq, sentAt);
        }

        [ClientRpc]
        public void BlobClientRpc(string text)
        {
            if (NetRpc.Intercept(this, nameof(BlobClientRpc), new object[] { text })) return;
            Blob?.Invoke(text);
        }
    }

    /// <summary>
    ///   relay-probe host [--udp] [--region R] [--data DIR] [--pings N] [--seconds S]
    ///   relay-probe join CODE [--udp] [--data DIR] [--seconds S]
    /// The host prints "JOINCODE XXXXXX" as soon as its slot exists; both print one "RESULT {json}" line at the end.
    /// Exit code 0 = every check passed on that side.
    /// </summary>
    public static class Program
    {
        const uint ProbeHash = 0x5E1A7001;
        static double Now => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

        public static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "";
            string code = mode == "join" && args.Length > 1 ? args[1] : null;
            string Opt(string name, string def) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
            bool udp = args.Contains("--udp");
            int pings = int.Parse(Opt("--pings", "100"));
            double seconds = double.Parse(Opt("--seconds", "90"), System.Globalization.CultureInfo.InvariantCulture);
            string data = Opt("--data", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "relay-probe-" + mode));
            System.IO.Directory.CreateDirectory(data);
            if (mode != "host" && mode != "join" || (mode == "join" && code == null))
            {
                Console.Error.WriteLine("usage: relay-probe host [--udp] [--region R] [--data DIR] | relay-probe join CODE [--udp] [--data DIR]");
                return 2;
            }

            var options = RelayOptions.FromEnvironment();
            if (udp) options.Dtls = false;
            if (Opt("--region", null) is { } region) options.Region = region;
            OnlineBoot.Register(data, options);
            NetworkManager.EmulateNetcodeLifecycle = true;
            NetDriver.Enabled = true;
            Console.WriteLine($"[probe] transport {NetTransports.Select("relay")}");

            var loop = new GameLoop("RelayProbe");
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            nm.SetSingleton();
            var prefab = new GameObject("ProbeObject");
            prefab.SetActive(false); // a template: never a scene object
            Object.DontDestroyOnLoad(prefab);
            var no = prefab.AddComponent<NetworkObject>();
            typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(no, ProbeHash);
            prefab.AddComponent<ProbeBehaviour>();
            nm.AddNetworkPrefab(prefab);

            var result = new Dictionary<string, object> { ["mode"] = mode, ["channel"] = options.Dtls ? "dtls" : "udp" };
            var sw = Stopwatch.StartNew();
            try
            {
                if (mode == "host") return Host(loop, nm, prefab, pings, seconds, result, sw);
                return Join(loop, nm, code, seconds, result, sw);
            }
            catch (Exception e)
            {
                result["error"] = e.Message;
                Console.WriteLine("RESULT " + JsonSerializer.Serialize(result));
                return 1;
            }
            finally
            {
                nm.Shutdown();
                // Let the transport's thread send its goodbyes (Disconnect, then the Relay CLOSE).
                for (int i = 0; i < 30; i++) { loop.Tick(1 / 60f); System.Threading.Thread.Sleep(16); }
                loop.Dispose();
            }
        }

        static void Tick(GameLoop loop)
        {
            loop.Tick(1 / 60f);
            System.Threading.Thread.Sleep(15);
        }

        static int Host(GameLoop loop, NetworkManager nm, GameObject prefab, int pings, double seconds, Dictionary<string, object> result, Stopwatch sw)
        {
            var backend = OnlineBoot.Backend;
            var t0 = Now;
            string code = backend.PrepareHostAsync(1).GetAwaiter().GetResult();
            result["prepareMs"] = Math.Round(Now - t0);
            result["signIn"] = backend.Session.LastSignIn;
            result["region"] = NetRelay.Region;
            Console.WriteLine("JOINCODE " + code);
            if (!nm.StartHost()) throw new Exception("StartHost failed");

            ulong client = ulong.MaxValue;
            nm.OnClientConnectedCallback += id => { if (id != 0) client = id; };
            bool clientLeft = false;
            nm.OnClientDisconnectCallback += id => { if (id == client) clientLeft = true; };
            var rtts = new List<double>();
            ProbeBehaviour.Ponged = (seq, sentAt) => rtts.Add(Now - sentAt);

            double connectedAt = -1;
            ProbeBehaviour probe = null;
            int sent = 0;
            double lastPing = 0;
            while (sw.Elapsed.TotalSeconds < seconds && !clientLeft)
            {
                Tick(loop);
                if (client == ulong.MaxValue) continue;
                if (probe == null)
                {
                    connectedAt = sw.Elapsed.TotalMilliseconds;
                    var go = Object.Instantiate(prefab);
                    go.SetActive(true);
                    go.GetComponent<NetworkObject>().Spawn();
                    probe = go.GetComponent<ProbeBehaviour>();
                    probe.BlobClientRpc(Blob());
                    continue;
                }
                if (sent < pings && Now - lastPing >= 50)
                {
                    lastPing = Now;
                    sent++;
                    probe.Counter.Value = sent;
                    probe.PingClientRpc(sent, Now);
                }
                if (sent >= pings && rtts.Count >= pings) break;
                if (sent >= pings && Now - lastPing > 5000) break; // the stragglers are lost
            }
            // Give the client a moment to read the last variable write, then let it leave first.
            double until = Now + 4000;
            while (!clientLeft && Now < until) Tick(loop);

            result["clientConnected"] = client != ulong.MaxValue;
            result["connectSeconds"] = connectedAt < 0 ? -1 : Math.Round(connectedAt / 1000, 2);
            result["pingsSent"] = sent;
            result["pongs"] = rtts.Count;
            Summarize(rtts, result, "rpcRtt");
            result["relayServerPingMs"] = Math.Round(OnlineBoot.Backend.RelayServerPingMs, 1);
            result["ok"] = client != ulong.MaxValue && rtts.Count >= pings * 0.95;
            Console.WriteLine("RESULT " + JsonSerializer.Serialize(result));
            return (bool)result["ok"] ? 0 : 1;
        }

        static int Join(GameLoop loop, NetworkManager nm, string code, double seconds, Dictionary<string, object> result, Stopwatch sw)
        {
            var backend = OnlineBoot.Backend;
            var t0 = Now;
            result["code"] = code;
            backend.PrepareJoinAsync(code).GetAwaiter().GetResult();
            result["prepareMs"] = Math.Round(Now - t0);
            result["signIn"] = backend.Session.LastSignIn;
            result["region"] = NetRelay.Region;
            bool connected = false;
            nm.OnClientConnectedCallback += id => connected = true;
            if (!nm.StartClient()) throw new Exception("StartClient failed");

            int pinged = 0, lastCounter = 0, counterChanges = 0;
            bool blobOk = false;
            double spawnedAt = -1;
            ProbeBehaviour.Pinged = (seq, sentAt) =>
            {
                pinged++;
                ProbeBehaviour.Spawned.PongServerRpc(seq, sentAt);
            };
            ProbeBehaviour.Blob = text => blobOk = text == Blob();
            double quietSince = Now;
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                Tick(loop);
                if (!nm.IsListening) { result["disconnectReason"] = nm.DisconnectReason; break; }
                var probe = ProbeBehaviour.Spawned;
                if (probe != null && spawnedAt < 0) spawnedAt = sw.Elapsed.TotalMilliseconds;
                if (probe != null && probe.Counter.Value != lastCounter) { lastCounter = probe.Counter.Value; counterChanges++; quietSince = Now; }
                if (lastCounter > 0 && Now - quietSince > 2500) break; // the host stopped writing: done
            }
            result["connected"] = connected;
            result["spawnSeen"] = spawnedAt >= 0;
            result["spawnSeconds"] = spawnedAt < 0 ? -1 : Math.Round(spawnedAt / 1000, 2);
            result["blob20kOk"] = blobOk;
            result["pingsReceived"] = pinged;
            result["finalCounter"] = lastCounter;
            result["counterChangesSeen"] = counterChanges;
            result["relayServerPingMs"] = Math.Round(OnlineBoot.Backend.RelayServerPingMs, 1);
            result["ok"] = connected && spawnedAt >= 0 && blobOk && pinged > 0 && lastCounter == pinged;
            Console.WriteLine("RESULT " + JsonSerializer.Serialize(result));
            return (bool)result["ok"] ? 0 : 1;
        }

        static string s_blob;
        static string Blob() => s_blob ??= string.Concat(Enumerable.Range(0, 2000).Select(i => $"{i:D9},"));

        static void Summarize(List<double> ms, Dictionary<string, object> result, string key)
        {
            if (ms.Count == 0) return;
            var s = ms.OrderBy(x => x).ToList();
            double P(double q) => Math.Round(s[Math.Min(s.Count - 1, (int)(q * s.Count))], 1);
            result[key + "MinMs"] = Math.Round(s[0], 1);
            result[key + "P50Ms"] = P(0.5);
            result[key + "P95Ms"] = P(0.95);
            result[key + "MaxMs"] = Math.Round(s[^1], 1);
        }
    }
}
