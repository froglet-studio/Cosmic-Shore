using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Reliable Unity-main-thread dispatcher.
    ///
    /// UniTask's own primitives (UniTask.SwitchToMainThread,
    /// UniTask.Yield(PlayerLoopTiming.Update)) are unreliable on the UniTask
    /// version this project ships (com.cysharp.unitask@86b6e6a2e286) because
    /// UniTask intentionally bypasses SynchronizationContext / ExecutionContext
    /// as a documented design choice (Cysharp/UniTask#319, #561, #151).
    /// A continuation scheduled with Yield(Update) is not guaranteed to resume
    /// on Unity's main thread.
    ///
    /// Unity's own UnitySynchronizationContext, captured once at
    /// BeforeSceneLoad on the main thread, marshals correctly. This dispatcher
    /// wraps Post(...) onto that context as an awaitable UniTask so any caller
    /// can do `await MainThreadDispatcher.SwitchToMainThreadAsync()` and be
    /// confident the continuation runs on the main thread.
    /// </summary>
    public static class MainThreadDispatcher
    {
        static SynchronizationContext _mainContext;
        static int _mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            // BeforeSceneLoad runs on Unity's main thread before any scene loads,
            // so SynchronizationContext.Current is the UnitySynchronizationContext
            // and Thread.CurrentThread is the main thread.
            _mainContext  = SynchronizationContext.Current;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsOnMainThread =>
            Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// Await-able switch to Unity's main thread. Returns a completed UniTask
        /// if we're already on the main thread; otherwise posts onto Unity's
        /// captured SynchronizationContext and resumes the awaiter from the
        /// post callback, which is guaranteed to run on the main thread.
        /// </summary>
        public static UniTask SwitchToMainThreadAsync()
        {
            if (IsOnMainThread) return UniTask.CompletedTask;

            var tcs = new UniTaskCompletionSource();
            _mainContext.Post(_ => tcs.TrySetResult(), null);
            return tcs.Task;
        }
    }
}
