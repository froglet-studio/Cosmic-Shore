using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One rung of the Drop Tower: every number a tower is laid from at one intensity. The tables are MEASURED by
    /// <c>Tools/Build/waystation_tower_course.py</c>, which reads them out of this file and fails the build when a
    /// tower stops being flyable (Docs/Studios/TIGER_MOTH_TIME.md has the table and why each number is what it is).
    /// </summary>
    public struct WaystationTowerSettings
    {
        /// <summary>Radius of a tower ring. Never a cluster ring's radius: it is how every peer tells the two apart
        /// from the broadcast course (<see cref="WaystationTower.IsTowerRing"/>).</summary>
        public float HopRingRadius;

        /// <summary>The middle of a hop's legal TRAVEL (u, fall plus landing, from the press point).</summary>
        public float WindowCentre;

        /// <summary>Half the legal travel window (u). Shorter lands short of or inside the floor plate; longer leaves
        /// too little runway to recharge before the next ring, which ends in the pipe.</summary>
        public float WindowHalfWidth;

        /// <summary>Chance a free sideways hop is a ROLL GATE: its ring is abeam rather than under the belly, so the
        /// pilot rolls 90 degrees before pressing.</summary>
        public float RollGateShare;

        /// <summary>Share of a plate's tiles that are danger prisms; the rest are plain.</summary>
        public float DangerShare;

        /// <summary>Share of D hops (a tower ring a sideways hop follows) that are TIGHT: the runway after them is cut
        /// short, so the deepest landing that still recharges and rolls before the next press - the SHELF - lies short
        /// of the window's far edge. The lanterns show where it lies (Lantern Towers, 2026-11-11).</summary>
        public float ShelfShare;

        /// <summary>How far short of the window's far edge a tight hop's shelf lies (u), drawn per hop between these.</summary>
        public float ShelfCutMin, ShelfCutMax;

        /// <summary>The deep band (u): the last stretch of the lanterns, where a landing grades DEEP.</summary>
        public float DeepBand;

        /// <summary>1 = a Time crystal (the crown gem) hangs on each tower's plunge, just short of its deepest landing.</summary>
        public float CrownGem;

        /// <summary>Half-width (u) of the autopilot's per-seat landing error: rivals that read the lanterns but differ.</summary>
        public float AiErrorU;
        public float WindowLow => WindowCentre - WindowHalfWidth;

        /// <summary>Longest travel of a legal hop (u).</summary>
        public float WindowHigh => WindowCentre + WindowHalfWidth;

        /// <summary>How far under the cruise line the floor plate lies (u): a landing must clear it by the hull's
        /// radius plus the landing clearance.</summary>
        public float PlateDepth => WindowLow - WaystationTower.HullRadius - WaystationTower.LandClear;

        /// <summary>How far under the cruise line the ring's plane lies (u), just past the plate.</summary>
        public float RingDepth => PlateDepth + WaystationTower.RingPastPlate;

        public static WaystationTowerSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new WaystationTowerSettings
            {
                HopRingRadius = new[] { 48f, 42f, 36f, 32f }[i],
                WindowCentre = new[] { 150f, 150f, 155f, 160f }[i],
                WindowHalfWidth = new[] { 100f, 84f, 70f, 58f }[i],
                RollGateShare = new[] { 0f, 0f, 0.5f, 1f }[i],
                DangerShare = new[] { 0.10f, 0.20f, 0.35f, 0.50f }[i],
                ShelfShare = new[] { 0f, 0.4f, 0.5f, 0.75f }[i],
                ShelfCutMin = new[] { 0f, 50f, 45f, 40f }[i],
                ShelfCutMax = new[] { 0f, 100f, 80f, 90f }[i],
                DeepBand = new[] { 45f, 45f, 40f, 30f }[i],
                CrownGem = new[] { 0f, 0f, 0f, 1f }[i],
                AiErrorU = new[] { 6f, 10f, 12f, 12f }[i],
            };
        }
    }

    /// <summary>One tile of a tower wall, in the course's own frame. Laid as an environment prism.</summary>
    public readonly struct WaystationTowerTile
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly bool Danger;

        /// <summary>The course index of the tower ring the plate belongs to, and which plate (0 floor, 1 pipe).</summary>
        public readonly int Gate, Wall;

        public WaystationTowerTile(Vector3 position, Quaternion rotation, Vector3 scale, bool danger, int gate, int wall)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Danger = danger;
            Gate = gate;
            Wall = wall;
        }
    }

    /// <summary>One segment of a lantern post, in the course's own frame. Laid as an environment prism.</summary>
    public readonly struct WaystationLantern
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        /// <summary>The course index of the tower ring whose hop the post marks.</summary>
        public readonly int Gate;

        /// <summary>Inside the deep band (the dense run at the post's foot).</summary>
        public readonly bool Deep;

        public WaystationLantern(Vector3 position, Quaternion rotation, Vector3 scale, int gate, bool deep)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Gate = gate;
            Deep = deep;
        }
    }

    /// <summary>
    /// Waystation's Drop Tower: a staircase of short hops laid along the drop line between two clusters
    /// (Docs/Studios/TIGER_MOTH_TIME.md). Pure: no <c>UnityEngine.Random</c>, no <c>Time</c>, no scene, so every peer
    /// derives the same tower and walls from the broadcast course, and the whole thing is measured offline.
    ///
    /// <para><b>Why a square wave.</b> Every Drop is a 90-degree turn: the hull falls along its belly and lands flying
    /// along the fall line. From Garrett's square exit gate (heading A, next cluster under the belly along D) the only
    /// drop pattern that makes progress toward the cluster is D, sideways, roll 180, D, sideways back: the sideways
    /// hop goes where the belly already points (no roll), and the roll before each D hop puts the belly back on D.
    /// A diagonal staircase (drop, roll 180, drop) cannot start from a square gate - its axis is 45 degrees off the
    /// drop line - so the square wave is the one that keeps Garrett's gate as the tower's door.</para>
    ///
    /// <para><b>Each hop has two plates.</b> The FLOOR lies across the belly side at <see
    /// cref="WaystationTowerSettings.PlateDepth"/> under the press point, with the ring just past it: a hop too short
    /// lands on or in front of it. The PIPE stands across the new heading just beyond the next press point: a hop too
    /// long leaves too little runway to recharge before the next ring, and the pilot flies into it. The fall itself is
    /// incorporeal, so a floor costs nothing to drop through.</para>
    /// </summary>
    public static class WaystationTower
    {
        // ── The hull (TigerMoth.prefab, TigerMothDropAction.asset; the model asserts these off the assets) ──

        public const float Cruise = 67f;
        public const float DropSpeed = 900f;
        public const float DropEaseSeconds = 0.12f;
        public const float LandSeconds = 0.25f;
        public const float FullCooldownSeconds = 20f;
        public const float FullCooldownDistance = 900f;
        public const float RollDegreesPerSecond = 70f;
        public const float HullRadius = 6f;

        // ── The tower's own constants (not per intensity: each is pinned by the hull, not by difficulty) ──

        /// <summary>Clearance (u) between a legal landing and the floor plate, beyond the hull's radius.</summary>
        public const float LandClear = 8f;

        /// <summary>How far past the floor plate the ring's plane lies (u).</summary>
        public const float RingPastPlate = 6f;

        /// <summary>Clearance (u) between the next ring's mouth and the pipe beyond it.</summary>
        public const float PipeClear = 14f;

        /// <summary>A pilot's reaction allowance (s) on top of every recharge or roll.</summary>
        public const float ReactSeconds = 0.35f;

        /// <summary>How far past the exit gate (u) the tower's first press point lies: the 90-degree roll that puts the
        /// cluster under the belly, plus the reaction.</summary>
        public const float Lead = Cruise * (90f / RollDegreesPerSecond + ReactSeconds);

        /// <summary>Least straight flight (u) from the tower's last landing to the next cluster's first ring.</summary>
        public const float FinalMin = 150f;

        /// <summary>Most extra depth (u) the tower's LAST D hop takes to fill the gap: the plunge into the cluster. Its
        /// recharge is paid while the cluster is flown, before the next tower.</summary>
        public const float PlungeCap = 300f;

        /// <summary>Clearance (u) a tower's plates keep from the cell's nucleus.</summary>
        public const float ShellPad = 10f;

        /// <summary>How far past the course shell's outer edge (u) a plate may reach: the shell is 0.9 of the
        /// membrane's 1200, and a plate has 50 u of the membrane still to spare at this reach.</summary>
        public const float MembraneReach = 70f;

        /// <summary>A plate's half-side beyond its ring's radius (u).</summary>
        public const float PlateMargin = 20f;

        /// <summary>Tiles along each side of a plate.</summary>
        public const int TilesPerSide = 8;

        /// <summary>A tile's thickness (u).</summary>
        public const float TileThickness = 3f;

        /// <summary>The final gap is the long drop, and it must leave a wormhole (a 900 u press-to-landing) with room.</summary>
        public const float LongGapMin = 1000f;

        /// <summary>Course seeds tried for a long final gap before the best one is kept.</summary>
        public const int SeedTries = 64;

        // ── The lanterns and the crown gem (Lantern Towers, 2026-11-11) ──

        /// <summary>How far short of the shelf the lanterns stop (u): the last post is a little inside the deepest landing.</summary>
        public const float LanternBack = 15f;

        /// <summary>Spacing (u) of the lantern posts' segments before the deep band, and inside it (denser).</summary>
        public const float LanternGap = 30f;
        public const float LanternDeepGap = 15f;

        /// <summary>A lantern segment's length along the drop line and its square side (u).</summary>
        public const float LanternSeg = 10f;
        public const float LanternSide = 3f;

        /// <summary>How far short of the plunge's deepest landing (u) the crown gem hangs: max-depth greed skips it.</summary>
        public const float GemSkip = 40f;

        const float RadiusTolerance = 0.01f;
        const float ShelfTolerance = 0.05f;

        // ── The Drop's own kinematics (TigerMothDropExecutor, closed form) ──

        /// <summary>Distance fallen while the trigger is held for <paramref name="hold"/> seconds.</summary>
        public static float Fall(float hold)
        {
            float t = Mathf.Max(0f, hold);
            if (t <= DropEaseSeconds)
            {
                float x = t / DropEaseSeconds;
                return DropSpeed * DropEaseSeconds * (x * x * x - 0.5f * x * x * x * x);
            }
            return DropSpeed * DropEaseSeconds * 0.5f + DropSpeed * (t - DropEaseSeconds);
        }

        /// <summary>Fall plus the landing turn's travel: where the colliders come back on, measured from the press.</summary>
        public static float Travel(float hold) => Travel(hold, Cruise);

        /// <summary>Fall plus the landing turn's travel at a given cruise: the executor lands to the speed it had at the
        /// press, so a hull slowed by a wall (or quickened by Afterburn) carries less (or more) than <see cref="Cruise"/>.</summary>
        public static float Travel(float hold, float cruise)
        {
            float t = Mathf.Max(0f, hold);
            float release = DropSpeed * TigerMothDropMath.Ease01(t / DropEaseSeconds);
            return Fall(t) + LandSeconds * (release + Mathf.Max(0f, cruise)) * 0.5f;
        }

        /// <summary>The hold that travels <paramref name="travel"/> units (bisection on the monotone <see cref="Travel"/>).</summary>
        public static float HoldForTravel(float travel) => HoldForTravel(travel, Cruise);

        /// <summary>The hold that travels <paramref name="travel"/> units at the live <paramref name="cruise"/>.</summary>
        public static float HoldForTravel(float travel, float cruise)
        {
            float lo = 0f, hi = 4f;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (Travel(mid, cruise) < travel) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        /// <summary>The recharge a hop of <paramref name="travel"/> units pays (the landing is not charged).</summary>
        public static float RechargeForTravel(float travel) =>
            FullCooldownSeconds * Mathf.Clamp01(Fall(HoldForTravel(travel)) / FullCooldownDistance);

        /// <summary>Seconds to roll <paramref name="degrees"/>.</summary>
        public static float RollSeconds(float degrees) => Mathf.Abs(degrees) / RollDegreesPerSecond;

        /// <summary>The runway (u) a pilot needs after landing at the window's far edge before pressing again.</summary>
        public static float Runway(in WaystationTowerSettings s, float rollDegrees) => RunwayAfter(s.WindowHigh, rollDegrees);

        /// <summary>The runway (u) a pilot needs after a hop of <paramref name="travel"/> before pressing again: the
        /// recharge or the roll, whichever is longer, plus the reaction.</summary>
        public static float RunwayAfter(float travel, float rollDegrees) =>
            Cruise * (Mathf.Max(RechargeForTravel(travel), RollSeconds(rollDegrees)) + ReactSeconds);

        // ── The shelf: how deep each hop may land (Lantern Towers) ──

        /// <summary>Is the D hop at course index <paramref name="gate"/> tight? Hashed on the index, so every peer agrees.</summary>
        public static bool IsTightHop(int gate, in WaystationTowerSettings s) =>
            s.ShelfShare > 0f && Hash01(gate, 7, 0) < s.ShelfShare;

        /// <summary>How far short of the far edge a tight hop's shelf lies (u).</summary>
        public static float ShelfCut(int gate, in WaystationTowerSettings s) =>
            s.ShelfCutMin + (s.ShelfCutMax - s.ShelfCutMin) * Hash01(gate, 7, 1);

        /// <summary>Does tower ring <paramref name="i"/> have a hop after it (so a shelf and lanterns)? Every tower ring
        /// but a tower's last, the plunge.</summary>
        public static bool HasShelf(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s) =>
            i >= 1 && i + 1 < course.Count && IsTowerRing(course[i], s) && IsTowerRing(course[i + 1], s);

        /// <summary>Degrees the pilot rolls between landing hop <paramref name="i"/> and pressing hop i + 1: the belly
        /// after a landing points back along the heading it pressed on (the previous ring's axis), and the next hop
        /// falls along the next ring's axis. 0, 90 or 180.</summary>
        public static float RollBefore(IReadOnlyList<RaceGate> course, int i) =>
            RaceCourseGeometry.Angle(-course[i - 1].Axis, course[i + 1].Axis);

        /// <summary>
        /// The SHELF of tower ring <paramref name="i"/>: the deepest landing (u of travel from its press point) after
        /// which the Drop is recharged and the roll done exactly as the hull reaches the next press point - from the
        /// broadcast course alone, so every peer and the autopilot read the same number. Near the window's far edge on a
        /// loose hop (past it by the reaction the runway keeps); shorter on a tight one. A landing deeper than the shelf
        /// presses late, and past <see cref="FarEdge"/> the next ring is out of reach. The lanterns end
        /// <see cref="LanternBack"/> short of it.
        ///
        /// <para>Why not the depth the course was laid for (<see cref="LaidDepth"/>, which keeps the reaction)? Measured
        /// by <c>waystation_tower_course.py --sweep</c>: lanterns there saved a reading pilot 1% against the best fixed
        /// depth, because a fixed pilot simply spends the reaction margin; at the shelf they save 3-4%.</para>
        /// </summary>
        public static float ShelfDepth(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s) =>
            HasShelf(course, i, s)
                ? DeepestLanding(course, i, s, 0f, s.WindowLow, s.WindowHigh + FarEdgeReach)
                : s.WindowHigh;

        /// <summary>The depth hop <paramref name="i"/> was laid for: its shelf with <see cref="ReactSeconds"/> kept spare
        /// (the window's far edge on a loose hop, the far edge less its cut on a tight one).</summary>
        public static float LaidDepth(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s) =>
            HasShelf(course, i, s)
                ? DeepestLanding(course, i, s, ReactSeconds, s.WindowLow, s.WindowHigh)
                : s.WindowHigh;

        /// <summary>
        /// The HARD far edge of tower ring <paramref name="i"/>'s hop: the deepest landing after which the next ring can
        /// still be dropped through at all - no reaction left, and the press late by as much as the next mouth allows (its
        /// radius less the hull's). Past it the grade is <see cref="HopGrade.Past"/>. Infinite on a plunge, which has no
        /// next hop.
        /// </summary>
        public static float FarEdge(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s)
        {
            if (!HasShelf(course, i, s)) return float.PositiveInfinity;
            float late = (course[i + 1].Radius - HullRadius) / Cruise;
            return DeepestLanding(course, i, s, -late, s.WindowLow, s.WindowHigh + FarEdgeReach);
        }

        /// <summary>How far past the window's far edge (u) <see cref="FarEdge"/> searches; the `edges` check measures the
        /// real overshoot at 33 u or less.</summary>
        const float FarEdgeReach = 400f;

        /// <summary>The deepest landing in [<paramref name="lo"/>, <paramref name="hi"/>] whose remaining cruise to the next
        /// press point covers the recharge or the roll plus <paramref name="spareSeconds"/> (bisection; the need grows
        /// and the cruise shrinks with depth, so the test is monotone).</summary>
        static float DeepestLanding(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s, float spareSeconds,
                                    float lo, float hi) =>
            DeepestLanding(Vector3.Dot(PressOf(course, i + 1, s) - PressOf(course, i, s), course[i].Axis),
                           RollBefore(course, i), spareSeconds, lo, hi);

        /// <summary>The same, for a hop <paramref name="span"/> u from its press point to the next one, with a
        /// <paramref name="roll"/> between: what <see cref="Lay"/> knows before the ring exists.</summary>
        static float DeepestLanding(float span, float roll, float spareSeconds, float lo, float hi)
        {
            if (Fits(span, hi, roll, spareSeconds)) return hi;
            if (!Fits(span, lo, roll, spareSeconds)) return lo;
            for (int k = 0; k < 40; k++)
            {
                float mid = 0.5f * (lo + hi);
                if (Fits(span, mid, roll, spareSeconds)) lo = mid; else hi = mid;
            }
            return lo;
        }

        static bool Fits(float span, float landing, float roll, float spareSeconds) =>
            span - landing + ShelfTolerance >=
            Cruise * (Mathf.Max(RechargeForTravel(landing), RollSeconds(roll)) + spareSeconds);

        /// <summary>The depth (u) the lanterns of tower ring <paramref name="i"/> reach: a little short of its shelf.</summary>
        public static float LanternEnd(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s) =>
            ShelfDepth(course, i, s) - LanternBack;

        /// <summary>How much deeper than a regular hop the plunge ring <paramref name="i"/> lies (u).</summary>
        public static float PlungeOf(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s) =>
            Vector3.Dot(course[i].Position - PressOf(course, i, s), course[i].Axis) - s.RingDepth;

        /// <summary>The crown gem's depth under the plunge's press point (u): the plunge's deepest landing less
        /// <see cref="GemSkip"/>.</summary>
        public static float GemDepth(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s) =>
            s.WindowHigh + PlungeOf(course, i, s) - GemSkip;

        /// <summary>Is tower ring <paramref name="i"/> a tower's last, the plunge?</summary>
        public static bool IsPlunge(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s) =>
            i >= 1 && IsTowerRing(course[i], s) && (i + 1 >= course.Count || !IsTowerRing(course[i + 1], s));

        /// <summary>
        /// Does a wall tile need regrowing? Only when it is gone: never laid, destroyed, or its pooled prism reused
        /// somewhere else (no longer ours and moved off its tile). A tile the Tiger Moth's dust STOLE stands where it
        /// was, in the thief's colour, and is left standing - it is still the wall, and laying a Blue tile on top of it
        /// would stack two prisms in one place.
        /// </summary>
        public static bool TileNeedsRegrow(bool exists, bool destroyed, bool ours, float displacement) =>
            !exists || destroyed || (!ours && displacement > 1f);

        // ── Hashes (the same on every peer and every runtime) ──

        /// <summary>A 16-bit fraction from three ints: Serpent Time's slab hash (SerpentTimeCluster.IsDangerSlab).</summary>
        public static float Hash01(int a, int b, int c)
        {
            uint h = unchecked((uint)a * 73856093u ^ (uint)b * 19349663u ^ (uint)c * 83492791u);
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFFu) / 65536f;
        }

        /// <summary>Is tile <paramref name="tile"/> of wall <paramref name="wall"/> on gate <paramref name="gate"/> a
        /// danger prism? None at share 0, all at share 1, scattered between.</summary>
        public static bool IsDangerTile(int gate, int wall, int tile, float share)
        {
            if (share <= 0f) return false;
            if (share >= 1f) return true;
            return Hash01(gate, wall * 7919 + 1, tile) < share;
        }

        /// <summary>Is sideways hop <paramref name="lateral"/> (1-based) of tower <paramref name="tower"/> a roll gate,
        /// and which way? Even hops are never free: each one cancels the hop before it, so a tower does not drift.</summary>
        public static bool IsRollGate(int tower, int lateral, float share, out bool positive)
        {
            positive = Hash01(tower, lateral, 977) < 0.5f;
            if (lateral % 2 == 0 || share <= 0f) return false;
            if (share >= 1f) return true;
            return Hash01(tower, lateral, 131) < share;
        }

        /// <summary>Is this broadcast gate a tower ring at this intensity?</summary>
        public static bool IsTowerRing(in RaceGate gate, in WaystationTowerSettings s) =>
            Mathf.Abs(gate.Radius - s.HopRingRadius) < RadiusTolerance;

        // ── Plates and obstacles ──

        /// <summary>The half-side of a plate (u).</summary>
        public static float PlateHalf(in WaystationTowerSettings s) => s.HopRingRadius + PlateMargin;

        /// <summary>A square plate: a centre, its normal, two in-plane axes and its half-side.</summary>
        public readonly struct Plate
        {
            public readonly Vector3 Centre, Normal, U, V;
            public readonly float Half;

            public Plate(Vector3 centre, Vector3 normal, Vector3 across, float half)
            {
                Centre = centre;
                Normal = RaceCourseGeometry.SafeNormalize(normal, Vector3.up);
                U = RaceCourseGeometry.SafeNormalize(Vector3.ProjectOnPlane(across, Normal),
                                                     RaceCourseGeometry.Perpendicular(Normal));
                V = Vector3.Cross(Normal, U);
                Half = half;
            }

            /// <summary>Distance (u) from <paramref name="p"/> to the plate's tiles, <see cref="TileThickness"/> thick.</summary>
            public float DistanceTo(Vector3 p)
            {
                Vector3 d = p - Centre;
                float a = Mathf.Clamp(Vector3.Dot(d, U), -Half, Half);
                float b = Mathf.Clamp(Vector3.Dot(d, V), -Half, Half);
                float n = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(d, Normal)) - TileThickness * 0.5f);
                Vector3 q = Centre + U * a + V * b;
                Vector3 inPlane = (p - q) - Normal * Vector3.Dot(p - q, Normal);
                return Mathf.Sqrt(inPlane.sqrMagnitude + n * n);
            }
        }

        /// <summary>What a tower must not cut: every ring, every stretch of corporeal flight, every plate laid so far.</summary>
        public sealed class Obstacles
        {
            public readonly List<(Vector3 a, Vector3 b)> Segments = new();
            public readonly List<Plate> Plates = new();
            public readonly List<RaceGate> Rings = new();

            /// <summary>The lantern posts laid so far, each from its window's near edge to its foot (Lantern Towers).</summary>
            public readonly List<(Vector3 a, Vector3 b)> Posts = new();
        }

        /// <summary>A post's reach (u) beyond its centre line: half a segment's length plus its side.</summary>
        const float PostPad = LanternSeg * 0.5f + LanternSide;

        /// <summary>How far a post stands from every corporeal flight line (u): the hull, a margin and the post's pad.</summary>
        const float PostClear = HullRadius + 4f + PostPad;

        /// <summary>The shortest distance between segments ab and cd (closed form, clamped to both segments).</summary>
        public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 u = b - a, v = d - c, w = a - c;
            float A = Vector3.Dot(u, u), B = Vector3.Dot(u, v), C = Vector3.Dot(v, v);
            float D = Vector3.Dot(u, w), E = Vector3.Dot(v, w);
            // A point against a segment (the ring and plate tests ask about points): the general case below treats a
            // zero-length segment as parallel and answers from a's end, which is wrong (measured: 88 u for 35 u).
            if (C < 1e-9f) return PointSegmentDistance(c, a, b);
            if (A < 1e-9f) return PointSegmentDistance(a, c, d);
            float den = A * C - B * B;
            float sN, sD = den, tN, tD = den;
            if (den < 1e-6f) { sN = 0f; sD = 1f; tN = E; tD = C; }
            else
            {
                sN = B * E - C * D;
                tN = A * E - B * D;
                if (sN < 0f) { sN = 0f; tN = E; tD = C; }
                else if (sN > sD) { sN = sD; tN = E + B; tD = C; }
            }
            if (tN < 0f)
            {
                tN = 0f;
                if (-D < 0f) sN = 0f; else if (-D > A) sN = sD; else { sN = -D; sD = A; }
            }
            else if (tN > tD)
            {
                tN = tD;
                if (-D + B < 0f) sN = 0f; else if (-D + B > A) sN = sD; else { sN = -D + B; sD = A; }
            }
            float sc = Mathf.Abs(sN) < 1e-6f || sD < 1e-12f ? 0f : sN / sD;
            float tc = Mathf.Abs(tN) < 1e-6f || tD < 1e-12f ? 0f : tN / tD;
            Vector3 gap = w + u * sc - v * tc;
            return gap.magnitude;
        }

        /// <summary>The distance from <paramref name="p"/> to segment ab.</summary>
        public static float PointSegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = Vector3.Dot(ab, ab);
            float t = len < 1e-12f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Does the post ab come within <paramref name="pad"/> of plate <paramref name="plate"/>? Sampled every
        /// 3 u, after a bounding test.</summary>
        static bool PostHitsPlate(Vector3 a, Vector3 b, in Plate plate, float pad)
        {
            if (SegmentDistance(a, b, plate.Centre, plate.Centre) > plate.Half * 1.415f + pad + TileThickness) return false;
            return SegmentHitsPlate(a, b, plate, pad);
        }

        /// <summary>Does the post ab come within <paramref name="pad"/> of a ring's rim or centre?</summary>
        static bool PostHitsRing(Vector3 a, Vector3 b, in RaceGate ring, float pad)
        {
            if (SegmentDistance(a, b, ring.Position, ring.Position) > ring.Radius + pad) return false;
            // Through the mouth: where the post crosses (or runs within pad of) the ring's plane, inside its radius.
            float da = Vector3.Dot(a - ring.Position, ring.Axis), db = Vector3.Dot(b - ring.Position, ring.Axis);
            if (da * db <= 0f && Mathf.Abs(da - db) > 1e-6f)
            {
                Vector3 x = a + (b - a) * (da / (da - db));
                if (Vector3.ProjectOnPlane(x - ring.Position, ring.Axis).magnitude < ring.Radius + pad) return true;
            }
            foreach (var (e, de) in new[] { (a, da), (b, db) })
                if (Mathf.Abs(de) < pad && Vector3.ProjectOnPlane(e - ring.Position, ring.Axis).magnitude < ring.Radius + pad)
                    return true;
            Vector3 u = RaceCourseGeometry.Perpendicular(ring.Axis);
            Vector3 w = Vector3.Cross(ring.Axis, u);
            for (int k = 0; k < 17; k++)
            {
                float t = k * Mathf.PI / 8f;
                Vector3 p = k == 16 ? ring.Position : ring.Position + (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * ring.Radius;
                if (SegmentDistance(a, b, p, p) < pad) return true;
            }
            return false;
        }

        /// <summary>The two posts of a hop pressed at <paramref name="press"/> falling along <paramref name="axis"/> after
        /// arriving along <paramref name="arrive"/>, from the window's near edge to the lanterns' foot at
        /// <paramref name="end"/>. <see cref="Lanterns"/> lays them from the broadcast course; this is the copy Lay
        /// keeps clear before the rings exist.</summary>
        static void AddPosts(Vector3 press, Vector3 axis, Vector3 arrive, float end, in WaystationTowerSettings s,
                             List<(Vector3 a, Vector3 b)> into)
        {
            if (end < s.WindowLow) return;
            Vector3 across = RaceCourseGeometry.SafeNormalize(Vector3.Cross(axis, arrive), RaceCourseGeometry.Perpendicular(axis));
            float offset = s.HopRingRadius + PlateMargin;
            for (int side = 0; side < 2; side++)
            {
                Vector3 post = press + across * (side == 0 ? offset : -offset);
                into.Add((post + axis * s.WindowLow, post + axis * end));
            }
        }

        static bool SegmentHitsPlate(Vector3 a, Vector3 b, in Plate plate, float clearance)
        {
            float length = Vector3.Distance(a, b);
            int n = Mathf.Max(1, (int)(length / 3f));
            for (int k = 0; k <= n; k++)
                if (plate.DistanceTo(a + (b - a) * ((float)k / n)) < clearance) return true;
            return false;
        }

        static bool RingHitsPlate(in RaceGate ring, in Plate plate)
        {
            Vector3 u = RaceCourseGeometry.Perpendicular(ring.Axis);
            Vector3 w = Vector3.Cross(ring.Axis, u);
            for (int k = 0; k < 17; k++)
            {
                float t = k * Mathf.PI / 8f;
                Vector3 p = k == 16 ? ring.Position : ring.Position + (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * ring.Radius;
                if (plate.DistanceTo(p) < 3f) return true;
            }
            return false;
        }

        /// <summary>The plate's nearest point clears the nucleus and its farthest corner is inside the membrane.</summary>
        static bool PlateInside(in Plate plate, float innerLimit, float outerLimit) =>
            plate.DistanceTo(Vector3.zero) >= innerLimit && plate.Centre.magnitude + plate.Half * 1.415f <= outerLimit;

        /// <summary>New plates inside the cell and clear of every obstacle; new flight clear of every plate.</summary>
        static bool ElementsClear(List<Plate> plates, List<(Vector3 a, Vector3 b)> segments, Obstacles o,
                                  float innerLimit, float outerLimit) =>
            ElementsClear(plates, segments, null, o, innerLimit, outerLimit);

        /// <summary>The same, with the new hops' lantern posts: each inside the cell and clear of every flight line, ring
        /// and plate; and the new flight lines and plates clear of every post already standing.</summary>
        static bool ElementsClear(List<Plate> plates, List<(Vector3 a, Vector3 b)> segments, List<(Vector3 a, Vector3 b)> posts,
                                  Obstacles o, float innerLimit, float outerLimit)
        {
            if (posts != null)
            {
                foreach (var (a, b) in posts)
                {
                    if (a.magnitude < innerLimit || b.magnitude < innerLimit || a.magnitude > outerLimit
                        || b.magnitude > outerLimit) return false;
                    foreach (var (c, d) in o.Segments)
                        if (SegmentDistance(a, b, c, d) < PostClear) return false;
                    foreach (var (c, d) in segments)
                        if (SegmentDistance(a, b, c, d) < PostClear) return false;
                    foreach (var ring in o.Rings)
                        if (PostHitsRing(a, b, ring, PostPad)) return false;
                    foreach (var plate in o.Plates)
                        if (PostHitsPlate(a, b, plate, PostPad)) return false;
                    foreach (var plate in plates)
                        if (PostHitsPlate(a, b, plate, PostPad)) return false;
                }
            }
            foreach (var (a, b) in o.Posts)
            {
                foreach (var (c, d) in segments)
                    if (SegmentDistance(a, b, c, d) < PostClear) return false;
                foreach (var plate in plates)
                    if (PostHitsPlate(a, b, plate, PostPad)) return false;
            }
            const float clear = HullRadius + 1f;
            foreach (var plate in plates)
            {
                if (!PlateInside(plate, innerLimit, outerLimit)) return false;
                foreach (var (a, b) in o.Segments)
                    if (SegmentHitsPlate(a, b, plate, clear)) return false;
                foreach (var ring in o.Rings)
                    if (RingHitsPlate(ring, plate)) return false;
                foreach (var other in o.Plates)
                    if (Vector3.Distance(plate.Centre, other.Centre) < 1.415f * (plate.Half + other.Half)) return false;
            }
            foreach (var (a, b) in segments)
            {
                foreach (var other in o.Plates)
                    if (SegmentHitsPlate(a, b, other, clear)) return false;
                foreach (var other in plates)
                    if (SegmentHitsPlate(a, b, other, clear)) return false;
            }
            return true;
        }

        // ── The tower ──

        /// <summary>
        /// The tower rings for one gap, from the exit gate it leaves to the next cluster's first ring, appended to
        /// <paramref name="into"/>. Returns the number of D hops laid (0 when not even one fits the gap and the cell).
        ///
        /// <para>Built forward, one D hop and one sideways hop at a time, while the next pair still fits the gap and
        /// clears everything already laid (<paramref name="obstacles"/>, which it extends). What is left over goes into
        /// the LAST D hop's depth - the plunge into the cluster, which has no pipe after it - not into a runway, which
        /// would widen the window past its authored width.</para>
        ///
        /// <para>A free sideways hop goes where the belly already points, or - at a roll gate, or where the belly's way
        /// is blocked - abeam after a 90-degree roll. Every even sideways hop goes back the way the one before it came,
        /// so the tower never drifts off the drop line.</para>
        /// </summary>
        public static int Lay(in RaceGate exitGate, Vector3 nextRing, int tower, in WaystationTowerSettings s,
                              float innerLimit, float outerLimit, Obstacles obstacles, List<RaceGate> into)
        {
            Vector3 heading = RaceCourseGeometry.SafeNormalize(exitGate.Axis, Vector3.forward);
            Vector3 press = exitGate.Position + heading * Lead;
            Vector3 down = RaceCourseGeometry.SafeNormalize(Vector3.ProjectOnPlane(nextRing - press, heading),
                                                            RaceCourseGeometry.Perpendicular(heading));
            float avail = Vector3.Dot(nextRing - press, down) - FinalMin;
            float high = s.WindowHigh, low = s.WindowLow;
            if (avail < high) return 0;

            float half = PlateHalf(s);
            float pipe = s.HopRingRadius + PipeClear + HullRadius;
            float lateralStride = high + Runway(s, 180f);

            var plates = new List<Plate>(4) { new Plate(press + down * s.PlateDepth, down, heading, half) };
            var segments = new List<(Vector3 a, Vector3 b)>(5) { (exitGate.Position, press) };
            AddFinals(press, down, nextRing, s, segments);
            if (!ElementsClear(plates, segments, obstacles, innerLimit, outerLimit)) return 0;
            obstacles.Plates.Add(plates[0]);
            obstacles.Segments.Add(segments[0]);
            var finals = new List<(Vector3 a, Vector3 b)>(3);
            AddFinals(press, down, nextRing, s, finals);

            var rings = new List<RaceGate>();
            var posts = new List<(Vector3 a, Vector3 b)>(4);
            Vector3 belly = -heading;            // where the belly points after the next D hop lands
            Vector3 lastSide = Vector3.zero;
            float used = 0f;                     // along the drop line, from the first press point
            int dHops = 1;
            Span3 options = default;

            for (int lateral = 1; ; lateral++)
            {
                bool gate = IsRollGate(tower, lateral, s.RollGateShare, out bool positive);
                Vector3 abeam = RaceCourseGeometry.SafeNormalize(Vector3.Cross(down, belly), RaceCourseGeometry.Perpendicular(down));
                if (!positive) abeam = -abeam;
                int count;
                if (lateral % 2 == 0) { options.Set(0, -lastSide, 0f); count = 1; }
                else if (gate) { options.Set(0, abeam, 90f); options.Set(1, -abeam, 90f); options.Set(2, belly, 0f); count = 3; }
                else { options.Set(0, belly, 0f); options.Set(1, abeam, 90f); options.Set(2, -abeam, 90f); count = 3; }

                // A TIGHT D hop (Lantern Towers): its runway is sized for a landing at its shelf, short of the far edge,
                // so a pilot who lands deeper than the shelf reaches the sideways press uncharged. Hashed on the ring's
                // course index, which is where it lands in the broadcast course.
                int dGate = into.Count + rings.Count;
                float shelf = IsTightHop(dGate, s) ? high - ShelfCut(dGate, s) : high;

                bool chosen = false;
                Vector3 side = default, lateralPress = default, next = default;
                float step = 0f;
                Vector3 arrive = lateral == 1 ? heading : lastSide;
                for (int k = 0; k < count && !chosen; k++)
                {
                    options.Get(k, out side, out float roll);
                    step = shelf + RunwayAfter(shelf, roll);
                    if (used + step + high > avail) continue;
                    lateralPress = press + down * step;
                    next = lateralPress + side * lateralStride;
                    plates.Clear();
                    plates.Add(new Plate(lateralPress + down * pipe, down, side, half));
                    plates.Add(new Plate(lateralPress + side * s.PlateDepth, side, down, half));
                    plates.Add(new Plate(next + side * pipe, side, down, half));
                    plates.Add(new Plate(next + down * s.PlateDepth, down, side, half));
                    segments.Clear();
                    segments.Add((press + down * low, lateralPress));
                    segments.Add((lateralPress + side * low, next));
                    AddFinals(next, down, nextRing, s, segments);
                    // The lanterns both hops will stand (Lanterns lays them from the broadcast course; the same numbers).
                    posts.Clear();
                    AddPosts(press, down, arrive, DeepestLanding(step, roll, 0f, low, high + FarEdgeReach) - LanternBack,
                             s, posts);
                    AddPosts(lateralPress, side, down,
                             DeepestLanding(lateralStride, 180f, 0f, low, high + FarEdgeReach) - LanternBack, s, posts);
                    if (!ElementsClear(plates, segments, posts, obstacles, innerLimit, outerLimit)) continue;
                    chosen = true;
                    used += step;
                }
                if (!chosen) break;

                rings.Add(new RaceGate(press + down * s.RingDepth, down, s.HopRingRadius));
                rings.Add(new RaceGate(lateralPress + side * s.RingDepth, side, s.HopRingRadius));
                obstacles.Plates.AddRange(plates);
                obstacles.Segments.Add(segments[0]);
                obstacles.Segments.Add(segments[1]);
                obstacles.Rings.Add(rings[rings.Count - 2]);
                obstacles.Rings.Add(rings[rings.Count - 1]);
                obstacles.Posts.AddRange(posts);
                finals.Clear();
                for (int k = 2; k < segments.Count; k++) finals.Add(segments[k]);
                press = next;
                lastSide = side;
                belly = -side;
                dHops++;
            }

            float plunge = Mathf.Clamp(avail - (used + high), 0f, PlungeCap);
            rings.Add(new RaceGate(press + down * (s.RingDepth + plunge), down, s.HopRingRadius));
            into.AddRange(rings);
            obstacles.Segments.AddRange(finals);
            obstacles.Rings.Add(rings[rings.Count - 1]);
            // The plunge's landing stretch (and the crown gem on it, at I4): a later tower's plate there would be landed
            // in, and would bury the gem (measured: I4 seed 11 before this, a plate 2.5 u from a gem).
            obstacles.Segments.Add((press + down * (low + plunge), press + down * (high + plunge)));
            return dHops;
        }

        /// <summary>The flight into the next cluster: the pilot lands anywhere in the window, so three lines, not one.</summary>
        static void AddFinals(Vector3 press, Vector3 down, Vector3 nextRing, in WaystationTowerSettings s,
                              List<(Vector3 a, Vector3 b)> into)
        {
            into.Add((press + down * s.WindowLow, nextRing));
            into.Add((press + down * s.WindowCentre, nextRing));
            into.Add((press + down * s.WindowHigh, nextRing));
        }

        /// <summary>Up to three (direction, roll) options, without an allocation per hop.</summary>
        struct Span3
        {
            Vector3 _d0, _d1, _d2;
            float _r0, _r1, _r2;

            public void Set(int i, Vector3 d, float r)
            {
                if (i == 0) { _d0 = d; _r0 = r; }
                else if (i == 1) { _d1 = d; _r1 = r; }
                else { _d2 = d; _r2 = r; }
            }

            public void Get(int i, out Vector3 d, out float r)
            {
                if (i == 0) { d = _d0; r = _r0; }
                else if (i == 1) { d = _d1; r = _r1; }
                else { d = _d2; r = _r2; }
            }
        }

        /// <summary>
        /// Waystation's course: Garrett's clusters (<see cref="WaystationCourse"/>, unchanged) with a Drop Tower in
        /// every gap but the LAST, which stays the long drop - the wormhole route - and comes last so its long
        /// recharge never costs a tower. Seeds are tried until the last gap is long enough to leave a wormhole.
        /// </summary>
        public static List<RaceGate> Build(int seed, WaystationCourseSettings course, int intensity, out int seedUsed)
        {
            var s = WaystationTowerSettings.ForIntensity(intensity);
            int per = Mathf.Max(1, course.RingsPerCluster);
            float innerLimit = course.InnerRadius / RaceCourseSource.DefaultInnerRadiusNucleusFactor + ShellPad;
            float outerLimit = course.OuterRadius + MembraneReach;
            List<RaceGate> best = null;
            float bestGap = -1f;
            seedUsed = seed;
            for (int attempt = 0; attempt < SeedTries; attempt++)
            {
                int trial = unchecked(seed + attempt * 7919);
                var clusters = WaystationCourse.Generate(trial, course);
                if (clusters == null || clusters.Count == 0) continue;
                float gap = LastGap(clusters, per);
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = clusters;
                    seedUsed = trial;
                }
                if (gap >= LongGapMin) break;
            }
            if (best == null) return null;

            var obstacles = ClusterObstacles(best, per);
            var laid = new List<RaceGate>(best.Count * 3);
            int gaps = best.Count / per - 1;
            for (int c = 0; c * per < best.Count; c++)
            {
                for (int k = 0; k < per && c * per + k < best.Count; k++) laid.Add(best[c * per + k]);
                if (c >= gaps - 1) continue;   // no gap after the last cluster; the last gap is the long drop
                Lay(best[c * per + per - 1], best[(c + 1) * per].Position, c, s, innerLimit, outerLimit, obstacles, laid);
            }
            return laid;
        }

        /// <summary>Every cluster ring, and the flight through each cluster and across the long last gap.</summary>
        public static Obstacles ClusterObstacles(IReadOnlyList<RaceGate> clusters, int per)
        {
            var o = new Obstacles();
            o.Rings.AddRange(clusters);
            int n = clusters.Count / per;
            for (int c = 0; c < n; c++)
            for (int k = 1; k < per; k++)
                o.Segments.Add((clusters[c * per + k - 1].Position, clusters[c * per + k].Position));
            if (n >= 2) o.Segments.Add((clusters[(n - 1) * per - 1].Position, clusters[(n - 1) * per].Position));
            return o;
        }

        /// <summary>Exit gate to first ring of the last gap (u), or 0 for a one-cluster course.</summary>
        public static float LastGap(IReadOnlyList<RaceGate> clusters, int per)
        {
            int n = clusters.Count / per;
            if (n < 2) return 0f;
            return Vector3.Distance(clusters[(n - 1) * per - 1].Position, clusters[(n - 1) * per].Position);
        }

        // ── The walls, from the broadcast course alone ──

        /// <summary>
        /// Every wall tile of every tower, from the course every peer was sent: the floor plate over each tower ring
        /// and the pipe beyond the next press point. Kinds come from <see cref="IsDangerTile"/>, so all peers agree.
        /// </summary>
        public static void Walls(IReadOnlyList<RaceGate> course, int intensity, List<WaystationTowerTile> into)
        {
            into.Clear();
            var s = WaystationTowerSettings.ForIntensity(intensity);
            for (int i = 1; i < course.Count; i++)
            {
                if (!IsTowerRing(course[i], s)) continue;
                Vector3 down = course[i].Axis;
                Vector3 press = PressOf(course, i, s);
                Vector3 across = course[i - 1].Axis;   // the heading the pilot arrives on
                Tiles(new Plate(press + down * s.PlateDepth, down, across, PlateHalf(s)), s, i, 0, into);

                if (i + 1 < course.Count && IsTowerRing(course[i + 1], s))
                {
                    Vector3 nextPress = PressOf(course, i + 1, s);
                    Tiles(new Plate(nextPress + down * (s.HopRingRadius + PipeClear + HullRadius), down, course[i + 1].Axis,
                                    PlateHalf(s)), s, i, 1, into);
                }
            }
        }

        /// <summary>
        /// Where the pilot presses for tower ring <paramref name="i"/>: on the line it arrives on, over the ring. Every
        /// ring but a tower's last lies <see cref="WaystationTowerSettings.RingDepth"/> under its press point; the last
        /// one (the plunge) lies deeper, so its press point is found from the hop before it.
        /// </summary>
        public static Vector3 PressOf(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s)
        {
            var ring = course[i];
            bool last = i + 1 >= course.Count || !IsTowerRing(course[i + 1], s);
            if (!last) return ring.Position - ring.Axis * s.RingDepth;
            var prev = course[i - 1];
            if (!IsTowerRing(prev, s)) return prev.Position + prev.Axis * Lead;   // the exit gate: a one-hop tower
            Vector3 lateralPress = prev.Position - prev.Axis * s.RingDepth;
            return lateralPress + prev.Axis * (s.WindowHigh + Runway(s, 180f));
        }

        // ── The lanterns and the crown gems, from the broadcast course alone ──

        /// <summary>
        /// Every lantern post of every hop that has a hop after it: two dotted posts either side of the drop line, from
        /// the window's near edge down to <see cref="LanternEnd"/>, a segment every <see cref="LanternGap"/> and every
        /// <see cref="LanternDeepGap"/> through the last <see cref="WaystationTowerSettings.DeepBand"/>. A tight hop's
        /// posts are visibly shorter: that is how a pilot reads the shelf before pressing. Pure, so every peer lays the
        /// same posts. The plunge has none: its deepest landing is the crown gem's business (and the cluster's).
        /// </summary>
        public static void Lanterns(IReadOnlyList<RaceGate> course, int intensity, List<WaystationLantern> into)
        {
            into.Clear();
            var s = WaystationTowerSettings.ForIntensity(intensity);
            float offset = s.HopRingRadius + PlateMargin;
            var scale = new Vector3(LanternSide, LanternSide, LanternSeg);
            for (int i = 1; i < course.Count; i++)
            {
                if (!HasShelf(course, i, s)) continue;
                float end = LanternEnd(course, i, s);
                if (end < s.WindowLow) continue;
                Vector3 axis = course[i].Axis;
                Vector3 press = PressOf(course, i, s);
                Vector3 across = RaceCourseGeometry.SafeNormalize(Vector3.Cross(axis, course[i - 1].Axis),
                                                                  RaceCourseGeometry.Perpendicular(axis));
                var rotation = Quaternion.LookRotation(axis, across);
                float deepFrom = Mathf.Max(s.WindowLow, end - s.DeepBand);
                for (int side = 0; side < 2; side++)
                {
                    Vector3 post = press + across * (side == 0 ? offset : -offset);
                    float d = end;
                    for (; d >= deepFrom - 1e-3f; d -= LanternDeepGap)
                        into.Add(new WaystationLantern(post + axis * d, rotation, scale, i, true));
                    for (d -= LanternGap - LanternDeepGap; d >= s.WindowLow - 1e-3f; d -= LanternGap)
                        into.Add(new WaystationLantern(post + axis * d, rotation, scale, i, false));
                }
            }
        }

        /// <summary>The crown gem of every tower (I4 only): one Time crystal on each plunge's drop line,
        /// <see cref="GemDepth"/> under its press point. Pure, so every peer stands it in the same place.</summary>
        public static void CrownGems(IReadOnlyList<RaceGate> course, int intensity, List<Vector3> into)
        {
            into.Clear();
            var s = WaystationTowerSettings.ForIntensity(intensity);
            if (s.CrownGem <= 0f) return;
            for (int i = 1; i < course.Count; i++)
                if (IsPlunge(course, i, s))
                    into.Add(PressOf(course, i, s) + course[i].Axis * GemDepth(course, i, s));
        }

        static void Tiles(in Plate plate, in WaystationTowerSettings s, int gate, int wall, List<WaystationTowerTile> into)
        {
            float tile = 2f * plate.Half / TilesPerSide;
            var rotation = Quaternion.LookRotation(plate.Normal, plate.U);
            var scale = new Vector3(tile, tile, TileThickness);
            int index = 0;
            for (int a = 0; a < TilesPerSide; a++)
            for (int b = 0; b < TilesPerSide; b++, index++)
            {
                Vector3 p = plate.Centre + plate.U * (-plate.Half + tile * (a + 0.5f)) + plate.V * (-plate.Half + tile * (b + 0.5f));
                into.Add(new WaystationTowerTile(p, rotation, scale, IsDangerTile(gate, wall, index, s.DangerShare), gate, wall));
            }
        }
    }
}
