using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Heap" - Jackstraw's arena, the Thresher's CHARGE game (<c>Assets/_Scripts/Controller/Arcade/JACKSTRAW.md</c>).
    /// A 0G heap of pick-up sticks with no table: straight rods pointing every way, each about a chain long
    /// (120 u against the Thresher's 22-140 u chain), so a swinging ball is always about to clip one.
    ///
    ///   • <b>Sticks</b> - <see cref="StickPrisms"/> flush prisms along one axis. Every prism is neutral
    ///     (<see cref="Domains.Blue"/>), so every team's ball breaks them alike and pays speed for each one
    ///     (the ball keeps <c>crushKeep</c> of its speed per kill below smash): a clean line to a rival is the
    ///     hot one, and the heap thins as the match goes on. <see cref="dangerTipEveryNth"/> sticks carry RED
    ///     TIPS (their two end prisms are danger); <see cref="ironwoodEveryNth"/> sticks carry an IRONWOOD heart
    ///     (their middle four prisms are shielded, two hits to cut).
    ///   • <b>Jacks</b> - three sticks crossed at right angles round a shielded knot cube: six arms of
    ///     <see cref="JackArmPrisms"/> leaving the knot's faces, so no two arms share volume.
    ///   • <b>Clearings</b> - bubbles no stick enters: one at the centre and a ring of them on a Fibonacci shell.
    ///     The controller floats the elemental crystals in them (<see cref="CollectClearings"/>), so refuelling
    ///     means leaving the cover of the heap.
    ///
    /// <para><b>0G.</b> Nothing here has an up: stick axes are uniform on the sphere and the clearings sit on a
    /// sphere, not a ring.</para>
    ///
    /// <para><b>Mirrored exactly by <c>Tools/Build/jackstraw_budget.py</c></b>, which the generator imports for the
    /// census, the volume and the PhaseThresholds. That is possible because every draw is <see
    /// CellEnvironmentSpawnableBase.Hash01"/> of an integer key (never the shared System.Random), every per-unit count
    /// is a const, and the only count-affecting rejections are deterministic geometry: a stick whose segment enters
    /// a clearing or comes within <see cref="StickGap"/> of another re-draws, at most <see cref="StickTries"/> times,
    /// then is skipped.</para>
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62): sticks keep <see cref="StickGap"/> between
    /// their axes (more than one section), a stick's prisms are laid a hair under their length (<see cref="Flush"/>),
    /// and a jack's arms start at the knot's faces.</para>
    ///
    /// <para><b>No intensity read.</b> The layout reads only the serialized fields below, one prefab per intensity,
    /// so each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by
    /// <c>Tools/Build/author_jackstraw_assets.py</c>.</para>
    /// </summary>
    public class SpawnableJackstraw : CellEnvironmentSpawnableBase
    {
        [Header("The heap")]
        [Tooltip("Radius (u) of the ball of space the sticks fill, around the arena centre. Every stick lies wholly inside it.")]
        [SerializeField, Min(60f)] float heapRadius = 470f;
        [Tooltip("Loose sticks in the heap (a stick that cannot find room after its tries is skipped).")]
        [SerializeField, Min(0)] int stickCount = 78;
        [Tooltip("Jacks: three sticks crossed at right angles round a shielded knot. Laid before the loose sticks.")]
        [SerializeField, Min(0)] int jackCount = 6;

        [Header("Clearings (the crystals float here)")]
        [Tooltip("Radius (u) of the clearing at the centre of the heap. 0 = none.")]
        [SerializeField, Min(0f)] float centreClearingRadius = 100f;
        [Tooltip("Clearings on a Fibonacci shell round the centre. 0 = none.")]
        [SerializeField, Min(0)] int ringClearingCount = 4;
        [Tooltip("Distance (u) of the shell the ring clearings sit on.")]
        [SerializeField, Min(0f)] float ringClearingDistance = 280f;
        [Tooltip("Radius (u) of each ring clearing.")]
        [SerializeField, Min(0f)] float ringClearingRadius = 70f;

        [Header("Hazards")]
        [Tooltip("Every Nth loose stick (slot 0, N, 2N...) has RED TIPS: its two end prisms are danger. 0 = none.")]
        [SerializeField, Min(0)] int dangerTipEveryNth = 8;
        [Tooltip("Every Nth loose stick (slot 1, N+1, ...) has an IRONWOOD heart: its middle four prisms are shielded. " +
                 "0 = none; must be 2 or more to mark any.")]
        [SerializeField, Min(0)] int ironwoodEveryNth = 10;

        // ── A stick (local frame: +z along the stick) ──
        /// <summary>Prisms per loose stick, and each one's length and square section (u). A stick is 120 u.</summary>
        public const int StickPrisms = 12;
        public const float Segment = 10f, Section = 3f;
        /// <summary>The ironwood heart: prisms [IronwoodFrom, IronwoodTo) of a stick are shielded.</summary>
        public const int IronwoodFrom = 4, IronwoodTo = 8;
        /// <summary>Least distance (u) between any two stick axes: a section and a half of air.</summary>
        public const float StickGap = 7.5f;
        /// <summary>Draws a stick (or a jack) gets before its slot is given up.</summary>
        public const int StickTries = 8;

        // ── A jack ──
        /// <summary>Prisms per arm (six arms), and the knot cube's edge (u).</summary>
        public const int JackArmPrisms = 5;
        public const float Knot = 9f;

        /// <summary>How close to exact contact a prism is laid along its stick.</summary>
        public const float Flush = 0.995f;

        /// <summary>Key offsets into <see cref="CellEnvironmentSpawnableBase.Hash01"/>: one stream per family, so
        /// changing one count never moves another family's pieces.</summary>
        public const int JackKeyBase = 1_000_000, StickKeyBase = 2_000_000;

        public static float StickHalf => StickPrisms * Segment * 0.5f;
        public static float JackReach => Knot * 0.5f + JackArmPrisms * Segment;

        protected override int DefaultSeed => 8484;
        protected override int LayCapacity => 4096;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                System.HashCode.Combine(heapRadius, stickCount, jackCount),
                System.HashCode.Combine(centreClearingRadius, ringClearingCount, ringClearingDistance, ringClearingRadius),
                System.HashCode.Combine(dangerTipEveryNth, ironwoodEveryNth, 1));

        int LayoutSeed => seed != 0 ? seed : DefaultSeed;

        /// <summary>One clearing: a ball no stick enters, in this structure's local space.</summary>
        public readonly struct Clearing
        {
            public readonly Vector3 Centre;
            public readonly float Radius;
            public Clearing(Vector3 centre, float radius) { Centre = centre; Radius = radius; }
        }

        readonly List<Clearing> _clearings = new();
        readonly List<Vector3> _segA = new(), _segB = new();

        protected override void BuildEnvironment()
        {
            _clearings.Clear();
            CollectClearings(_clearings);
            _segA.Clear();
            _segB.Clear();

            for (int j = 0; j < jackCount; j++) TryLayJack(j);
            for (int s = 0; s < stickCount; s++) TryLayStick(s);
        }

        /// <summary>
        /// Every clearing in this structure's local space (the arena centre is its origin). The centre one first (when it has a radius), then the shell. Read by the controller to float the
        /// crystals, and by the AI to run for cover-free fuel. Pure: it reads only the serialized fields, so a baked
        /// layout answers it too.
        /// </summary>
        public void CollectClearings(List<Clearing> into)
        {
            if (centreClearingRadius > 0f) into.Add(new Clearing(Vector3.zero, centreClearingRadius));
            for (int k = 0; k < ringClearingCount; k++)
                into.Add(new Clearing(Fibonacci(k, ringClearingCount) * ringClearingDistance, ringClearingRadius));
        }

        // ── Draws ──

        int Key(int family, int slot, int attempt, int draw) =>
            unchecked(LayoutSeed * 7919 + family + (slot * StickTries + attempt) * 4 + draw);

        /// <summary>A direction uniform on the sphere from two hashes: no axis is up.</summary>
        static Vector3 UnitFrom(int key)
        {
            float u = 2f * Hash01(key) - 1f;
            float phi = 2f * Mathf.PI * Hash01(unchecked(key + 7919));
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
            return new Vector3(s * Mathf.Cos(phi), s * Mathf.Sin(phi), u);
        }

        /// <summary>A point uniform in a ball of radius <paramref name="r"/> (cube root, so the ball fills evenly).</summary>
        static Vector3 InBall(int key, float r) =>
            UnitFrom(key) * (r * Mathf.Pow(Hash01(unchecked(key + 104729)), 1f / 3f));

        /// <summary>Point <paramref name="k"/> of <paramref name="n"/> on the unit Fibonacci sphere.</summary>
        public static Vector3 Fibonacci(int k, int n)
        {
            float y = 1f - 2f * (k + 0.5f) / n;
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = GoldenAngle * k;
            return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s);
        }

        // ── Room ──

        bool ClearOfClearings(Vector3 a, Vector3 b, float pad)
        {
            for (int i = 0; i < _clearings.Count; i++)
            {
                var c = _clearings[i];
                float r = c.Radius + pad;
                if (PointSegmentSqr(c.Centre, a, b) < r * r) return false;
            }
            return true;
        }

        bool ClearOfSticks(Vector3 a, Vector3 b)
        {
            float g2 = StickGap * StickGap;
            for (int i = 0; i < _segA.Count; i++)
                if (SegmentSegmentSqr(a, b, _segA[i], _segB[i]) < g2) return false;
            return true;
        }

        void TryLayStick(int slot)
        {
            float half = StickHalf;
            for (int t = 0; t < StickTries; t++)
            {
                var centre = InBall(Key(StickKeyBase, slot, t, 0), heapRadius - half);
                var axis = UnitFrom(Key(StickKeyBase, slot, t, 1));
                Vector3 a = centre - axis * half, b = centre + axis * half;
                if (!ClearOfClearings(a, b, Section) || !ClearOfSticks(a, b)) continue;
                _segA.Add(a);
                _segB.Add(b);
                LayStick(slot, centre, axis);
                return;
            }
        }

        void LayStick(int slot, Vector3 centre, Vector3 axis)
        {
            bool redTips = dangerTipEveryNth > 0 && slot % dangerTipEveryNth == 0;
            bool ironwood = ironwoodEveryNth > 1 && slot % ironwoodEveryNth == 1;
            var rot = Facing(axis);
            var size = new Vector3(Section, Section, Segment * Flush);
            float start = -StickHalf + Segment * 0.5f;
            for (int k = 0; k < StickPrisms; k++)
            {
                var kind = PrismKind.Plain;
                if (redTips && (k == 0 || k == StickPrisms - 1)) kind = PrismKind.Danger;
                else if (ironwood && k >= IronwoodFrom && k < IronwoodTo) kind = PrismKind.Shielded;
                Emit(centre + axis * (start + Segment * k), rot, size, Domains.Blue, kind);
            }
        }

        void TryLayJack(int slot)
        {
            float reach = JackReach;
            for (int t = 0; t < StickTries; t++)
            {
                var centre = InBall(Key(JackKeyBase, slot, t, 0), heapRadius - reach);
                var x = UnitFrom(Key(JackKeyBase, slot, t, 1));
                var helper = Mathf.Abs(x.z) < 0.9f ? Vector3.forward : Vector3.right;
                var y = Vector3.Cross(x, helper).normalized;
                var z = Vector3.Cross(x, y);
                bool room = true;
                foreach (var axis in new[] { x, y, z })
                {
                    Vector3 a = centre - axis * reach, b = centre + axis * reach;
                    if (!ClearOfClearings(a, b, Section) || !ClearOfSticks(a, b)) { room = false; break; }
                }
                if (!room) continue;
                foreach (var axis in new[] { x, y, z })
                {
                    _segA.Add(centre - axis * reach);
                    _segB.Add(centre + axis * reach);
                }
                LayJack(centre, x, y, z);
                return;
            }
        }

        void LayJack(Vector3 centre, Vector3 x, Vector3 y, Vector3 z)
        {
            // The knot: a shielded cube square to the three arms, so each arm leaves one of its faces.
            Emit(centre, Quaternion.LookRotation(z, y), Vector3.one * Knot, Domains.Blue, PrismKind.Shielded);
            var size = new Vector3(Section, Section, Segment * Flush);
            foreach (var axis in new[] { x, -x, y, -y, z, -z })
            {
                var rot = Facing(axis);
                for (int k = 0; k < JackArmPrisms; k++)
                    Emit(centre + axis * (Knot * 0.5f + Segment * (k + 0.5f)), rot, size, Domains.Blue);
            }
        }

        static Quaternion Facing(Vector3 axis) =>
            Quaternion.LookRotation(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);

        // ── Geometry (mirrored line for line in jackstraw_budget.py) ──

        static float PointSegmentSqr(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a + ab * t - p).sqrMagnitude;
        }

        /// <summary>Squared least distance between segments p1-q1 and p2-q2 (Ericson, Real-Time Collision Detection 5.1.9).</summary>
        public static float SegmentSegmentSqr(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-6f && e <= 1e-6f) return r.sqrMagnitude;
            if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 1e-6f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return (p1 + d1 * s - (p2 + d2 * t)).sqrMagnitude;
        }
    }
}
