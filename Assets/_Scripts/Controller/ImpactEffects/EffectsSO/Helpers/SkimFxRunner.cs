using System.Threading;
using CosmicShore.Gameplay;
using Cysharp.Threading.Tasks;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{
    // ------------------------------------------------------------
    // Small internal helper: spawns & updates skim FX, then cleans up.
    // Lifetime is scaled by vessel speed: progress += speed * deltaTime
    // so total duration ~= particleDurationAtSpeedOne / speed.
    // ------------------------------------------------------------
    internal static class SkimFxRunner
    {
        // Prefab names already reported, so a dense trail of unauthored prisms logs once, not once
        // per contact. Names (not instances) - the pool recycles the objects.
        static readonly System.Collections.Generic.HashSet<string> _warnedMissingFx = new();

        static void WarnMissingSkimFxOnce(Prism prism)
        {
            string key = prism.name;
            if (!_warnedMissingFx.Add(key)) return;
            Debug.LogWarning(
                $"[SkimFxRunner] Prism prefab '{key}' has no ParticleEffect assigned - skimming it " +
                "produces no beam. Assign one on the prism prefab to give this mass skim feedback.",
                prism);
        }

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

            // Not every prism prefab authors a skim beam - MenuTrailBlock Variant, FloraBlock and
            // ShieldedHealthBlock all leave ParticleEffect empty. Instantiate(null) THROWS, and this
            // runs once per prism entering the skimmer, so skimming that mass turned into an
            // exception per contact with no visual either way. Name the prefab once and draw
            // nothing rather than failing silently in a swallowed UniTaskVoid.
            if (!prism.ParticleEffect)
            {
                WarnMissingSkimFxOnce(prism);
                return;
            }

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
                    if (SafeLookRotation.TryGet(distance, prism.transform.up, out var rotation, prism, logError: false))
                        particle.transform.SetPositionAndRotation(shipTransform.position, rotation);
                    else
                        particle.transform.position = shipTransform.position;

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