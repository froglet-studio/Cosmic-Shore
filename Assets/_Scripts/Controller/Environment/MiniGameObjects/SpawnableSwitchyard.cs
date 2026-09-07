using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Switchyard" - the arena of <see cref="GameModes.Hijack"/>, and the first environment
    /// in the game built to be RIDDEN rather than flown through or shot at.
    ///
    /// Three great-circle RAILS ring a hollow core, meeting at spiny BURRS of raw prism where the
    /// rings cross. Every rail is an open ribbon the Urchin latches onto and grinds; every burr is
    /// a solid it rolls over. Both are ordinary conserved mass in an ordinary domain colour, so
    /// every verb the mode has - grind, convert, launch, rake - is the platform's, applied to an
    /// arena shaped to invite it.
    ///
    /// <para><b>The launch contract is the whole arena, and it is EXACT.</b> Each rail is the arc
    /// from theta_j + <see cref="railHalfGapDegrees"/> to theta_(j+1) - <see cref="railHalfGapDegrees"/>.
    /// A circle's tangent at an angle <c>g</c> short of a station passes through that station's
    /// RADIAL at radius <c>R / cos g</c>, a distance <c>R * tan g</c> further on - so placing every
    /// burr centre at exactly <c>R / cos g</c> means a pilot who grinds a rail to its end and does
    /// NOT STEER flies straight into the cluster it points at. The reward for launching is
    /// geometry, not a bonus (see HIJACK.md "Why the launch pays nothing"). That claim is proved
    /// offline in <c>Tools/Build/hijack_budget.py</c>, which walks each rail's real prism
    /// positions and measures the angle to the burr; the C# below is a transcription of the model
    /// it proves, not the other way round. <b>Change a number here and re-run that script.</b></para>
    ///
    /// <para><b>Prism spacing is DERIVED, and that is load-bearing.</b> The 40 prisms span the arc
    /// ENDPOINT TO ENDPOINT, so the terminal prism sits exactly on the launch tangent. Centring a
    /// round 8u spacing inside the arc instead insets that prism by ~1u and tilts the launch
    /// 0.32 degrees off the burr - caught by the proof before a line of this file was written.</para>
    ///
    /// <para><b>CLOSED FORM - there is no <c>System.Random</c> draw anywhere in
    /// <see cref="BuildEnvironment"/>.</b> Every count is arithmetic and every position a
    /// trig expression, which is what lets the Python model mirror this file exactly rather than
    /// estimate it, and what lets <c>author_hijack_assets.py</c> derive the cell's PhaseThresholds
    /// from the same numbers that build the arena. The inherited <c>seed</c> and <c>density</c>
    /// knobs are therefore inert here by design; do not introduce a draw to "add variety" without
    /// giving up the mirror.</para>
    ///
    /// <para><b>PAINTING: the full triad, exactly equal per domain, and no Blue.</b> Each rail is
    /// three THIRDS from its low-theta end, rotated by <c>(j + k)</c>, so every rail offers every
    /// domain a fast stretch (fair from any spawn slot) and the 15x speed cliff at each boundary IS
    /// the lesson that "my colour is fast, yours is loot". Each big burr wears the THIRD domain of
    /// the two rings that cross there, so the biggest prizes are hostile to both approaches by
    /// construction. A two-domain lobby leaves the third colour's mass symmetric unclaimed loot -
    /// which is why it is painted a real domain rather than <see cref="Domains.Blue"/>.</para>
    ///
    /// <para><b>INTENSITY scales burr mass ONLY</b> - the rail network, the radii, the launch gaps
    /// and the spawn ring are identical at every level, so the arena's shape, its aiming and its
    /// spawn geometry never move. A bigger yard is a longer, more contested match at a fixed
    /// target, not a scarcer one. Four prefab variants differ only in
    /// <see cref="bigBurrShells"/> / <see cref="smallBurrShells"/>.</para>
    ///
    /// <para><b>COLLIDER BUDGET: every prism is <see cref="PrismKind.Plain"/>.</b> Zero always-on
    /// mesh colliders are authored - no shielded, super-shielded or danger mass anywhere - so the
    /// active count is bounded by <c>PrismColliderLodManager</c>'s radius rather than by the 2,772
    /// (I1) to 9,930 (I4) population. The only mesh colliders that can ever appear are the ones a
    /// MASS-5 pilot creates by riding their own colour, which is a player act on a player's own
    /// mass, bounded by how much rail one pilot can cover.</para>
    ///
    /// DETERMINISM: clients build locally with no seed sync. Closed form means every peer lays a
    /// byte-identical arena for free.
    /// </summary>
    public class SpawnableSwitchyard : CellEnvironmentSpawnableBase
    {
        // ── The ring network (identical at every intensity) ──────────────────

        [Header("Rings")]
        [Tooltip("Great-circle radius of all three rings. FIXED across intensities: the spawn " +
                 "shell, the burr radius and the launch gap are all defined against it. Keep the " +
                 "outermost mass (ringRadius/cos(halfGap) + big burr radius) well inside the " +
                 "r=1200 membrane.")]
        [SerializeField] float ringRadius = 900f;

        [Tooltip("Stations per ring, evenly spaced. EVEN stations are the ring's axis crossings " +
                 "(shared with another ring) and carry BIG burrs; ODD stations carry small ones. " +
                 "8 is the shipped value and the one the geometry proof is written against.")]
        [SerializeField, Min(4)] int stationsPerRing = 8;

        [Tooltip("How far short of each station a rail stops, in degrees - so it is the LAUNCH " +
                 "GAP and the burr radius in one number: the burr sits at ringRadius/cos(this) " +
                 "from the core, ringRadius*tan(this) beyond the rail's end. Widen it and the " +
                 "flight is longer and the aim more forgiving; narrow it and rails nearly touch " +
                 "their burrs. Re-run Tools/Build/hijack_budget.py after any change.")]
        [SerializeField, Range(2f, 20f)] float railHalfGapDegrees = 12.5f;

        [Tooltip("Prisms per rail, spread endpoint to endpoint along the arc. Spacing is DERIVED " +
                 "from this and the arc length - never authored - because only a prism sitting " +
                 "exactly ON the arc's end lies on the tangent that aims at the burr.")]
        [SerializeField, Min(4)] int railPrisms = 40;

        [Tooltip("Every prism in the yard. (3,3,6) is the Track Projector's own prism, so a rail " +
                 "a pilot projects reads as arena rail. Local +Z runs ALONG a rail (the invariant " +
                 "the 1D ride rests on) and RADIALLY OUT of a burr (a 6-long spine).")]
        [SerializeField] Vector3 prismScale = new(3f, 3f, 6f);

        // ── Burrs (the intensity dial) ───────────────────────────────────────

        [Header("Burrs (the intensity dial)")]
        [Tooltip("Concentric Fibonacci shells in a BIG burr - the six axis crossings, reachable " +
                 "from two rings each and therefore the most contested mass in the yard. Shell s " +
                 "holds round(4*pi*s*s) prisms at radius s*shellPitch, so the spine spacing stays " +
                 "roughly constant as the burr grows. THE intensity dial: 3/4/5/6.")]
        [SerializeField, Range(1, 8)] int bigBurrShells = 3;

        [Tooltip("Shells in a small burr - the twelve mid-arc stations, one ring's own. 2/2/3/3.")]
        [SerializeField, Range(1, 8)] int smallBurrShells = 2;

        [Tooltip("Radius step between shells. ~10 against a 6-long spine leaves gaps a spike " +
                 "volley passes through into the interior, and an outer-shell spacing the marble " +
                 "roll's ground search bridges cleanly.")]
        [SerializeField, Min(2f)] float shellPitch = 10f;

        // ── Orientation ──────────────────────────────────────────────────────

        [Header("Orientation")]
        [Tooltip("Whole-yard rotation about world Y, applied as the LAST build step. Aligns the " +
                 "rail midpoints with the equatorial spawn ring the mode's scene authors " +
                 "(spawnFormation EquatorialRing), so every pilot opens the match looking at a " +
                 "rail rather than into the gap between two.")]
        [SerializeField] float yawDegrees = 22.5f;

        /// <summary>Closed form: the seed is never consulted. Kept because the base contract
        /// requires it and because a future variant might want a draw.</summary>
        protected override int DefaultSeed => 45;

        protected override int LayCapacity => 12000;

        /// <summary>
        /// One laid structure: a contiguous slice of <see cref="CellEnvironmentSpawnableBase._cachedLays"/>
        /// plus everything <see cref="HijackYard"/> needs to describe it. The base class lays every
        /// environment as ONE trail, which is exactly wrong here twice over: a rail must be its own
        /// OPEN ribbon (a shared trail has one pair of ends, so 23 of the 24 rails could never
        /// launch), and a burr must declare itself a <see cref="PrismscapeDimension.Volume"/> or
        /// <c>PrismscapeTopology.DimensionOf</c> reads the authored trail and routes a rider onto
        /// the 1D grind through a solid.
        /// </summary>
        readonly struct Segment
        {
            public readonly int Start;
            public readonly int Count;
            public readonly PrismscapeDimension Dimension;
            public readonly string Label;
            /// <summary>Burr: its centre. Rail: the far END, the point the launch leaves from.</summary>
            public readonly Vector3 Anchor;
            /// <summary>Rail: its low-theta START (where a pilot latches on). Burr: unused.</summary>
            public readonly Vector3 Origin;
            /// <summary>Burr: its radius. Rail: 0.</summary>
            public readonly float Radius;
            /// <summary>Burr: the colour it was LAID in. Rail: the domain of its first third.</summary>
            public readonly Domains Painted;
            public readonly bool IsBurr;
            public readonly bool BigBurr;
            /// <summary>Rail only: the centre of the burr its far end aims at, matched to a burr
            /// INDEX by proximity once every burr has been registered.</summary>
            public readonly Vector3 TargetCentre;

            public Segment(int start, int count, PrismscapeDimension dimension, string label,
                           Vector3 anchor, Vector3 origin, float radius, Domains painted,
                           bool isBurr, bool bigBurr, Vector3 targetCentre)
            {
                Start = start; Count = count; Dimension = dimension; Label = label;
                Anchor = anchor; Origin = origin; Radius = radius; Painted = painted;
                IsBurr = isBurr; BigBurr = bigBurr; TargetCentre = targetCentre;
            }
        }

        /// <summary>
        /// How close a rail's named target must be to a burr centre to BE that burr. Generous by
        /// design: the shipped yard's burr centres are 705u apart at their closest and a rail's
        /// target lands within 1e-13u of its own, so any tolerance between those two numbers is
        /// the same answer. It is a nearest-match rather than an exact one because the two
        /// expressions that produce an axis crossing are not bit-identical - a big burr is laid
        /// at <c>RingA[axis] * r</c> while a rail names <c>RingPoint(k, theta, r)</c>, and float
        /// <c>cos(pi/2)</c> is not exactly zero.
        ///
        /// <para>A quantize-to-whole-units key was written first and REJECTED after the offline
        /// model measured the worst burr coordinate sitting 0.049 of a unit from a .5 boundary.
        /// It works today, but it is a hash of a float coincidence: nudge the yaw or the radius
        /// and a coordinate lands on the boundary, float32 and float64 round it opposite ways,
        /// the lookup misses, and that rail's launch silently aims at nothing. Proximity has no
        /// boundary to land on. <b>General rule: resolving identity by rounding a float is a
        /// tolerance with a cliff in the middle of it.</b></para>
        /// </summary>
        const float BurrMatchRadius = 50f;

        readonly List<Segment> _segments = new(64);

        // ── Derived geometry (one definition, read by build AND by the editor gizmo) ──

        float HalfGapRad => railHalfGapDegrees * Mathf.Deg2Rad;

        /// <summary>Distance from the core to a burr centre. The one number that makes the launch
        /// aimed: the rail's end tangent passes through the station radial exactly here.</summary>
        public float BurrRadiusFromCore => ringRadius / Mathf.Cos(HalfGapRad);

        /// <summary>Rail end to burr centre - the length of the unpowered flight.</summary>
        public float LaunchGap => ringRadius * Mathf.Tan(HalfGapRad);

        public float BigBurrRadius => shellPitch * bigBurrShells;
        public float SmallBurrRadius => shellPitch * smallBurrShells;

        /// <summary>The three great circles, parametrised so a 120-degree turn about (1,1,1) maps
        /// ring k to ring k+1 - which is what makes the painting below provably 3-fold symmetric
        /// rather than symmetric-looking.</summary>
        static readonly Vector3[] RingA = { Vector3.right, Vector3.up, Vector3.forward };
        static readonly Vector3[] RingB = { Vector3.up, Vector3.forward, Vector3.right };

        /// <summary>The playable triad, in ActiveDomains order. Blue is the no-team sentinel and
        /// is deliberately absent: unclaimed mass here is a REAL domain's, so a two-domain lobby
        /// finds the third colour's mass hostile to both sides and (by the symmetry) equidistant.</summary>
        static readonly Domains[] Triad = { Domains.Jade, Domains.Ruby, Domains.Gold };

        Vector3 RingPoint(int k, float theta, float radius) =>
            radius * (Mathf.Cos(theta) * RingA[k] + Mathf.Sin(theta) * RingB[k]);

        Vector3 RingTangent(int k, float theta) =>
            (-Mathf.Sin(theta) * RingA[k] + Mathf.Cos(theta) * RingB[k]).normalized;

        float StationTheta(int j) => j * (2f * Mathf.PI / stationsPerRing);

        // ── Build ────────────────────────────────────────────────────────────

        protected override void BuildEnvironment()
        {
            _segments.Clear();

            BuildRails();
            BuildBurrs();
        }

        /// <summary>
        /// 24 rails: one open ribbon per (ring, station). Each is laid low-theta first, which is
        /// also the order the painting's thirds run in and the order <c>TrailFollower</c> indexes -
        /// so "the far end" is a stable notion for the AI and for the launch.
        /// </summary>
        void BuildRails()
        {
            float gap = HalfGapRad;

            for (int k = 0; k < 3; k++)
            {
                for (int j = 0; j < stationsPerRing; j++)
                {
                    int start = _cachedLays.Count;

                    float t0 = StationTheta(j) + gap;
                    float t1 = StationTheta(j + 1) - gap;

                    for (int i = 0; i < railPrisms; i++)
                    {
                        // Endpoint to endpoint - see the class header. The terminal prism MUST
                        // land on t1 or the launch stops being aimed.
                        float theta = Mathf.Lerp(t0, t1, i / (float)(railPrisms - 1));
                        Vector3 pos = RingPoint(k, theta, ringRadius);
                        Vector3 tangent = RingTangent(k, theta);
                        Vector3 outward = pos.normalized;

                        // Local +Z along the rail, +Y outward: the pose the 1D ride expects, and
                        // the pose the Track Projector lays, so a projected rail is arena rail.
                        Emit(Yawed(pos), Yawed(Quaternion.LookRotation(tangent, outward)),
                             prismScale, RailDomain(k, j, i));
                    }

                    // The far end IS the launch point, and the burr it aims at sits on the next
                    // station's radial at BurrRadiusFromCore - the whole launch contract, in one
                    // expression, recorded here so nothing downstream has to re-derive it.
                    Vector3 railEnd = Yawed(RingPoint(k, t1, ringRadius));
                    Vector3 railStart = Yawed(RingPoint(k, t0, ringRadius));
                    Vector3 target = Yawed(RingPoint(k, StationTheta(j + 1), BurrRadiusFromCore));

                    _segments.Add(new Segment(start, _cachedLays.Count - start,
                        PrismscapeDimension.Trail, $"RAIL{k}-{j}",
                        railEnd, railStart, 0f, RailDomain(k, j, 0), false, false, target));
                }
            }
        }

        /// <summary>
        /// 6 big burrs on the axis crossings + 12 small ones mid-arc. Each is one Volume: a solid
        /// of radial spines the ride rolls over on its boundary, which is what it honestly is.
        /// </summary>
        void BuildBurrs()
        {
            float r = BurrRadiusFromCore;

            // The six axis crossings. Each is shared by exactly two rings, and wears the THIRD
            // domain - always hostile to both rings that launch into it.
            for (int axis = 0; axis < 3; axis++)
            {
                // Ring k passes through axis a iff a is RingA[k] or RingB[k]; with the cyclic
                // basis above, axis a is shared by rings a and (a+2)%3.
                var domain = Triad[(3 - axis - (axis + 2) % 3) % 3];
                Vector3 dir = RingA[axis];
                EmitBurr(dir * r, bigBurrShells, domain, $"BIGBURR+{axis}", true);
                EmitBurr(-dir * r, bigBurrShells, domain, $"BIGBURR-{axis}", true);
            }

            // The mid-arc stations: one ring's own, so no crossing to resolve.
            for (int k = 0; k < 3; k++)
                for (int j = 1; j < stationsPerRing; j += 2)
                    EmitBurr(RingPoint(k, StationTheta(j), r), smallBurrShells,
                             Triad[(k + (j - 1) / 2) % 3], $"BURR{k}-{j}", false);
        }

        /// <summary>
        /// One burr: concentric Fibonacci shells, <c>round(4*pi*s*s)</c> spines on shell s. Every
        /// spine points RADIALLY OUT of the burr centre, so the cluster reads as a sea urchin and
        /// the outward face a rider rolls on is the flat end of a 6-long prism.
        /// </summary>
        void EmitBurr(Vector3 centre, int shells, Domains domain, string label, bool big)
        {
            int start = _cachedLays.Count;

            for (int s = 1; s <= shells; s++)
            {
                int n = Mathf.RoundToInt(4f * Mathf.PI * s * s);
                float shellRadius = s * shellPitch;

                for (int i = 0; i < n; i++)
                {
                    float y = 1f - 2f * (i + 0.5f) / n;
                    float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    float phi = i * GoldenAngle;
                    Vector3 dir = new(ring * Mathf.Cos(phi), y, ring * Mathf.Sin(phi));

                    // Any stable perpendicular will do for the up - the spine is what reads, and
                    // a prism square in cross-section has no preferred roll.
                    Vector3 up = Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.up;

                    Emit(Yawed(centre + dir * shellRadius),
                         Yawed(Quaternion.LookRotation(dir, up)), prismScale, domain);
                }
            }

            _segments.Add(new Segment(start, _cachedLays.Count - start,
                PrismscapeDimension.Volume, label, Yawed(centre), Vector3.zero,
                shells * shellPitch, domain, true, big, Vector3.zero));
        }

        // ── Painting ─────────────────────────────────────────────────────────

        /// <summary>
        /// Rail (k, j) in three THIRDS from its low-theta end, the run rotated by (j + k). Every
        /// rail therefore offers every domain a fast stretch - fair from any spawn slot - and the
        /// speed cliff at each boundary is the mode's own tutorial.
        /// </summary>
        Domains RailDomain(int k, int j, int i)
        {
            int a = railPrisms - 2 * (railPrisms / 3);      // the first third takes the remainder
            int b = railPrisms / 3;
            int third = i < a ? 0 : (i < a + b ? 1 : 2);
            return Triad[((j + k + third) % 3 + 3) % 3];
        }

        // ── Yaw (the last build step) ────────────────────────────────────────

        Vector3 Yawed(Vector3 p) => Quaternion.Euler(0f, yawDegrees, 0f) * p;
        Quaternion Yawed(Quaternion q) => Quaternion.Euler(0f, yawDegrees, 0f) * q;

        // ── Laying: one trail per structure ──────────────────────────────────

        /// <summary>
        /// Lays every segment as its own trail and publishes the yard's map of itself.
        ///
        /// <para>Segments are laid SEQUENTIALLY behind one <c>BeginArenaBuild</c> bracket rather
        /// than fired off concurrently. The lay budget is a shared per-frame counter, so 42
        /// concurrent lays would still only place a frame's worth of prisms - but each would first
        /// request its own 256-prism async CLONE batch, i.e. the whole arena cloned in one frame,
        /// which is the exact spike the budget exists to prevent. The bracket is what keeps the
        /// arena-ready gate closed across the gaps between segments, when no lay is in flight and
        /// an absence-of-activity check would misread the pause as "arena done".</para>
        ///
        /// <para>The trails and the <see cref="HijackYard"/> map are built UP FRONT, before any
        /// prism is laid: a Trail is a live object the follower reads as it fills, so a rider can
        /// latch onto a rail that is still being laid, and the AI can plan against a yard whose
        /// prisms have not all arrived. Nothing here waits on the lay.</para>
        /// </summary>
        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (prism == null || _cachedLays == null || _segments.Count == 0) return;

            var trailsBySegment = new Trail[_segments.Count];
            var yard = container.AddComponent<HijackYard>();

            // Pass 1 - burrs, so every rail can name one by INDEX. A big burr is registered once
            // and found by both of the rings that launch into it.
            var burrCentres = new List<Vector3>(18);
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                trailsBySegment[i] = NewTrail(seg);
                if (!seg.IsBurr) continue;

                yard.AddBurr(seg.Anchor, seg.Radius, seg.BigBurr, seg.Painted, trailsBySegment[i]);
                burrCentres.Add(seg.Anchor);
            }

            // Pass 2 - rails, matched to the burr their far end aims at. 24 x 18 distance tests,
            // once, at build time.
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                if (seg.IsBurr) continue;
                yard.AddRail(seg.Origin, seg.Anchor,
                             NearestBurr(burrCentres, seg.TargetCentre), trailsBySegment[i]);
            }

            if (Application.isPlaying)
                LaySegmentsAsync(container, trailsBySegment).Forget();
            else
                LaySegmentsSync(container, trailsBySegment);
        }

        async UniTaskVoid LaySegmentsAsync(GameObject container, Trail[] trailsBySegment)
        {
            // Announced BEFORE the first await: the gate must already be closed when the very
            // first frame of the build ticks it.
            PrismTrailBuilder.BeginArenaBuild();
            try
            {
                for (int i = 0; i < _segments.Count; i++)
                {
                    if (!container) return;
                    var seg = _segments[i];
                    await PrismTrailBuilder.LayBudgetedAsync(
                        prism, SegmentLays(seg), container.transform, trailsBySegment[i],
                        $"{container.name}::{seg.Label}", LayBudgetMsPerFrame);
                }
            }
            finally
            {
                PrismTrailBuilder.EndArenaBuild();
            }
        }

        void LaySegmentsSync(GameObject container, Trail[] trailsBySegment)
        {
            for (int i = 0; i < _segments.Count; i++)
                PrismTrailBuilder.LaySync(prism, SegmentLays(_segments[i]), container.transform,
                    trailsBySegment[i], $"{container.name}::{_segments[i].Label}");
        }

        /// <summary>The burr a rail's far end aims at, or -1 if none is within
        /// <see cref="BurrMatchRadius"/> - which on a correctly authored yard cannot happen, and
        /// which every consumer already treats as "this rail launches into empty space".</summary>
        static int NearestBurr(List<Vector3> centres, Vector3 target)
        {
            int best = -1;
            float bestSqr = BurrMatchRadius * BurrMatchRadius;
            for (int i = 0; i < centres.Count; i++)
            {
                float sqr = (centres[i] - target).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
            }
            return best;
        }

        /// <summary>Registers the trail with the base's list so cleanup and every trail consumer
        /// sees all 42 of them, not just the last.</summary>
        Trail NewTrail(in Segment seg)
        {
            // isLoop FALSE for a rail, explicitly: an open ribbon is what has ends to launch off.
            var trail = new Trail(false) { Dimension = seg.Dimension };
            trails.Add(trail);
            return trail;
        }

        /// <summary>
        /// One segment's lays, decimated INSIDE the segment. The base applies preview thinning to
        /// the whole cached list at once; doing that here would stride across segment boundaries
        /// and hand a rail somebody else's prisms.
        /// </summary>
        List<PrismLay> SegmentLays(in Segment seg)
        {
            var slice = new List<PrismLay>(seg.Count);
            for (int i = 0; i < seg.Count; i++)
                slice.Add(_cachedLays[seg.Start + i]);
            return PrismLayDecimation.Apply(slice);
        }

        // ── Cache contract ───────────────────────────────────────────────────

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                System.HashCode.Combine(ringRadius, stationsPerRing, railHalfGapDegrees, railPrisms),
                prismScale, bigBurrShells, smallBurrShells,
                System.HashCode.Combine(shellPitch, yawDegrees, /* layout revision */ 1));
    }
}
