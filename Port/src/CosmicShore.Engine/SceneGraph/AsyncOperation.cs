using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine
{
    /// <summary>
    /// An in-flight engine operation (UnityEngine.AsyncOperation): progress, isDone, the
    /// <see cref="completed"/> callback, and <see cref="allowSceneActivation"/> for scene
    /// loads (held at progress 0.9 until activation is allowed, as in the original).
    /// Awaitable directly so code written against the port's former Task-returning loads
    /// keeps working; UniTask's <c>ToUniTask</c> wraps <see cref="AsTask"/>.
    /// </summary>
    public class AsyncOperation
    {
        readonly TaskCompletionSource<bool> _tcs = new();
        Action<AsyncOperation> _completed;
        bool _allowActivation = true;

        public bool isDone { get; private set; }
        public float progress { get; internal set; }
        public int priority { get; set; }

        public bool allowSceneActivation
        {
            get => _allowActivation;
            set { _allowActivation = value; if (value) ActivationAllowed?.Invoke(); }
        }

        internal event Action ActivationAllowed;

        /// <summary>Raised once when the operation finishes; subscribing after completion invokes immediately (original contract).</summary>
        public event Action<AsyncOperation> completed
        {
            add { if (isDone) value?.Invoke(this); else _completed += value; }
            remove => _completed -= value;
        }

        internal void Complete()
        {
            if (isDone) return;
            progress = 1f;
            isDone = true;
            var c = _completed;
            _completed = null;
            try { c?.Invoke(this); }
            catch (Exception e) { Debug.LogException(e); }
            _tcs.TrySetResult(true);
        }

        internal void Fail(Exception e)
        {
            if (isDone) return;
            isDone = true;
            _tcs.TrySetException(e);
        }

        public Task AsTask() => _tcs.Task;
        /// <summary>
        /// Completes on the thread that completed the operation (the game loop), never via a
        /// thread-pool hop — an extra hop lets the awaiting continuation miss a frame pump.
        /// </summary>
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || _tcs.Task.IsCompleted) return _tcs.Task;
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
            var relay = new TaskCompletionSource<bool>();
            var reg = cancellationToken.Register(() => relay.TrySetCanceled(cancellationToken));
            _tcs.Task.ContinueWith(t =>
            {
                reg.Dispose();
                if (t.IsFaulted) relay.TrySetException(t.Exception.InnerExceptions);
                else relay.TrySetResult(true);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return relay.Task;
        }
        public TaskAwaiter<bool> GetAwaiter() => _tcs.Task.GetAwaiter();

        public static AsyncOperation Completed() { var op = new AsyncOperation(); op.Complete(); return op; }
    }

    /// <summary>An asset load in flight (UnityEngine.ResourceRequest).</summary>
    public class ResourceRequest : AsyncOperation
    {
        public Object asset { get; internal set; }
    }
}
