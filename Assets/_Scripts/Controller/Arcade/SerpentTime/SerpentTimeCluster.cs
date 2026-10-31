using System.Collections.Generic;
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

        /// <summary>Rings per gate on intensity 1-4: three on the two open courses, two where the mouths shrink.</summary>
        public static readonly int[] DefaultRingsByIntensity = { 3, 3, 2, 2 };

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

        // ── Ring cover (Shombith, 2026-10-31: "rings get covered by prisms we shoot and find our way") ──

        /// <summary>Covered rings per gate on intensity 1-4: one of three, two of three, one of two, both of two.</summary>
        public static readonly int[] DefaultCoveredByIntensity = { 1, 2, 1, 2 };

        /// <summary>
        /// Is ring <paramref name="slot"/> of gate <paramref name="gateIndex"/> covered, when <paramref name="covered"/> of
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
        /// The centres of a square grid of cover slabs across <paramref name="ring"/>'s mouth, <paramref name="spacing"/>
        /// apart in the ring's plane, kept <paramref name="edgeMargin"/> inside its rim so the ring itself stays visible.
        /// A cell is kept when its centre sits within the trimmed mouth less a third of a cell, so the slabs reach the rim
        /// without poking past it. Clears <paramref name="into"/> first.
        /// </summary>
        public static void CoverCells(in RaceGate ring, float spacing, float edgeMargin, List<Vector3> into)
        {
            into.Clear();
            if (spacing <= 0f) return;
            float reach = ring.Radius - Mathf.Max(0f, edgeMargin) - spacing * 0.35f;
            if (reach <= 0f) { into.Add(ring.Position); return; }

            Vector3 axis = ring.Axis.sqrMagnitude > 1e-6f ? ring.Axis.normalized : Vector3.forward;
            Vector3 s = Vector3.Cross(axis, Vector3.up);
            if (s.sqrMagnitude < 1e-4f) s = Vector3.Cross(axis, Vector3.right);
            s.Normalize();
            Vector3 u = Vector3.Cross(s, axis);

            int n = Mathf.CeilToInt(reach / spacing);
            for (int a = -n; a <= n; a++)
            for (int b = -n; b <= n; b++)
            {
                float x = a * spacing, y = b * spacing;
                if (x * x + y * y > reach * reach) continue;
                into.Add(ring.Position + s * x + u * y);
            }
        }
    }
}
