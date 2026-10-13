using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// The online server's wire format. Every connection to the server is TCP and opens with one
    /// JSON frame ([int32 length][UTF-8 JSON]) that says what it is:
    ///  • <c>hello</c>: a game's CONTROL connection. Lobby requests (create, join, query, refresh,
    ///    save, leave, delete, remove, assert, ping) and their replies follow as frames, matched by
    ///    <see cref="OnlineMessage.Rid"/>; the server also pushes events (<c>link</c>, <c>moved</c>).
    ///  • <c>link-open</c>: a joining client's RELAY link to a session's host.
    ///  • <c>link-accept</c>: the host's half of that link, opened when the server asked for it.
    /// A link is answered with one frame; once both halves are paired the server copies bytes
    /// between them untouched, so the netcode's own frames (<see cref="NetSocket"/>) run end to end.
    /// LAN discovery is UDP on <see cref="DiscoveryPort"/>: a probe <c>AMOEBIUS?</c>, answered
    /// (and announced) as <c>AMOEBIUS!</c> + <see cref="ServerAnnouncement"/> JSON.
    /// </summary>
    internal static class OnlineWire
    {
        /// <summary>Bumped whenever a message changes meaning; the server refuses another version.</summary>
        public const int Version = 1;

        /// <summary>The server's TCP port and the LAN discovery UDP port.</summary>
        public const int DefaultPort = 47777;
        /// <summary>LAN discovery's UDP port (settable so tests never collide with a game running on the machine).</summary>
        public static int DiscoveryPort { get; set; } = 47777;

        public const string ProbePrefix = "AMOEBIUS?";
        public const string AnswerPrefix = "AMOEBIUS!";

        /// <summary>A control frame is a lobby message; a 100-player presence lobby is far below this.</summary>
        public const int MaxFrame = 8 * 1024 * 1024;

        /// <summary>The address a join hands NetworkManager so the transport connects through the relay.</summary>
        public const string RelayScheme = "amoebius-relay:";

        static readonly JsonSerializerOptions s_json = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

        public static byte[] Encode(OnlineMessage m) => JsonSerializer.SerializeToUtf8Bytes(m, s_json);

        public static OnlineMessage Decode(byte[] json) => JsonSerializer.Deserialize<OnlineMessage>(json, s_json);

        public static string EncodeAnnouncement(ServerAnnouncement a) => AnswerPrefix + JsonSerializer.Serialize(a, s_json);

        public static ServerAnnouncement DecodeAnnouncement(string text)
        {
            if (text == null || !text.StartsWith(AnswerPrefix, StringComparison.Ordinal)) return null;
            try { return JsonSerializer.Deserialize<ServerAnnouncement>(text.Substring(AnswerPrefix.Length), s_json); }
            catch (JsonException) { return null; }
        }

        /// <summary>Writes one frame (callers serialise writes per stream).</summary>
        public static void WriteFrame(Stream s, OnlineMessage m)
        {
            var body = Encode(m);
            var buf = new byte[4 + body.Length];
            BitConverter.TryWriteBytes(buf.AsSpan(0, 4), body.Length);
            body.CopyTo(buf, 4);
            s.Write(buf, 0, buf.Length);
            s.Flush();
        }

        /// <summary>Reads one frame; throws at end of stream or on a frame that cannot be a message.</summary>
        public static OnlineMessage ReadFrame(Stream s)
        {
            var header = new byte[4];
            ReadExactly(s, header, 4);
            int len = BitConverter.ToInt32(header, 0);
            if (len <= 0 || len > MaxFrame) throw new InvalidDataException($"bad frame length {len}");
            var body = new byte[len];
            ReadExactly(s, body, len);
            return Decode(body) ?? throw new InvalidDataException("empty frame");
        }

        static void ReadExactly(Stream s, byte[] buf, int len)
        {
            int got = 0;
            while (got < len)
            {
                int n = s.Read(buf, got, len - got);
                if (n <= 0) throw new EndOfStreamException();
                got += n;
            }
        }

        /// <summary>
        /// The options every online socket uses: no Nagle delay, and TCP keep-alive probing after 15 s
        /// of silence, so a peer that vanished without closing (a pulled cable, a sleeping laptop) is
        /// dropped in under a minute instead of after the OS's retransmission timeout.
        /// </summary>
        public static void Tune(TcpClient c)
        {
            c.NoDelay = true;
            try
            {
                c.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                c.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 15);
                c.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                c.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 4);
            }
            catch (Exception) { /* a platform without the per-socket knobs keeps its defaults */ }
        }

        /// <summary>A TCP connection with the options every online socket uses.</summary>
        public static TcpClient Connect(string host, int port, int timeoutMs)
        {
            var c = new TcpClient();
            Tune(c);
            try
            {
                var task = c.ConnectAsync(host, port);
                if (!task.Wait(timeoutMs) || !c.Connected) throw new TimeoutException($"connect to {host}:{port} timed out");
                return c;
            }
            catch
            {
                c.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Windows reports an ICMP port-unreachable (a probe to a port nobody listens on, an answer to a
        /// prober that already closed) as an exception on the socket's NEXT receive. Discovery sockets
        /// turn that off (SIO_UDP_CONNRESET), so one stale datagram cannot cut a receive loop short.
        /// </summary>
        public static void IgnoreConnectionResets(UdpClient udp)
        {
            if (!OperatingSystem.IsWindows()) return;
            const int SioUdpConnReset = -1744830452;
            try { udp.Client.IOControl(SioUdpConnReset, new byte[] { 0, 0, 0, 0 }, null); }
            catch (Exception) { }
        }

        /// <summary>"host:port", "host" (the default port), or "[v6]:port" → its parts.</summary>
        public static bool TryParseEndpoint(string text, out string host, out int port)
        {
            host = null;
            port = DefaultPort;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (text.StartsWith("[", StringComparison.Ordinal))
            {
                int close = text.IndexOf(']');
                if (close < 0) return false;
                host = text.Substring(1, close - 1);
                var rest = text.Substring(close + 1);
                return rest.Length == 0 || (rest.StartsWith(":", StringComparison.Ordinal) && int.TryParse(rest.Substring(1), out port) && port is > 0 and < 65536);
            }
            int colon = text.LastIndexOf(':');
            if (colon > 0 && text.IndexOf(':') == colon)
            {
                host = text.Substring(0, colon);
                return int.TryParse(text.Substring(colon + 1), out port) && port is > 0 and < 65536;
            }
            host = text;
            return true;
        }
    }

    /// <summary>One control or link message. Unused fields stay at their defaults and are not sent.</summary>
    internal sealed class OnlineMessage
    {
        /// <summary>A request or opening frame: hello, create, join, query, refresh, save, leave, delete, remove, assert, ping, link-open, link-accept, link-reject.</summary>
        public string Op { get; set; }
        /// <summary>A server push: link (open your half of a relay link), moved (this server stood down for another).</summary>
        public string Ev { get; set; }
        /// <summary>Request id a reply echoes; 0 on events.</summary>
        public long Rid { get; set; }
        public bool Ok { get; set; }
        /// <summary>A refusal's <see cref="SessionError"/> name.</summary>
        public string Err { get; set; }
        public string Msg { get; set; }

        public int V { get; set; }
        public string Player { get; set; }
        public string Key { get; set; }
        public string Build { get; set; }
        /// <summary>hello reply: the server's id and the address it sees this client at.</summary>
        public string Server { get; set; }
        public string Addr { get; set; }
        public int Port { get; set; }

        public string Sid { get; set; }
        public string Link { get; set; }
        public SessionRecord Session { get; set; }
        public Dictionary<string, PropRecord> Props { get; set; }
        public List<FilterRecord> Filters { get; set; }
        public List<SessionSummary> Found { get; set; }
    }

    /// <summary>A query filter on the wire.</summary>
    internal sealed class FilterRecord
    {
        public int Field { get; set; }
        public string Value { get; set; }
        public int Operation { get; set; }

        public static FilterRecord From(FilterOption f) => new() { Field = (int)f.Field, Value = f.Value, Operation = (int)f.Operation };
        public FilterOption ToOption() => new((FilterField)Field, Value, (FilterOperation)Operation);
    }

    /// <summary>A query result on the wire: what <see cref="ISessionInfo"/> exposes, no roster.</summary>
    internal sealed class SessionSummary : ISessionInfo
    {
        public string Id { get; set; }
        public long CreatedTicks { get; set; }
        public int MaxPlayers { get; set; }
        public int AvailableSlots { get; set; }
        public bool IsLocked { get; set; }

        [JsonIgnore] public DateTime Created => new(CreatedTicks, DateTimeKind.Utc);
        [JsonIgnore] public bool HasPassword => false;

        public static SessionSummary From(SessionRecord r)
        {
            var info = SessionRules.Info(r);
            return new SessionSummary { Id = r.Id, CreatedTicks = r.Created, MaxPlayers = info.MaxPlayers, AvailableSlots = info.AvailableSlots, IsLocked = r.IsLocked };
        }
    }

    /// <summary>What a server says about itself on the LAN (probe answers and periodic announcements).</summary>
    internal sealed class ServerAnnouncement
    {
        public int V { get; set; }
        public string Id { get; set; }
        public int Port { get; set; }
        /// <summary>When it started (UTC ticks): the oldest server is the one everyone converges on.</summary>
        public long Started { get; set; }
        /// <summary>Hosted on purpose (<c>--server</c>, the launcher) rather than inside a game: never stands down.</summary>
        public bool Standalone { get; set; }
        public string Name { get; set; }
        public bool Keyed { get; set; }

        /// <summary>True when <paramref name="a"/> is the server everyone should converge on rather than <paramref name="b"/>.</summary>
        public static bool Better(ServerAnnouncement a, ServerAnnouncement b)
        {
            if (b == null) return true;
            if (a.Standalone != b.Standalone) return a.Standalone;
            if (a.Started != b.Started) return a.Started < b.Started;
            return string.CompareOrdinal(a.Id, b.Id) < 0;
        }
    }
}
