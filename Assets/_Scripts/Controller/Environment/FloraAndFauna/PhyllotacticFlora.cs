using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A plant that grows the way real plants do: a small set of growing TIPS, each advancing
    /// along its own heading, pulled toward a growth axis, wandering a little, occasionally
    /// splitting - and, past a depth, opening <b>whorls</b> of leaves spaced at the golden angle
    /// (phyllotaxis, the same constant the canopies and shell patches in the authored
    /// environments use).
    ///
    /// <b>Why a third flora growth model.</b> The two we had are surfaces, not plants:
    /// <see cref="AssembledFlora"/> crystallises a triply-periodic minimal surface (gyroid,
    /// Schwarz P) out of bonded lattice sites, and <see cref="BranchingFlora"/> grows a
    /// crystaltropic scribble that only reads as structure in bulk. A GARDEN needs plants with a
    /// silhouette - a trunk that rises and opens a crown, a tendril that creeps along a trellis,
    /// a rosette that carpets a bed - and it needs them to be the SAME species varying by
    /// parameter, not three bespoke behaviours. So this is one growth model with a form dial:
    ///
    /// <list type="bullet">
    /// <item><b>Arbor</b> - one tip, strong axis tropism, branching, wide whorls: a garden tree.</item>
    /// <item><b>Tendril</b> - several tips, weak tropism, heavy wander, no whorls: a creeper.</item>
    /// <item><b>Rosette</b> - one tip, no rise, whorls from the first step: dense bed cover.</item>
    /// </list>
    ///
    /// Everything else is inherited and unchanged: prisms are conserved mass laid through the
    /// ordinary health-prism path, growth is gated on <see cref="Cell.FloraGrowingEnabled"/>
    /// (steady until Frenzy, no self-limit), the live-prism budget frees as fauna graze so a
    /// cropped plant regrows, sites are claimed in <see cref="PrismSpatialIndex"/> before the
    /// spawn (colliders are blind for the first 0.6s), death withers spindle-by-spindle from the
    /// extremities and drops the elemental crystal. No clock removes anything.
    /// </summary>
    public class PhyllotacticFlora : Flora
    {
        [Header("Form - stem")]
        [Tooltip("Growing tips seeded at the root. 1 = a single trunk; more = a clump/creeper.")]
        [SerializeField, Min(1)] int initialTips = 1;
        [Tooltip("Hard cap on simultaneously growing tips (bounds the per-tick decision cost).")]
        [SerializeField, Min(1)] int maxTips = 10;
        [SerializeField, Min(1)] int maxDepth = 26;
        [Tooltip("Maximum LIVE prisms this flora can hold. Consumption frees budget - a grazed " +
                 "plant regrows toward this cap instead of staying a permanent fragment.")]
        [SerializeField, Min(1)] int maxTotalSpawnedObjects = 400;
        [Tooltip("Tips advanced per grow tick.")]
        [SerializeField, Min(1)] int growthsPerTick = 3;
        [Tooltip("Instantiations executed per frame. The tick DECIDES (and claims sites); the " +
                 "drain spreads the prefab instantiation over frames - same pacing contract as " +
                 "AssembledFlora, throughput preserved.")]
        [SerializeField, Min(1)] int maxSpawnsPerFrame = 1;

        [Header("Form - segment")]
        [SerializeField, Min(0.1f)] float segmentLength = 15f;
        [Tooltip("Length multiplier per depth - <1 tapers the plant toward its tips.")]
        [SerializeField, Range(0.6f, 1.2f)] float segmentTaper = 0.97f;

        [Header("Form - heading")]
        [Tooltip("Per-step pull toward the growth axis (the planting site's normal, or outward " +
                 "from the cell centre when unplanted ground). 0 = a creeper that ignores up.")]
        [SerializeField, Range(0f, 1f)] float tropism = 0.35f;
        [Tooltip("Random deviation added to the heading each step - the difference between a mast " +
                 "and a vine.")]
        [SerializeField, Range(0f, 1f)] float wander = 0.2f;
        [Tooltip("Half-angle of the cone the initial tips are seeded into, around the growth axis.")]
        [SerializeField, Range(0f, 90f)] float spreadDegrees = 20f;

        [Header("Form - branching")]
        [SerializeField, Min(0)] int branchStartDepth = 3;
        [SerializeField, Range(0f, 1f)] float branchChance = 0.25f;
        [SerializeField, Range(0f, 90f)] float branchAngle = 34f;

        [Header("Form - whorl (the phyllotactic head)")]
        [Tooltip("Depth at which the plant starts opening leaf whorls. Above maxDepth = never " +
                 "(a bare tendril).")]
        [SerializeField, Min(0)] int whorlStartDepth = 6;
        [Tooltip("Open a whorl every N steps past the start depth.")]
        [SerializeField, Min(1)] int whorlEvery = 4;
        [SerializeField, Min(0)] int whorlLeaves = 5;
        [SerializeField, Min(0f)] float whorlRadius = 10f;
        [Tooltip("Extra whorl radius at full depth, as a fraction of the base radius - a crown " +
                 "that opens as it climbs.")]
        [SerializeField, Range(0f, 3f)] float whorlFlare = 0.8f;

        /// <summary>The phyllotaxis constant - the same golden angle the authored canopies use.</summary>
        const float GoldenAngle = 2.39996323f;

        // Site claim radius: below half a segment so a legitimate next site is never blocked,
        // above any per-step drift so a genuine duplicate always is (the GyroidAssembler rule).
        const float ClaimRadiusFraction = 0.4f;

        struct Tip
        {
            public GameObject gameObject;   // the spindle this tip grows from
            public Vector3 heading;
            public int depth;
            public float roll;              // golden-angle phase carried down the stem
        }

        struct SpawnOrder
        {
            public Tip parent;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 heading;
            public bool becomesTip;
            public float decidedAt;
        }

        // A Frenzy hold can outlive a site claim; an order whose claim lapsed could overlap
        // another grower. Dropped at drain - the next tick simply re-decides it.
        const float MaxOrderAgeSeconds = PrismSpatialIndex.ReservationTtlSeconds - 1f;

        readonly List<Tip> _tips = new();
        readonly Queue<SpawnOrder> _pending = new();
        Vector3 _axis = Vector3.up;

        public override void Initialize(Cell cell)
        {
            // Base plants us (honoring an authored site) and starts the grow cadence. The first
            // tick finds no tips and no prisms, so it no-ops - exactly like BranchingFlora, whose
            // trunks are also seeded after the base call.
            base.Initialize(cell);

            _axis = GrowthUp;
            SafeLookRotation.TrySet(transform, _axis, transform);
            SeedTips();
        }

        public override void Plant()
        {
            // A pinned site (a garden bed, or the Lifeform Matrix toy's spawn-here station) wins;
            // otherwise disperse across the cell like every other flora.
            if (TryGetPlantPositionOverride(out var pinned))
            {
                transform.position = pinned;
                return;
            }
            float radius = ResolvePlantRadius(legacyRadius: 150f);
            transform.position = cellData.CrystalTransform.position + radius * Random.onUnitSphere;
        }

        // ── Growth ────────────────────────────────────────────────────────────

        public override void Grow()
        {
            // Live-prism budget (frees as fauna graze - a cropped plant regrows).
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects) return;

            // Frenzy gate: steady growth until the cell tops out, then freeze and resume when an
            // active force brings the mass back down. Cell.FloraGrowingEnabled is the only gate.
            if (cell && !cell.FloraGrowingEnabled) return;

            if (_tips.Count == 0)
            {
                // Reawakening: every tip was grazed off or grew out. Re-sprout from survivors so
                // the plant keeps producing instead of sitting inert. Guarded on having survivors
                // so a freshly-planted flora doesn't double-seed before Initialize finishes.
                if (healthTracker != null && healthTracker.Count > 0) ReseedTips();
                return;
            }

            int decisions = Mathf.Min(growthsPerTick, _tips.Count);
            for (int i = 0; i < decisions; i++)
            {
                // Round-robin from the front so every tip advances, not just the first few.
                var tip = _tips[0];
                _tips.RemoveAt(0);
                if (!tip.gameObject || tip.depth >= maxDepth) continue;
                DecideStep(tip);
            }
        }

        void DecideStep(Tip tip)
        {
            float len = segmentLength * Mathf.Pow(segmentTaper, tip.depth);

            Vector3 heading = Vector3.Slerp(tip.heading, _axis, tropism * 0.3f);
            if (wander > 0f) heading += Random.onUnitSphere * wander;
            if (heading.sqrMagnitude < 0.0001f) heading = _axis;
            heading.Normalize();

            Vector3 pos = tip.gameObject.transform.position + heading * len;
            if (!Claim(pos, len))
            {
                // Site taken (a neighbour plant, or a sibling tip this same tick). Keep the tip
                // alive with a nudged heading - next tick it tries elsewhere. Nothing is lost.
                tip.heading = (heading + Random.onUnitSphere * 0.6f).normalized;
                Keep(tip);
                return;
            }

            _pending.Enqueue(new SpawnOrder
            {
                parent = tip,
                position = pos,
                rotation = SpawnPoint.LookRotation(heading, _axis),
                heading = heading,
                becomesTip = true,
                decidedAt = Time.time,
            });

            // A whorl at this node - the leaf head. Terminal leaves (they never become tips),
            // which is what bounds a mature plant's cost.
            int nodeDepth = tip.depth + 1;
            if (whorlLeaves > 0 && nodeDepth >= whorlStartDepth &&
                (nodeDepth - whorlStartDepth) % whorlEvery == 0)
                DecideWhorl(tip, pos, heading, nodeDepth);
        }

        void DecideWhorl(Tip tip, Vector3 node, Vector3 heading, int depth)
        {
            float t = maxDepth > 0 ? Mathf.Clamp01(depth / (float)maxDepth) : 0f;
            float radius = whorlRadius * (1f + whorlFlare * t);
            Vector3 basis = Vector3.Cross(heading, Mathf.Abs(Vector3.Dot(heading, _axis)) > 0.95f
                ? Vector3.right : _axis);
            if (basis.sqrMagnitude < 0.0001f) basis = Vector3.Cross(heading, Vector3.forward);
            basis.Normalize();

            for (int i = 0; i < whorlLeaves; i++)
            {
                float angle = tip.roll + i * GoldenAngle;
                Vector3 outward = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, heading) * basis;
                Vector3 pos = node + outward * radius;
                if (!Claim(pos, radius)) continue;

                _pending.Enqueue(new SpawnOrder
                {
                    parent = tip,
                    position = pos,
                    rotation = SpawnPoint.LookRotation(outward, heading),
                    heading = outward,
                    becomesTip = false,
                    decidedAt = Time.time,
                });
            }
        }

        static bool Claim(Vector3 position, float spacing)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return true;
            return index.TryReserve(position, Mathf.Max(1.5f, ClaimRadiusFraction * spacing));
        }

        void Update()
        {
            if (_pending.Count == 0) return;
            // Parity with the WaitForSeconds grow loop: frozen at timeScale 0 (menu pause).
            if (Time.timeScale <= 0f) return;
            // Orders decided just before Frenzy WAIT here (sites stay claimed) and execute when
            // growing re-enables - the same freeze-and-resume the tick gate gives.
            if (cell && !cell.FloraGrowingEnabled) return;

            int spawned = 0;
            while (spawned < maxSpawnsPerFrame && _pending.Count > 0)
            {
                var order = _pending.Dequeue();
                if (Time.time - order.decidedAt > MaxOrderAgeSeconds) continue;
                Execute(order);
                spawned++;
            }
        }

        void Execute(SpawnOrder order)
        {
            // The parent spindle may have been eaten between decision and drain; the claimed site
            // simply lapses with its TTL.
            if (!order.parent.gameObject) return;

            var newSpindle = Instantiate(spindle, order.parent.gameObject.transform);
            newSpindle.LifeForm = this;
            newSpindle.transform.position = order.position;
            newSpindle.transform.rotation = order.rotation;
            AddSpindle(newSpindle);

            var leaf = Instantiate(healthPrism, order.position, order.rotation);
            leaf.transform.SetParent(newSpindle.transform, false);
            leaf.transform.localPosition = Vector3.zero;
            leaf.transform.localRotation = Quaternion.identity;
            leaf.LifeForm = this;
            leaf.ChangeTeam(domain);
            AddHealthBlock(leaf);
            leaf.Initialize("flora");

            if (!order.becomesTip) return;

            Keep(new Tip
            {
                gameObject = newSpindle.gameObject,
                heading = order.heading,
                depth = order.parent.depth + 1,
                roll = order.parent.roll + GoldenAngle,
            });

            // Split: past the branch depth a node may fork, so the plant fills volume instead of
            // growing one wire. Bounded by maxTips.
            if (order.parent.depth + 1 >= branchStartDepth && _tips.Count < maxTips &&
                Random.value < branchChance)
            {
                Vector3 axis = Vector3.Cross(order.heading, _axis);
                if (axis.sqrMagnitude < 0.0001f) axis = Vector3.Cross(order.heading, Vector3.right);
                Vector3 forked = Quaternion.AngleAxis(branchAngle, axis.normalized) *
                                 (Quaternion.AngleAxis(Random.Range(0f, 360f), order.heading) * order.heading);
                Keep(new Tip
                {
                    gameObject = newSpindle.gameObject,
                    heading = forked.normalized,
                    depth = order.parent.depth + 1,
                    roll = order.parent.roll + GoldenAngle * 0.5f,
                });
            }
        }

        void Keep(Tip tip)
        {
            if (_tips.Count >= maxTips) return;
            _tips.Add(tip);
        }

        // ── Seeding / reawakening ─────────────────────────────────────────────

        void SeedTips()
        {
            for (int i = 0; i < initialTips; i++)
            {
                var root = AddSpindle();
                root.transform.position = transform.position;

                var leaf = Instantiate(healthPrism, transform.position, transform.rotation);
                leaf.transform.SetParent(root.transform, false);
                leaf.transform.localPosition = Vector3.zero;
                leaf.LifeForm = this;
                leaf.ChangeTeam(domain);
                AddHealthBlock(leaf);
                leaf.Initialize("flora");

                // Fan the initial tips around the axis at the golden angle so a multi-tip clump
                // never sends two shoots the same way.
                float roll = i * GoldenAngle;
                Vector3 basis = Vector3.Cross(_axis, Mathf.Abs(_axis.y) > 0.95f ? Vector3.right : Vector3.up)
                    .normalized;
                Vector3 tilt = Quaternion.AngleAxis(roll * Mathf.Rad2Deg, _axis) * basis;
                Vector3 heading = Quaternion.AngleAxis(spreadDegrees, tilt) * _axis;

                Keep(new Tip { gameObject = root.gameObject, heading = heading.normalized, depth = 0, roll = roll });
            }
        }

        /// <summary>
        /// Re-sprout tips from surviving spindles so a grazed plant recovers instead of sitting
        /// as a dead fragment. Survivors are sampled at random (not first-N) so repeated reseeds
        /// don't keep picking the same exhausted end of the plant.
        /// </summary>
        void ReseedTips()
        {
            var survivors = new List<Spindle>();
            foreach (var hp in healthTracker.All)
            {
                if (!hp) continue;
                var sp = hp.GetComponentInParent<Spindle>();
                if (sp) survivors.Add(sp);
            }
            if (survivors.Count == 0) return;

            int target = Mathf.Max(1, Mathf.Min(initialTips, maxTips));
            for (int i = 0; i < target && i < survivors.Count; i++)
            {
                int pick = Random.Range(i, survivors.Count);
                (survivors[i], survivors[pick]) = (survivors[pick], survivors[i]);
                Keep(new Tip
                {
                    gameObject = survivors[i].gameObject,
                    heading = (survivors[i].transform.forward + _axis).normalized,
                    // Regrowth restarts mid-plant, not from scratch: a re-sprouted shoot opens
                    // whorls again rather than climbing the whole stem a second time.
                    depth = Mathf.Max(0, whorlStartDepth - 1),
                    roll = Random.value * Mathf.PI * 2f,
                });
            }
        }

        protected override void Die(string killerName = "")
        {
            // The drain is not a coroutine, so Die's StopAllCoroutines does not stop it: without
            // this a dying plant keeps parenting fresh spindles onto an evaporating corpse
            // (stalls the wither wait, and hard-destroys the child with its spindle - a pop-out).
            _pending.Clear();
            _tips.Clear();
            base.Die(killerName);
        }

        public override void RemoveSpindle(Spindle spindle)
        {
            base.RemoveSpindle(spindle);
            if (!spindle) return;
            for (int i = _tips.Count - 1; i >= 0; i--)
                if (_tips[i].gameObject == spindle.gameObject) _tips.RemoveAt(i);
        }
    }
}
