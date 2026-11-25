using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Lists" - Riposte's arena, the Swordfish's CHARGE game (<c>Assets/_Scripts/Controller/Arcade/RIPOSTE.md</c>
    /// section 4): Jackstraw's heap of pick-up sticks with a jousting yard cut into it. It IS a
    /// <see cref="SpawnableJackstraw"/> (the clearings and the heap radius are the base's fields, so the controller
    /// floats the crystals and the AI reads the heap exactly as in Jackstraw), but it lays its own pieces, each placed
    /// by a rule in <see cref="RiposteYard"/>:
    ///
    ///   • <b>Lanes</b> - straight tubes no stick enters, from the centre out through the first
    ///     <see cref="laneCount"/> ring clearings. The ramp winds up on them.
    ///   • <b>Rails</b> - <see cref="railsPerLane"/> rows of pickets round each lane, parallel to it: fuel the bill
    ///     cuts without leaving the line.
    ///   • <b>Gates</b> - a jack on a lane's axis whose four cross arms are shielded.
    ///   • <b>Loose sticks and jacks</b> - Jackstraw's scatter, between the lanes. Those within
    ///     <see cref="thornBand"/> of a lane wall carry red tips; those within <see cref="ironwoodCoreRadius"/> of
    ///     the centre have an ironwood heart.
    ///
    /// <para>Every draw is <see cref="CellEnvironmentSpawnableBase.Hash01"/> of an integer key and every rejection is
    /// deterministic geometry, so the layout is the same on every peer and is mirrored exactly by
    /// <c>Tools/Build/riposte_yard_budget.py</c>. The base's stickCount and jackCount are left at 0 on these prefabs:
    /// this class lays everything. No intensity is read: one prefab per rung. Every prism is neutral
    /// (<see cref="Domains.Blue"/>). Authored by <c>Tools/Build/author_riposte_assets.py</c>.</para>
    /// </summary>
    public class SpawnableRiposteYard : SpawnableJackstraw
    {
        [Header("Lanes (straight, stick-free: the ramp winds up here)")]
        [Tooltip("Lanes, each from the centre out through one ring clearing (the first N of them). No stick enters a lane.")]
        [SerializeField, Min(0)] int laneCount = 4;
        [Tooltip("Radius (u) of a lane's empty tube.")]
        [SerializeField, Min(10f)] float laneRadius = 45f;

        [Header("Rails (pickets along a lane: fuel on the line)")]
        [Tooltip("Rows of pickets round each lane, parallel to it, just outside its wall. 0 = none.")]
        [SerializeField, Min(0)] int railsPerLane = 4;

        [Header("Gates (a jack across a lane, cross arms shielded)")]
        [Tooltip("Every Nth lane (0, N, 2N...) carries gates. 0 = no lane is gated; 1 = every lane.")]
        [SerializeField, Min(0)] int gateEveryNthLane = 0;
        [Tooltip("Gates on a gated lane, spaced evenly between the centre clearing and the lane's refuel clearing.")]
        [SerializeField, Min(0)] int gatesPerLane = 1;

        [Header("The scatter between the lanes")]
        [Tooltip("Loose sticks (a stick that cannot find room after its tries is skipped).")]
        [SerializeField, Min(0)] int looseSticks = 24;
        [Tooltip("Loose jacks (three sticks round a shielded knot). Laid before the loose sticks.")]
        [SerializeField, Min(0)] int looseJacks = 0;
        [Tooltip("A loose stick within this distance (u) of a lane's wall carries red tips. 0 = none.")]
        [SerializeField, Min(0f)] float thornBand = 0f;
        [Tooltip("A loose stick centred within this distance (u) of the arena centre has an ironwood heart. 0 = none.")]
        [SerializeField, Min(0f)] float ironwoodCoreRadius = 0f;

        /// <summary>Key offsets into <see cref="CellEnvironmentSpawnableBase.Hash01"/>, apart from Jackstraw's.</summary>
        public const int LooseJackKeyBase = 3_000_000, LooseStickKeyBase = 4_000_000;

        protected override int DefaultSeed => 9797;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                base.BuildParameterHash(),
                System.HashCode.Combine(laneCount, laneRadius, railsPerLane, gateEveryNthLane, gatesPerLane),
                System.HashCode.Combine(looseSticks, looseJacks, thornBand, ironwoodCoreRadius, 1));

        readonly List<Clearing> _yardClearings = new();
        readonly List<Vector3> _a = new(), _b = new();
        readonly List<Vector3> _laneEnd = new();

        int YardSeed => seed != 0 ? seed : DefaultSeed;

        int YardKey(int family, int slot, int attempt, int draw) =>
            unchecked(YardSeed * 7919 + family + (slot * StickTries + attempt) * 4 + draw);

        static Vector3 Unit(int key)
        {
            float u = 2f * Hash01(key) - 1f;
            float phi = 2f * Mathf.PI * Hash01(unchecked(key + 7919));
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
            return new Vector3(s * Mathf.Cos(phi), s * Mathf.Sin(phi), u);
        }

        static Vector3 Ball(int key, float r) =>
            Unit(key) * (r * Mathf.Pow(Hash01(unchecked(key + 104729)), 1f / 3f));

        static Quaternion Face(Vector3 axis) =>
            Quaternion.LookRotation(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);

        protected override void BuildEnvironment()
        {
            _yardClearings.Clear();
            CollectClearings(_yardClearings);
            _a.Clear();
            _b.Clear();
            _laneEnd.Clear();

            bool hasCentre = _yardClearings.Count > 0 && _yardClearings[0].Centre == Vector3.zero;
            float centreRadius = hasCentre ? _yardClearings[0].Radius : 0f;
            int ring = _yardClearings.Count - (hasCentre ? 1 : 0);
            int lanes = Mathf.Min(laneCount, ring);
            for (int k = 0; k < lanes; k++) _laneEnd.Add(RiposteYard.LaneAxis(k, ring) * HeapRadius);

            for (int k = 0; k < lanes; k++) LayGates(k, ring, centreRadius, _yardClearings[(hasCentre ? 1 : 0) + k]);
            for (int k = 0; k < lanes; k++) LayRails(k, ring, centreRadius);
            for (int j = 0; j < looseJacks; j++) TryLayLooseJack(j);
            for (int s = 0; s < looseSticks; s++) TryLayLooseStick(s);
        }

        // ── Room ──

        bool ClearOfYardClearings(Vector3 a, Vector3 b)
        {
            for (int i = 0; i < _yardClearings.Count; i++)
            {
                float r = _yardClearings[i].Radius + Section;
                if (RiposteYard.PointSegmentSqr(_yardClearings[i].Centre, a, b) < r * r) return false;
            }
            return true;
        }

        bool ClearOfLaid(Vector3 a, Vector3 b)
        {
            float g2 = StickGap * StickGap;
            for (int i = 0; i < _a.Count; i++)
                if (SegmentSegmentSqr(a, b, _a[i], _b[i]) < g2) return false;
            return true;
        }

        /// <summary>Least distance (u) from segment a-b to any lane's axis; infinity with no lanes.</summary>
        float LaneDistance(Vector3 a, Vector3 b)
        {
            float best = float.PositiveInfinity;
            for (int k = 0; k < _laneEnd.Count; k++)
                best = Mathf.Min(best, SegmentSegmentSqr(a, b, Vector3.zero, _laneEnd[k]));
            return Mathf.Sqrt(best);
        }

        bool ClearOfLanes(Vector3 a, Vector3 b) => LaneDistance(a, b) >= laneRadius + Section;

        void Claim(Vector3 a, Vector3 b)
        {
            _a.Add(a);
            _b.Add(b);
        }

        // ── Gates ──

        void LayGates(int lane, int ring, float centreRadius, Clearing refuel)
        {
            if (!RiposteYard.GatedLane(lane, gateEveryNthLane)) return;
            var axis = RiposteYard.LaneAxis(lane, ring);
            RiposteYard.Basis(axis, out var e1, out var e2);
            float from = centreRadius, to = refuel.Centre.magnitude - refuel.Radius;
            var size = new Vector3(Section, Section, Segment * Flush);
            for (int m = 0; m < gatesPerLane; m++)
            {
                var centre = axis * RiposteYard.GateDistance(from, to, m, gatesPerLane);
                Claim(centre - axis * JackReach, centre + axis * JackReach);
                Claim(centre - e1 * JackReach, centre + e1 * JackReach);
                Claim(centre - e2 * JackReach, centre + e2 * JackReach);
                Emit(centre, Quaternion.LookRotation(e2, e1), Vector3.one * Knot, Domains.Blue, PrismKind.Shielded);
                LayArm(centre, axis, size, PrismKind.Plain);
                LayArm(centre, -axis, size, PrismKind.Plain);
                LayArm(centre, e1, size, PrismKind.Shielded);
                LayArm(centre, -e1, size, PrismKind.Shielded);
                LayArm(centre, e2, size, PrismKind.Shielded);
                LayArm(centre, -e2, size, PrismKind.Shielded);
            }
        }

        void LayArm(Vector3 centre, Vector3 axis, Vector3 size, PrismKind kind)
        {
            var rot = Face(axis);
            for (int k = 0; k < JackArmPrisms; k++)
                Emit(centre + axis * (Knot * 0.5f + Segment * (k + 0.5f)), rot, size, Domains.Blue, kind);
        }

        // ── Rails ──

        void LayRails(int lane, int ring, float centreRadius)
        {
            var axis = RiposteYard.LaneAxis(lane, ring);
            float half = RiposteYard.PicketLength * 0.5f;
            var rot = Face(axis);
            var size = new Vector3(Section, Section, Segment * Flush);
            for (int p = 0; p < railsPerLane; p++)
            {
                var offset = RiposteYard.RailOffset(axis, lane, p, railsPerLane, laneRadius);
                for (int j = 0; ; j++)
                {
                    float along = RiposteYard.PicketCentre(centreRadius, j);
                    Vector3 a = axis * (along - half) + offset, b = axis * (along + half) + offset;
                    if (b.magnitude > HeapRadius) break;
                    if (!ClearOfYardClearings(a, b) || !ClearOfLaid(a, b)) continue;
                    Claim(a, b);
                    for (int k = 0; k < RiposteYard.PicketPrisms; k++)
                        Emit(a + axis * (Segment * (k + 0.5f)), rot, size, Domains.Blue);
                }
            }
        }

        // ── The scatter ──

        void TryLayLooseStick(int slot)
        {
            float half = StickHalf;
            for (int t = 0; t < StickTries; t++)
            {
                var centre = Ball(YardKey(LooseStickKeyBase, slot, t, 0), HeapRadius - half);
                var axis = Unit(YardKey(LooseStickKeyBase, slot, t, 1));
                Vector3 a = centre - axis * half, b = centre + axis * half;
                if (!ClearOfYardClearings(a, b) || !ClearOfLanes(a, b) || !ClearOfLaid(a, b)) continue;
                Claim(a, b);

                bool thorned = RiposteYard.Thorned(LaneDistance(a, b), laneRadius, thornBand);
                bool ironwood = RiposteYard.Ironwood(centre, ironwoodCoreRadius);
                var rot = Face(axis);
                var size = new Vector3(Section, Section, Segment * Flush);
                float start = -half + Segment * 0.5f;
                for (int k = 0; k < StickPrisms; k++)
                {
                    var kind = PrismKind.Plain;
                    if (thorned && (k == 0 || k == StickPrisms - 1)) kind = PrismKind.Danger;
                    else if (ironwood && k >= IronwoodFrom && k < IronwoodTo) kind = PrismKind.Shielded;
                    Emit(centre + axis * (start + Segment * k), rot, size, Domains.Blue, kind);
                }
                return;
            }
        }

        void TryLayLooseJack(int slot)
        {
            float reach = JackReach;
            for (int t = 0; t < StickTries; t++)
            {
                var centre = Ball(YardKey(LooseJackKeyBase, slot, t, 0), HeapRadius - reach);
                var x = Unit(YardKey(LooseJackKeyBase, slot, t, 1));
                RiposteYard.Basis(x, out var y, out var z);
                bool room = Room(centre, x, reach) && Room(centre, y, reach) && Room(centre, z, reach);
                if (!room) continue;
                Claim(centre - x * reach, centre + x * reach);
                Claim(centre - y * reach, centre + y * reach);
                Claim(centre - z * reach, centre + z * reach);
                Emit(centre, Quaternion.LookRotation(z, y), Vector3.one * Knot, Domains.Blue, PrismKind.Shielded);
                var size = new Vector3(Section, Section, Segment * Flush);
                LayArm(centre, x, size, PrismKind.Plain);
                LayArm(centre, -x, size, PrismKind.Plain);
                LayArm(centre, y, size, PrismKind.Plain);
                LayArm(centre, -y, size, PrismKind.Plain);
                LayArm(centre, z, size, PrismKind.Plain);
                LayArm(centre, -z, size, PrismKind.Plain);
                return;
            }
        }

        bool Room(Vector3 centre, Vector3 axis, float reach)
        {
            Vector3 a = centre - axis * reach, b = centre + axis * reach;
            return ClearOfYardClearings(a, b) && ClearOfLanes(a, b) && ClearOfLaid(a, b);
        }
    }
}
