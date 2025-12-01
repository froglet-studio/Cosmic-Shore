using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Utility.ClassExtensions
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
    }
}