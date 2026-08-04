using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Utility
{
    public static class UniTaskExtensions
    {
        public static async UniTask WaitOneFrame(this MonoBehaviour mono, Action onComplete)
        {
            await UniTask.Yield();
            onComplete?.Invoke();
        }

        public static async UniTask WaitOneFrame(this MonoBehaviour mono, CancellationToken ct, Action onComplete)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            onComplete?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────────
        // AsMainThread - UGS / Netcode await boundary helper
        // ─────────────────────────────────────────────────────────────────────
        //
        // UGS-SDK and Netcode Tasks complete on the .NET ThreadPool, so code
        // after `await someUgsTask` runs on ThreadPool. From there:
        //   • Any UnityEngine.Object access (incl. `== null` → op_Equality
        //     → GetInstanceID → EnsureRunningOnMainThread) throws.
        //   • Any Obvious.Soap ScriptableEvent.Raise() invokes its listeners
        //     inline on ThreadPool, and any listener touching Unity state crashes.
        //
        // UniTask's own primitives are unreliable on this UniTask version
        // (com.cysharp.unitask@86b6e6a2e286):
        //   • SwitchToMainThread() - awaiter's IsCompleted returns true from
        //     ThreadPool → continuation runs inline on ThreadPool.
        //   • Yield(PlayerLoopTiming.Update) - yields, but the resumption is not
        //     guaranteed on the main thread because UniTask intentionally bypasses
        //     SynchronizationContext (Cysharp/UniTask#319, #561, #151).
        //
        // We marshal through Unity's own SynchronizationContext via
        // MainThreadDispatcher, which is properly main-thread-bound.
        //
        // Usage: `await someTask.AsMainThread();` - encodes "this is a
        // cross-thread call, resume on main thread" at the call boundary so
        // callers don't have to remember a separate Yield/Switch line.

        public static async UniTask AsMainThread(this Task task)
        {
            await task;
            await MainThreadDispatcher.SwitchToMainThreadAsync();
        }

        public static async UniTask<T> AsMainThread<T>(this Task<T> task)
        {
            var result = await task;
            await MainThreadDispatcher.SwitchToMainThreadAsync();
            return result;
        }

        public static async UniTask AsMainThread(this UniTask task)
        {
            await task;
            await MainThreadDispatcher.SwitchToMainThreadAsync();
        }

        public static async UniTask<T> AsMainThread<T>(this UniTask<T> task)
        {
            var result = await task;
            await MainThreadDispatcher.SwitchToMainThreadAsync();
            return result;
        }
    }
}
