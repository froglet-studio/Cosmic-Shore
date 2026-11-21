using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CosmicShore.Engine.Tasks
{
    /// <summary>
    /// SynchronizationContext bound to the game loop. Installed for the duration of every
    /// <see cref="GameLoop.Tick"/>, so standard <c>await</c>s of external Tasks (file IO,
    /// sockets, backend SDKs) capture it and their continuations marshal back onto the
    /// loop automatically — pumped at the start of each frame. This replaces the entire
    /// Unity-era MainThreadDispatcher / `.AsMainThread()` machinery structurally.
    /// </summary>
    public sealed class GameSynchronizationContext : SynchronizationContext
    {
        readonly GameLoop _loop;
        readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> _queue = new();

        public GameSynchronizationContext(GameLoop loop) { _loop = loop; }

        public override void Post(SendOrPostCallback d, object state) => _queue.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object state)
        {
            if (_loop.IsOnLoopThread)
            {
                d(state);
                return;
            }

            using var done = new ManualResetEventSlim(false);
            Exception captured = null;
            _queue.Enqueue((s =>
            {
                try { d(s); }
                catch (Exception e) { captured = e; }
                finally { done.Set(); }
            }, state));
            done.Wait();
            if (captured != null) throw captured;
        }

        public override SynchronizationContext CreateCopy() => this;

        /// <summary>Drain queued posts on the loop thread. Bounded per frame to the count at entry.</summary>
        internal void Pump()
        {
            int count = _queue.Count;
            for (int i = 0; i < count && _queue.TryDequeue(out var item); i++)
            {
                try { item.Callback(item.State); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
