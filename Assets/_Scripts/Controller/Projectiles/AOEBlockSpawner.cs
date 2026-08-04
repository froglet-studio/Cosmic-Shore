using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
    public class AOEBlockSpawner : AOEBlockCreation
    {
        [SerializeField] private SpawnableBase spawnable;
        [SerializeField] int repetitions = 1;
        [SerializeField] float delayBetweenSpawns = 0.3f;

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                // AOEExplosion.Initialize() zeroes localScale to hide the explosion
                // mesh during its delay. This subclass doesn't use the base explosion
                // animation, so restore scale before spawning children.
                transform.localScale = Vector3.one;

                for (int i = 0; i < repetitions; i++)
                {
                    var position = Vessel.Transform.position;
                    var rotation = Quaternion.LookRotation(Vessel.VesselStatus.Course, Vessel.Transform.up);
                    var team = Vessel.VesselStatus.Domain;

                    // No intensity: the ring geometry is speed-independent by design (the old
                    // speed-as-intensity pass tilted the ring closed at low speed).
                    spawnable.Spawn(position, rotation, team);

                    await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenSpawns), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
                }

                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                // The rings are pooled prisms parented under the Boost pool, so this spawner shell
                // has nothing left to own once spawning finishes - destroy it instead of leaking an
                // empty AOE object per hit (same lifecycle as AOEDangerHemisphereBlocks).
                if (!ct.IsCancellationRequested && this != null)
                    Destroy(gameObject);
            }
        }
    }
}
