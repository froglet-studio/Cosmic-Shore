using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One rung of the race garden (SPEARLINE.md §2b): how much it helps the Swordfish and how much it costs.</summary>
    public struct SpearlineGardenSettings
    {
        public bool HedgeBothSides;     // a hedge each side of the line, or one side only (alternating leg to leg)
        public float HedgeLength;       // u along the leg
        public bool HedgeShieldSkin;    // the line-facing row is shielded: a stroke only cracks it first
        public bool HedgeDangerCaps;    // a danger column caps each end of a hedge
        public int Pillars;             // pillars a lap, standing on the line of legs into sealed rings
        public PrismKind PillarKind;    // plain (cut through it), shielded
        public bool PillarDangerBands;  // every other band of a pillar is danger
    }

    /// <summary>One garden prism: where it stands, how it is turned, its size and its kind.</summary>
    public readonly struct SpearlineGardenPart
    {
        public readonly Vector3 Position, Forward, Up, Scale;
        public readonly PrismKind Kind;
        public readonly bool Pillar;   // a pillar's (a cost) or a hedge's (fuel)
        public readonly int Leg;

        public SpearlineGardenPart(Vector3 position, Vector3 forward, Vector3 up, Vector3 scale, PrismKind kind, bool pillar,
                                   int leg)
        {
            Position = position;
            Forward = forward;
            Up = up;
            Scale = scale;
            Kind = kind;
            Pillar = pillar;
            Leg = leg;
        }
    }

    /// <summary>
    /// Spearline's race garden (Yash, 2026-11-25: "shape the environment by intensity, easier at I1 to harder at I4, so
    /// the Swordfish can take advantage of it"). Every piece has a reason in the Swordfish's own kit:
    /// <list type="bullet">
    /// <item><b>Fuel hedges</b> (help): rows of plain prisms either side of the flown line, outside the hull's path and
    /// inside a stroke's reach. The ramp runs on what the bill slices (RHINO_RAMP_BOOST.md § Fuel) and Spearline lays no
    /// other mass on the line, so a hedge is ramp. They stand only on legs into UNSEALED rings, where the Spearline coach
    /// leaves the AI's sword hand free, so the AI exploits them too.</item>
    /// <item><b>Pillars</b> (cost): a column standing across the line mid-leg on legs into SEALED rings. Dodging it
    /// breaks the straight and bleeds the ramp before the seal; ramming it slows the hull (a danger band hard).
    /// Prisms are not walls, so a pillar can cost time and never trap anyone.</item>
    /// </list>
    /// The rungs trade help for cost: I1 hedges on both sides and no pillar; I4 short, danger-capped hedges on one side
    /// and three pillars. Pure: a function of the lap and the rung, so every peer lays the same garden from the broadcast
    /// rings and <c>SpearlineGardenTests</c> holds each placement rule. Nothing here touches the vessel, the seals, or
    /// <see cref="SpearlineRuins"/> (the other Time games' generator).
    /// </summary>
    public static class SpearlineGarden
    {
        /// <summary>A hedge prism's centre stands at least this far (u) from the flown line (measured across the leg's
        /// local frame; on a bending leg it can sit up to about 2 u further out): the hull (reach about 10 u,
        /// <see cref="SpearlineSeals.HullReach"/>) and an Easy AI's lane error (10 u) pass clear of it.</summary>
        public const float HedgeInner = 22f;

        /// <summary>...and at most this far: inside a stroke of the bill.</summary>
        public const float HedgeOuter = 34f;

        /// <summary>Hedge prisms: spacing along the leg, the three heights, and the size.</summary>
        public const float HedgeSpacing = 10f, HedgeTier = 9f;
        public static readonly Vector3 HedgeScale = new(8f, 8f, 9f);

        /// <summary>A pillar: its radius round its own axis, its half-height across the line, and where on its leg.</summary>
        public const float PillarRadius = 16f, PillarHalfHeight = 50f, PillarAt = 0.4f;
        public const int PillarAround = 8;
        public static readonly Vector3 PillarScale = new(9f, 9f, 9f);

        /// <summary>No garden prism within this distance (u) of a ring's mouth (the seals and the last look at a pane).</summary>
        public const float RingMargin = SpearlineRuins.RingMargin;

        /// <summary>No garden prism within this distance (u) of ANOTHER leg's flown line: nothing stands in a path
        /// it was not placed for.</summary>
        public const float OtherLaneClear = SpearlineRuins.LaneRadius;

        public static SpearlineGardenSettings ForIntensity(int intensity)
        {
            switch (Mathf.Clamp(intensity, 1, 4))
            {
                case 1: return new SpearlineGardenSettings
                {
                    HedgeBothSides = true, HedgeLength = 200f, Pillars = 0, PillarKind = PrismKind.Plain,
                };
                case 2: return new SpearlineGardenSettings
                {
                    HedgeBothSides = false, HedgeLength = 160f, Pillars = 1, PillarKind = PrismKind.Plain,
                };
                case 3: return new SpearlineGardenSettings
                {
                    HedgeBothSides = false, HedgeLength = 140f, HedgeShieldSkin = true, Pillars = 2,
                    PillarKind = PrismKind.Shielded,
                };
                default: return new SpearlineGardenSettings
                {
                    HedgeBothSides = false, HedgeLength = 100f, HedgeShieldSkin = true, HedgeDangerCaps = true, Pillars = 3,
                    PillarKind = PrismKind.Shielded, PillarDangerBands = true,
                };
            }
        }

        /// <summary>
        /// The garden of <paramref name="lap"/> on this rung, appended to <paramref name="into"/>. <paramref name="sealed"/>
        /// is the lap's sealed rings (<see cref="SpearlineSeals.SealedRings"/>). The leg that closes the lap (into ring 0,
        /// where the field starts) carries nothing. A prism too near a ring or in another leg's lane is left out; a
        /// pillar is laid whole or not at all.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> lap, IReadOnlyList<bool> @sealed, in SpearlineGardenSettings s,
                               List<SpearlineGardenPart> into)
        {
            int n = lap.Count;
            if (n < 3) return;
            int hedgeLegs = 0, pillars = 0;
            for (int leg = 0; leg < n - 1; leg++)
            {
                int next = leg + 1;
                if (!@sealed[next])
                {
                    int first = into.Count;
                    float side = (hedgeLegs & 1) == 0 ? 1f : -1f;
                    LayHedge(lap, leg, side, s, into);
                    if (s.HedgeBothSides) LayHedge(lap, leg, -side, s, into);
                    Clear(lap, leg, into, first);
                    if (into.Count > first) hedgeLegs++;
                }
                else if (pillars < s.Pillars)
                {
                    int first = into.Count;
                    LayPillar(lap, leg, s, into);
                    bool whole = true;
                    for (int i = first; i < into.Count; i++) whole &= Stands(lap, leg, into[i].Position);
                    if (whole) pillars++;
                    else into.RemoveRange(first, into.Count - first);
                }
            }
        }

        static void LayHedge(IReadOnlyList<RaceGate> lap, int leg, float side, in SpearlineGardenSettings s,
                             List<SpearlineGardenPart> into)
        {
            var a = lap[leg];
            var b = lap[leg + 1];
            float legLength = (b.Position - a.Position).magnitude;
            if (legLength <= 0f) return;
            int stations = Mathf.Max(2, Mathf.RoundToInt(s.HedgeLength / HedgeSpacing));
            // Centred a little before the middle of the leg, the stretch where a ramp is wound up.
            float t0 = 0.45f - 0.5f * s.HedgeLength / legLength;
            float dt = HedgeSpacing / legLength;
            for (int k = 0; k < stations; k++)
            {
                float t = t0 + k * dt;
                if (t <= 0f || t >= 1f) continue;
                Vector3 at = SpearlineRuins.LinePoint(a, b, t);
                Vector3 dir = SpearlineRuins.LineDirection(a, b, t);
                SpearlineSeals.Basis(dir, out var u, out var v);
                bool cap = s.HedgeDangerCaps && (k == 0 || k == stations - 1);
                for (int tier = -1; tier <= 1; tier++)
                {
                    Vector3 up = v * (tier * HedgeTier);
                    if (s.HedgeShieldSkin)
                    {
                        Add(at + u * (side * HedgeInner) + up, dir, v, cap ? PrismKind.Danger : PrismKind.Shielded, leg, into);
                        Add(at + u * (side * HedgeOuter) + up, dir, v, cap ? PrismKind.Danger : PrismKind.Plain, leg, into);
                    }
                    else
                    {
                        Add(at + u * (side * 0.5f * (HedgeInner + HedgeOuter)) + up, dir, v,
                            cap ? PrismKind.Danger : PrismKind.Plain, leg, into);
                    }
                }
            }
        }

        static void Add(Vector3 p, Vector3 forward, Vector3 up, PrismKind kind, int leg, List<SpearlineGardenPart> into) =>
            into.Add(new SpearlineGardenPart(p, forward, up, HedgeScale, kind, false, leg));

        /// <summary>A column across the line: rings of prisms round an axis square to the flight, centred on the line.</summary>
        static void LayPillar(IReadOnlyList<RaceGate> lap, int leg, in SpearlineGardenSettings s, List<SpearlineGardenPart> into)
        {
            var a = lap[leg];
            var b = lap[leg + 1];
            Vector3 c = SpearlineRuins.LinePoint(a, b, PillarAt);
            Vector3 dir = SpearlineRuins.LineDirection(a, b, PillarAt);
            SpearlineSeals.Basis(dir, out var u, out var v);
            // Its axis leans by a seeded angle in the plane across the line, so pillars do not all stand alike.
            float lean = SpearlineSeals.Hash01(leg, 5, 91) * Mathf.PI;
            Vector3 axis = u * Mathf.Cos(lean) + v * Mathf.Sin(lean);
            Vector3 side = Vector3.Cross(dir, axis).normalized;
            int bands = Mathf.RoundToInt(2f * PillarHalfHeight / PillarScale.y);
            for (int band = 0; band < bands; band++)
            {
                float h = -PillarHalfHeight + (band + 0.5f) * (2f * PillarHalfHeight / bands);
                PrismKind kind = s.PillarDangerBands && (band & 1) == 1 ? PrismKind.Danger : s.PillarKind;
                for (int k = 0; k < PillarAround; k++)
                {
                    float angle = 2f * Mathf.PI * (k + 0.5f * (band & 1)) / PillarAround;
                    Vector3 radial = dir * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                    into.Add(new SpearlineGardenPart(c + axis * h + radial * PillarRadius, axis, radial, PillarScale, kind,
                                                     true, leg));
                }
            }
        }

        // ── Clearance ────────────────────────────────────────────────────

        static void Clear(IReadOnlyList<RaceGate> lap, int leg, List<SpearlineGardenPart> parts, int first)
        {
            int keep = first;
            for (int i = first; i < parts.Count; i++)
            {
                if (!Stands(lap, leg, parts[i].Position)) continue;
                parts[keep++] = parts[i];
            }
            parts.RemoveRange(keep, parts.Count - keep);
        }

        /// <summary>May a garden prism of leg <paramref name="leg"/> stand at <paramref name="p"/>: clear of every ring's
        /// mouth, and out of every OTHER leg's lane?</summary>
        public static bool Stands(IReadOnlyList<RaceGate> lap, int leg, Vector3 p)
        {
            if (SpearlineRuins.RingClearance(lap, p) < RingMargin) return false;
            for (int other = 0; other < lap.Count; other++)
                if (other != leg && SpearlineRuins.LegDistance(lap, other, p) < OtherLaneClear) return false;
            return true;
        }
    }
}
