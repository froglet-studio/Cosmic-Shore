using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime executor for <see cref="SquirrelTubeActionSO"/> - the Squirrel's "Oak Trunk" tube.
    /// See <c>SQUIRREL_TUBE.md</c>.
    ///
    /// Pressing the ability trigger lays a long wall of thick danger prisms straight out in front of
    /// the vessel (along the nose / flight direction), so a Squirrel flying straight rockets through
    /// the hollow centre while it obstructs everyone else. No preview - it just places.
    ///
    /// Every ring is laid through the shared <see cref="BoostRingBuilder"/> - the same primitive
    /// behind the omnicrystal ring and the joust ring - so the prisms are POOLED
    /// (<see cref="PrismType.Boost"/>: fast bloom, waitTime 0) and carry a FULL-SIZE collider from
    /// frame 0 (transform final at stamp under the clock law): the skimmer collides with the wall
    /// deterministically at any speed, while a vessel flying the centre usually never touches it.
    /// Rings are laid a few per frame and the prisms returned to the pool on teardown - never
    /// Instantiate/Destroy. Each blooms in, registers with the spatial index, and is removed only by
    /// an active force. A long cooldown gates re-use (surfaced to the HUD via
    /// <see cref="CooldownRemaining01"/>).
    /// </summary>
    public sealed class SquirrelTubeActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [Tooltip("Pooled-prism spawn channel (EventOnSpawnPrismAndReturn) - same asset the vessel " +
                 "trail uses. BoostRingBuilder routes the rings to the dedicated Boost pool " +
                 "(fast bloom, full-size collider from frame 0). Never Instantiated.")]
        [SerializeField] private PrismEventChannelWithReturnSO prismSpawnChannel;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        float _cooldownEndTime;
        float _activeCooldown;

        CancellationTokenSource _spawnCts;

        // Pooled prisms laid by this executor, tracked so they can be returned to the pool on
        // teardown. ReturnToPool self-unsubscribes, so it is safe on an already-returned prism.
        readonly List<Prism> _tubePrisms = new();

        /// <summary>
        /// Cooldown remaining as a 0-1 fraction: 1 right after a deploy (full cooldown left),
        /// 0 when ready again. Read by the Squirrel HUD to drive the tube cooldown icon fill.
        /// </summary>
        public float CooldownRemaining01 =>
            _activeCooldown <= 0f ? 0f : Mathf.Clamp01((_cooldownEndTime - Time.time) / _activeCooldown);

        /// <summary>True when the tube can be deployed again (off cooldown).</summary>
        public bool TubeReady => Time.time >= _cooldownEndTime;

        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            Cleanup();
        }

        void OnTurnEndOfMiniGame() => Cleanup();

        // Stateless: vessel context is passed into Begin/Commit each call (matches ShipActionSO).

        // ---------------- API ----------------

        /// <summary>Press: lay the tube straight out in front of the vessel.</summary>
        public void Begin(SquirrelTubeActionSO so, IVesselStatus status)
        {
            if (!so || status?.Vessel?.Transform == null) return;
            if (Time.time < _cooldownEndTime) return;   // on cooldown → no-op

            var vessel = status.Vessel.Transform;
            // Lead the placement by the vessel's speed so the tube mouth appears ~LeadSeconds of
            // travel ahead (fast Squirrels get more room to line up), floored at ForwardOffset so it
            // never forms on top of the vessel when slow/stopped. Axis is the nose / flight
            // direction, so flying straight carries the vessel through the hollow centre.
            float offset = Mathf.Max(so.ForwardOffset, status.Speed * so.LeadSeconds);
            Vector3 origin = vessel.position + vessel.forward * offset;
            SpawnTube(so, status, new Pose(origin, vessel.rotation));

            // TIME -> cooldown: the boost-ring recharge shortens as the vessel's live Time level
            // rises (atFull authored on the SO; the generic map Time multiplier stays 1.0 because
            // VesselTransformer consumes it for boost speed). Deficit Time lengthens it.
            float cooldown = so.Cooldown * ElementalScaling.Multiplier(
                status, Element.Time, so.CooldownMultiplierAtFullTime, so.MinCooldownMultiplier);
            _activeCooldown = cooldown;
            _cooldownEndTime = Time.time + cooldown;
        }

        /// <summary>Release: nothing - the tube is placed on press (no preview).</summary>
        public void Commit(SquirrelTubeActionSO so, IVesselStatus status) { }

        // ---------------- Tube spawn (pooled, via the shared boost-ring builder) ----------------

        void SpawnTube(SquirrelTubeActionSO so, IVesselStatus status, Pose pose)
        {
            if (!prismSpawnChannel)
            {
                CSDebug.LogWarning("[SquirrelTube] prismSpawnChannel not wired - cannot spawn tube.");
                return;
            }

            _spawnCts?.Cancel();
            _spawnCts?.Dispose();
            _spawnCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            SpawnTubeAsync(so, status, pose, _spawnCts.Token).Forget();
        }

        /// <summary>
        /// Lays the tube ring-by-ring through <see cref="BoostRingBuilder"/> - the same primitive
        /// the omnicrystal and joust rings use, so every ring is a wall of pooled Boost prisms with
        /// full-size colliders from frame 0 (the skim is deterministic at any speed). Batched a few
        /// rings per frame to spread the spawn cost.
        /// </summary>
        async UniTaskVoid SpawnTubeAsync(SquirrelTubeActionSO so, IVesselStatus status, Pose pose, CancellationToken ct)
        {
            var spec = new BoostRingSpec(so.Segments, so.Radius, Vector3.one * so.PrismScale,
                so.Danger ? PrismKind.Danger : PrismKind.Plain);
            string playerName = status.PlayerName;
            Domains domain = status.Domain;
            int ringsPerFrame = Mathf.Max(1, so.SpawnPerFrame / spec.Segments);

            // TIME level-5 'Twin Rings': the deploy gains extra rings while the Time upgrade is
            // active (per-deploy snapshot - an in-flight tube keeps the rings it was laid with).
            int rings = so.Rings;
            if (status.ElementalAbilityHandler?.IsUpgradeActive(Element.Time) == true)
                rings += so.UpgradeExtraRings;

            for (int z = 0; z < rings; z++)
            {
                if (ct.IsCancellationRequested) return;

                Vector3 ringCenter = pose.position + pose.rotation * (Vector3.forward * (z * so.RingSpacing));
                BoostRingBuilder.LayRing(prismSpawnChannel, new Pose(ringCenter, pose.rotation), spec,
                    domain, playerName, $"{playerName}::Tube::{z}", trail: null, collected: _tubePrisms);

                if ((z + 1) % ringsPerFrame == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        // ---------------- Cleanup ----------------

        void Cleanup()
        {
            _spawnCts?.Cancel();
            _spawnCts?.Dispose();
            _spawnCts = null;

            // Return pooled prisms rather than destroying them. Clear the danger state first so a
            // recycled prism can't carry its danger flag into a plain trail block the shared pool
            // later hands out. ReturnToPool self-unsubscribes, so an already-returned prism is a
            // safe no-op; only live tube prisms are recycled here.
            for (int i = 0; i < _tubePrisms.Count; i++)
            {
                var p = _tubePrisms[i];
                if (!p || p.destroyed) continue;
                PrismKinds.Clear(p);
                p.ReturnToPool();
            }
            _tubePrisms.Clear();
        }
    }
}
