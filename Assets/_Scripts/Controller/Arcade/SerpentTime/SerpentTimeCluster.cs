using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Time's <b>ring clusters</b>: every gate of the course stands as two or three smaller rings side by side in
    /// the gate's plane, all visible, any one of which threads the gate (Shombith, 2026-10-27: "multiple rings in one
    /// place"). A wall plugs one ring; the others stay open, so walling is a choice for the racer behind and not a dead end.
    ///
    /// <para>Pure geometry, the same on every peer: each peer lays its own cluster from the course it was sent, so the
    /// layout depends only on the gate and its index. Sizes are shares of the gate's own mouth, so the course's per-
    /// intensity mouth ladder still sets how hard the race is. The rings never overlap: two neighbours sit
    /// <c>2 · spread · sin(π / k)</c> mouths apart, which <see cref="IsSeparated"/> holds above two ring radii.</para>
    ///
    /// <para>The studio prototype: <c>Docs/Studios/VesselStudio/serpent.html</c>; the brief: <c>Docs/Studios/SERPENT_TIME.md</c>.</para>
    /// </summary>
    public static class SerpentTimeCluster
    {
        /// <summary>How far each gate's cluster is turned from the previous gate's, so neighbouring gates do not line up.</summary>
        public const float TwistPerGateRadians = 1.7f;

        /// <summary>Rings per gate on intensity 1-4: one everywhere (2026-10-31). Clusters stay available.</summary>
        public static readonly int[] DefaultRingsByIntensity = { 1, 1, 1, 1 };

        /// <summary>
        /// Each ring's mouth and its distance from the gate's centre, as shares of the gate's mouth, for a
        /// <paramref name="rings"/>-ring cluster. Three rings: half a mouth each, 0.65 out. Two: 0.6 each, 0.7 out.
        /// </summary>
        public static void Layout(int rings, out float ringScale, out float spread)
        {
            if (rings >= 3) { ringScale = 0.5f; spread = 0.65f; }
            else if (rings == 2) { ringScale = 0.6f; spread = 0.7f; }
            else { ringScale = 1f; spread = 0f; }
        }

        /// <summary>Ring <paramref name="slot"/> of the default <see cref="Layout"/> for <paramref name="rings"/> rings.</summary>
        public static RaceGate Ring(in RaceGate gate, int gateIndex, int slot, int rings)
        {
            Layout(rings, out float ringScale, out float spread);
            return Ring(gate, gateIndex, slot, rings, ringScale, spread);
        }

        /// <summary>Rings per gate at <paramref name="intensity"/> (1-4), from a 4-entry ladder; 1 means the plain gate.</summary>
        public static int RingsFor(int intensity, int[] ladder)
        {
            if (ladder == null || ladder.Length == 0) return 1;
            return Mathf.Max(1, ladder[Mathf.Clamp(intensity, 1, ladder.Length) - 1]);
        }

        /// <summary>
        /// Ring <paramref name="slot"/> of <paramref name="rings"/> at <paramref name="gate"/>: its centre sits
        /// <paramref name="spread"/> mouths out from the gate's centre, in the gate's plane, and its mouth is
        /// <paramref name="ringScale"/> of the gate's. Same axis as the gate, so the race's flow is unchanged.
        /// </summary>
        public static RaceGate Ring(in RaceGate gate, int gateIndex, int slot, int rings, float ringScale, float spread)
        {
            if (rings <= 1) return gate;

            Vector3 axis = gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis.normalized : Vector3.forward;
            Vector3 s = Vector3.Cross(axis, Vector3.up);
            if (s.sqrMagnitude < 1e-4f) s = Vector3.Cross(axis, Vector3.right);
            s.Normalize();
            Vector3 u = Vector3.Cross(s, axis);

            float angle = gateIndex * TwistPerGateRadians + slot * (2f * Mathf.PI / rings);
            Vector3 offset = (Mathf.Cos(angle) * s + Mathf.Sin(angle) * u) * (spread * gate.Radius);
            return new RaceGate(gate.Position + offset, axis, Mathf.Max(1f, ringScale * gate.Radius));
        }

        /// <summary>Do neighbouring rings of a <paramref name="rings"/>-ring cluster stand clear of each other?</summary>
        public static bool IsSeparated(int rings, float ringScale, float spread) =>
            rings <= 1 || 2f * spread * Mathf.Sin(Mathf.PI / rings) > 2f * ringScale;

        /// <summary>Does every ring of the cluster stay inside a sphere of <paramref name="outer"/> mouths round the gate?</summary>
        public static bool FitsWithin(float ringScale, float spread, float outer) => spread + ringScale <= outer;

        // ── Guarded rings (Shombith, 2026-10-31: "one ring only or max 2", guarded by flora) ──

        /// <summary>
        /// Is ring <paramref name="slot"/> of gate <paramref name="gateIndex"/> guarded (covered), when <paramref name="covered"/> of
        /// its <paramref name="rings"/> rings are? The covered run starts at a slot picked from the gate's index, so which
        /// ring is open moves round from gate to gate and the same on every peer.
        /// </summary>
        public static bool IsCovered(int gateIndex, int slot, int rings, int covered)
        {
            rings = Mathf.Max(1, rings);
            covered = Mathf.Clamp(covered, 0, rings);
            if (covered == 0) return false;
            if (covered == rings) return true;
            int start = (int)((unchecked((uint)gateIndex * 2654435761u) >> 16) % (uint)rings);
            int k = ((slot - start) % rings + rings) % rings;
            return k < covered;
        }

        /// <summary>
        /// The playable domain a guard plant at ring <paramref name="slot"/> of gate <paramref name="gateIndex"/> grows in:
        /// Jade, Ruby or Gold by a hash of the two, the same on every peer and spread evenly over the course. Never Blue
        /// (<see cref="CellLifeSpawnerBase.PickRandomDomain"/>: a Blue lifeform is bait to every domain's fauna).
        /// </summary>
        public static Domains GuardDomain(int gateIndex, int slot)
        {
            uint h = unchecked((uint)gateIndex * 2654435761u ^ (uint)slot * 40503u);
            h ^= h >> 15;
            return (h % 3u) switch { 0u => Domains.Jade, 1u => Domains.Ruby, _ => Domains.Gold };
        }

        /// <summary>Where a gate's guard stands (Shombith, 2026-10-31: "don't just place them on the rings ... a little
        /// bit earlier or around the ring").</summary>
        public enum GuardPlacement
        {
            /// <summary>On the racing line, a little before the ring: dodge it, or hit its heart on the way in.</summary>
            Approach = 0,
            /// <summary>Beside the ring, its heart in the ring's plane off the centre: it half-covers the mouth.</summary>
            Beside = 1,
            /// <summary>A pair either side of the line a little before the ring: a narrow door to thread first.</summary>
            Door = 2,
        }

        /// <summary>
        /// The placement at gate <paramref name="gateIndex"/>: a fixed walk Beside, Approach, Door, Beside, ... so every lap
        /// mixes all three and every peer agrees. Gate 0 is always Beside, so nothing stands on the start line (the start
        /// is behind gate 0, pointed through it).
        /// </summary>
        public static GuardPlacement PlacementFor(int gateIndex) =>
            (GuardPlacement)(((Mathf.Max(0, gateIndex) * 2 + 1) % 3 + 3) % 3);

        /// <summary>
        /// Where the guard hearts of <paramref name="placement"/> go for ring <paramref name="slot"/> of gate
        /// <paramref name="gateIndex"/>, written into <paramref name="hearts"/> (length 2 or more); returns how many (1, or
        /// 2 for a door). "Before" is against the ring's axis (the flow), by a distance in <paramref name="approachDistance"/>
        /// picked from the gate; "beside" and the door's sides lie on a direction across the axis, also picked from the gate.
        /// The same on every peer.
        /// </summary>
        public static int GuardHearts(in RaceGate ring, int gateIndex, int slot, GuardPlacement placement,
                                      Vector2 approachDistance, float besideFraction, float doorHalfWidth, Vector3[] hearts)
        {
            Vector3 axis = ring.Axis.sqrMagnitude > 1e-6f ? ring.Axis.normalized : Vector3.forward;
            Vector3 s = Vector3.Cross(axis, Vector3.up);
            if (s.sqrMagnitude < 1e-4f) s = Vector3.Cross(axis, Vector3.right);
            s.Normalize();
            Vector3 u = Vector3.Cross(s, axis);

            float angle = Hash01(gateIndex, slot, 0x68e31da4u) * 2f * Mathf.PI;
            Vector3 across = Mathf.Cos(angle) * s + Mathf.Sin(angle) * u;
            float back = Mathf.Lerp(Mathf.Min(approachDistance.x, approachDistance.y),
                                    Mathf.Max(approachDistance.x, approachDistance.y), Hash01(gateIndex, slot, 0xb5297a4du));

            switch (placement)
            {
                case GuardPlacement.Beside:
                    hearts[0] = ring.Position + across * (ring.Radius * besideFraction);
                    return 1;
                case GuardPlacement.Door:
                    Vector3 door = ring.Position - axis * back;
                    hearts[0] = door + across * doorHalfWidth;
                    hearts[1] = door - across * doorHalfWidth;
                    return 2;
                default:
                    hearts[0] = ring.Position - axis * back;
                    return 1;
            }
        }

        static float Hash01(int gateIndex, int slot, uint salt)
        {
            uint h = unchecked((uint)gateIndex * 2654435761u ^ (uint)slot * 40503u ^ salt);
            h ^= h >> 15;
            h = unchecked(h * 2246822519u);
            h ^= h >> 13;
            return (h & 0xFFFFFFu) / (float)0x1000000;
        }

        /// <summary>
        /// Does a sniper round fired from <paramref name="origin"/> along <paramref name="forward"/> (unit) pass through a
        /// guard's heart at <paramref name="heart"/>? In front of the muzzle, within the round's <paramref name="range"/>, and
        /// within <paramref name="hitRadius"/> of its line (or inside its cone of <paramref name="coneHalfAngleDegrees"/>
        /// there, where the cone is the wider). The rifle's own geometry asked of one point; the rifle is never changed.
        /// </summary>
        public static bool RoundPassesHeart(Vector3 origin, Vector3 forward, float range, float coneHalfAngleDegrees,
                                            Vector3 heart, float hitRadius)
        {
            Vector3 to = heart - origin;
            float along = Vector3.Dot(to, forward);
            if (along <= 0f || along > range) return false;
            float lateral = (to - forward * along).magnitude;
            float cone = along * Mathf.Tan(Mathf.Clamp(coneHalfAngleDegrees, 0f, 89f) * Mathf.Deg2Rad);
            return lateral <= Mathf.Max(hitRadius, cone);
        }
    }
}
