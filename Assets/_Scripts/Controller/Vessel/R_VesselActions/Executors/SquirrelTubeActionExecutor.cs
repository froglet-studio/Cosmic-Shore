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
    /// Runtime executor for <see cref="SquirrelTubeActionSO"/> — the Squirrel's "Oak Trunk" tube.
    /// See <c>SQUIRREL_TUBE.md</c>.
    ///
    /// Pressing the ability trigger lays a long wall of thick danger prisms straight out in front of
    /// the vessel (along the nose / flight direction), so a Squirrel flying straight rockets through
    /// the hollow centre while it obstructs everyone else. No preview — it just places.
    ///
    /// The prisms are POOLED — pulled via <see cref="PrismEventChannelWithReturnSO"/> from the
    /// dedicated <c>Boost</c> pool (<see cref="PrismType.Boost"/>): fast-growing prisms whose collider
    /// turns on immediately, so a skimmer can boost off them right away even though a vessel flying the
    /// centre usually never touches them. The joust danger-block formation
    /// (<c>AOEDangerHemisphereBlocks</c>) draws from the same pool. Each prism is configured like a
    /// trail block, laid a few per frame, and returned to the pool on teardown — never
    /// Instantiate/Destroy. Each blooms in, registers with the spatial index, and is removed only by an
    /// active force. A long cooldown gates re-use (surfaced to the HUD via
    /// <see cref="CooldownRemaining01"/>).
    /// </summary>
    public sealed class SquirrelTubeActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [Tooltip("Pooled-prism spawn channel (EventOnSpawnPrismAndReturn) — same asset the vessel " +
                 "trail uses. The SO's PrismType (Boost) selects the dedicated fast/immediate-collider " +
                 "pool. The tube's prisms are pooled, never Instantiated.")]
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

            _activeCooldown = so.Cooldown;
            _cooldownEndTime = Time.time + so.Cooldown;
        }

        /// <summary>Release: nothing — the tube is placed on press (no preview).</summary>
        public void Commit(SquirrelTubeActionSO so, IVesselStatus status) { }

        // ---------------- Tube spawn (pooled) ----------------

        void SpawnTube(SquirrelTubeActionSO so, IVesselStatus status, Pose pose)
        {
            if (!prismSpawnChannel)
            {
                CSDebug.LogWarning("[SquirrelTube] prismSpawnChannel not wired — cannot spawn tube.");
                return;
            }

            var points = BuildRingPoints(so);

            _spawnCts?.Cancel();
            _spawnCts?.Dispose();
            _spawnCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            SpawnTubeAsync(so, status, pose, points, _spawnCts.Token).Forget();
        }

        async UniTaskVoid SpawnTubeAsync(SquirrelTubeActionSO so, IVesselStatus status, Pose pose,
            IReadOnlyList<SpawnPoint> points, CancellationToken ct)
        {
            int perFrame = so.SpawnPerFrame;
            string playerName = status.PlayerName;
            Domains domain = status.Domain;

            for (int i = 0; i < points.Count; i++)
            {
                if (ct.IsCancellationRequested) return;

                var p = points[i];
                Vector3 worldPos = pose.position + pose.rotation * p.Position;
                Quaternion worldRot = pose.rotation * p.Rotation;
                SpawnOnePooledPrism(so, playerName, domain, worldPos, worldRot, p.Scale);

                if ((i + 1) % perFrame == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        /// <summary>
        /// Pulls one prism from the shared pool and configures it like the trail does (team, danger,
        /// grow-in). IsDangerous is set BEFORE Initialize so Initialize's MakeDangerous repaints it
        /// to the team's dangerous material.
        /// </summary>
        void SpawnOnePooledPrism(SquirrelTubeActionSO so, string playerName, Domains domain,
            Vector3 worldPos, Quaternion worldRot, Vector3 scale)
        {
            var ret = prismSpawnChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = domain,
                Rotation = worldRot,
                SpawnPosition = worldPos,
                Scale = scale,
                PrismType = so.PrismType
            });

            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism))
                return;

            prism.ownerID = playerName;
            prism.TargetScale = scale;
            prism.ChangeTeam(domain);
            if (so.Danger && prism.prismProperties != null)
                prism.prismProperties.IsDangerous = true; // Initialize → MakeDangerous repaints it

            prism.Initialize(playerName);
            _tubePrisms.Add(prism);
        }

        /// <summary>
        /// Ring positions around the local +z axis (container-local; the caller supplies the world
        /// pose). Every ring is centred on the axis so a vessel down the middle passes through the
        /// hollow centre.
        /// </summary>
        List<SpawnPoint> BuildRingPoints(SquirrelTubeActionSO so)
        {
            int rings = so.Rings;
            int segments = so.Segments;
            float radius = so.Radius;
            float spacing = so.RingSpacing;
            var scale = Vector3.one * so.PrismScale;

            var points = new List<SpawnPoint>(rings * segments);

            for (int z = 0; z < rings; z++)
            {
                float depth = z * spacing;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * (2f * Mathf.PI / segments);
                    Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    Vector3 position = radial * radius + Vector3.forward * depth;
                    // Long side runs along the tube axis; block "up" points outward radially.
                    var rotation = Quaternion.LookRotation(Vector3.forward, radial);
                    points.Add(new SpawnPoint(position, rotation, scale));
                }
            }

            return points;
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
