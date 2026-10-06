using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GameTask = CosmicShore.Engine.Tasks.GameTask;

namespace Cysharp.Threading.Tasks
{
    /// <summary>Original contract: the awaitable returned by <c>UniTask.Yield()</c> (no cancellation).</summary>
    public readonly struct YieldAwaitable
    {
        readonly PlayerLoopTiming _timing;
        public YieldAwaitable(PlayerLoopTiming timing) { _timing = timing; }
        public UniTask.Awaiter GetAwaiter() => UniTask.YieldCore(_timing, default).GetAwaiter();
        public UniTask ToUniTask() => UniTask.YieldCore(_timing, default);
    }

    /// <summary>Resumes on the game-loop thread (inline when already there).</summary>
    public readonly struct SwitchToMainThreadAwaitable
    {
        readonly CancellationToken _ct;
        public SwitchToMainThreadAwaitable(CancellationToken cancellationToken) { _ct = cancellationToken; }
        public Awaiter GetAwaiter() => new(_ct);

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            readonly CancellationToken _ct;
            public Awaiter(CancellationToken ct) { _ct = ct; }
            public bool IsCompleted => GameTask.SwitchToMainThread().GetAwaiter().IsCompleted;
            public void GetResult() => _ct.ThrowIfCancellationRequested();
            public void OnCompleted(Action continuation) => GameTask.SwitchToMainThread().GetAwaiter().OnCompleted(continuation);
            public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
        }
    }

    /// <summary>Resumes on a thread-pool thread.</summary>
    public readonly struct SwitchToThreadPoolAwaitable
    {
        public Awaiter GetAwaiter() => default;

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            public bool IsCompleted => false;
            public void GetResult() { }
            public void OnCompleted(Action continuation) => ThreadPool.QueueUserWorkItem(static c => ((Action)c)(), continuation);
            public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
        }
    }

    /// <summary>Original contract: a manually completed UniTask (reusable result holder).</summary>
    public class UniTaskCompletionSource
    {
        readonly UniTaskSource _source = new();
        public UniTask Task => new(_source);
        public bool TrySetResult() => _source.TrySetResult();
        public bool TrySetException(Exception exception) => _source.TrySetException(exception);
        public bool TrySetCanceled(CancellationToken cancellationToken = default) => _source.TrySetCanceled(cancellationToken);
        public UniTaskStatus UnsafeGetStatus() => _source.Status;
    }

    public class UniTaskCompletionSource<T>
    {
        readonly UniTaskSource<T> _source = new();
        public UniTask<T> Task => new(_source);
        public bool TrySetResult(T result) => _source.TrySetResult(result);
        public bool TrySetException(Exception exception) => _source.TrySetException(exception);
        public bool TrySetCanceled(CancellationToken cancellationToken = default) => _source.TrySetCanceled(cancellationToken);
        public UniTaskStatus UnsafeGetStatus() => _source.Status;
    }

    public readonly partial struct UniTask
    {
        // ── combinators ─────────────────────────────────────────────────────

        public static UniTask WhenAll(params UniTask[] tasks) => WhenAll((IEnumerable<UniTask>)tasks);

        public static async UniTask WhenAll(IEnumerable<UniTask> tasks)
        {
            var list = new List<UniTask>(tasks);
            Exception first = null;
            foreach (var t in list)
            {
                try { await t; }
                catch (Exception e) { first ??= e; }
            }
            if (first != null) throw first;
        }

        public static UniTask<T[]> WhenAll<T>(params UniTask<T>[] tasks) => WhenAll((IEnumerable<UniTask<T>>)tasks);

        public static async UniTask<T[]> WhenAll<T>(IEnumerable<UniTask<T>> tasks)
        {
            var list = new List<UniTask<T>>(tasks);
            var results = new T[list.Count];
            Exception first = null;
            for (int i = 0; i < list.Count; i++)
            {
                try { results[i] = await list[i]; }
                catch (Exception e) { first ??= e; }
            }
            if (first != null) throw first;
            return results;
        }

        public static async UniTask<(T1, T2)> WhenAll<T1, T2>(UniTask<T1> task1, UniTask<T2> task2)
        {
            var r1 = await task1; var r2 = await task2;
            return (r1, r2);
        }

        public static async UniTask<(T1, T2, T3)> WhenAll<T1, T2, T3>(UniTask<T1> task1, UniTask<T2> task2, UniTask<T3> task3)
        {
            var r1 = await task1; var r2 = await task2; var r3 = await task3;
            return (r1, r2, r3);
        }

        /// <summary>Completes with the index of the first task to finish.</summary>
        public static UniTask<int> WhenAny(params UniTask[] tasks) => WhenAny((IEnumerable<UniTask>)tasks);

        public static UniTask<int> WhenAny(IEnumerable<UniTask> tasks)
        {
            var source = new UniTaskSource<int>();
            int i = 0;
            foreach (var t in tasks)
            {
                int index = i++;
                var awaiter = t.GetAwaiter();
                void Done()
                {
                    try { awaiter.GetResult(); source.TrySetResult(index); }
                    catch (Exception e) { source.TrySetException(e); }
                }
                if (awaiter.IsCompleted) { Done(); break; }
                awaiter.OnCompleted(Done);
            }
            return new UniTask<int>(source);
        }

        public static UniTask<(int winArgumentIndex, T result)> WhenAny<T>(params UniTask<T>[] tasks)
        {
            var source = new UniTaskSource<(int, T)>();
            for (int i = 0; i < tasks.Length; i++)
            {
                int index = i;
                var awaiter = tasks[i].GetAwaiter();
                void Done()
                {
                    try { source.TrySetResult((index, awaiter.GetResult())); }
                    catch (Exception e) { source.TrySetException(e); }
                }
                if (awaiter.IsCompleted) { Done(); break; }
                awaiter.OnCompleted(Done);
            }
            return new UniTask<(int, T)>(source);
        }

        public static async UniTask<(bool hasResultLeft, T result)> WhenAny<T>(UniTask<T> leftTask, UniTask rightTask)
        {
            var winner = await WhenAny(leftTask.AsUniTask(), rightTask);
            return winner == 0 ? (true, await leftTask) : (false, default);
        }
    }

    /// <summary>Original contract: UniTaskExtensions.</summary>
    public static class UniTaskExtensions
    {
        /// <summary>Fire and forget; faults go to <see cref="UniTaskScheduler"/> (logged, never swallowed).</summary>
        public static void Forget(this UniTask task)
        {
            var awaiter = task.GetAwaiter();
            if (awaiter.IsCompleted) { Observe(awaiter); return; }
            awaiter.OnCompleted(() => Observe(awaiter));

            static void Observe(UniTask.Awaiter a)
            {
                try { a.GetResult(); }
                catch (Exception e) { UniTaskScheduler.PublishUnobservedTaskException(e); }
            }
        }

        public static void Forget<T>(this UniTask<T> task) => task.AsUniTask().Forget();

        public static void Forget(this UniTask task, Action<Exception> exceptionHandler, bool handleExceptionOnMainThread = true)
        {
            var awaiter = task.GetAwaiter();
            void Observe()
            {
                try { awaiter.GetResult(); }
                catch (Exception e) { if (exceptionHandler != null) exceptionHandler(e); else UniTaskScheduler.PublishUnobservedTaskException(e); }
            }
            if (awaiter.IsCompleted) Observe(); else awaiter.OnCompleted(Observe);
        }

        public static UniTask AsUniTask<T>(this UniTask<T> task) => task;

        public static async UniTask<AsyncUnit> AsAsyncUnitUniTask(this UniTask task) { await task; return AsyncUnit.Default; }

        public static UniTask AsUniTask(this Task task, bool useCurrentSynchronizationContext = true) => UniTaskInterop.FromTask(task, useCurrentSynchronizationContext);
        public static UniTask<T> AsUniTask<T>(this Task<T> task, bool useCurrentSynchronizationContext = true) => UniTaskInterop.FromTask(task, useCurrentSynchronizationContext);
        public static UniTask AsUniTask(this ValueTask task) => UniTaskInterop.FromTask(task.AsTask(), true);
        public static UniTask<T> AsUniTask<T>(this ValueTask<T> task) => UniTaskInterop.FromTask(task.AsTask(), true);

        public static UniTask ToUniTask(this Task task, bool useCurrentSynchronizationContext = true) => UniTaskInterop.FromTask(task, useCurrentSynchronizationContext);
        public static UniTask<T> ToUniTask<T>(this Task<T> task, bool useCurrentSynchronizationContext = true) => UniTaskInterop.FromTask(task, useCurrentSynchronizationContext);

        /// <summary>Awaits an engine operation (scene load, instantiate, asset load). Cancelling abandons the wait, not the operation (original contract).</summary>
        public static UniTask ToUniTask(this CosmicShore.Engine.AsyncOperation asyncOperation, IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default, bool cancelImmediately = false)
            => UniTaskInterop.FromTask(asyncOperation.WaitAsync(cancellationToken), true);

        /// <summary>Awaits a web request; a non-success result throws <see cref="CosmicShore.Engine.Networking.UnityWebRequestException"/> (original contract).</summary>
        public static async UniTask<CosmicShore.Engine.Networking.UnityWebRequest> ToUniTask(this CosmicShore.Engine.Networking.UnityWebRequestAsyncOperation asyncOperation,
            IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            await UniTaskInterop.FromTask(asyncOperation.WaitAsync(cancellationToken), true);
            var request = asyncOperation.webRequest;
            if (request.result != CosmicShore.Engine.Networking.UnityWebRequest.Result.Success)
                throw new CosmicShore.Engine.Networking.UnityWebRequestException(request);
            return request;
        }

        public static async Task AsTask(this UniTask task) => await task;
        public static async Task<T> AsTask<T>(this UniTask<T> task) => await task;

        public static async ValueTask AsValueTask(this UniTask task) => await task;
        public static async ValueTask<T> AsValueTask<T>(this UniTask<T> task) => await task;

        /// <summary>Completes (canceled) when <paramref name="cancellationToken"/> fires, otherwise with the task.</summary>
        public static UniTask AttachExternalCancellation(this UniTask task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return task;
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled(cancellationToken);
            var source = new UniTaskSource();
            var reg = cancellationToken.Register(static s => ((UniTaskSource)s).TrySetCanceled(), source);
            var awaiter = task.GetAwaiter();
            void Done()
            {
                reg.Dispose();
                try { awaiter.GetResult(); source.TrySetResult(); }
                catch (OperationCanceledException) { source.TrySetCanceled(); }
                catch (Exception e) { source.TrySetException(e); }
            }
            if (awaiter.IsCompleted) Done(); else awaiter.OnCompleted(Done);
            return new UniTask(source);
        }

        public static UniTask<T> AttachExternalCancellation<T>(this UniTask<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return task;
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled<T>(cancellationToken);
            var source = new UniTaskSource<T>();
            var reg = cancellationToken.Register(static s => ((UniTaskSource<T>)s).TrySetCanceled(), source);
            var awaiter = task.GetAwaiter();
            void Done()
            {
                reg.Dispose();
                try { source.TrySetResult(awaiter.GetResult()); }
                catch (OperationCanceledException) { source.TrySetCanceled(); }
                catch (Exception e) { source.TrySetException(e); }
            }
            if (awaiter.IsCompleted) Done(); else awaiter.OnCompleted(Done);
            return new UniTask<T>(source);
        }

        public static UniTask WithCancellation(this UniTask task, CancellationToken cancellationToken) => task.AttachExternalCancellation(cancellationToken);
        public static UniTask<T> WithCancellation<T>(this UniTask<T> task, CancellationToken cancellationToken) => task.AttachExternalCancellation(cancellationToken);

        /// <summary>Throws <see cref="TimeoutException"/> if the task outlives <paramref name="timeout"/> (scaled game time unless ignoreTimeScale).</summary>
        public static async UniTask Timeout(this UniTask task, TimeSpan timeout, DelayType delayType = DelayType.DeltaTime, PlayerLoopTiming timeoutCheckTiming = PlayerLoopTiming.Update, CancellationTokenSource taskCancellationTokenSource = null)
        {
            using var cts = new CancellationTokenSource();
            int winner = await UniTask.WhenAny(task, UniTask.Delay(timeout, delayType, cancellationToken: cts.Token).SuppressCancellationThrow().AsUniTask());
            if (winner == 1) { taskCancellationTokenSource?.Cancel(); throw new TimeoutException($"Exceed Timeout:{timeout}"); }
            cts.Cancel();
            await task;
        }

        public static async UniTask<T> Timeout<T>(this UniTask<T> task, TimeSpan timeout, DelayType delayType = DelayType.DeltaTime, PlayerLoopTiming timeoutCheckTiming = PlayerLoopTiming.Update, CancellationTokenSource taskCancellationTokenSource = null)
        {
            await ((UniTask)task).Timeout(timeout, delayType, timeoutCheckTiming, taskCancellationTokenSource);
            return await task;
        }

        public static async UniTask ContinueWith(this UniTask task, Action continuationFunction) { await task; continuationFunction(); }
        public static async UniTask ContinueWith<T>(this UniTask<T> task, Action<T> continuationFunction) { continuationFunction(await task); }
        public static async UniTask<TR> ContinueWith<T, TR>(this UniTask<T> task, Func<T, TR> continuationFunction) => continuationFunction(await task);
    }
}
