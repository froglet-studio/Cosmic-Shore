using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;

namespace Cysharp.Threading.Tasks
{
    /// <summary>
    /// Original contract: Cysharp's allocation-light awaitable. A <c>default</c> UniTask is an
    /// already-completed one; otherwise it wraps a <see cref="UniTaskSource"/> whose continuations
    /// run synchronously when it completes (never via a SynchronizationContext hop). Resumption on
    /// the game loop comes from the awaitables the factories build on (the engine's scheduler).
    /// </summary>
    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder))]
    public readonly partial struct UniTask
    {
        internal readonly UniTaskSource source;

        internal UniTask(UniTaskSource source) { this.source = source; }

        public UniTaskStatus Status => source?.Status ?? UniTaskStatus.Succeeded;

        public Awaiter GetAwaiter() => new(this);

        /// <summary>Completes with <c>true</c> instead of throwing when this task is canceled.</summary>
        public UniTask<bool> SuppressCancellationThrow()
        {
            var status = Status;
            if (status == UniTaskStatus.Succeeded) return UniTask.FromResult(false);
            if (status == UniTaskStatus.Canceled) return UniTask.FromResult(true);
            return SuppressCore(this);

            static async UniTask<bool> SuppressCore(UniTask task)
            {
                try { await task; return false; }
                catch (OperationCanceledException) { return true; }
            }
        }

        /// <summary>Upstream memoizes a single-await source; shim sources are already re-awaitable.</summary>
        public UniTask Preserve() => this;

        public UniTask<AsyncUnit> AsAsyncUnitUniTask()
        {
            return Core(this);
            static async UniTask<AsyncUnit> Core(UniTask t) { await t; return AsyncUnit.Default; }
        }

        public override string ToString() => source == null ? "()" : $"({Status})";

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            readonly UniTask _task;

            public Awaiter(in UniTask task) { _task = task; }

            public bool IsCompleted => _task.source == null || _task.source.IsCompleted;

            public void GetResult() => _task.source?.ThrowIfNotSucceeded();

            public void OnCompleted(System.Action continuation)
            {
                if (_task.source == null) continuation();
                else _task.source.OnCompleted(continuation);
            }

            public void UnsafeOnCompleted(System.Action continuation) => OnCompleted(continuation);

            /// <summary>Upstream's non-allocating continuation hook.</summary>
            public void SourceOnCompleted(System.Action<object> continuation, object state)
                => OnCompleted(() => continuation(state));
        }
    }

    /// <summary>Original contract: a UniTask producing a value. <c>default</c> is completed with <c>default(T)</c>.</summary>
    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder<>))]
    public readonly struct UniTask<T>
    {
        internal readonly UniTaskSource<T> source;
        readonly T _result;

        public UniTask(T result)
        {
            source = null;
            _result = result;
        }

        internal UniTask(UniTaskSource<T> source)
        {
            this.source = source;
            _result = default;
        }

        public UniTaskStatus Status => source?.Status ?? UniTaskStatus.Succeeded;

        public Awaiter GetAwaiter() => new(this);

        /// <summary>Completes with <c>(true, default)</c> instead of throwing when canceled.</summary>
        public UniTask<(bool IsCanceled, T Result)> SuppressCancellationThrow()
        {
            if (source == null) return UniTask.FromResult((false, _result));
            return Core(this);

            static async UniTask<(bool, T)> Core(UniTask<T> task)
            {
                try { return (false, await task); }
                catch (OperationCanceledException) { return (true, default); }
            }
        }

        public UniTask<T> Preserve() => this;

        public static implicit operator UniTask(UniTask<T> self)
        {
            if (self.source == null) return UniTask.CompletedTask;
            return new UniTask(self.source);
        }

        public override string ToString() => source == null ? $"({_result})" : $"({Status})";

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            readonly UniTask<T> _task;

            public Awaiter(in UniTask<T> task) { _task = task; }

            public bool IsCompleted => _task.source == null || _task.source.IsCompleted;

            public T GetResult() => _task.source == null ? _task._result : _task.source.GetResult();

            public void OnCompleted(System.Action continuation)
            {
                if (_task.source == null) continuation();
                else _task.source.OnCompleted(continuation);
            }

            public void UnsafeOnCompleted(System.Action continuation) => OnCompleted(continuation);

            public void SourceOnCompleted(System.Action<object> continuation, object state)
                => OnCompleted(() => continuation(state));
        }
    }

    /// <summary>
    /// Original contract: the fire-and-forget async return type. Exceptions escaping an
    /// <c>async UniTaskVoid</c> method are published to <see cref="UniTaskScheduler"/> (logged
    /// via the engine's <c>Debug.LogException</c> by default); cancellation is ignored.
    /// </summary>
    [AsyncMethodBuilder(typeof(AsyncUniTaskVoidMethodBuilder))]
    public readonly struct UniTaskVoid
    {
        /// <summary>No-op: an <c>async UniTaskVoid</c> method is already running when it returns.</summary>
        public void Forget() { }
    }
}
