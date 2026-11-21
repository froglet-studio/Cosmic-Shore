using System;
using System.Collections.Generic;
using System.Threading;

namespace CosmicShore.Engine.Tasks
{
    /// <summary>
    /// A continuation that runs exactly once — either when the scheduler reaches it, or
    /// synchronously from <see cref="CancellationToken"/> cancellation (matching the
    /// Unity-era UniTask contract that ported code relies on: <c>cts.Cancel()</c> runs
    /// the awaiting code's catch/finally before Cancel returns).
    /// </summary>
    internal sealed class ScheduledItem
    {
        readonly Action _continuation;
        int _state;
        CancellationTokenRegistration _registration;

        public ScheduledItem(Action continuation, CancellationToken token)
        {
            _continuation = continuation;
            if (token.CanBeCanceled)
                _registration = token.Register(static s => ((ScheduledItem)s).Invoke(), this);
        }

        public bool Invoked => Volatile.Read(ref _state) == 1;

        public void Invoke()
        {
            if (Interlocked.Exchange(ref _state, 1) == 1) return;
            _registration.Dispose();
            try { _continuation(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>
    /// Frame-driven continuation scheduler — the first-party replacement for UniTask's
    /// player-loop integration. Continuations resume on the loop thread during the
    /// scheduler phases of <see cref="GameLoop.Tick"/> (cancellation resumes them
    /// synchronously on the cancelling thread, same as the original). Enqueue is
    /// thread-safe so external threads can hop onto the loop.
    /// </summary>
    public sealed class GameTaskScheduler
    {
        internal sealed class WaitEntry
        {
            public Func<bool> Predicate;
            public CancellationToken Token;
            public Exception Error;
            public ScheduledItem Item;
        }

        sealed class DelayEntry
        {
            public float Due;
            public bool Unscaled;
            public ScheduledItem Item;
        }

        readonly object _gate = new();
        List<ScheduledItem> _nextFrame = new(), _nextFrameRun = new();
        List<ScheduledItem> _endOfFrame = new(), _endOfFrameRun = new();
        readonly List<DelayEntry> _delays = new();
        readonly List<WaitEntry> _waits = new();

        internal void EnqueueNextFrame(Action continuation, CancellationToken token)
        {
            var item = new ScheduledItem(continuation, token);
            lock (_gate) _nextFrame.Add(item);
        }

        internal void EnqueueEndOfFrame(Action continuation, CancellationToken token)
        {
            var item = new ScheduledItem(continuation, token);
            lock (_gate) _endOfFrame.Add(item);
        }

        internal void EnqueueDelay(float seconds, bool unscaled, CancellationToken token, Action continuation)
        {
            float now = unscaled ? Time.unscaledTime : Time.time;
            var entry = new DelayEntry
            {
                Due = now + seconds,
                Unscaled = unscaled,
                Item = new ScheduledItem(continuation, token),
            };
            lock (_gate) _delays.Add(entry);
        }

        internal void EnqueueWait(WaitEntry entry)
        {
            lock (_gate) _waits.Add(entry);
        }

        /// <summary>Runs after Update each frame (matches the original coroutine resume point).</summary>
        internal void RunFrame()
        {
            // Yields: swap buffers so continuations awaiting again land next frame.
            lock (_gate) (_nextFrame, _nextFrameRun) = (_nextFrameRun, _nextFrame);
            foreach (var item in _nextFrameRun) item.Invoke();
            _nextFrameRun.Clear();

            // Delays: completion when due (cancellation already ran the continuation).
            for (int i = _delays.Count - 1; i >= 0; i--)
            {
                var entry = _delays[i];
                if (entry.Item.Invoked)
                {
                    _delays.RemoveAt(i);
                    continue;
                }
                float now = entry.Unscaled ? Time.unscaledTime : Time.time;
                if (now >= entry.Due)
                {
                    _delays.RemoveAt(i);
                    entry.Item.Invoke();
                }
            }

            // WaitUntil/WaitWhile: predicate or predicate exception completes the wait.
            for (int i = _waits.Count - 1; i >= 0; i--)
            {
                var entry = _waits[i];
                if (entry.Item.Invoked)
                {
                    _waits.RemoveAt(i);
                    continue;
                }

                bool complete;
                try { complete = entry.Predicate(); }
                catch (Exception e)
                {
                    entry.Error = e;
                    complete = true;
                }

                if (complete)
                {
                    _waits.RemoveAt(i);
                    entry.Item.Invoke();
                }
            }
        }

        internal void RunEndOfFrame()
        {
            lock (_gate) (_endOfFrame, _endOfFrameRun) = (_endOfFrameRun, _endOfFrame);
            foreach (var item in _endOfFrameRun) item.Invoke();
            _endOfFrameRun.Clear();
        }
    }
}
