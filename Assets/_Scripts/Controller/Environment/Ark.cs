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

        // ── Wake ─────────────────────────────────────────────────────────────
        // A ship leaves a wake. The Ark's is conserved prism mass in its own domain, laid on
        // DISTANCE (never a clock) through the canonical lay path, so it is ordinary grazeable
        // food-web citizenry the moment it exists - which is also the only honest way to make a
        // 150-prism hull matter to a swarm grazing a 10,000-prism world: the ribbon is where the
        // Ark HAS BEEN, it is dense along a line rather than spread over a sphere, and following
        // it leads to the ship.
        //
        // It lives on its own STATIONARY root, not under the Ark: the hull rides the Ark's
        // transform (that is what makes it a moving body), and a wake that moved with the ship
        // would be a second hull rather than a trail.
        Transform _wakeRoot;
        Trail _wakeTrail;
        readonly List<Prism> _wake = new();
        readonly List<(Prism prism, float dueAt)> _wakeWithering = new();
        Prism _wakePrefab;
        Domains _wakeDomain;
        float _wakeSpacing;
        Vector3 _wakeScale = Vector3.one;
        int _wakeBudget;
        Vector3 _lastWakeAt;
        bool _wakeArmed;
        float _speed;                                     // live speed this frame
        float _approachSpeed;                             // speed at the destination's core
        float _cruiseSpeed;                               // speed in open water between cells
        float _slowRadius;                                // range over which cruise eases to approach
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
        /// <summary>The Ark's speed THIS frame — a live reading of the arrival profile, not a
        /// setting (see <see cref="SetSpeedProfile"/>).</summary>
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

            _speed = _approachSpeed = _cruiseSpeed = Mathf.Max(1f, speed);
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

        // ── Wake ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Arm the wake: from here the Ark lays one prism every <paramref name="spacing"/> units
        /// of travel, at <paramref name="scale"/>, in <paramref name="domain"/> - well spaced and
        /// far larger than a vessel's trail prism, so the ribbon reads as a ship's wake rather
        /// than as another pilot's line.
        ///
        /// Laid on DISTANCE, never on a clock, so the wake is a record of where the ship went and
        /// its density is a property of the ship's speed - dense through the slow pass under a
        /// cell's core, sparse across the open water it crosses under way.
        ///
        /// <paramref name="parent"/> must be a STATIONARY transform (the toybox root, never this
        /// Ark): the hull rides the Ark's transform, and a wake that rode it too would just be a
        /// longer hull.
        ///
        /// It is ordinary conserved mass with no lifespan: nothing retires a wake prism except
        /// the food web eating it, or <see cref="RetireWakeBefore"/> when the cell it was laid in
        /// is struck. <paramref name="budget"/> is a backstop for a voyage that somehow outruns
        /// its corridor, not a lifespan - reaching it retires the OLDEST, never the nearest.
        /// </summary>
        public void ConfigureWake(Prism prefab, Domains domain, float spacing, Vector3 scale,
            int budget, Transform parent)
        {
            if (!prefab || spacing <= 0.5f) return;

            _wakePrefab = prefab;
            _wakeDomain = domain;
            _wakeSpacing = spacing;
            _wakeScale = scale;
            _wakeBudget = Mathf.Max(16, budget);

            var go = new GameObject("ArkWake");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _wakeRoot = go.transform;
            _wakeTrail = new Trail();

            _lastWakeAt = transform.position;
            _wakeArmed = true;
        }

        /// <summary>Wake prisms standing right now — the census number that would climb if the
        /// corridor stopped retiring behind the Ark.</summary>
        public int WakeCount => _wake.Count;

        /// <summary>The newest wake prism, or null - the boundary
        /// <see cref="RetireWakeBefore"/> retires up to. A PRISM, never an index: the list is
        /// front-removed, so any recorded number goes stale on the first retire.</summary>
        public Prism WakeHead => _wake.Count > 0 ? _wake[^1] : null;

        /// <summary>
        /// Retire every wake prism laid before <paramref name="mark"/> - the ribbon goes with the
        /// cell it was laid in, the same rule <see cref="Cell.RequestCellSwap"/> already applies
        /// to loose trail mass in a struck world. Continuity of existence is not waived: each
        /// prism withers on the grow clock and only then goes back to the environment pool.
        /// </summary>
        public void RetireWakeBefore(Prism mark)
        {
            if (!mark) return;
            int stop = _wake.IndexOf(mark);
            if (stop <= 0) return;
            RetireOldestWake(stop);
        }

        void RetireOldestWake(int count)
        {
            count = Mathf.Min(count, _wake.Count);
            for (int i = 0; i < count; i++)
            {
                var prism = _wake[i];
                _wakeTrail?.RemoveOldest();
                // Already gone - the food web ate it, which is the whole point of the wake being
                // ordinary mass. Consumed prisms stay ACTIVE with destroyed = true, so the
                // aliveness test needs both (Docs/ECOSYSTEM.md - "a devoured prism never
                // deactivates"); withering or pool-returning one would fight whoever took it.
                if (!prism || prism.destroyed) continue;
                prism.TargetScale = RetiredScale;
                _wakeWithering.Add((prism, Time.time + WitherSeconds));
            }
            // RemoveOldest above walks the Trail in step; the list is the authority on order.
            _wake.RemoveRange(0, count);
        }

        void TickWake()
        {
            if (!_wakeArmed || _retiring || _hullLost) return;

            if ((transform.position - _lastWakeAt).sqrMagnitude >= _wakeSpacing * _wakeSpacing)
            {
                _lastWakeAt = transform.position;
                LayWakePrism();
            }

            // Backstop only - a voyage whose corridor is not retiring behind it.
            if (_wake.Count > _wakeBudget)
                RetireOldestWake(_wake.Count - _wakeBudget);

            for (int i = _wakeWithering.Count - 1; i >= 0; i--)
            {
                var (prism, dueAt) = _wakeWithering[i];
                if (prism && Time.time < dueAt) continue;
                _wakeWithering.RemoveAt(i);
                ReleaseWakePrism(prism);
            }
        }

        void LayWakePrism()
        {
            // Astern and a little below the keel - a wake trails a ship, it does not run
            // through it, and laying inside the hull would put the two in the same grid cell.
            var pose = new SpawnPoint(
                transform.position - transform.forward * (_wakeScale.z * 1.5f),
                Quaternion.LookRotation(transform.forward, transform.up),
                _wakeScale);

            var prism = PrismTrailBuilder.LayOne(_wakePrefab,
                new PrismLay(pose, _wakeDomain), _wakeRoot, _wakeTrail, WakeOwnerId);
            if (!prism) return;

            // LayOne writes localPosition - the wake root sits at the world origin unrotated,
            // so local IS world here. Stated rather than assumed: a future parent with a pose
            // would silently place the whole ribbon somewhere else.
            _wake.Add(prism);
        }

        void ReleaseWakePrism(Prism prism)
        {
            if (!prism || prism.destroyed) return;
            if (!EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
        }

        /// <summary>Owner id no pilot carries, so the self-trail contact grace can never mistake
        /// the wake for a vessel's own fresh ribbon.</summary>
        const string WakeOwnerId = "ArkWake";

        void StrikeWake()
        {
            for (int i = 0; i < _wakeWithering.Count; i++)
                ReleaseWakePrism(_wakeWithering[i].prism);
            _wakeWithering.Clear();

            for (int i = 0; i < _wake.Count; i++)
                ReleaseWakePrism(_wake[i]);
            _wake.Clear();
            _wakeTrail?.Clear();
            _wakeArmed = false;

            if (_wakeRoot) Destroy(_wakeRoot.gameObject);
            _wakeRoot = null;
        }

        // ── Course ───────────────────────────────────────────────────────────

        /// <summary>
        /// The arrival profile: an Ark makes way in open water and comes in SLOW under a cell's
        /// core, the way a ship enters harbour. One function gives both halves of that, because
        /// both are read off the SAME quantity — range to the destination. Leaving a cell, the
        /// next core is a whole corridor spacing away, so the Ark is already at
        /// <paramref name="cruiseSpeed"/> by the time its stern clears the membrane; arriving, it
        /// eases to <paramref name="approachSpeed"/> across the last <paramref name="slowRadius"/>
        /// (the destination cell's own membrane, so the deceleration IS entering the cell).
        ///
        /// No acceleration state is kept: speed is a pure function of position, so a destination
        /// change (the corridor advancing) re-reads it on the same frame with nothing to unwind.
        /// </summary>
        public void SetSpeedProfile(float approachSpeed, float cruiseSpeed, float slowRadius)
        {
            _approachSpeed = Mathf.Max(1f, approachSpeed);
            _cruiseSpeed = Mathf.Max(_approachSpeed, cruiseSpeed);
            _slowRadius = Mathf.Max(0f, slowRadius);
        }

        /// <summary>Point the voyage at a world position; the Ark cruises there on its own.</summary>
        public void SetDestination(Vector3 worldPosition)
        {
            _destination = worldPosition;
            _hasDestination = true;
        }

        /// <summary>
        /// Point the voyage at a world position and state the range over which the Ark should
        /// slow onto it — the destination cell's membrane radius.
        /// </summary>
        public void SetDestination(Vector3 worldPosition, float slowRadius)
        {
            _slowRadius = Mathf.Max(0f, slowRadius);
            SetDestination(worldPosition);
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
                    // Smoothstep over the approach band: flat cruise outside it, flat approach
                    // speed at the core, and no discontinuity at either end — an Ark that
                    // stepped between two speeds would read as a stutter, not as a landing.
                    float t = _slowRadius > 1f ? Mathf.Clamp01(dist / _slowRadius) : 1f;
                    _speed = Mathf.Lerp(_approachSpeed, _cruiseSpeed, t * t * (3f - 2f * t));

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
            // NOTE the !destroyed gate: a devoured environment prism never deactivates - Consume
            // → SetupDestruction leaves the GameObject ACTIVE with destroyed=true, hidden and
            // collider-less - so activeInHierarchy alone would keep paying sync for every prism
            // the food web has already taken.
            if (moved)
                for (int i = 0; i < _prisms.Count; i++)
                {
                    var prism = _prisms[i];
                    if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy)
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
                        if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy
                            && prism.SpatialIndexId >= 0)
                            index.NotifyCellChanged(prism.SpatialIndexId);
                    }
            }

            TickWake();

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
        void OnDestroy()
        {
            // Scene teardown / an Ark destroyed outside RetireAsync: the wake root is a SIBLING,
            // so nothing else would collect it.
            if (_wakeRoot) Destroy(_wakeRoot.gameObject);
        }

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
        /// the Wanderway tether's own retirement) and is then retired the way environment mass
        /// is retired everywhere - hull prisms come from the environment pool and carry NO
        /// pool-return handler (the strike partition test is <c>OnReturnToPool != null</c>), so
        /// they are destroy-drained with the Ark's own root, exactly like a swapped world's
        /// environment. A prism that DOES wear a return handler is handed back to its pool
        /// instead - defensive, for a future trail-pooled hull prefab. This is the voyage
        /// apparatus being struck by the explicit, player-initiated end of the toy - the same
        /// event class as a satellite world's strike (Docs/ECOSYSTEM.md §19) - never a decay:
        /// a live voyage never calls it.
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

            // Pool-carrying prisms (none today - see the summary) go home; everything else is
            // instantiated-class mass and dies with the root below, ~150 prisms in one frame
            // (well under the 150-per-frame drain slice a 10-20k world needs).
            for (int i = 0; i < _prisms.Count; i++)
            {
                var prism = _prisms[i];
                if (prism && prism.gameObject.activeInHierarchy && prism.OnReturnToPool != null)
                {
                    prism.transform.SetParent(null, false);
                    prism.ReturnToPool();
                }
            }
            _prisms.Clear();

            // The wake is NOT under this root (it is stationary by design), so it has to be
            // struck explicitly or it outlives the voyage that laid it.
            StrikeWake();

            if (!cancelled && this) Destroy(gameObject);
        }
    }
}
