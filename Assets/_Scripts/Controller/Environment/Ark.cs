using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An <b>Ark</b> - a mothership: a prism-bodied home that travels the hypersea, wears a
    /// domain, and lives or dies by the food web. The Ark is a first-class fundamental (added at
    /// the prompter's request - CLAUDE.md, "The fundamentals"): it is the anchor of the faction-
    /// mission arc, and its first vehicle is the Arkway toy, where it sets the pace of a voyage
    /// through a corridor of cells.
    ///
    /// Everything about it composes with the shipped fundamentals instead of adding parallel
    /// systems:
    ///
    ///   • Its HULL is ordinary conserved prism mass, laid through the canonical
    ///     <see cref="PrismTrailBuilder"/> path in its owner's domain - so it registers with the
    ///     spatial index, binds to the cell that contains it, feeds that cell's volume books, and
    ///     is GRAZEABLE: in a nucleus-less cell, herbivores of another domain eat it and
    ///     herbivores of its own never do. Protecting an Ark is therefore controlling the cell -
    ///     no aggro system, no scripted threat.
    ///   • It MOVES the way a creature moves: the hull prisms ride one container transform, and
    ///     every frame each prism honours the mover contract
    ///     (<see cref="Prism.NotifyPositionChanged"/> - spatial index + shell + render entity),
    ///     exactly as fauna body prisms do. On a coarse cadence each prism also re-binds to the
    ///     cell that actually contains it (<see cref="PrismSpatialIndex.NotifyCellChanged"/>),
    ///     so the food web that can see it is always the local one. Between cells it binds to
    ///     nothing - open water is nobody's feeding ground.
    ///   • It DIES the way a creature dies - when its last hull prism is destroyed - but it is
    ///     deliberately NOT a <see cref="LifeForm"/>: no elemental heart (the lifeform-crystal
    ///     invariant governs lifeforms; an Ark is a vessel-like home, not a creature), no
    ///     starvation clock, no reproduction. Its only deaths are active forces: fauna
    ///     consumption and player abilities.
    ///
    /// The Ark itself never removes mass and never runs a timer over anyone else's - the one
    /// clock it owns is its own unhurried course.
    /// </summary>
    public sealed class Ark : MonoBehaviour
    {
        // ── Hull proportions ─────────────────────────────────────────────────
        // The hull is a spindle: rings of plates along the keel axis with a lens radius profile,
        // staggered ring to ring so the plating reads as a shell rather than a stack of hoops.
        const float PlateSpacing = 9f;                    // arc length per plate around a ring
        const float RingSpacing = 9.5f;                   // keel distance between rings
        const float RadiusFactor = 0.22f;                 // max hull radius = length × this
        static readonly Vector3 PlateScale = new(2.6f, 2.6f, 4.8f);
        static readonly Vector3 CapScale = new(3.4f, 3.4f, 6.4f);

        /// <summary>Scale a retiring hull prism withers to before returning to the pool
        /// (the Wanderway tether's own exit - continuity of existence is not waived).</summary>
        static readonly Vector3 RetiredScale = new(0.02f, 0.02f, 0.02f);
        const float WitherSeconds = 0.8f;

        const float AliveScanSeconds = 0.5f;              // hull-integrity scan cadence
        const float CellRebindSeconds = 2.5f;             // cell re-bind + grid re-file cadence
        const float TurnDegreesPerSecond = 40f;           // how fast the bow swings onto course

        readonly List<Prism> _prisms = new();
        Trail _trail;
        float _speed;
        Vector3 _destination;
        bool _hasDestination;
        bool _laying;
        bool _layComplete;
        bool _hullLost;
        bool _retiring;
        float _nextAliveScanAt;
        float _nextRebindAt;
        TMPro.TMP_Text _label;
        int _aliveCount;
        CancellationTokenSource _layCts;

        /// <summary>Raised ONCE, when the last hull prism has been destroyed or devoured.</summary>
        public event Action HullDestroyed;

        /// <summary>Total hull prisms laid (the full-health denominator).</summary>
        public int TotalCount { get; private set; }

        /// <summary>Live hull prisms as of the last integrity scan.</summary>
        public int AliveCount => _aliveCount;

        /// <summary>Hull integrity in [0, 1]. 1 until the hull has finished laying.</summary>
        public float HealthFraction =>
            !_layComplete || TotalCount <= 0 ? 1f : Mathf.Clamp01((float)_aliveCount / TotalCount);

        /// <summary>True once the hull has been fully laid and then wholly destroyed.</summary>
        public bool IsHullLost => _hullLost;

        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public float Speed => _speed;

        /// <summary>Create an Ark root at a pose. Lay the hull with <see cref="LayHullAsync"/>.</summary>
        public static Ark Create(Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Ark");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            return go.AddComponent<Ark>();
        }

        // ── Hull ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Lay the hull through the canonical budgeted path (a few prisms per frame, each growing
        /// in from zero - nothing pops; joins any load-gate hold the caller has raised). The hull
        /// wears <paramref name="domain"/> - its owner's colour - and an owner id no pilot
        /// carries, so the self-trail grace can never mistake it for a vessel's own ribbon.
        /// </summary>
        public async UniTask LayHullAsync(Prism prismPrefab, Domains domain, float hullLength,
            float speed, CancellationToken ct)
        {
            if (!prismPrefab)
            {
                CSDebug.LogWarning("[Ark] No prism prefab - the Ark cannot exist without a hull.");
                return;
            }

            _speed = Mathf.Max(1f, speed);
            _trail = new Trail();

            // The lay runs on the Ark's OWN linked token so RetireAsync can stop it: a retire
            // taken mid-lay (the voyage ended during the veiled build) must not leave the lay
            // producing pooled prisms AFTER the wither pass has swept - those would then die
            // with the Ark's GameObject, and pooled prisms destroyed outright corrupt the pool.
            var lays = BuildHullLays(Mathf.Max(30f, hullLength), domain);
            _laying = true;
            _layCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                await PrismTrailBuilder.LayBudgetedAsync(prismPrefab, lays, transform, _trail,
                    "Ark", 4f, _prisms, _layCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Retire (or teardown) cancelled the lay - what was already laid is in _prisms
                // and the retire's wither pass owns it from here.
            }
            finally
            {
                _laying = false;
                _layCts.Dispose();
                _layCts = null;
            }
            if (_retiring) return;

            TotalCount = _prisms.Count;
            _aliveCount = TotalCount;
            _layComplete = true;

            _label = ToyFactory.AddLabel(transform, "ARK", new Color(1f, 0.92f, 0.6f),
                hullLength * RadiusFactor + 14f, 18f);
        }

        /// <summary>
        /// The hull plan, in LOCAL space (+z is the bow), every plate in <paramref name="domain"/>
        /// - the Ark wears exactly one colour. Pure math - the same shape every time, because an
        /// Ark is a ship, not a roll.
        /// </summary>
        public static List<PrismLay> BuildHullLays(float length, Domains domain)
        {
            var lays = new List<PrismLay>(180);
            int rings = Mathf.Max(6, Mathf.RoundToInt(length / RingSpacing));
            float maxRadius = length * RadiusFactor;

            for (int i = 0; i < rings; i++)
            {
                float t = rings > 1 ? i / (float)(rings - 1) : 0.5f;
                float z = (t - 0.5f) * length;
                float radius = maxRadius * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 0.92f, t)), 0.75f);
                int plates = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * radius / PlateSpacing));
                float phase = (i % 2) * Mathf.PI / plates; // stagger alternate rings

                for (int p = 0; p < plates; p++)
                {
                    float a = phase + 2f * Mathf.PI * p / plates;
                    var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    var pos = radial * radius + Vector3.forward * z;
                    // Plates lie tangent to the hull: long axis along the keel, "up" out the shell.
                    var rot = Quaternion.LookRotation(Vector3.forward, radial);
                    lays.Add(new PrismLay(new SpawnPoint(pos, rot, PlateScale), domain));
                }
            }

            // Bow and stern caps.
            lays.Add(new PrismLay(new SpawnPoint(Vector3.forward * (length * 0.5f + 5f),
                Quaternion.identity, CapScale), domain));
            lays.Add(new PrismLay(new SpawnPoint(Vector3.back * (length * 0.5f + 5f),
                Quaternion.identity, CapScale), domain));
            return lays;
        }

        // ── Course ───────────────────────────────────────────────────────────

        /// <summary>Point the voyage at a world position; the Ark cruises there on its own.</summary>
        public void SetDestination(Vector3 worldPosition)
        {
            _destination = worldPosition;
            _hasDestination = true;
        }

        /// <summary>True when the Ark is within <paramref name="within"/> of its destination.</summary>
        public bool HasArrived(float within) =>
            _hasDestination && (transform.position - _destination).sqrMagnitude <= within * within;

        void Update()
        {
            if (_retiring) return;

            bool moved = false;
            if (_hasDestination && !_hullLost)
            {
                Vector3 to = _destination - transform.position;
                float dist = to.magnitude;
                if (dist > 0.5f)
                {
                    Vector3 dir = to / dist;
                    transform.position += dir * Mathf.Min(_speed * Time.deltaTime, dist);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(dir, Vector3.up),
                        TurnDegreesPerSecond * Time.deltaTime);
                    moved = true;
                }
            }

            // The mover contract, exactly as fauna bodies honour it: every frame the hull moves,
            // each prism's spatial-index position, shell pose and render-entity matrix follow the
            // transform (Prism.NotifyPositionChanged is cheap when the occupancy bucket is
            // unchanged).
            if (moved)
                for (int i = 0; i < _prisms.Count; i++)
                {
                    var prism = _prisms[i];
                    if (prism && prism.gameObject.activeInHierarchy)
                        prism.NotifyPositionChanged();
                }

            // Coarse cadence: re-bind each prism to the cell that actually contains it now, which
            // also re-files its (otherwise stale) density-grid bucket so fauna steer at the hull
            // where it IS, not where it entered the cell.
            if (moved && Time.time >= _nextRebindAt)
            {
                _nextRebindAt = Time.time + CellRebindSeconds;
                var index = PrismSpatialIndex.Instance;
                if (index != null)
                    for (int i = 0; i < _prisms.Count; i++)
                    {
                        var prism = _prisms[i];
                        if (prism && prism.gameObject.activeInHierarchy && prism.SpatialIndexId >= 0)
                            index.NotifyCellChanged(prism.SpatialIndexId);
                    }
            }

            if (_layComplete && !_hullLost && Time.time >= _nextAliveScanAt)
            {
                _nextAliveScanAt = Time.time + AliveScanSeconds;
                ScanHullIntegrity();
            }
        }

        /// <summary>
        /// Count the hull prisms still standing. A prism leaves two ways and both read the same
        /// here: destroyed (an ability - <c>destroyed</c> set, object deactivated) or devoured
        /// (fauna - <c>Prism.Consume</c> implodes it back to its pool, object deactivated).
        /// </summary>
        void ScanHullIntegrity()
        {
            int alive = 0;
            for (int i = 0; i < _prisms.Count; i++)
            {
                var prism = _prisms[i];
                if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy)
                    alive++;
            }
            _aliveCount = alive;

            if (_label)
                _label.text = alive == TotalCount ? "ARK" : $"ARK\n<size=60%>{HealthFraction:P0} hull</size>";

            if (alive == 0 && TotalCount > 0)
            {
                _hullLost = true;
                HullDestroyed?.Invoke();
            }
        }

        // ── Retire ───────────────────────────────────────────────────────────

        /// <summary>
        /// End-of-voyage exit: the surviving hull withers out (one grow-clock re-stamp per prism,
        /// the Wanderway tether's own retirement) and returns to the pool it was laid from, then
        /// the Ark destroys itself. This is the voyage apparatus being struck by the explicit,
        /// player-initiated end of the toy - the same event class as a satellite world's strike
        /// (Docs/ECOSYSTEM.md §19) - never a decay: a live voyage never calls it.
        /// </summary>
        public async UniTask RetireAsync(CancellationToken ct)
        {
            if (_retiring) return;
            _retiring = true;

            // Stop a lay still in flight and wait it out, so no prism can be laid after the
            // wither pass below has swept the list.
            _layCts?.Cancel();
            try
            {
                while (_laying)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            catch (OperationCanceledException)
            {
                return; // teardown - the scene unload takes the remainder, pool included
            }

            for (int i = 0; i < _prisms.Count; i++)
            {
                var prism = _prisms[i];
                if (prism && prism.gameObject.activeInHierarchy)
                    prism.TargetScale = RetiredScale;
            }

            bool cancelled = false;
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(WitherSeconds),
                    DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled = true; // still hand the pool its prisms back below
            }

            for (int i = 0; i < _prisms.Count; i++)
            {
                var prism = _prisms[i];
                if (prism && prism.gameObject.activeInHierarchy && prism.OnReturnToPool != null)
                    prism.ReturnToPool();
            }
            _prisms.Clear();

            if (!cancelled && this) Destroy(gameObject);
        }
    }
}
