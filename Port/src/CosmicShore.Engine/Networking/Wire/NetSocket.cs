using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// The port's transport: reliable, ordered, length-prefixed frames over TCP. The server
    /// accepts any number of peers; a client holds one connection. Socket I/O runs on background
    /// threads and everything they receive is queued for the main thread, which drains it once
    /// per frame (<see cref="Poll"/>) - the same place the original transport delivers its events.
    /// Every frame is [int32 length][payload]; payload[0] is the message kind.
    /// </summary>
    internal sealed class NetSocket : IDisposable
    {
        public enum EventKind { Connected, Data, Disconnected }

        public readonly struct Event
        {
            public readonly EventKind Kind;
            public readonly int Peer;
            public readonly byte[] Payload;
            public Event(EventKind k, int peer, byte[] payload) { Kind = k; Peer = peer; Payload = payload; }
        }

        sealed class Peer
        {
            public int Id;
            public TcpClient Client;
            public NetworkStream Stream;
            public readonly object SendLock = new();
            public volatile bool Closed;
        }

        const int MaxFrame = 64 * 1024 * 1024;

        readonly ConcurrentQueue<Event> _inbox = new();
        readonly ConcurrentDictionary<int, Peer> _peers = new();
        TcpListener _listener;
        Thread _acceptThread;
        int _nextPeer;
        volatile bool _disposed;

        public bool IsServer { get; private set; }
        public int ListenPort { get; private set; }
        public int PeerCount => _peers.Count;

        public static NetSocket Listen(string address, int port)
        {
            var s = new NetSocket { IsServer = true };
            var ip = string.IsNullOrEmpty(address) || address == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(address);
            s._listener = new TcpListener(ip, port);
            s._listener.ExclusiveAddressUse = true; // a second instance on the same port must fail and pick another
            s._listener.Start();
            s.ListenPort = ((IPEndPoint)s._listener.LocalEndpoint).Port;
            s._acceptThread = new Thread(s.AcceptLoop) { IsBackground = true, Name = "net-accept" };
            s._acceptThread.Start();
            return s;
        }

        /// <summary>Connects on a background thread; a Connected or Disconnected event reports the outcome.</summary>
        public static NetSocket Connect(string address, int port, int timeoutMs)
        {
            var s = new NetSocket { IsServer = false };
            var t = new Thread(() =>
            {
                var client = new TcpClient { NoDelay = true };
                try
                {
                    var task = client.ConnectAsync(address, port);
                    if (!task.Wait(timeoutMs) || !client.Connected) throw new TimeoutException($"connect to {address}:{port} timed out");
                    s.AddPeer(client);
                }
                catch (Exception e)
                {
                    try { client.Dispose(); } catch { }
                    Console.WriteLine($"[net] connect failed: {e.GetBaseException().Message}");
                    s._inbox.Enqueue(new Event(EventKind.Disconnected, 0, null));
                }
            }) { IsBackground = true, Name = "net-connect" };
            t.Start();
            return s;
        }

        void AcceptLoop()
        {
            while (!_disposed)
            {
                TcpClient c;
                try { c = _listener.AcceptTcpClient(); }
                catch { if (_disposed) return; continue; }
                c.NoDelay = true;
                AddPeer(c);
            }
        }

        void AddPeer(TcpClient client)
        {
            var p = new Peer { Id = Interlocked.Increment(ref _nextPeer), Client = client, Stream = client.GetStream() };
            _peers[p.Id] = p;
            _inbox.Enqueue(new Event(EventKind.Connected, p.Id, null));
            new Thread(() => ReadLoop(p)) { IsBackground = true, Name = $"net-read-{p.Id}" }.Start();
        }

        void ReadLoop(Peer p)
        {
            var header = new byte[4];
            try
            {
                while (!p.Closed && !_disposed)
                {
                    ReadExactly(p.Stream, header, 4);
                    int len = BitConverter.ToInt32(header, 0);
                    if (len < 0 || len > MaxFrame) throw new InvalidDataException($"bad frame length {len}");
                    var buf = new byte[len];
                    ReadExactly(p.Stream, buf, len);
                    _inbox.Enqueue(new Event(EventKind.Data, p.Id, buf));
                }
            }
            catch (Exception) { }
            Drop(p);
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

        void Drop(Peer p)
        {
            if (!_peers.TryRemove(p.Id, out _)) return;
            p.Closed = true;
            try { p.Client.Close(); } catch { }
            _inbox.Enqueue(new Event(EventKind.Disconnected, p.Id, null));
        }

        /// <summary>Queue a frame to one peer (0 = the server, from a client).</summary>
        public void Send(int peer, byte[] payload, int length = -1)
        {
            if (length < 0) length = payload.Length;
            Peer p;
            if (!IsServer) { p = null; foreach (var kv in _peers) { p = kv.Value; break; } }
            else _peers.TryGetValue(peer, out p);
            if (p == null || p.Closed) return;
            try
            {
                lock (p.SendLock)
                {
                    p.Stream.Write(BitConverter.GetBytes(length), 0, 4);
                    p.Stream.Write(payload, 0, length);
                }
            }
            catch (Exception) { Drop(p); }
        }

        public void Disconnect(int peer)
        {
            if (!IsServer) { foreach (var kv in _peers) Drop(kv.Value); return; }
            if (_peers.TryGetValue(peer, out var p)) Drop(p);
        }

        public bool Poll(out Event e) => _inbox.TryDequeue(out e);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _listener?.Stop(); } catch { }
            foreach (var kv in _peers)
            {
                kv.Value.Closed = true;
                try { kv.Value.Client.Close(); } catch { }
            }
            _peers.Clear();
        }
    }
}
