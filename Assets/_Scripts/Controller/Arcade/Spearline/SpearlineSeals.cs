using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One intensity's cut of Spearline's seals (<see cref="SpearlineSeals.ForIntensity"/>).</summary>
    public struct SpearlineSealSettings
    {
        public int SealEvery;          // every Nth ring carries a seal (1: every ring); ring 0 never does
        public int ShieldEvery;        // every Nth SEALED ring is shielded (0: none, 1: all)
        public float OpenFraction;     // share of the seal inside HoleRadius of the crossing that must be gone
        public float RegrowSeconds;    // a cut seal prism grows back this long after the field has passed it
    }

    /// <summary>
    /// Spearline's seals (SPEARLINE.md): a pane of prisms laid IN a ring's mouth, square to its axis, on a hex grid. A
    /// sealed ring counts only when the pilot crosses it through a HOLE: of the seal prisms within
    /// <see cref="HoleRadius"/> of the crossing point (measured in the ring's plane), at least
    /// <see cref="SpearlineSealSettings.OpenFraction"/> must already be gone.
    ///
    /// <para><b>Why that is the Swordfish's Time question.</b> The Swordfish's hull rams a plain prism for free
    /// (<c>VesselDamagePrismEffect</c>, no slow), so a hull alone punches a lane about as wide as itself - under the
    /// fraction. The bill cuts a band along every swing, and Quickdraw (Time) is the sword's RATE: the swipe recovery
    /// 0.35 -> 0.14 s, the energize hold 1 -> 0.4 s. So the race is how much speed a pilot gives up at each seal to swing
    /// it open, and Time buys back that speed. A shielded seal takes two strokes in the same place (the first pops the
    /// shield), which is where the rate pays most.</para>
    ///
    /// <para>Pure and deterministic: a seal is a function of its broadcast ring, so every peer lays the same prisms and
    /// nothing new is replicated. The vessel is untouched (the LOCKED mode rule).</para>
    /// </summary>
    public static class SpearlineSeals
    {
        /// <summary>The hex grid's pitch, a prism's face and its depth along the ring's axis (u).</summary>
        public const float Spacing = 9f, PrismSize = 7.5f, PrismDepth = 2f;

        /// <summary>The disc stops this far inside the ring's mouth, so the seal never clips the ring's own frame.</summary>
        public const float RimInset = 2f;

        /// <summary>The hole a crossing is judged on: a little wider than the hull, so a ram alone does not open it.</summary>
        public const float HoleRadius = 22f;

        /// <summary>The MODEL of what the Swordfish cuts on the way through, used by the tests to hold the ladder (not read
        /// at runtime): the hull's half-width, and the half-width of the band one bill stroke clears across the pane.
        /// Model numbers off the Rhino airframe and the bill's capsule; a playtest moves them.</summary>
        public const float ModelHullRadius = 6f, ModelStrokeHalfWidth = 5f;

        /// <summary>Fewer seal prisms than this inside the hole (a crossing at the mouth's very edge) reads as open.</summary>
        public const int MinJudged = 4;

        public static SpearlineSealSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new SpearlineSealSettings
            {
                SealEvery = new[] { 2, 1, 1, 1 }[i],
                ShieldEvery = new[] { 0, 0, 2, 1 }[i],
                OpenFraction = new[] { 0.45f, 0.6f, 0.6f, 0.75f }[i],
                RegrowSeconds = new[] { 4f, 3.5f, 3f, 2.5f }[i],
            };
        }

        public static bool IsSealed(int ring, in SpearlineSealSettings s) =>
            ring >= 1 && s.SealEvery > 0 && ring % s.SealEvery == (s.SealEvery == 1 ? 0 : 1);

        /// <summary>Which sealed rings are shielded: counted among the sealed rings, so I3 alternates and I4 is all.</summary>
        public static bool IsShielded(int ring, in SpearlineSealSettings s)
        {
            if (!IsSealed(ring, s) || s.ShieldEvery <= 0) return false;
            int ordinal = s.SealEvery == 1 ? ring - 1 : (ring - 1) / s.SealEvery;
            return ordinal % s.ShieldEvery == s.ShieldEvery - 1;
        }

        public static PrismKind KindFor(int ring, in SpearlineSealSettings s) =>
            IsShielded(ring, s) ? PrismKind.Shielded : PrismKind.Plain;

        /// <summary>The ring's plane basis: two unit vectors square to its axis and to each other.</summary>
        public static void Basis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            axis = axis.normalized;
            Vector3 seed = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(axis, seed).normalized;
            v = Vector3.Cross(axis, u).normalized;
        }

        /// <summary>The seal prism centres for one ring, on a hex grid clipped to the mouth (less the inset).</summary>
        public static void Lay(in RaceGate gate, List<Vector3> into)
        {
            Basis(gate.Axis, out var u, out var v);
            float r = Mathf.Max(0f, gate.Radius - RimInset - 0.5f * PrismSize);
            float rowStep = Spacing * 0.8660254f;
            int rows = Mathf.CeilToInt(r / rowStep);
            for (int j = -rows; j <= rows; j++)
            {
                float y = j * rowStep;
                float shift = (j & 1) == 0 ? 0f : 0.5f * Spacing;
                int cols = Mathf.CeilToInt(r / Spacing) + 1;
                for (int k = -cols; k <= cols; k++)
                {
                    float x = k * Spacing + shift;
                    if (x * x + y * y > r * r) continue;
                    into.Add(gate.Position + u * x + v * y);
                }
            }
        }

        /// <summary>Where the segment <paramref name="prev"/>-<paramref name="cur"/> meets the ring's plane (false if it does not).</summary>
        public static bool PlaneCrossing(in RaceGate gate, Vector3 prev, Vector3 cur, out Vector3 point)
        {
            Vector3 n = gate.Axis.normalized;
            float a = Vector3.Dot(prev - gate.Position, n), b = Vector3.Dot(cur - gate.Position, n);
            point = default;
            if ((a > 0f) == (b > 0f) || Mathf.Approximately(a, b)) return false;
            float t = a / (a - b);
            point = Vector3.Lerp(prev, cur, t);
            return true;
        }

        /// <summary>The share of the seal inside the hole round <paramref name="crossing"/> that is gone, judged on the
        /// prisms whose centres are <paramref name="positions"/> and whose standing is <paramref name="standing"/>. A
        /// hole holding fewer than <see cref="MinJudged"/> seal prisms (the mouth's edge) answers 1.</summary>
        public static float OpenShare(in RaceGate gate, Vector3 crossing, IReadOnlyList<Vector3> positions,
                                      System.Func<int, bool> standing)
        {
            Vector3 n = gate.Axis.normalized;
            int judged = 0, gone = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 d = positions[i] - crossing;
                d -= n * Vector3.Dot(d, n);
                if (d.sqrMagnitude > HoleRadius * HoleRadius) continue;
                judged++;
                if (!standing(i)) gone++;
            }
            return judged < MinJudged ? 1f : gone / (float)judged;
        }
    }
}
