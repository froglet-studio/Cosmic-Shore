using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay.Audio;
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
    ///   • It can be DRAWN as a model (<see cref="UseModel"/>, <see cref="ArkModel"/>; every Arkway voyage's
    ///     Ark is ArkReverse): the hull is then laid into the model's sockets - each prism its socket's shield tier
    ///     (super-shielded, shielded, plain, danger) and size - the model wears the hull's domain, and its jets and
    ///     wings fly its turns. The hull is still the same conserved mass in the same domain; the model only says
    ///     where each prism sits. Without a model it is the procedural spindle below.
    ///
    ///   • Its REACTOR (<see cref="UseReactor"/>; Garrett, 2026-10-28): the omni crystal aboard powers the Ark's prism
    ///     creation. An Ark is the one body that can contain an omni crystal and harvest its power instead of morphing it
    ///     into something else, so a destroyed or taken hull prism grows back into its own slot at a FIXED RATE, one
    ///     prism a beat, nearest the reactor first (<see cref="ArkReactor"/>). The mass law's fourth sign-off fences it
    ///     (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md): only an Ark, only into its own empty slots, at the fixed rate,
    ///     each prism growing in. A fallen Ark (no hull left) never regrows.
    ///
    /// The Ark itself never removes mass and never runs a timer over anyone else's - the one
    /// clock it owns is its own unhurried course.
    /// </summary>
    /// <summary>
    /// The cell-aware speed profile (<see cref="Ark.SetSpeedProfile(in ArkSpeedProfile)"/>): the Ark CRUISES in open water,
    /// brakes to <see cref="Entry"/> across the <see cref="MembraneBand"/> outside the destination membrane, eases on to
    /// <see cref="Crawl"/> over the same band inside it, crawls the cytoplasm to the core and on out through the far side
    /// of the cell it is leaving, and only then accelerates back to cruise. Every speed is clamped to
    /// <see cref="Ceiling"/>: an escort's Ark must never outrun the slowest hull guarding it.
    /// </summary>
    [System.Serializable]
    public struct ArkSpeedProfile
    {
        [Min(1f), Tooltip("Open-water speed between cells, u/s. Keep it below every escort hull's cruise.")]
        public float Cruise;
        [Min(1f), Tooltip("Speed AT the destination membrane, u/s (the end of the braking band).")]
        public float Entry;
        [Min(1f), Tooltip("Speed through a cell's cytoplasm, u/s: from one membrane band inside the destination to the " +
                          "core, and on out until the stern clears the cell it is leaving.")]
        public float Crawl;
        [Min(0f), Tooltip("Width, u, of the braking band outside a membrane and of the easing band inside it.")]
        public float MembraneBand;
        [Min(0f), Tooltip("Half the hull's length, u: the crawl holds until this far past the departed membrane.")]
        public float HullHalfLength;
        [Min(1f), Tooltip("No speed in the profile exceeds this, u/s.")]
        public float Ceiling;

        public static ArkSpeedProfile Default => new()
        {
            Cruise = 45f, Entry = 22f, Crawl = 14f, MembraneBand = 400f, HullHalfLength = 60f, Ceiling = 50f,
        };

        /// <summary>The profile with every speed at least 1 and none above <see cref="Ceiling"/>; crawl, entry, cruise ascending.</summary>
        public ArkSpeedProfile Sanitized()
        {
            var p = this;
            p.Ceiling = p.Ceiling <= 0f ? float.MaxValue : Mathf.Max(1f, p.Ceiling);
            p.Cruise = Mathf.Clamp(p.Cruise, 1f, p.Ceiling);
            p.Entry = Mathf.Clamp(p.Entry, 1f, p.Cruise);
            p.Crawl = Mathf.Clamp(p.Crawl, 1f, p.Entry);
            p.MembraneBand = Mathf.Max(0f, p.MembraneBand);
            p.HullHalfLength = Mathf.Max(0f, p.HullHalfLength);
            return p;
        }

        /// <summary>
        /// The speed at a position, from its range past the destination membrane (<paramref name="dIn"/>, negative
        /// inside) and past the departed membrane (<paramref name="dOut"/>, negative inside; +inf with no departure).
        /// The slower of an exit ramp and an entry ramp, so it is continuous everywhere. Pure: tests read it directly.
        /// </summary>
        public float SpeedAt(float dIn, float dOut)
        {
            float band = Mathf.Max(1f, MembraneBand);
            // Leaving: crawl until the stern clears the departed membrane, then ramp up to cruise over one band.
            float exit = float.IsPositiveInfinity(dOut) ? Cruise
                : Mathf.Lerp(Crawl, Cruise, Smooth((dOut - HullHalfLength) / band));
            // Arriving: cruise outside the band, brake to entry at the membrane, ease to crawl one band inside it.
            float entry = dIn >= 0f
                ? Mathf.Lerp(Entry, Cruise, Smooth(dIn / band))
                : Mathf.Lerp(Crawl, Entry, Smooth(1f + dIn / band));
            return Mathf.Min(exit, entry);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }

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
        // The cell-aware profile (SetSpeedProfile(in ArkSpeedProfile)); off = the legacy two-speed arrival profile.
        ArkSpeedProfile _profile;
        bool _useProfile;
        float _destinationRadius;
        Vector3 _departure;
        float _departureRadius;
        bool _hasDeparture;
        bool _insideCell;
        bool _underWay;
        bool _laying;
        bool _layComplete;
        bool _hullLost;
        bool _retiring;
        float _nextAliveScanAt;
        float _nextRebindAt;
        int _aliveCount;
        CancellationTokenSource _layCts;

        // ── Model ────────────────────────────────────────────────────────────
        // An Ark given a model (UseModel) is drawn as that model, its hull laid into the model's sockets; without one it
        // keeps the procedural spindle.
        CosmicShore.ScriptableObjects.ArkModelDefinitionSO _modelDefinition;
        ThemeManagerDataContainerSO _theme;
        ArkModel _model;

        // ── Reactor ──────────────────────────────────────────────────────────
        // Every hull prism has a SLOT: where it was laid, on what, and which prism holds it now. An empty slot (its prism
        // destroyed, or taken by the hold) is where the reactor's power grows a prism back.
        sealed class HullSlot
        {
            public Transform Parent;
            public PrismLay Lay;
            public bool RidesBone;
            public Vector3 ArkLocal;                       // the slot's centre in the Ark's space at rest
            public Prism Prism;
            public bool Counted;                           // its prism was seen live and seated (HullPrismLost)
        }

        const string RegrowOwnerId = "Ark::regrow";

        readonly List<HullSlot> _slots = new();
        readonly List<Vector3> _slotPositions = new();
        readonly List<bool> _slotEmpty = new();
        CosmicShore.ScriptableObjects.ArkReactorSO _reactorDefinition;
        Prism _hullPrefab;
        float _nextRegrowAt;
        float _lastRegrowAt = float.NegativeInfinity;

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

        /// <summary>The domain the hull was laid in - the squad whose home this is.</summary>
        public Domains Domain { get; private set; }

        /// <summary>Test seam: wear <paramref name="domain"/> without laying a hull.</summary>
        internal void SetDomainForTests(Domains domain) => Domain = domain;

        /// <summary>The Ark's hold: where its payloads come from (<see cref="ArkHold"/>,
        /// Docs/Pilots/ARTIFACTS.md §2). Every Ark has one.</summary>
        public ArkHold Hold { get; private set; }
        /// <summary>The Ark's speed THIS frame — a live reading of the arrival profile, not a
        /// setting (see <see cref="SetSpeedProfile"/>).</summary>
        public float Speed => _speed;

        /// <summary>True while the hull's centre is inside the destination's or the departed cell's membrane (the
        /// cell-aware profile; the legacy profile reads it from the slow band).</summary>
        public bool InsideCell => _insideCell;

        /// <summary>Range to the destination point, or -1 with none.</summary>
        public float DistanceToDestination => _hasDestination ? Vector3.Distance(transform.position, _destination) : -1f;

        /// <summary>Create an Ark root at a pose. Lay the hull with <see cref="LayHullAsync"/>.</summary>
        /// <summary>Every enabled Ark (a cell's substrate senses the one inside it as a cling-only hull,
        /// <see cref="SubstrateCellHost"/>). Joined in OnEnable, left in OnDisable.</summary>
        public static readonly List<Ark> Live = new(2);

        void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        void OnDisable() => Live.Remove(this);

        public static Ark Create(Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Ark");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var ark = go.AddComponent<Ark>();
            ark.Hold = go.AddComponent<ArkHold>();
            ark.Hold.Bind(ark);
            return ark;
        }

        // ── Model ────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw this Ark as <paramref name="definition"/>'s model (call before <see cref="LayHullAsync"/>): the hull is
        /// laid into the model's sockets, one prism per socket in its shield tier's kind and size, the model's Domain
        /// slot wears the hull's domain (from <paramref name="theme"/>'s material sets), and its jets and wings fly the
        /// Ark's turns (<see cref="ArkModel"/>). A null definition keeps the spindle.
        /// </summary>
        public void UseModel(CosmicShore.ScriptableObjects.ArkModelDefinitionSO definition, ThemeManagerDataContainerSO theme)
        {
            _modelDefinition = definition;
            _theme = theme;
        }

        /// <summary>The model this Ark is drawn as, or null (the spindle).</summary>
        public ArkModel Model => _model;

        /// <summary>
        /// Give this Ark a reactor (call before <see cref="LayHullAsync"/>): the omni crystal aboard regrows its hull at
        /// a fixed rate. A null definition leaves the hull as laid: no regrowth.
        /// </summary>
        public void UseReactor(CosmicShore.ScriptableObjects.ArkReactorSO definition) => _reactorDefinition = definition;

        /// <summary>True when this Ark's hull regrows (it was given a reactor).</summary>
        public bool Regrows => _reactorDefinition;

        /// <summary>Raised when one hull prism grows back into its slot.</summary>
        public event Action<Prism> HullPrismRegrown;

        /// <summary>Hull slots: the full-health prism count the reactor regrows toward.</summary>
        public int SlotCount => _slots.Count;

        /// <summary>
        /// Raised once for every hull prism that was seated and is now gone (devoured, or destroyed by an ability), with
        /// the shield tier its SLOT was laid in (a plate gnawed through has already shed its live shield flags). Read
        /// <see cref="Prism.DestroyerName"/> and <see cref="Prism.DestroyedByCreature"/> for who took it. A plate the
        /// hold hands a carrier is not a loss. Detected on the integrity scan (every 0.5 s) and before a regrowth
        /// reuses the slot, so no loss is missed. One event serves every listener: the escort's plate metrics and its
        /// friendly-fire count (a loss not <see cref="Prism.DestroyedByCreature"/>).
        /// </summary>
        public event Action<Prism, PrismKind> HullPrismLost;

        /// <summary>Seated hull prisms by their slot's authored tier (danger counts as plain).</summary>
        public void CountAlive(out int plain, out int shielded, out int superShielded)
        {
            plain = shielded = superShielded = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!IsSeated(slot)) continue;
                switch (slot.Lay.Kind)
                {
                    case PrismKind.SuperShielded: superShielded++; break;
                    case PrismKind.Shielded: shielded++; break;
                    default: plain++; break;
                }
            }
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

            Domain = domain;
            _speed = _approachSpeed = _cruiseSpeed = Mathf.Max(1f, speed);
            _trail = new Trail();
            _hullPrefab = prismPrefab;

            // The lay runs on the Ark's OWN linked token so RetireAsync can stop it: a retire
            // taken mid-lay (the voyage ended during the veiled build) must not leave the lay
            // producing pooled prisms AFTER the wither pass has swept - those would then die
            // with the Ark's GameObject, and pooled prisms destroyed outright corrupt the pool.
            //
            // With a model the hull is laid into the model's sockets (each prism its socket's shield tier and size,
            // a jaw's blades riding the jaw) and the model wears the same domain; without one, the spindle.
            _model = _modelDefinition ? ArkModel.Attach(this, _modelDefinition) : null;
            List<ArkModel.LayBatch> batches;
            if (_model)
            {
                _model.ApplyDomain(domain, _theme);
                batches = _model.BuildLays(domain);
            }
            else
            {
                batches = new List<ArkModel.LayBatch>
                {
                    new(transform, BuildHullLays(Mathf.Max(30f, hullLength), domain), false),
                };
            }
            _laying = true;
            _layCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                foreach (var batch in batches)
                {
                    // A batch on a bone collects apart so the model can tell those prisms when the bone moves them;
                    // whatever a cancelled batch did lay still joins the hull, so the retire's wither reaches it.
                    var laid = batch.RidesBone ? new List<Prism>() : _prisms;
                    int first = laid.Count;
                    try
                    {
                        await PrismTrailBuilder.LayBudgetedAsync(prismPrefab, batch.Lays, batch.Parent, _trail,
                            "Ark", 4f, laid, _layCts.Token, admitAuthoredScale: _model != null);
                    }
                    finally
                    {
                        AddSlots(batch, laid, first);
                        if (laid != _prisms)
                        {
                            _prisms.AddRange(laid);
                            _model.AddRiders(laid);
                        }
                    }
                }
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
            for (int i = 0; i < _slots.Count; i++) _slots[i].Counted = IsSeated(_slots[i]);
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

        // ── Hold ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Hand <paramref name="count"/> live hull prisms to the hold, the ones nearest
        /// <paramref name="near"/> first, so a payload comes off the flank the pilot is at.
        /// The prisms LEAVE the hull: they are no longer the Ark's to move, scan or retire, so
        /// every payload taken is plating the Ark no longer has (its health falls with it). Mass
        /// is moved, never minted (Docs/Pilots/ARTIFACTS.md §2).
        ///
        /// Never takes the last <paramref name="keep"/> plates: an Ark whose hold emptied its own
        /// hull would fall to its own crew, and an Ark's only deaths are active forces from
        /// outside (fauna, rival abilities). Returns how many were moved into
        /// <paramref name="into"/> (which is not cleared).
        /// </summary>
        public int TakeHullPrisms(int count, Vector3 near, int keep, List<Prism> into)
        {
            if (!_layComplete || _hullLost || _retiring || count <= 0) return 0;

            int alive = 0;
            for (int i = 0; i < _prisms.Count; i++)
                if (IsLive(_prisms[i])) alive++;
            int give = Mathf.Min(count, alive - Mathf.Max(1, keep));
            if (give <= 0) return 0;

            for (int n = 0; n < give; n++)
            {
                int best = -1;
                float bestSqr = float.PositiveInfinity;
                for (int i = 0; i < _prisms.Count; i++)
                {
                    if (!IsLive(_prisms[i])) continue;
                    float d = (_prisms[i].transform.position - near).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; best = i; }
                }
                if (best < 0) return n;
                into.Add(_prisms[best]);
                _prisms.RemoveAt(best);
            }

            // The full-health denominator falls with the hull: the plating is gone from the
            // ship, not damaged on it, and a carried parcel must not read as a wound the hold
            // could heal.
            TotalCount = Mathf.Max(0, TotalCount - give);
            _aliveCount = Mathf.Max(0, _aliveCount - give);
            return give;
        }

        static bool IsLive(Prism prism) => prism && !prism.destroyed && prism.gameObject.activeInHierarchy;

        /// <summary>Every live hull prism, nearest to <paramref name="near"/> first, into <paramref name="into"/>
        /// (cleared first). Menu-rate: a sort over the hull.</summary>
        public void GetLiveHullPrisms(Vector3 near, List<Prism> into)
        {
            into.Clear();
            for (int i = 0; i < _prisms.Count; i++)
                if (IsLive(_prisms[i])) into.Add(_prisms[i]);
            into.Sort((a, b) => (a.transform.position - near).sqrMagnitude
                .CompareTo((b.transform.position - near).sqrMagnitude));
        }

        /// <summary>
        /// The hull prism destroyed LAST, or null: the one whose force the fall is credited to
        /// (<see cref="ArkFall"/>). Read when the hull is lost; a prism the pool has already
        /// re-issued (no longer destroyed) is not a candidate.
        /// </summary>
        public Prism LastFallenHullPrism()
        {
            Prism last = null;
            float at = float.NegativeInfinity;
            for (int i = 0; i < _prisms.Count; i++)
            {
                var prism = _prisms[i];
                if (!prism || !prism.destroyed || prism.DestroyedAt < at) continue;
                at = prism.DestroyedAt;
                last = prism;
            }
            return last;
        }

        // ── Reactor ──────────────────────────────────────────────────────────

        /// <summary>
        /// Record one batch's slots: every lay is a slot, holding the prism laid for it (matched by the pose the lay
        /// stated, so a lay that failed leaves its slot empty for the reactor rather than shifting the rest).
        /// </summary>
        void AddSlots(ArkModel.LayBatch batch, List<Prism> laid, int first)
        {
            int start = _slots.Count;
            for (int i = 0; i < batch.Lays.Count; i++)
            {
                var lay = batch.Lays[i];
                _slots.Add(new HullSlot
                {
                    Parent = batch.Parent,
                    Lay = lay,
                    RidesBone = batch.RidesBone,
                    ArkLocal = batch.Parent
                        ? transform.InverseTransformPoint(batch.Parent.TransformPoint(lay.Point.Position))
                        : lay.Point.Position,
                });
            }
            for (int k = first; k < laid.Count; k++)
            {
                var prism = laid[k];
                if (!prism) continue;
                var at = prism.transform.localPosition;
                int best = -1;
                float bestSqr = 0.01f;
                for (int i = start; i < _slots.Count; i++)
                {
                    if (_slots[i].Prism) continue;
                    float d = (_slots[i].Lay.Point.Position - at).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; best = i; }
                }
                if (best >= 0) _slots[best].Prism = prism;
            }
        }

        /// <summary>The slot's prism is live and still the hull's (the hold re-parents what it hands a carrier).</summary>
        static bool IsSeated(HullSlot slot) =>
            slot.Prism && !slot.Prism.destroyed && slot.Prism.gameObject.activeInHierarchy
            && slot.Prism.transform.parent == slot.Parent;

        Transform ReactorTransform => _model ? _model.ReactorPivot : transform;

        /// <summary>
        /// The fixed rate: one beat every <see cref="CosmicShore.ScriptableObjects.ArkReactorSO.RegrowSeconds"/>, and a
        /// beat regrows one prism when a slot is empty. A beat with nothing to regrow is not saved up.
        /// </summary>
        /// <summary>
        /// Whether the reactor may regrow right now (default true). The Ark escort gates it (Garrett, 2026-10-31): the Ark
        /// heals only inside the nucleus of a world the squad controls. A closed gate is not saved up: beats that fall
        /// while closed are lost, exactly as a beat with nothing to regrow is.
        /// </summary>
        public bool RegrowAllowed { get; set; } = true;

        void TickReactor()
        {
            if (!_reactorDefinition || _retiring) return;
            float now = Time.time;
            float period = _reactorDefinition.RegrowSeconds;
            if (_layComplete && !_hullLost && ArkReactor.Beat(ref _nextRegrowAt, now, period) && RegrowAllowed && RegrowOne())
                _lastRegrowAt = now;

            if (_model) _model.SetReactor(now - _lastRegrowAt < 2f * period);
        }

        /// <summary>
        /// Grow one prism back into the empty slot nearest the reactor, through the canonical lay (it grows in on the
        /// prism's own clock: nothing pops). A slot the food web emptied keeps its place in the hull list (the
        /// full-health count already holds it); a slot the hold emptied gets its plating back, so the full-health count
        /// rises with it.
        /// </summary>
        bool RegrowOne()
        {
            _slotPositions.Clear();
            _slotEmpty.Clear();
            bool any = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool empty = slot.Parent && !IsSeated(slot);
                _slotPositions.Add(slot.ArkLocal);
                _slotEmpty.Add(empty);
                any |= empty;
            }
            if (!any) return false;

            int pick = ArkReactor.NearestEmpty(_slotPositions, _slotEmpty,
                transform.InverseTransformPoint(ReactorTransform.position));
            if (pick < 0) return false;
            var target = _slots[pick];

            var prism = PrismTrailBuilder.LayOne(_hullPrefab, target.Lay, target.Parent, _trail, RegrowOwnerId,
                admitAuthoredScale: _model != null, watchForReveal: false);
            if (!prism) return false;
            // A hull prism is a mover's: it must not queue behind a growing corridor for its creation completion.
            prism.CompletesAsLiveRibbon = true;

            NoteSlotLoss(target);
            int at = IndexOfHullPrism(target.Prism);
            if (at >= 0)
            {
                _prisms[at] = prism;
            }
            else
            {
                _prisms.Add(prism);
                TotalCount++;
            }
            target.Prism = prism;
            target.Counted = true;
            _aliveCount = Mathf.Min(TotalCount, _aliveCount + 1);
            if (target.RidesBone && _model) _model.AddRider(prism);

            FMODOneShotVolumeHelper.PlaySFXOneShot(_reactorDefinition.RegrowSound, prism.transform.position, 1f);
            HullPrismRegrown?.Invoke(prism);
            return true;
        }

        /// <summary>
        /// Where <paramref name="prism"/> sits in the hull list, by REFERENCE, or -1 when it left the hull (the hold took
        /// it) or was never laid. Never by Unity equality: a destroyed prism compares equal to null and to every other
        /// destroyed one, so a lost slot would read as "taken" and its regrowth would count the plating twice.
        /// </summary>
        int IndexOfHullPrism(Prism prism)
        {
            if (ReferenceEquals(prism, null)) return -1;
            for (int i = 0; i < _prisms.Count; i++)
                if (ReferenceEquals(_prisms[i], prism)) return i;
            return -1;
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

            // CATCH UP, don't skip. One prism per frame is a rate, and the Ark's speed is not:
            // it runs fast leaving a cell and slow on the approach, and the corridor's own
            // builds hitch the frame. A single test per Update lays ONE prism however far the
            // ship travelled and then moves the mark to where it now is, so every hitch and
            // every fast stretch silently swallows the prisms that stretch was owed - which
            // reads exactly as a wake arriving in inconsistent batches with gaps between them.
            // Stepping the mark by one spacing per prism keeps the wake evenly spaced in
            // DISTANCE, which is the only thing it was ever meant to be even in.
            //
            // The per-frame cap is what keeps that honest: a several-thousand-unit jump (a
            // teleport, a long stall) must not lay a thousand prisms in one frame to cover
            // ground nobody watched the ship cross. Past the cap the mark snaps forward, so the
            // gap is taken deliberately, once, instead of accumulating silently every frame.
            const int MaxWakePrismsPerTick = 8;
            float spacingSqr = _wakeSpacing * _wakeSpacing;
            int laid = 0;
            while ((transform.position - _lastWakeAt).sqrMagnitude >= spacingSqr)
            {
                if (laid >= MaxWakePrismsPerTick)
                {
                    _lastWakeAt = transform.position;
                    break;
                }

                // Step the mark ALONG the travelled line rather than to the ship: the ship is
                // already past this prism's place by the time we lay it.
                Vector3 toShip = transform.position - _lastWakeAt;
                _lastWakeAt += toShip.normalized * _wakeSpacing;
                LayWakePrism(_lastWakeAt);
                laid++;
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

        /// <param name="at">Where the ship WAS when this prism was owed - not where it is now.
        /// A catch-up tick lays several in one frame, and laying them all at the live position
        /// would stack them in one place instead of spacing them along the wake.</param>
        void LayWakePrism(Vector3 at)
        {
            // Astern and a little below the keel - a wake trails a ship, it does not run
            // through it, and laying inside the hull would put the two in the same grid cell.
            var pose = new SpawnPoint(
                at - transform.forward * (_wakeScale.z * 1.5f),
                Quaternion.LookRotation(transform.forward, transform.up),
                _wakeScale);

            // watchForReveal: FALSE. The wake is a CONTINUOUS source - it lays for as long as the
            // voyage runs - and the reveal watch is the arena-ready gate's "has everything
            // materialized" set. A source that keeps adding to it can never all-clear, so the
            // load veil holds forever with its settling count jittering: a hang, not a slow load.
            var prism = PrismTrailBuilder.LayOne(_wakePrefab,
                new PrismLay(pose, _wakeDomain), _wakeRoot, _wakeTrail, WakeOwnerId,
                watchForReveal: false);
            if (!prism) return;
            // The wake is a ribbon behind a mover: it must not queue behind a growing
            // corridor's flora for its creation completion.
            prism.CompletesAsLiveRibbon = true;

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
            _useProfile = false;
            _approachSpeed = Mathf.Max(1f, approachSpeed);
            _cruiseSpeed = Mathf.Max(_approachSpeed, cruiseSpeed);
            _slowRadius = Mathf.Max(0f, slowRadius);
            _destinationRadius = _slowRadius;
        }

        /// <summary>
        /// The cell-aware profile (<see cref="ArkSpeedProfile"/>; the Ark escort, Garrett 2026-10-31: the Ark slowly enters
        /// a cell and crawls through its cytoplasm under attack, with short gaps between cells). Still a pure function of
        /// position: range past the destination membrane (<see cref="SetDestination(Vector3, float)"/>) and past the
        /// membrane of the cell just left (<see cref="SetDeparture"/>). The legacy overload above turns it off.
        /// </summary>
        public void SetSpeedProfile(in ArkSpeedProfile profile)
        {
            _profile = profile.Sanitized();
            _useProfile = true;
            _approachSpeed = _profile.Crawl;
            _cruiseSpeed = _profile.Cruise;
        }

        /// <summary>The cell the Ark is leaving: its centre and membrane radius. The crawl holds until the stern clears
        /// it (cell-aware profile only).</summary>
        public void SetDeparture(Vector3 centre, float membraneRadius)
        {
            _departure = centre;
            _departureRadius = Mathf.Max(0f, membraneRadius);
            _hasDeparture = true;
        }

        /// <summary>Forget the departed cell (open water behind).</summary>
        public void ClearDeparture() => _hasDeparture = false;

        /// <summary>
        /// Whether the Ark is MAKING WAY. False until the voyage actually begins, and it is a
        /// gate on the movement itself rather than a convention about when to set a destination.
        ///
        /// It exists because the Ark is created and its hull laid BEHIND THE LOAD VEIL, and
        /// `Update` keeps running the whole time the screen is covered — so an Ark with a course
        /// sails for the entire build. At the cruise speed the arrival profile gives it (72 u/s),
        /// a 40-second build puts it 2,880 units downrange: past the first cell, parked at a core
        /// the player cannot see, and the voyage opens with no Ark anywhere. It was survivable at
        /// the old flat 18 u/s and is not at 4x that.
        ///
        /// General shape: <b>a build behind a veil is not a pause</b> — every Update in the scene
        /// runs through it, so anything that moves has to be told not to yet.
        /// </summary>
        public void SetUnderway(bool underWay) => _underWay = underWay;

        /// <summary>Point the voyage at a world position; the Ark cruises there on its own —
        /// once <see cref="SetUnderway"/> has been called.</summary>
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
            _destinationRadius = _slowRadius;
            SetDestination(worldPosition);
        }

        /// <summary>True when the Ark is within <paramref name="within"/> of its destination.</summary>
        public bool HasArrived(float within) =>
            _hasDestination && (transform.position - _destination).sqrMagnitude <= within * within;

        void Update()
        {
            if (_retiring) return;

            bool moved = false;
            if (_underWay && _hasDestination && !_hullLost)
            {
                Vector3 to = _destination - transform.position;
                float dist = to.magnitude;
                if (dist > 0.5f)
                {
                    // Smoothstep over the approach band: flat cruise outside it, flat approach
                    // speed at the core, and no discontinuity at either end — an Ark that
                    // stepped between two speeds would read as a stutter, not as a landing.
                    if (_useProfile)
                    {
                        float dIn = dist - _destinationRadius;
                        float dOut = _hasDeparture
                            ? Vector3.Distance(transform.position, _departure) - _departureRadius
                            : float.PositiveInfinity;
                        _insideCell = dIn < 0f || dOut < 0f;
                        _speed = _profile.SpeedAt(dIn, dOut);
                    }
                    else
                    {
                        float t = _slowRadius > 1f ? Mathf.Clamp01(dist / _slowRadius) : 1f;
                        _insideCell = dist < _slowRadius;
                        _speed = Mathf.Lerp(_approachSpeed, _cruiseSpeed, t * t * (3f - 2f * t));
                    }

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

            TickReactor();
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

        static bool IsGone(Prism prism) => !prism || prism.destroyed || !prism.gameObject.activeInHierarchy;

        /// <summary>A counted slot whose prism is gone is a loss, raised once; a prism the hold took leaves quietly.</summary>
        void NoteSlotLoss(HullSlot slot)
        {
            if (!slot.Counted) return;
            if (ReferenceEquals(slot.Prism, null) || IsGone(slot.Prism))
            {
                slot.Counted = false;
                HullPrismLost?.Invoke(slot.Prism, slot.Lay.Kind);
            }
            else if (!IsSeated(slot)) slot.Counted = false;
        }

        void ScanHullIntegrity()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Counted) NoteSlotLoss(slot);
                else if (IsSeated(slot)) slot.Counted = true;
            }

            int alive = 0;
            for (int i = 0; i < _prisms.Count; i++)
            {
                var prism = _prisms[i];
                if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy)
                    alive++;
            }
            _aliveCount = alive;

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

            _underWay = false;

            // Every parcel still riding a carrier is hull the Ark no longer holds, so the
            // retire below would never reach it: the hold spends them first.
            if (Hold) Hold.StrikeParcels();

            // Disarm the wake FIRST, before any await. A retire is followed by the next voyage's
            // veiled build, and this method waits out a lay and then a 0.8s wither - so a wake
            // left armed keeps laying into the successor's arena hold. StrikeWake at the bottom
            // is the cleanup; this is the stop.
            _wakeArmed = false;

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
