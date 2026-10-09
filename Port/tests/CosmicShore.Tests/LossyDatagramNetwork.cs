using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>A name as a datagram address (equal names = the same peer).</summary>
    sealed class MemEndPoint : EndPoint
    {
        public readonly string Name;
        public MemEndPoint(string name) { Name = name; }
        public override string ToString() => Name;
        public override bool Equals(object obj) => obj is MemEndPoint m && m.Name == Name;
        public override int GetHashCode() => Name.GetHashCode();
    }

    /// <summary>
    /// An in-memory datagram network that loses, duplicates, delays and reorders - what a relay path over the
    /// internet can do - deterministically (seeded). Each node is an <see cref="IDatagramLink"/>, so the real
    /// <see cref="UdpTransport"/> reliability layer runs over it unchanged, exactly as it runs over Unity Relay.
    /// </summary>
    sealed class LossyDatagramNetwork
    {
        public double LossPercent, DuplicatePercent, ReorderPercent;
        /// <summary>A reordered datagram is held back up to this long; every datagram also gets <see cref="BaseDelayMs"/>.</summary>
        public int MaxExtraDelayMs = 30, BaseDelayMs = 2;
        /// <summary>Drop everything (a dead line).</summary>
        public volatile bool Silent;
        public int LargestDatagram, Datagrams, Dropped, Duplicated;

        readonly Random _rng;
        readonly object _lock = new();
        readonly Dictionary<string, Node> _nodes = new();
        readonly Stopwatch _clock = Stopwatch.StartNew();

        public LossyDatagramNetwork(int seed = 7) { _rng = new Random(seed); }

        public Node Add(string name)
        {
            var n = new Node(this, new MemEndPoint(name));
            lock (_lock) _nodes[name] = n;
            return n;
        }

        double Now => _clock.Elapsed.TotalMilliseconds;

        void Deliver(Node from, EndPoint to, byte[] data)
        {
            lock (_lock)
            {
                Datagrams++;
                LargestDatagram = Math.Max(LargestDatagram, data.Length);
                if (Silent || !_nodes.TryGetValue(to.ToString(), out var target)) { Dropped++; return; }
                if (_rng.NextDouble() * 100 < LossPercent) { Dropped++; return; }
                int copies = _rng.NextDouble() * 100 < DuplicatePercent ? 2 : 1;
                if (copies == 2) Duplicated++;
                for (int i = 0; i < copies; i++)
                {
                    double delay = BaseDelayMs + (_rng.NextDouble() * 100 < ReorderPercent ? _rng.NextDouble() * MaxExtraDelayMs : 0);
                    target.Enqueue(Now + delay, from.Address, data);
                }
            }
        }

        public sealed class Node : IDatagramLink
        {
            readonly LossyDatagramNetwork _net;
            readonly List<(double due, EndPoint from, byte[] data)> _queue = new();
            public readonly MemEndPoint Address;
            public bool Disposed { get; private set; }

            public Node(LossyDatagramNetwork net, MemEndPoint address) { _net = net; Address = address; }

            internal void Enqueue(double due, EndPoint from, byte[] data)
            {
                lock (_queue) _queue.Add((due, from, data));
            }

            public int Receive(byte[] buffer, int waitMicros, out EndPoint from)
            {
                double until = _net.Now + waitMicros / 1000.0;
                while (true)
                {
                    lock (_queue)
                    {
                        int best = -1;
                        double now = _net.Now;
                        for (int i = 0; i < _queue.Count; i++)
                            if (_queue[i].due <= now && (best < 0 || _queue[i].due < _queue[best].due)) best = i;
                        if (best >= 0)
                        {
                            var item = _queue[best];
                            _queue.RemoveAt(best);
                            from = item.from;
                            Buffer.BlockCopy(item.data, 0, buffer, 0, item.data.Length);
                            return item.data.Length;
                        }
                    }
                    if (_net.Now >= until) { from = null; return -1; }
                    Thread.Sleep(0);
                }
            }

            public void Send(byte[] buffer, int length, EndPoint to) => _net.Deliver(this, to, buffer.AsSpan(0, length).ToArray());
            public void Service(double nowMs) { }
            public bool IsServer(EndPoint from, EndPoint server) => from != null && from.Equals(server);
            public int LocalPort => 0;
            public string Fault => null;
            public void Dispose() { Disposed = true; }
        }
    }

    /// <summary>Reads a transport's events with a deadline, keeping the ones a test has not asked for yet.</summary>
    sealed class EventPump
    {
        readonly INetTransport _t;
        readonly List<NetEvent> _pending = new();
        public EventPump(INetTransport t) { _t = t; }

        public NetEvent Next(Func<NetEvent, bool> match, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                for (int i = 0; i < _pending.Count; i++)
                    if (match(_pending[i])) { var e = _pending[i]; _pending.RemoveAt(i); return e; }
                while (_t.Poll(out var ev)) _pending.Add(ev);
                if (_pending.Exists(e => match(e))) continue;
                if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("the expected transport event never came");
                Thread.Sleep(1);
            }
        }

        public NetEvent Next(NetEventKind kind, int timeoutMs = 10000) => Next(e => e.Kind == kind, timeoutMs);

        /// <summary>Every event of <paramref name="kind"/> that arrives within <paramref name="ms"/>.</summary>
        public List<NetEvent> Drain(NetEventKind kind, int ms)
        {
            var sw = Stopwatch.StartNew();
            var list = new List<NetEvent>();
            while (sw.ElapsedMilliseconds < ms)
            {
                while (_t.Poll(out var ev)) _pending.Add(ev);
                list.AddRange(_pending.FindAll(e => e.Kind == kind));
                _pending.RemoveAll(e => e.Kind == kind);
                Thread.Sleep(1);
            }
            return list;
        }
    }
}
