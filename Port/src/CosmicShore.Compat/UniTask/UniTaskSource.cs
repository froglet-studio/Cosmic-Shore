using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cysharp.Threading.Tasks
{
    /// <summary>
    /// The promise behind every pending <see cref="UniTask"/>. Deliberately NOT a
    /// <see cref="Task"/>: .NET refuses to inline a Task's await continuation whenever a
    /// non-default SynchronizationContext is current (<c>AwaitTaskContinuation.IsValidLocationForInlining</c>),
    /// and the port engine installs its <c>GameSynchronizationContext</c> for the whole of every
    /// frame — so a Task-backed UniTask would bounce every nested <c>await</c> onto the thread pool.
    /// UniTask's contract is the opposite: continuations run synchronously, on the completing
    /// thread, the moment the source completes. This class implements exactly that.
    ///
    /// Unlike upstream, a source may be awaited more than once (every awaiter is kept), which
    /// makes the shim strictly more permissive; no ported code relies on the upstream throw.
    /// </summary>
    internal class UniTaskSource
    {
        const int PendingState = (int)UniTaskStatus.Pending;

        readonly object _gate = new();
        int _status;
        ExceptionDispatchInfo _error;
        Action _continuation;
        List<Action> _moreContinuations;

        public UniTaskStatus Status
        {
            get
            {
                OnTouched();
                return (UniTaskStatus)Volatile.Read(ref _status);
            }
        }

        internal bool IsCompleted => Status != UniTaskStatus.Pending;

        /// <summary>Hook for lazily-started sources (<see cref="UniTask.Defer(Func{UniTask})"/>).</summary>
        protected virtual void OnTouched() { }

        public bool TrySetResult() => TryComplete(UniTaskStatus.Succeeded, null);

        /// <summary>An <see cref="OperationCanceledException"/> completes the source as Canceled (upstream parity).</summary>
        public bool TrySetException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            if (exception is AggregateException { InnerExceptions.Count: 1 } agg) exception = agg.InnerExceptions[0];
            var status = exception is OperationCanceledException ? UniTaskStatus.Canceled : UniTaskStatus.Faulted;
            return TryComplete(status, ExceptionDispatchInfo.Capture(exception));
        }

        public bool TrySetCanceled(CancellationToken cancellationToken = default)
            => TryComplete(UniTaskStatus.Canceled,
                ExceptionDispatchInfo.Capture(new OperationCanceledException(cancellationToken)));

        protected bool TryComplete(UniTaskStatus status, ExceptionDispatchInfo error, Action commitUnderLock = null)
        {
            Action first;
            List<Action> more;
            lock (_gate)
            {
                if (_status != PendingState) return false;
                commitUnderLock?.Invoke();
                _error = error;
                Volatile.Write(ref _status, (int)status);
                first = _continuation;
                more = _moreContinuations;
                _continuation = null;
                _moreContinuations = null;
            }

            Run(first);
            if (more != null)
                foreach (var c in more) Run(c);
            return true;
        }

        static void Run(Action continuation)
        {
            if (continuation == null) return;
            try { continuation(); }
            catch (Exception e) { CosmicShore.Engine.Debug.LogException(e); }
        }

        /// <summary>Register a continuation. Runs synchronously now if already complete.</summary>
        public void OnCompleted(Action continuation)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
            OnTouched();
            lock (_gate)
            {
                if (_status == PendingState)
                {
                    if (_continuation == null) _continuation = continuation;
                    else (_moreContinuations ??= new List<Action>()).Add(continuation);
                    return;
                }
            }
            continuation();
        }

        /// <summary>Throw the stored fault/cancellation (preserving its stack); throws if still pending.</summary>
        public void ThrowIfNotSucceeded()
        {
            OnTouched();
            int status = Volatile.Read(ref _status);
            if (status == PendingState)
                throw new InvalidOperationException(
                    "UniTask has not completed yet — UniTask never blocks; await it instead of calling GetResult().");
            _error?.Throw();
        }

        /// <summary>The stored exception (null unless Faulted/Canceled) — for observers like Forget.</summary>
        public Exception Exception => _error?.SourceException;
    }

    internal class UniTaskSource<T> : UniTaskSource
    {
        T _result;

        public bool TrySetResult(T result)
            => TryComplete(UniTaskStatus.Succeeded, null, () => _result = result);

        public T GetResult()
        {
            ThrowIfNotSucceeded();
            return _result;
        }
    }

    /// <summary>Boxed async state machine: the continuation every await in a UniTask method resumes.</summary>
    internal sealed class StateMachineBox<TStateMachine> where TStateMachine : System.Runtime.CompilerServices.IAsyncStateMachine
    {
        public TStateMachine StateMachine;
        public readonly Action MoveNextAction;

        public StateMachineBox() { MoveNextAction = MoveNext; }

        void MoveNext() => StateMachine.MoveNext();
    }

    /// <summary>A source that only ever completes by cancellation (<see cref="UniTask.Never(CancellationToken)"/>).</summary>
    internal sealed class NeverSource<T> : UniTaskSource<T>
    {
        public NeverSource(CancellationToken token)
        {
            if (token.CanBeCanceled)
                token.Register(static s => ((NeverSource<T>)s).TrySetCanceled(), this);
        }
    }

    /// <summary>Starts its factory on the first observation (status read, await, or GetResult).</summary>
    internal sealed class DeferSource : UniTaskSource
    {
        Func<UniTask> _factory;

        public DeferSource(Func<UniTask> factory) { _factory = factory ?? throw new ArgumentNullException(nameof(factory)); }

        protected override void OnTouched()
        {
            var factory = Interlocked.Exchange(ref _factory, null);
            if (factory == null) return;
            UniTask inner;
            try { inner = factory(); }
            catch (Exception e) { TrySetException(e); return; }
            UniTaskInterop.Transfer(inner, this);
        }
    }

    internal sealed class DeferSource<T> : UniTaskSource<T>
    {
        Func<UniTask<T>> _factory;

        public DeferSource(Func<UniTask<T>> factory) { _factory = factory ?? throw new ArgumentNullException(nameof(factory)); }

        protected override void OnTouched()
        {
            var factory = Interlocked.Exchange(ref _factory, null);
            if (factory == null) return;
            UniTask<T> inner;
            try { inner = factory(); }
            catch (Exception e) { TrySetException(e); return; }
            UniTaskInterop.Transfer(inner, this);
        }
    }

    /// <summary>Bridges between UniTask sources, <see cref="Task"/>s and each other.</summary>
    internal static class UniTaskInterop
    {
        /// <summary>Forward <paramref name="from"/>'s outcome into <paramref name="to"/> when it completes.</summary>
        public static void Transfer(UniTask from, UniTaskSource to)
        {
            var awaiter = from.GetAwaiter();
            if (awaiter.IsCompleted) { Complete(awaiter, to); return; }
            awaiter.OnCompleted(() => Complete(awaiter, to));
        }

        public static void Transfer<T>(UniTask<T> from, UniTaskSource<T> to)
        {
            var awaiter = from.GetAwaiter();
            if (awaiter.IsCompleted) { Complete(awaiter, to); return; }
            awaiter.OnCompleted(() => Complete(awaiter, to));
        }

        static void Complete(UniTask.Awaiter awaiter, UniTaskSource to)
        {
            try { awaiter.GetResult(); }
            catch (Exception e) { to.TrySetException(e); return; }
            to.TrySetResult();
        }

        static void Complete<T>(UniTask<T>.Awaiter awaiter, UniTaskSource<T> to)
        {
            T result;
            try { result = awaiter.GetResult(); }
            catch (Exception e) { to.TrySetException(e); return; }
            to.TrySetResult(result);
        }

        /// <summary>
        /// Wrap a <see cref="Task"/>. With <paramref name="useCurrentSynchronizationContext"/> (the
        /// upstream default) and a context installed — on the game loop, the engine's — the
        /// completion is posted back to it, so the awaiting code resumes on the loop thread even
        /// when the task finished on the thread pool. Otherwise the completion runs inline on
        /// whichever thread finished the task (upstream <c>useCurrentSynchronizationContext: false</c>).
        /// </summary>
        public static UniTask FromTask(Task task, bool useCurrentSynchronizationContext)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (task.IsCompleted) return FromCompletedTask(task);

            var source = new UniTaskSource();
            var context = useCurrentSynchronizationContext ? SynchronizationContext.Current : null;
            // ContinueWith(ExecuteSynchronously) — unlike an await continuation — inlines on the
            // completing thread even when a custom SynchronizationContext is current.
            task.ContinueWith(static (t, state) =>
            {
                var (src, ctx) = ((UniTaskSource, SynchronizationContext))state;
                if (ctx == null) TransferTask(t, src);
                else ctx.Post(static s => { var (tt, ss) = ((Task, UniTaskSource))s; TransferTask(tt, ss); }, (t, src));
            }, (source, context), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return new UniTask(source);
        }

        public static UniTask<T> FromTask<T>(Task<T> task, bool useCurrentSynchronizationContext)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (task.IsCompleted) return FromCompletedTask(task);

            var source = new UniTaskSource<T>();
            var context = useCurrentSynchronizationContext ? SynchronizationContext.Current : null;
            task.ContinueWith(static (t, state) =>
            {
                var (src, ctx) = ((UniTaskSource<T>, SynchronizationContext))state;
                if (ctx == null) TransferTask(t, src);
                else ctx.Post(static s => { var (tt, ss) = ((Task<T>, UniTaskSource<T>))s; TransferTask(tt, ss); }, (t, src));
            }, (source, context), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return new UniTask<T>(source);
        }

        static UniTask FromCompletedTask(Task task)
        {
            try { task.GetAwaiter().GetResult(); }
            catch (Exception e) { return UniTask.FromException(e); }
            return UniTask.CompletedTask;
        }

        static UniTask<T> FromCompletedTask<T>(Task<T> task)
        {
            try { return UniTask.FromResult(task.GetAwaiter().GetResult()); }
            catch (Exception e) { return UniTask.FromException<T>(e); }
        }

        static void TransferTask(Task task, UniTaskSource to)
        {
            try { task.GetAwaiter().GetResult(); }
            catch (Exception e) { to.TrySetException(e); return; }
            to.TrySetResult();
        }

        static void TransferTask<T>(Task<T> task, UniTaskSource<T> to)
        {
            T result;
            try { result = task.GetAwaiter().GetResult(); }
            catch (Exception e) { to.TrySetException(e); return; }
            to.TrySetResult(result);
        }
    }
}
