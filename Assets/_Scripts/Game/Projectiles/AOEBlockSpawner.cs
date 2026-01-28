using System;
using System.Collections;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    using UnityEngine;
    using Cysharp.Threading.Tasks;
    using System.Threading;

    public class AOEBlockSpawner : AOEBlockCreation
    {
        [SerializeField] private SpawnableAbstractBase spawnable;

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(ExplosionDelay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                var position = Vessel.Transform.position;
                var rotation = Vessel.Transform.rotation;
                var team = Vessel.VesselStatus.Domain;

                var spawned = spawnable.Spawn(
                    position,
                    rotation,
                    team,
                    (int)(Vessel.VesselStatus.ResourceSystem.Resources[0].CurrentAmount * 10)
                );

                spawned.transform.SetParent(transform, worldPositionStays: true);

                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate , ct);
            }
            catch (OperationCanceledException) { }
        }
    }

}