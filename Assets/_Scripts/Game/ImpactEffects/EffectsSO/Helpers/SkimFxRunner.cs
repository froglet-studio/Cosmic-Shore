using System.Threading;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    // ------------------------------------------------------------
    // Small internal helper: spawns & updates skim FX, then cleans up.
    // Lifetime is scaled by ship speed: progress += speed * deltaTime
    // so total duration ~= particleDurationAtSpeedOne / speed.
    // ------------------------------------------------------------
    internal static class SkimFxRunner
    {
        public static async UniTaskVoid RunAsync(
            IShipStatus shipStatus,
            TrailBlock trailBlock,
            float particleDurationAtSpeedOne)
        {
            if (shipStatus?.ShipTransform == null || trailBlock == null)
                return;

            // Auto-cancel when the trailBlock is destroyed.
            CancellationToken token = trailBlock.GetCancellationTokenOnDestroy();

            var particle = Object.Instantiate(trailBlock.ParticleEffect, trailBlock.transform, true);
            try
            {
                float progress = 0f; // accumulates speed * dt

                while (!token.IsCancellationRequested)
                {
                    float speed = Mathf.Max(0f, shipStatus.Speed);

                    // If speed is ~0, just wait a frame and try again
                    if (speed <= 0.0001f)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    // Position & orient the particle as a tube from ship to block
                    Vector3 distance = trailBlock.transform.position - shipStatus.ShipTransform.position;
                    particle.transform.localScale = new Vector3(1f, 1f, distance.magnitude);
                    particle.transform.SetPositionAndRotation(
                        shipStatus.ShipTransform.position,
                        Quaternion.LookRotation(distance, trailBlock.transform.up));

                    // Advance lifetime with speed scaling
                    progress += speed * Time.deltaTime;
                    if (progress >= particleDurationAtSpeedOne)
                        break;

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                if (particle) Object.Destroy(particle);
            }
        }
    }
}