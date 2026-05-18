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
        // AsMainThread — UGS / Netcode await boundary helper
        // ─────────────────────────────────────────────────────────────────────
        //
        // UGS-SDK and Netcode Tasks complete on the .NET ThreadPool, so code
        // after `await someUgsTask` runs on ThreadPool. From there:
        //   • Any UnityEngine.Object access (incl. `== null` → op_Equality
        //     → GetInstanceID → EnsureRunningOnMainThread) throws.
        //   • Any Obvious.Soap ScriptableEvent.Raise() invokes its listeners
        //     inline on ThreadPool, and any listener touching Unity state crashes.
        //
        // UniTask.SwitchToMainThread() is unreliable on the UniTask version this
        // project ships (com.cysharp.unitask@86b6e6a2e286): its awaiter reports
        // IsCompleted=true from ThreadPool, so the continuation runs inline and
        // the switch is a no-op. UniTask.Yield(PlayerLoopTiming.Update) has
        // IsCompleted=false unconditionally and reliably schedules onto Unity's
        // PlayerLoop (main thread), so we wrap the await in that primitive.
        //
        // Usage: `await someTask.AsMainThread();` — encodes "this is a
        // cross-thread call, resume on main thread" at the call boundary so
        // callers don't have to remember a separate Yield/Switch line.

        public static async UniTask AsMainThread(this Task task)
        {
            await task;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        public static async UniTask<T> AsMainThread<T>(this Task<T> task)
        {
            var result = await task;
            await UniTask.Yield(PlayerLoopTiming.Update);
            return result;
        }

        public static async UniTask AsMainThread(this UniTask task)
        {
            await task;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        public static async UniTask<T> AsMainThread<T>(this UniTask<T> task)
        {
            var result = await task;
            await UniTask.Yield(PlayerLoopTiming.Update);
            return result;
        }
    }
}
