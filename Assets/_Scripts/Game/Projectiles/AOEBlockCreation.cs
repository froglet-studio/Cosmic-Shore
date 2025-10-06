using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEBlockCreation : AOEExplosion
    {
        [Header("Block Creation")]
        [SerializeField] protected Vector3 blockScale = new Vector3(20f, 10f, 5f);
        [SerializeField] protected bool shielded = true;

        [Header("Events")]
        [SerializeField] private PrismEventChannelWithReturnSO _prismSpawnEvent;

        [Header("Block Parameters")]
        [SerializeField] protected float blockCount = 8f; // TODO: int
        [SerializeField] protected int ringCount = 3;
        [SerializeField] protected float radius = 30f;

        protected readonly List<Trail> trails = new();
        protected CancellationTokenSource _cts;

        protected string OwnerIdBase =>
            Vessel?.VesselStatus?.Player?.PlayerUUID ?? "UnknownOwner";

        private void OnDisable() => CancelExplosion();

        /// <summary>Start the AoE block creation (UniTask-based, no coroutines).</summary>
        public virtual void BeginExplosion()
        {
            CancelExplosion();
            _cts = new CancellationTokenSource();
            ExplodeAsync(_cts.Token).Forget();
        }

        public void CancelExplosion()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        private async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                if (ExplosionDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(ExplosionDelay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                for (int ring = 0; ring < ringCount; ring++)
                {
                    ct.ThrowIfCancellationRequested();

                    trails.Add(new Trail());

                    for (int i = 0; i < (int)blockCount; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        float phase = (ring % 2) * 0.5f;
                        float scale = ring / 2f + 1f;
                        float tilt  = ring;
                        float sweep = -ring / 2f;

                        CreateRingBlock(i, phase, scale, tilt, sweep, trails[ring]);
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException) { /* expected */ }
        }

        protected void CreateRingBlock(int i, float phase, float scale, float tilt, float sweep, Trail trail)
        {
            var fwd = transform.forward;
            float angle = ((i + phase) / blockCount) * Mathf.PI * 2f;

            var offset =
                scale * radius * Mathf.Cos(angle) * transform.right +
                scale * radius * Mathf.Sin(angle) * transform.up +
                sweep * radius * fwd;

            var pos = transform.position + offset;
            var lookForward = offset + tilt * radius * fwd;
            var up = fwd;

            CreateBlock(pos, lookForward, up, $"::AOE::{Time.time}::{i}", trail);
        }

        /// <summary>Requests an Interactive Prism from PrismFactory via event, then configures it.</summary>
        protected Prism CreateBlock(Vector3 position, Vector3 forward, Vector3 up, string ownerSuffix, Trail trail)
        {
            if (_prismSpawnEvent == null)
            {
                Debug.LogError("[AOEBlockCreation] Prism spawn event channel is not assigned.");
                return null;
            }

            var data = new PrismEventData
            {
                ownDomain       = Domain,
                Rotation        = Quaternion.LookRotation(forward, up),
                SpawnPosition   = position,
                Scale           = blockScale,
                Velocity        = Vector3.zero,
                PrismType       = PrismType.Interactive,
                TargetTransform = null,
                OnGrowCompleted = null
            };

            var ret = _prismSpawnEvent.RaiseEvent(data);
            if (!ret.SpawnedObject)
            {
                Debug.LogWarning("[AOEBlockCreation] PrismFactory returned null. Spawn aborted.");
                return null;
            }

            var block = ret.SpawnedObject.GetComponent<Prism>();
            if (!block)
            {
                Debug.LogWarning("[AOEBlockCreation] Spawned object has no Prism component.");
                return null;
            }

            // Ownership & unique tagging
            block.ownerID = OwnerIdBase + ownerSuffix + position;
            block.TargetScale = blockScale;

            if (shielded)
                block.prismProperties.IsShielded = true;
            
            block.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");
            trail.Add(block);
            return block;
        }
    }
}
