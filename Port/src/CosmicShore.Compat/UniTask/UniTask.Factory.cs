using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GameTask = CosmicShore.Engine.Tasks.GameTask;
using EngineTime = CosmicShore.Engine.Time;

namespace Cysharp.Threading.Tasks
{
    // Static factories. Every frame-bound wait resumes on the game loop through the engine's
    // GameTask scheduler; cancellation completes a wait synchronously from Cancel() (the
    // engine's contract — upstream's `cancelImmediately: true`; the flag is accepted and the
    // immediate behavior is used either way). A token already canceled at the call returns a
    // canceled task without touching the loop.
    public readonly partial struct UniTask
    {
        public static readonly UniTask CompletedTask = default;

        // ── Pre-completed ────────────────────────────────────────────────

        public static UniTask<T> FromResult<T>(T value) => new(value);

        public static UniTask FromException(Exception exception)
        {
            var source = new UniTaskSource();
            source.TrySetException(exception);
            return new UniTask(source);
        }

        public static UniTask<T> FromException<T>(Exception exception)
        {
            var source = new UniTaskSource<T>();
            source.TrySetException(exception);
            return new UniTask<T>(source);
        }

        public static UniTask FromCanceled(CancellationToken cancellationToken = default)
        {
            var source = new UniTaskSource();
            source.TrySetCanceled(cancellationToken);
            return new UniTask(source);
        }

        public static UniTask<T> FromCanceled<T>(CancellationToken cancellationToken = default)
        {
            var source = new UniTaskSource<T>();
            source.TrySetCanceled(cancellationToken);
            return new UniTask<T>(source);
        }

        /// <summary>A task that never completes — except by cancellation of <paramref name="cancellationToken"/>.</summary>
        public static UniTask Never(CancellationToken cancellationToken) => new(new NeverSource<AsyncUnit>(cancellationToken));

        public static UniTask<T> Never<T>(CancellationToken cancellationToken) => new(new NeverSource<T>(cancellationToken));

        // ── Wrappers ─────────────────────────────────────────────────────

        public static UniTask Create(Func<UniTask> factory) => factory();

        public static UniTask<T> Create<T>(Func<UniTask<T>> factory) => factory();

        /// <summary>Invokes <paramref name="factory"/> only when the result is first observed (awaited / status read).</summary>
        public static UniTask Defer(Func<UniTask> factory) => new(new DeferSource(factory));

        public static UniTask<T> Defer<T>(Func<UniTask<T>> factory) => new(new DeferSource<T>(factory));

        /// <summary>Run an <c>async UniTaskVoid</c> lambda now.</summary>
        public static void Void(Func<UniTaskVoid> asyncAction) => asyncAction().Forget();

        public static void Void(Func<CancellationToken, UniTaskVoid> asyncAction, CancellationToken cancellationToken)
            => asyncAction(cancellationToken).Forget();

        /// <summary>Wrap an <c>async UniTaskVoid</c> lambda as a plain <see cref="System.Action"/> (event handlers).</summary>
        public static System.Action Action(Func<UniTaskVoid> asyncAction) => () => asyncAction().Forget();

        public static System.Action Action(Func<CancellationToken, UniTaskVoid> asyncAction, CancellationToken cancellationToken)
            => () => asyncAction(cancellationToken).Forget();

        // ── Frame waits ──────────────────────────────────────────────────

        /// <summary>Resume on a later frame (Update-class timing). Upstream returns an awaitable, not a UniTask.</summary>
        public static YieldAwaitable Yield() => new(PlayerLoopTiming.Update);

        public static YieldAwaitable Yield(PlayerLoopTiming timing) => new(timing);

        public static UniTask Yield(CancellationToken cancellationToken, bool cancelImmediately = false)
            => Yield(PlayerLoopTiming.Update, cancellationToken, cancelImmediately);

        public static UniTask Yield(PlayerLoopTiming timing, CancellationToken cancellationToken, bool cancelImmediately = false)
            => cancellationToken.IsCancellationRequested ? FromCanceled(cancellationToken) : YieldCore(timing, cancellationToken);

        /// <summary>Resume on the next frame (never the current one, whatever phase the caller is in).</summary>
        public static UniTask NextFrame() => YieldCore(PlayerLoopTiming.Update, default);

        public static UniTask NextFrame(PlayerLoopTiming timing) => YieldCore(timing, default);

        public static UniTask NextFrame(CancellationToken cancellationToken, bool cancelImmediately = false)
            => NextFrame(PlayerLoopTiming.Update, cancellationToken, cancelImmediately);

        public static UniTask NextFrame(PlayerLoopTiming timing, CancellationToken cancellationToken, bool cancelImmediately = false)
            => cancellationToken.IsCancellationRequested ? FromCanceled(cancellationToken) : YieldCore(timing, cancellationToken);

        public static UniTask WaitForEndOfFrame(CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => cancellationToken.IsCancellationRequested ? FromCanceled(cancellationToken) : YieldCore(PlayerLoopTiming.PostLateUpdate, cancellationToken);

        public static UniTask WaitForEndOfFrame(CosmicShore.Engine.MonoBehaviour coroutineRunner, CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => WaitForEndOfFrame(cancellationToken, cancelImmediately);

        /// <summary>Approximation: the engine exposes no fixed-step resume point, so this resumes like an Update yield.</summary>
        public static UniTask WaitForFixedUpdate(CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => Yield(PlayerLoopTiming.FixedUpdate, cancellationToken, cancelImmediately);

        public static UniTask DelayFrame(int delayFrameCount, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            if (delayFrameCount < 0)
                throw new ArgumentOutOfRangeException(nameof(delayFrameCount), "Delay frame count must be non-negative.");
            if (cancellationToken.IsCancellationRequested) return FromCanceled(cancellationToken);
            return DelayFrameCore(delayFrameCount, delayTiming, cancellationToken);
        }

        // ── Time waits ───────────────────────────────────────────────────

        public static UniTask WaitForSeconds(float duration, bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => Delay(TimeSpan.FromSeconds(duration), ignoreTimeScale, delayTiming, cancellationToken, cancelImmediately);

        public static UniTask WaitForSeconds(int duration, bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => Delay(TimeSpan.FromSeconds(duration), ignoreTimeScale, delayTiming, cancellationToken, cancelImmediately);

        /// <summary>Delay in scaled game time, or unscaled when <paramref name="ignoreTimeScale"/>.</summary>
        public static UniTask Delay(int millisecondsDelay, bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => Delay(TimeSpan.FromMilliseconds(millisecondsDelay), ignoreTimeScale, delayTiming, cancellationToken, cancelImmediately);

        public static UniTask Delay(TimeSpan delayTimeSpan, bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => Delay(delayTimeSpan, ignoreTimeScale ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime,
                delayTiming, cancellationToken, cancelImmediately);

        public static UniTask Delay(int millisecondsDelay, DelayType delayType,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => Delay(TimeSpan.FromMilliseconds(millisecondsDelay), delayType, delayTiming, cancellationToken, cancelImmediately);

        /// <summary>
        /// The delay clock starts at the CALL (as upstream), not at the first await — so a delay
        /// passed to <see cref="WhenAll(UniTask[])"/> or <see cref="WhenAny(UniTask[])"/> runs in
        /// parallel with its siblings. The timing parameter is accepted; delays are checked in
        /// the engine scheduler phase each frame.
        /// </summary>
        public static UniTask Delay(TimeSpan delayTimeSpan, DelayType delayType,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            if (delayTimeSpan < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delayTimeSpan), "Delay must be non-negative.");
            if (cancellationToken.IsCancellationRequested) return FromCanceled(cancellationToken);
            return DelayCore(delayTimeSpan, delayType, cancellationToken);
        }

        // ── Predicate waits ──────────────────────────────────────────────

        /// <summary>Completes on the first check where <paramref name="predicate"/> is true (checked now, then once per frame).</summary>
        public static UniTask WaitUntil(Func<bool> predicate, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (cancellationToken.IsCancellationRequested) return FromCanceled(cancellationToken);
            return WaitUntilCore(predicate, cancellationToken);
        }

        public static UniTask WaitWhile(Func<bool> predicate, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return WaitUntil(() => !predicate(), timing, cancellationToken, cancelImmediately);
        }

        /// <summary>Completes (successfully) when <paramref name="cancellationToken"/> is canceled.</summary>
        public static UniTask WaitUntilCanceled(CancellationToken cancellationToken, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool completeImmediately = false)
        {
            if (cancellationToken.IsCancellationRequested) return CompletedTask;
            var source = new UniTaskSource();
            if (cancellationToken.CanBeCanceled)
                cancellationToken.Register(static s => ((UniTaskSource)s).TrySetResult(), source);
            return new UniTask(source);
        }

        // ── Thread hops ──────────────────────────────────────────────────

        /// <summary>Resume on the game-loop thread (completes synchronously when already on it).</summary>
        public static SwitchToMainThreadAwaitable SwitchToMainThread(CancellationToken cancellationToken = default) => new(cancellationToken);

        public static SwitchToMainThreadAwaitable SwitchToMainThread(PlayerLoopTiming timing, CancellationToken cancellationToken = default)
            => new(cancellationToken);

        public static SwitchToThreadPoolAwaitable SwitchToThreadPool() => default;

        public static UniTask RunOnThreadPool(System.Action action, bool configureAwait = true, CancellationToken cancellationToken = default)
            => UniTaskInterop.FromTask(Task.Run(action, cancellationToken), configureAwait);

        public static UniTask RunOnThreadPool(Func<UniTask> action, bool configureAwait = true, CancellationToken cancellationToken = default)
            => UniTaskInterop.FromTask(Task.Run(() => action().AsTask(), cancellationToken), configureAwait);

        public static UniTask<T> RunOnThreadPool<T>(Func<T> func, bool configureAwait = true, CancellationToken cancellationToken = default)
            => UniTaskInterop.FromTask(Task.Run(func, cancellationToken), configureAwait);

        public static UniTask<T> RunOnThreadPool<T>(Func<UniTask<T>> func, bool configureAwait = true, CancellationToken cancellationToken = default)
            => UniTaskInterop.FromTask(Task.Run(() => func().AsTask(), cancellationToken), configureAwait);

        // ── Cores ────────────────────────────────────────────────────────

        static bool IsEndOfFrame(PlayerLoopTiming t) => t is PlayerLoopTiming.PostLateUpdate or PlayerLoopTiming.LastPostLateUpdate;
        static bool IsPreLate(PlayerLoopTiming t) => t is PlayerLoopTiming.PreLateUpdate or PlayerLoopTiming.LastPreLateUpdate;

        internal static async UniTask YieldCore(PlayerLoopTiming timing, CancellationToken cancellationToken)
        {
            if (IsEndOfFrame(timing))
            {
                await GameTask.WaitForEndOfFrame(cancellationToken);
                return;
            }
            if (IsPreLate(timing))
            {
                await GameTask.Yield(cancellationToken);
                return;
            }

            // Update-class timings resume on a LATER frame. The engine scheduler runs after Update
            // in the same frame, so a yield issued from Update would otherwise resume before the
            // next frame — gate on the frame counter (a re-enqueue from inside the scheduler phase
            // always lands on the next frame, so this never spins).
            int frame = EngineTime.frameCount;
            do { await GameTask.Yield(cancellationToken); }
            while (EngineTime.frameCount <= frame);
        }

        static async UniTask DelayFrameCore(int frames, PlayerLoopTiming timing, CancellationToken cancellationToken)
        {
            if (frames == 0)
            {
                await GameTask.Yield(cancellationToken);
                return;
            }
            for (int i = 0; i < frames; i++)
                await YieldCore(timing, cancellationToken);
        }

        static async UniTask DelayCore(TimeSpan span, DelayType type, CancellationToken cancellationToken)
        {
            if (type == DelayType.Realtime)
            {
                var stopwatch = Stopwatch.StartNew();
                do { await GameTask.Yield(cancellationToken); }
                while (stopwatch.Elapsed < span);
                return;
            }
            await GameTask.Delay((float)span.TotalSeconds, type == DelayType.UnscaledDeltaTime, cancellationToken);
        }

        static async UniTask WaitUntilCore(Func<bool> predicate, CancellationToken cancellationToken)
            => await GameTask.WaitUntil(predicate, cancellationToken);
    }
}
