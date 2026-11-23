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

    }
}
