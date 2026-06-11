using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Tasks
{
    /// <summary>
    /// First-party async primitives replacing UniTask. Awaiting any of these resumes on
    /// the game loop during its scheduler phase — main-thread affinity is structural,
    /// not a per-call-site discipline (this retires the old `.AsMainThread()` contract).
    /// Cancellation runs the awaiting continuation synchronously from
    /// <c>CancellationTokenSource.Cancel()</c>, matching the Unity-era UniTask behavior
    /// ported code depends on (cancel → catch/finally runs before Cancel returns).
    ///
    /// Mapping from the Unity-era code:
    ///   `await UniTask.Yield(PlayerLoopTiming.Update[, ct])` → `await GameTask.Yield([ct])`
    ///   `await UniTask.Delay(ms[, ct])`                      → `await GameTask.Delay(ms[, ct])`
    ///   `await UniTask.WaitUntil(pred[, ct])`                → `await GameTask.WaitUntil(pred[, ct])`
    ///   `async UniTask` / `async UniTaskVoid` signatures     → `async Task` (+ `.Forget()`)
    /// </summary>
    public static class GameTask
    {
        internal static GameTaskScheduler Scheduler
            => (GameLoop.Current ?? throw new InvalidOperationException(
                "GameTask requires an active GameLoop.")).Scheduler;

        /// <summary>Resume during the next frame's scheduler phase.</summary>
        public static YieldAwaitable Yield(CancellationToken cancellationToken = default)
            => new(cancellationToken);

        /// <summary>Resume after <paramref name="seconds"/> of scaled game time.</summary>
        public static DelayAwaitable Delay(float seconds, CancellationToken cancellationToken = default)
            => new(seconds, unscaled: false, cancellationToken);

        public static DelayAwaitable Delay(float seconds, bool unscaledTime, CancellationToken cancellationToken = default)
            => new(seconds, unscaledTime, cancellationToken);

        /// <summary>Resume after <paramref name="milliseconds"/> of scaled game time (UniTask.Delay-style call sites).</summary>
        public static DelayAwaitable Delay(int milliseconds, CancellationToken cancellationToken = default)
            => new(milliseconds / 1000f, unscaled: false, cancellationToken);

        /// <summary>Resume on the frame the predicate first returns true (checked once per frame; completes synchronously if already true).</summary>
        public static WaitUntilAwaitable WaitUntil(Func<bool> predicate, CancellationToken cancellationToken = default)
            => new(predicate, cancellationToken);

        public static WaitUntilAwaitable WaitWhile(Func<bool> predicate, CancellationToken cancellationToken = default)
            => new(() => !predicate(), cancellationToken);

        /// <summary>Resume at the end of the current frame (after LateUpdate).</summary>
        public static EndOfFrameAwaitable WaitForEndOfFrame(CancellationToken cancellationToken = default)
            => new(cancellationToken);

        /// <summary>
        /// Hop onto the game loop from any thread. Completes synchronously when already
        /// on the loop thread. The replacement for the Unity-era MainThreadDispatcher.
        /// </summary>
        public static SwitchToMainThreadAwaitable SwitchToMainThread() => new();
    }

    public readonly struct YieldAwaitable
    {
        readonly CancellationToken _token;
        public YieldAwaitable(CancellationToken token) { _token = token; }
        public Awaiter GetAwaiter() => new(_token);

        public readonly struct Awaiter : INotifyCompletion
        {
            readonly CancellationToken _token;
            public Awaiter(CancellationToken token) { _token = token; }
            public bool IsCompleted => false;
            public void OnCompleted(Action continuation)
                => GameTask.Scheduler.EnqueueNextFrame(continuation, _token);
            public void GetResult() => _token.ThrowIfCancellationRequested();
        }
    }

    public readonly struct DelayAwaitable
    {
        readonly float _seconds;
        readonly bool _unscaled;
        readonly CancellationToken _token;

        public DelayAwaitable(float seconds, bool unscaled, CancellationToken token)
        {
            _seconds = seconds;
            _unscaled = unscaled;
            _token = token;
        }

        public Awaiter GetAwaiter() => new(_seconds, _unscaled, _token);

        public readonly struct Awaiter : INotifyCompletion
        {
            readonly float _seconds;
            readonly bool _unscaled;
            readonly CancellationToken _token;

            public Awaiter(float seconds, bool unscaled, CancellationToken token)
            {
                _seconds = seconds;
                _unscaled = unscaled;
                _token = token;
            }

            public bool IsCompleted => false;
            public void OnCompleted(Action continuation)
                => GameTask.Scheduler.EnqueueDelay(_seconds, _unscaled, _token, continuation);
            public void GetResult() => _token.ThrowIfCancellationRequested();
        }
    }

    public readonly struct WaitUntilAwaitable
    {
        readonly GameTaskScheduler.WaitEntry _entry;

        public WaitUntilAwaitable(Func<bool> predicate, CancellationToken token)
        {
            _entry = new GameTaskScheduler.WaitEntry
            {
                Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate)),
                Token = token,
            };
        }

        public Awaiter GetAwaiter() => new(_entry);

        public readonly struct Awaiter : INotifyCompletion
        {
            readonly GameTaskScheduler.WaitEntry _entry;

            internal Awaiter(GameTaskScheduler.WaitEntry entry) { _entry = entry; }

            public bool IsCompleted
            {
                get
                {
                    if (_entry.Token.IsCancellationRequested) return true;
                    try { return _entry.Predicate(); }
                    catch (Exception e)
                    {
                        // Surface synchronously through GetResult (UniTask parity).
                        _entry.Error = e;
                        return true;
                    }
                }
            }

            public void OnCompleted(Action continuation)
            {
                _entry.Item = new ScheduledItem(continuation, _entry.Token);
                GameTask.Scheduler.EnqueueWait(_entry);
            }

            public void GetResult()
            {
                if (_entry.Error is { } error) throw error;
                _entry.Token.ThrowIfCancellationRequested();
            }
        }
    }

    public readonly struct EndOfFrameAwaitable
    {
        readonly CancellationToken _token;
        public EndOfFrameAwaitable(CancellationToken token) { _token = token; }
        public Awaiter GetAwaiter() => new(_token);

        public readonly struct Awaiter : INotifyCompletion
        {
            readonly CancellationToken _token;
            public Awaiter(CancellationToken token) { _token = token; }
            public bool IsCompleted => false;
            public void OnCompleted(Action continuation)
                => GameTask.Scheduler.EnqueueEndOfFrame(continuation, _token);
            public void GetResult() => _token.ThrowIfCancellationRequested();
        }
    }

    public readonly struct SwitchToMainThreadAwaitable
    {
        public Awaiter GetAwaiter() => new();

        public readonly struct Awaiter : INotifyCompletion
        {
            public bool IsCompleted => GameLoop.Current?.IsOnLoopThread == true;
            public void OnCompleted(Action continuation)
            {
                var loop = GameLoop.Current
                    ?? throw new InvalidOperationException("SwitchToMainThread requires an active GameLoop.");
                loop.SyncContext.Post(_ => continuation(), null);
            }
            public void GetResult() { }
        }
    }

    public static class GameTaskExtensions
    {
        /// <summary>
        /// Fire-and-forget with exception observation: faults are logged through
        /// <see cref="Debug.LogException(Exception)"/>, cancellation is swallowed.
        /// </summary>
        public static async void Forget(this Task task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
