using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Utilities;
using CosmicShore.Utility;
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
        [SerializeField] protected float blockCount = 8f; 
        [SerializeField] protected int ringCount = 3;
        [SerializeField] protected float radius = 30f;

        // [Visual Note] Trails list tracks all spawned objects so we can delete them on Reset
        protected readonly List<Trail> trails = new();
        
        protected string OwnerIdBase => Vessel?.VesselStatus?.Player?.PlayerUUID ?? "UnknownOwner";

        /// <summary>
        /// CLEANUP OVERRIDE: Called automatically by AOEExplosion when OnResetForReplay fires.
        /// </summary>
        protected override void PerformResetCleanup()
        {
            // 1. Stop any active spawning tasks
            CancelExplosion();

            // 2. Destroy all the blocks we spawned
            foreach (var block in from trail in trails where trail != null from block in trail.TrailList where block select block)
            {
                Destroy(block.gameObject);
            }
            trails.Clear();

            // 3. Destroy this spawner object
            Destroy(gameObject);
        }

        public virtual void BeginExplosion()
        {
            // [Optimization] Use the base class Detonate to ensure tokens are managed centrally
            Detonate();
        }

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
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
                    // Spread creation over frames to avoid spikes
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException) { /* expected on TurnEnd/Reset */ }
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

        protected Prism CreateBlock(Vector3 position, Vector3 forward, Vector3 up, string ownerSuffix, Trail trail)
        {
            if (_prismSpawnEvent == null)
            {
                Debug.LogError("[AOEBlockCreation] Prism spawn event channel is not assigned.");
                return null;
            }

            SafeLookRotation.TryGet(forward, up, out var rotation, this);

            var data = new PrismEventData
            {
                ownDomain       = Domain,
                Rotation        = rotation,
                SpawnPosition   = position,
                Scale           = blockScale,
                Velocity        = Vector3.zero,
                PrismType       = PrismType.Interactive,
                TargetTransform = null,
                OnGrowCompleted = null
            };

            var ret = _prismSpawnEvent.RaiseEvent(data);
            if (!ret.SpawnedObject) return null;

            var block = ret.SpawnedObject.GetComponent<Prism>();
            if (!block) return null;

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