using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace CosmicShore.Game.Projectiles
{
    public class AOEBlockSpawner : AOEBlockCreation
    {
        [SerializeField] private SpawnableAbstractBase spawnable;
        [SerializeField] int repetitions = 1;
        [SerializeField] float delayBetweenSpawns = 0.3f;

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                for (int i = 0; i < repetitions; i++)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenSpawns), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                    var position = Vessel.Transform.position;
                    var rotation = Quaternion.LookRotation(Vessel.VesselStatus.Course, Vessel.Transform.up);
                    var team = Vessel.VesselStatus.Domain;

                    spawnable.Spawn(position, rotation, team, (int)Vessel.VesselStatus.Speed);
                }

                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            }
            catch (OperationCanceledException) { }
        }
    }
}