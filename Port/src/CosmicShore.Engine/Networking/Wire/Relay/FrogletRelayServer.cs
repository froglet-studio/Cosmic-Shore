using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Froglet's own relay: the server side of Unity Relay's message protocol (<see cref="RelayProtocol"/>)
    /// plus the Relay Allocations REST shape (<c>POST /v1/allocate</c>, <c>/v1/joincode</c>, <c>/v1/join</c>),
    /// so the same client (<see cref="RelayAllocationClient"/>, <see cref="RelayLink"/>) talks to it and to
    /// UGS alike (docs/MULTIPLAYER.md §6.7). It is the stand-in every relay test runs against, and a relay
    /// Froglet could host itself if UGS's bill ever demands it.
    ///
    /// What it enforces, as the service does: a BIND is accepted only with a valid HMAC from the
    /// allocation's key, and only with a larger nonce when it comes from a new address; a RELAY is
    /// forwarded only from the address the sender bound, and only to an allocation it CONNECT_REQUESTed
    /// (or that requested it); a binding silent for 10 s is dropped. Allocations are kept in memory.
    ///
    /// Run it standalone with the player: <c>CosmicShore --relay-server [UDP_PORT] [HTTP_PORT] [ADVERTISED_HOST]</c>.
    /// </summary>
    public sealed class FrogletRelayServer : IDisposable
    {
        /// <summary>The join-code alphabet the Relay service uses.</summary>
        public const string JoinCodeAlphabet = "6789BCDFGHJKLMNPQRTW";
        public const int BindingTimeoutMs = 10000;

        sealed class Alloc
        {
            public RelayAllocation Data;
            public string Key;
            public int MaxConnections;
            public IPEndPoint BoundAt;
            public int LastNonce = -1;
            public double LastSeen;
            public double Created;
            public readonly HashSet<string> Links = new();
        }

        readonly Socket _udp;
        readonly HttpListener _http;
        readonly Thread _udpThread, _httpThread;
        readonly object _lock = new();
        readonly Dictionary<string, Alloc> _byKey = new();
        readonly Dictionary<string, Alloc> _byJoinCode = new(StringComparer.OrdinalIgnoreCase);
        readonly byte[] _rx = new byte[2048], _tx = new byte[2048];
        readonly long _origin = System.Diagnostics.Stopwatch.GetTimestamp();
        volatile bool _stopped;

        public int UdpPort { get; }
        public int HttpPort { get; }
        public string AdvertisedHost { get; }
        public string BaseUrl => $"http://{(AdvertisedHost == "0.0.0.0" ? "127.0.0.1" : AdvertisedHost)}:{HttpPort}";

        /// <summary>Datagrams forwarded and messages refused, for the tests and the console.</summary>
        public long Forwarded, Refused, BindsAccepted, BindsRejected;

        double Now => System.Diagnostics.Stopwatch.GetElapsedTime(_origin).TotalMilliseconds;

        FrogletRelayServer(int udpPort, int httpPort, string advertisedHost)
        {
            AdvertisedHost = string.IsNullOrWhiteSpace(advertisedHost) ? "127.0.0.1" : advertisedHost;
            _udp = DirectLink.NewSocket(AddressFamily.InterNetwork);
            _udp.Bind(new IPEndPoint(IPAddress.Any, udpPort));
            UdpPort = ((IPEndPoint)_udp.LocalEndPoint).Port;
            if (httpPort == 0) httpPort = FreeTcpPort();
            _http = new HttpListener();
            // Loopback by default; a LAN or internet relay passes its own host (Linux binds it as given;
            // Windows needs a URL reservation for anything but localhost).
            string host = AdvertisedHost is "127.0.0.1" or "localhost" ? "127.0.0.1" : "+";
            _http.Prefixes.Add($"http://{host}:{httpPort}/");
            _http.Start();
            HttpPort = httpPort;
            _udpThread = new Thread(UdpLoop) { IsBackground = true, Name = "relay-udp" };
            _httpThread = new Thread(HttpLoop) { IsBackground = true, Name = "relay-http" };
            _udpThread.Start();
            _httpThread.Start();
        }

        public static FrogletRelayServer Start(int udpPort = 0, int httpPort = 0, string advertisedHost = "127.0.0.1")
            => new(udpPort, httpPort, advertisedHost);

        static int FreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        // ── Allocations (the REST API's work, also callable in-process) ──

        public RelayAllocation Allocate(int maxConnections)
        {
            var id = Guid.NewGuid();
            var a = new RelayAllocation
            {
                AllocationId = id.ToString(),
                AllocationIdBytes = id.ToByteArray(bigEndian: true),
                Key = RandomNumberGenerator.GetBytes(RelayProtocol.KeyLength),
                ServerHost = AdvertisedHost == "0.0.0.0" ? "127.0.0.1" : AdvertisedHost,
                ServerPort = UdpPort,
                Region = "froglet-local",
            };
            // Connection data names the allocation (its first 16 bytes) behind random padding; the service's is
            // encrypted and opaque to clients, which only ever echo it back.
            a.ConnectionData = new byte[RelayProtocol.ConnectionDataLength];
            RandomNumberGenerator.Fill(a.ConnectionData);
            a.AllocationIdBytes.CopyTo(a.ConnectionData, 0);
            lock (_lock) _byKey[a.LinkKey] = new Alloc { Data = a, Key = a.LinkKey, MaxConnections = Math.Max(1, maxConnections), Created = Now };
            return a;
        }

        public string CreateJoinCode(string allocationId)
        {
            lock (_lock)
            {
                var alloc = _byKey.Values.FirstOrDefault(x => x.Data.AllocationId == allocationId)
                    ?? throw new RelayServiceException(404, "allocation not found");
                if (alloc.Data.JoinCode.Length > 0) return alloc.Data.JoinCode;
                string code;
                do code = new string(Enumerable.Range(0, 6).Select(_ => JoinCodeAlphabet[RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length)]).ToArray());
                while (_byJoinCode.ContainsKey(code));
                alloc.Data.JoinCode = code;
                _byJoinCode[code] = alloc;
                return code;
            }
        }

        public RelayAllocation Join(string joinCode)
        {
            Alloc host;
            lock (_lock)
                if (!_byJoinCode.TryGetValue(joinCode ?? "", out host)) throw new RelayServiceException(404, "join code not found");
            var a = Allocate(1);
            a.HostConnectionData = host.Data.ConnectionData;
            a.JoinCode = joinCode;
            return a;
        }

        public int AllocationCount { get { lock (_lock) return _byKey.Count; } }

        // ── The data plane ──────────────────────────────────────────

        void UdpLoop()
        {
            while (!_stopped)
            {
                try
                {
                    if (_udp.Poll(5000, SelectMode.SelectRead))
                    {
                        while (_udp.Available > 0)
                        {
                            EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                            int n;
                            try { n = _udp.ReceiveFrom(_rx, ref ep); }
                            catch (SocketException) { continue; }
                            lock (_lock) Handle(n, (IPEndPoint)ep);
                        }
                    }
                    lock (_lock) Expire();
                }
                catch (ObjectDisposedException) { return; }
                catch (Exception e) { Console.WriteLine($"[relay] {e.GetType().Name}: {e.Message}"); }
            }
        }

        void Handle(int n, IPEndPoint from)
        {
            var b = _rx.AsSpan(0, n);
            if (!RelayProtocol.TryReadHeader(b, out byte type))
            {
                if (RelayProtocol.IsWrongVersion(b) && n >= RelayProtocol.CloseLength)
                    Reply(from, RelayProtocol.WriteError(_tx, b.Slice(4, 16), RelayProtocol.ErrInvalidProtocolVersion));
                return;
            }
            double now = Now;
            switch (type)
            {
                case RelayProtocol.Bind when n >= RelayProtocol.BindLength:
                {
                    var alloc = ByConnectionData(b.Slice(8, RelayProtocol.ConnectionDataLength));
                    Span<byte> mac = stackalloc byte[RelayProtocol.HmacLength];
                    if (alloc != null) RelayProtocol.ComputeBindHmac(b[..RelayProtocol.BindSignedLength], alloc.Data.Key, mac);
                    if (alloc == null || !CryptographicOperations.FixedTimeEquals(mac, b.Slice(RelayProtocol.BindSignedLength, RelayProtocol.HmacLength)))
                    {
                        BindsRejected++; // silently, as the service does
                        return;
                    }
                    int nonce = RelayProtocol.BindNonce(b);
                    bool sameAddress = alloc.BoundAt != null && alloc.BoundAt.Equals(from);
                    if (alloc.BoundAt != null && !sameAddress && nonce <= alloc.LastNonce) { BindsRejected++; return; }
                    alloc.BoundAt = from;
                    alloc.LastNonce = Math.Max(alloc.LastNonce, nonce);
                    alloc.LastSeen = now;
                    BindsAccepted++;
                    Reply(from, RelayProtocol.WriteBindReceived(_tx));
                    return;
                }
                case RelayProtocol.Ping when n >= RelayProtocol.PingLength:
                {
                    var alloc = Bound(b.Slice(4, 16), from);
                    if (alloc == null) { Refuse(from, b.Slice(4, 16), RelayProtocol.ErrClientPlayerMismatch); return; }
                    alloc.LastSeen = now;
                    Reply(from, b);
                    return;
                }
                case RelayProtocol.ConnectRequest when n >= RelayProtocol.ConnectRequestLength:
                {
                    var asker = Bound(b.Slice(4, 16), from);
                    if (asker == null) { Refuse(from, b.Slice(4, 16), RelayProtocol.ErrClientPlayerMismatch); return; }
                    asker.LastSeen = now;
                    var target = ByConnectionData(b.Slice(21, RelayProtocol.ConnectionDataLength));
                    if (target == null) { Refuse(from, b.Slice(4, 16), RelayProtocol.ErrAllocationNotFound); return; }
                    if (target == asker) { Refuse(from, b.Slice(4, 16), RelayProtocol.ErrSelfConnectNotAllowed); return; }
                    if (target.BoundAt == null) { Refuse(from, b.Slice(4, 16), RelayProtocol.ErrNotConnected); return; }
                    if (!target.Links.Contains(asker.Key) && target.Links.Count >= target.MaxConnections) { Refuse(from, b.Slice(4, 16), RelayProtocol.ErrNotConnected); return; }
                    asker.Links.Add(target.Key);
                    target.Links.Add(asker.Key);
                    Reply(from, RelayProtocol.WriteAccepted(_tx, target.Data.AllocationIdBytes, asker.Data.AllocationIdBytes));
                    return;
                }
                case RelayProtocol.Relay when n >= RelayProtocol.RelayHeaderLength:
                {
                    var sender = Bound(b.Slice(4, 16), from);
                    if (sender == null) { Refuse(from, b.Slice(4, 16), RelayProtocol.ErrClientPlayerMismatch); return; }
                    sender.LastSeen = now;
                    int len = RelayProtocol.RelayContentLength(b);
                    if (len > RelayProtocol.MaxRelayContent || RelayProtocol.RelayHeaderLength + len > n) { Refused++; return; }
                    var toKey = RelayProtocol.Key(b.Slice(20, 16));
                    if (!sender.Links.Contains(toKey) || !_byKey.TryGetValue(toKey, out var target) || target.BoundAt == null)
                    {
                        Refuse(from, b.Slice(4, 16), RelayProtocol.ErrNotConnected);
                        return;
                    }
                    Reply(target.BoundAt, b[..(RelayProtocol.RelayHeaderLength + len)]);
                    Forwarded++;
                    return;
                }
                case RelayProtocol.Disconnect when n >= RelayProtocol.DisconnectLength:
                {
                    var asker = Bound(b.Slice(4, 16), from);
                    if (asker == null) { Refuse(from, b.Slice(4, 16), RelayProtocol.ErrClientPlayerMismatch); return; }
                    var otherKey = RelayProtocol.Key(b.Slice(20, 16));
                    asker.Links.Remove(otherKey);
                    if (_byKey.TryGetValue(otherKey, out var other)) other.Links.Remove(asker.Key);
                    Reply(from, b[..RelayProtocol.DisconnectLength]); // the service echoes it as confirmation
                    return;
                }
                case RelayProtocol.Close when n >= RelayProtocol.CloseLength:
                {
                    var alloc = Bound(b.Slice(4, 16), from);
                    if (alloc != null) Unbind(alloc);
                    return;
                }
            }
        }

        Alloc ByConnectionData(ReadOnlySpan<byte> connectionData)
            => _byKey.TryGetValue(RelayProtocol.Key(connectionData[..16]), out var a) && connectionData.SequenceEqual(a.Data.ConnectionData) ? a : null;

        /// <summary>The allocation, if it is bound at exactly this address.</summary>
        Alloc Bound(ReadOnlySpan<byte> id, IPEndPoint from)
            => _byKey.TryGetValue(RelayProtocol.Key(id), out var a) && a.BoundAt != null && a.BoundAt.Equals(from) ? a : null;

        void Refuse(IPEndPoint to, ReadOnlySpan<byte> id, byte code)
        {
            Refused++;
            Reply(to, _tx.AsSpan(0, RelayProtocol.WriteError(_tx, id, code)));
        }

        void Reply(IPEndPoint to, int length) => Reply(to, _tx.AsSpan(0, length));

        void Reply(IPEndPoint to, ReadOnlySpan<byte> bytes)
        {
            try { _udp.SendTo(bytes, SocketFlags.None, to); }
            catch (SocketException) { }
        }

        void Unbind(Alloc a)
        {
            foreach (var k in a.Links) if (_byKey.TryGetValue(k, out var o)) o.Links.Remove(a.Key);
            a.Links.Clear();
            a.BoundAt = null;
        }

        void Expire()
        {
            double now = Now;
            List<Alloc> gone = null;
            foreach (var a in _byKey.Values)
            {
                if (a.BoundAt != null && now - a.LastSeen > BindingTimeoutMs)
                {
                    Reply(a.BoundAt, RelayProtocol.WriteError(_tx, a.Data.AllocationIdBytes, RelayProtocol.ErrTimeout));
                    Unbind(a);
                }
                // An allocation nobody bound within a minute, or unbound for five, is gone (the service's own life is shorter).
                if (a.BoundAt == null && now - Math.Max(a.Created, a.LastSeen) > (a.LastSeen > 0 ? 300000 : 60000)) (gone ??= new()).Add(a);
            }
            if (gone == null) return;
            foreach (var a in gone)
            {
                _byKey.Remove(a.Key);
                if (a.Data.JoinCode.Length > 0 && _byJoinCode.TryGetValue(a.Data.JoinCode, out var j) && j == a) _byJoinCode.Remove(a.Data.JoinCode);
            }
        }

        // ── The REST API ────────────────────────────────────────────

        void HttpLoop()
        {
            while (!_stopped)
            {
                HttpListenerContext ctx;
                try { ctx = _http.GetContext(); }
                catch (Exception) { if (_stopped) return; continue; }
                Task.Run(() => Serve(ctx));
            }
        }

        void Serve(HttpListenerContext ctx)
        {
            int status = 200;
            JsonNode body;
            try
            {
                string path = ctx.Request.Url!.AbsolutePath.TrimEnd('/');
                JsonNode req = null;
                if (ctx.Request.HasEntityBody)
                {
                    using var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    var text = r.ReadToEnd();
                    if (text.Length > 0) req = JsonNode.Parse(text);
                }
                var meta = new JsonObject { ["requestId"] = Guid.NewGuid().ToString() };
                switch (path)
                {
                    case "/v1/allocate":
                        status = 201;
                        body = new JsonObject { ["meta"] = meta, ["data"] = new JsonObject { ["allocation"] = Allocate((int?)req?["maxConnections"] ?? 1).ToJson() } };
                        break;
                    case "/v1/joincode":
                        body = new JsonObject { ["meta"] = meta, ["data"] = new JsonObject { ["joinCode"] = CreateJoinCode((string)req?["allocationId"]) } };
                        break;
                    case "/v1/join":
                    {
                        var a = Join((string)req?["joinCode"]);
                        var json = a.ToJson();
                        json["hostConnectionData"] = Convert.ToBase64String(a.HostConnectionData);
                        body = new JsonObject { ["meta"] = meta, ["data"] = new JsonObject { ["allocation"] = json } };
                        break;
                    }
                    case "/v1/regions":
                        body = new JsonObject { ["data"] = new JsonObject { ["regions"] = new JsonArray(new JsonObject { ["id"] = "froglet-local", ["description"] = "Froglet relay" }) } };
                        break;
                    case "/health":
                        body = new JsonObject { ["ok"] = true, ["allocations"] = AllocationCount, ["forwarded"] = Forwarded };
                        break;
                    default:
                        status = 404;
                        body = new JsonObject { ["title"] = "Not Found", ["detail"] = path };
                        break;
                }
                if (status == 201 || status == 200) meta["status"] = status;
            }
            catch (RelayServiceException e) { status = e.Status; body = new JsonObject { ["title"] = "Error", ["detail"] = e.Message, ["status"] = e.Status }; }
            catch (Exception e) { status = 400; body = new JsonObject { ["title"] = "Bad Request", ["detail"] = e.Message, ["status"] = 400 }; }
            try
            {
                var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes);
                ctx.Response.Close();
            }
            catch (Exception) { }
        }

        public void Dispose()
        {
            if (_stopped) return;
            _stopped = true;
            try { _http.Stop(); _http.Close(); } catch { }
            try { _udp.Dispose(); } catch { }
        }
    }
}
