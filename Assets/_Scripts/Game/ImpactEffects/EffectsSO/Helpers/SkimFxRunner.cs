using System.Threading;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    // ------------------------------------------------------------
    // Small internal helper: spawns & updates skim FX, then cleans up.
    // Lifetime is scaled by vessel speed: progress += speed * deltaTime
    // so total duration ~= particleDurationAtSpeedOne / speed.
    // ------------------------------------------------------------
    internal static class SkimFxRunner
    {
        public static async UniTaskVoid RunAsync(
            IVesselStatus vesselStatus,
            Prism prism,
            float particleDurationAtSpeedOne)
        {
            if (vesselStatus == null || !prism)
                return;

            var shipTransform = vesselStatus.ShipTransform;
            if (!shipTransform)
                return;

            // Auto-cancel when prism is destroyed
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                prism.GetCancellationTokenOnDestroy());

            var token = linkedCts.Token;

            var particle = Object.Instantiate(prism.ParticleEffect, prism.transform, true);
            try
            {
                float progress = 0f;

                while (!token.IsCancellationRequested)
                {
                    // 🔑 Explicit null-check cancellation
                    if (shipTransform == null || prism == null)
                    {
                        linkedCts.Cancel(); // cancel everything
                        break;
                    }

                    float speed = Mathf.Max(0f, vesselStatus.Speed);

                    if (speed <= 0.0001f)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    Vector3 distance = prism.transform.position - shipTransform.position;
                    particle.transform.localScale = new Vector3(1f, 1f, distance.magnitude);
                    particle.transform.SetPositionAndRotation(
                        shipTransform.position,
                        Quaternion.LookRotation(distance, prism.transform.up));

                    progress += speed * Time.deltaTime;
                    if (progress >= particleDurationAtSpeedOne)
                        break;

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                if (particle) Object.Destroy(particle);
                linkedCts.Dispose();
            }
        }
    }

}