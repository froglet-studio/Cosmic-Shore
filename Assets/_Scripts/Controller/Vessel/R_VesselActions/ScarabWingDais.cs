using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pose/scale/tier set of the <b>scarab-wing dais</b> — the sun-disc rosette a
    /// <see cref="ScarabSwitch"/> lays when a ball threads it (SCARAB.md §5). Pure closed-form
    /// geometry with no Unity scene dependency, so it is edit-mode testable
    /// (<c>ScarabWingDaisTests</c>) and bit-identical on every peer, which is the whole
    /// requirement for a prism structure that is re-built locally rather than replicated.
    ///
    /// <para><b>The motif.</b> <see cref="ScarabWingDaisSettings.PairCount"/> mirrored WING PAIRS
    /// evenly spaced around the ring. Each wing is a FAN of blade prisms rooted at a shoulder just
    /// off its pair's axis, sweeping from "pointing radially outward, alongside the sun" round to
    /// "pointing tangentially, reaching into the neighbouring pair", with blade LENGTH and WIDTH
    /// rising together along the sweep. The pair's two fans meet in a chevron on the pair axis and
    /// their long outer blades shingle over the neighbouring pairs' — so the pattern is continuous
    /// all the way round, which is what makes it read as one dais rather than ten ornaments.
    /// A super-shielded CUBE sits on each pair's axis in the crook of its two wings: the scarab's
    /// sun disc, which the stellation renders as an eight-pointed star.</para>
    ///
    /// <para><b>Why a fan and not a tiled petal.</b> A blade only reads as its own unit when the
    /// next blade's TIP has moved further than the blade is WIDE, i.e. when the fan's angular step
    /// exceeds <c>width / length</c> — about 7.6° at the shipped sizes, so a wing needs ~60°+ of
    /// sweep. Twenty wings around a circle have 18° each. Tiling and legibility are therefore
    /// incompatible at ten pairs, and the shingle is the resolution, not a compromise: it is also
    /// what the reference scarab does, where the two wings overlap each other and the body.
    /// A version that constrained each wing inside its own 18° sector was built and rejected —
    /// its blades nest into a single spike.</para>
    ///
    /// <para><b>Sizes are stated, not grown</b>, so every consumer must widen the prism's scale
    /// window (<c>Prism.AdmitTargetScale</c>) before assigning <c>TargetScale</c> — see
    /// <see cref="ScarabSwitch"/>. The longest blade is ~1.9× the ring radius, far past the
    /// interactive prism pool's authored (40, 10, 10) ceiling.</para>
    /// </summary>
    public static class ScarabWingDais
    {
        /// <summary>
        /// Uniform scale correction for a SHIELDED blade so its octahedron occupies the same
        /// envelope as the plain blade it stands in for.
        ///
        /// <para>A shield's semi-axes are <c>CIRCUMSCRIBING_SCALE × the box HALF-extents</c>, i.e.
        /// <c>1.5 × the box's full size</c> — so an unfitted shielded blade would render THREE
        /// TIMES the length of its neighbours and the size gradient the wing is built on would be
        /// destroyed by a tier that is supposed to be a texture. Fitting the PRISM (never the
        /// pattern) is <c>Docs/ECOSYSTEM.md §35</c>'s rule; the factor is derived from the
        /// generator's own constant so it cannot drift, and it is UNIFORM so the blade's aspect —
        /// its identity — is exact.</para>
        ///
        /// <para>Cost: the box volume falls by 27× while the shield multiplies mass by 4.5, so a
        /// shielded blade is ~1/6 the mass of the plain blade it replaces. That is the honest
        /// price of a same-size diamond and is why the tier reads as texture rather than as bulk.</para>
        /// </summary>
        public static readonly float ShieldedFit = 1f / OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        /// <summary>
        /// Authored-edge → apparent-extent factor for the SUPER-shielded sun core. The stellation's
        /// spike tips sit at the corners of a cube <c>CIRCUMSCRIBING_SCALE ×</c> the authored one,
        /// so a designer states the star they want to SEE and the cube is derived.
        /// </summary>
        public static readonly float SunApparentFactor = StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        /// <summary>One prism of the dais, in world space, ready to lay.</summary>
        public readonly struct Element
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public readonly PrismKind Kind;
            /// <summary>Which mirrored pair this belongs to, 0..PairCount-1.</summary>
            public readonly int Pair;
            /// <summary>+1 / -1 for the two wings of the pair; 0 for the sun core.</summary>
            public readonly int WingSign;
            /// <summary>Blade index along its wing; -1 for the sun core.</summary>
            public readonly int Feather;

            public Element(Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind,
                           int pair, int wingSign, int feather)
            {
                Position = position; Rotation = rotation; Scale = scale; Kind = kind;
                Pair = pair; WingSign = wingSign; Feather = feather;
            }

            public bool IsSunCore => Feather < 0;
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the whole dais, ordered <b>outward along the wings</b>
        /// (every wing's blade 0, then every wing's blade 1, …) and the ten sun cores LAST — so a
        /// budgeted lay blooms the rosette from the ring outward and ignites the suns at the end,
        /// rather than filling one wing at a time.
        /// </summary>
        /// <param name="settings">Authored shape (all distances are multiples of <paramref name="ringRadius"/>).</param>
        /// <param name="center">The switch's centre.</param>
        /// <param name="axis">The dais normal — the switch's placement axis (the vessel's course).</param>
        /// <param name="basisU">Unit vector in the dais plane; the pair-0 axis direction.</param>
        /// <param name="basisV">The in-plane vector completing a right-handed frame (<c>basisU × basisV == axis</c>).</param>
        /// <param name="ringRadius">The switch ring's radius. Everything scales off it, so the Mass
        /// element grows the whole rosette with the ring and nothing needs a second dial.</param>
        public static void Generate(in ScarabWingDaisSettings settings, Vector3 center, Vector3 axis,
                                    Vector3 basisU, Vector3 basisV, float ringRadius,
                                    List<Element> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();

            int pairs = Mathf.Max(1, settings.PairCount);
            int feathers = Mathf.Max(1, settings.FeathersPerWing);
            float R = Mathf.Max(0.01f, ringRadius);

            // The dish is a shallow bowl keyed on planar radius, so it needs the rosette's own
            // outer reach as its reference. That is the outermost blade's tip, which is identical
            // on every wing — one evaluation, before the loop.
            float reach = OuterReach(settings, R);

            float thickness = R * settings.BladeThickness;
            float sunEdge = R * settings.SunApparentDiameter / SunApparentFactor;
            float sunRadius = R * settings.SunRadius;

            for (int f = 0; f < feathers; f++)
            {
                float t = feathers > 1 ? f / (float)(feathers - 1) : 0.5f;
                PrismKind kind = KindAt(settings, f);
                float fit = kind == PrismKind.Shielded ? ShieldedFit : 1f;

                float length = R * Mathf.LerpUnclamped(settings.BladeLengthStart, settings.BladeLengthEnd,
                                                       Shape(t, settings.BladeGrowthShape));
                float width = R * Mathf.LerpUnclamped(settings.BladeWidthStart, settings.BladeWidthEnd,
                                                      Shape(t, settings.BladeGrowthShape));
                float rollDeg = Mathf.LerpUnclamped(0f, settings.BladeRollEndDeg, t);

                for (int p = 0; p < pairs; p++)
                {
                    float pairDeg = p * 360f / pairs;
                    for (int s = 1; s >= -1; s -= 2)
                    {
                        Blade(settings, R, pairDeg, s, t, length, out Vector2 root2, out Vector2 tip2);

                        Vector3 root = Plane(center, basisU, basisV, root2)
                                     + axis * Dish(settings, R, root2.magnitude, reach);
                        Vector3 tip = Plane(center, basisU, basisV, tip2)
                                    + axis * Dish(settings, R, tip2.magnitude, reach);

                        Vector3 span = tip - root;
                        float len3 = span.magnitude;
                        if (len3 <= 1e-4f) continue;
                        Vector3 forward = span / len3;

                        // `up` is the dais normal made perpendicular to the blade, so a dished
                        // blade still lies flat in the rosette instead of twisting. It can never
                        // be degenerate here: the dish rise is a small fraction of a blade's
                        // length, so forward is never parallel to the axis.
                        Vector3 up = axis - Vector3.Dot(axis, forward) * forward;
                        if (up.sqrMagnitude < 1e-6f) up = basisU;
                        if (!SafeLookRotation.TryGet(forward, up, out Quaternion rot, null, false)) continue;
                        rot = Quaternion.AngleAxis(rollDeg * s, forward) * rot;

                        into.Add(new Element(
                            root + span * 0.5f, rot,
                            new Vector3(width, thickness, len3) * fit,
                            kind, p, s, f));
                    }
                }
            }

            // The sun cores last: ten stars igniting in the crooks once the wings have bloomed.
            for (int p = 0; p < pairs; p++)
            {
                float pairDeg = p * 360f / pairs;
                Vector2 planar = Polar(sunRadius, pairDeg);
                Vector3 pos = Plane(center, basisU, basisV, planar)
                            + axis * Dish(settings, R, sunRadius, reach);
                Vector3 radial = (Plane(center, basisU, basisV, Polar(1f, pairDeg)) - center).normalized;
                if (!SafeLookRotation.TryGet(radial, axis, out Quaternion rot, null, false))
                    rot = Quaternion.identity;
                into.Add(new Element(pos, rot, Vector3.one * sunEdge, PrismKind.SuperShielded, p, 0, -1));
            }
        }

        /// <summary>The tier a blade wears: the authored three-cycle, rotated by
        /// <see cref="ScarabWingDaisSettings.TierCycleOffset"/>.</summary>
        public static PrismKind KindAt(in ScarabWingDaisSettings settings, int feather)
        {
            int i = ((feather + settings.TierCycleOffset) % 3 + 3) % 3;
            return i switch
            {
                0 => PrismKind.Plain,
                1 => PrismKind.Shielded,
                _ => PrismKind.Danger,
            };
        }

        /// <summary>Planar radius of the outermost blade's tip — the rosette's own outer reach.</summary>
        public static float OuterReach(in ScarabWingDaisSettings settings, float ringRadius)
        {
            float R = Mathf.Max(0.01f, ringRadius);
            float length = R * Mathf.LerpUnclamped(settings.BladeLengthStart, settings.BladeLengthEnd, 1f);
            Blade(settings, R, 0f, 1, 1f, length, out _, out Vector2 tip);
            float sun = R * (settings.SunRadius + 0.5f * settings.SunApparentDiameter);
            return Mathf.Max(Mathf.Max(tip.magnitude, sun), R * 1.01f);
        }

        /// <summary>Root and tip of one blade, in the dais plane's 2D coordinates.</summary>
        static void Blade(in ScarabWingDaisSettings settings, float R, float pairDeg, int wingSign,
                          float t, float length, out Vector2 root, out Vector2 tip)
        {
            float shoulderDeg = pairDeg + wingSign * settings.ShoulderOffsetDeg;
            Vector2 shoulder = Polar(R * settings.ShoulderRadius, shoulderDeg);

            float psi = Mathf.LerpUnclamped(settings.FanStartDeg, settings.FanEndDeg,
                                            Shape(t, settings.FanShape));
            Vector2 dir = Polar(1f, shoulderDeg + wingSign * psi);
            float rootOffset = R * Mathf.LerpUnclamped(settings.RootOffsetStart, settings.RootOffsetEnd, t);

            root = shoulder + dir * rootOffset;
            tip = shoulder + dir * (rootOffset + length);
        }

        /// <summary>Rise out of the dais plane at <paramref name="planarRadius"/> — a shallow bowl
        /// that opens back toward the vessel that placed the switch.</summary>
        static float Dish(in ScarabWingDaisSettings settings, float R, float planarRadius, float reach)
        {
            if (Mathf.Approximately(settings.DishRise, 0f)) return 0f;
            float u = Mathf.Clamp01(planarRadius / Mathf.Max(0.01f, reach));
            return R * settings.DishRise * Mathf.Pow(u, Mathf.Max(0.01f, settings.DishPower));
        }

        static float Shape(float t, float power) =>
            Mathf.Approximately(power, 1f) ? t : Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.01f, power));

        static Vector2 Polar(float radius, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        static Vector3 Plane(Vector3 center, Vector3 basisU, Vector3 basisV, Vector2 planar) =>
            center + basisU * planar.x + basisV * planar.y;
    }

    /// <summary>
    /// Authored shape of the <see cref="ScarabWingDais"/>. Every distance is a MULTIPLE OF THE
    /// SWITCH RING RADIUS, so the Mass element grows the rosette with the ring it surrounds and
    /// there is exactly one size dial (SCARAB.md §7's one-parameter-per-element contract).
    /// </summary>
    [Serializable]
    public struct ScarabWingDaisSettings
    {
        [Header("Rosette")]
        [Tooltip("Mirrored wing PAIRS spaced evenly around the switch. Ten is the authored motif.")]
        [Range(3, 24)] public int PairCount;

        [Tooltip("Blades per wing. A multiple of 3 closes the base/shielded/danger cycle exactly; " +
                 "9 is three complete cycles. THIS IS THE COST DIAL: prisms = PairCount x 2 x this " +
                 "+ PairCount, and a third of the blades are shielded (always-on mesh colliders).")]
        [Range(3, 18)] public int FeathersPerWing;

        [Tooltip("Rotates the base -> shielded -> danger cycle. 0 puts DANGER on the longest, " +
                 "outermost blades, so the rosette's rim is the part that bites a pilot.")]
        [Range(0, 2)] public int TierCycleOffset;

        [Header("Wing fan (distances are multiples of the ring radius)")]
        [Tooltip("Radius at which a wing's shoulder sits — where its blades are rooted.")]
        public float ShoulderRadius;

        [Tooltip("How far off its pair's axis a shoulder sits, in degrees. Small values make the " +
                 "pair's two fans meet in a tight chevron on the axis.")]
        public float ShoulderOffsetDeg;

        [Tooltip("Angle of the FIRST (innermost, shortest) blade off the shoulder's own radial. " +
                 "Near zero it points straight out, alongside the sun core.")]
        public float FanStartDeg;

        [Tooltip("Angle of the LAST (outermost, longest) blade. Past ~60 degrees the wing sweeps " +
                 "tangentially and shingles over the neighbouring pairs, which is what makes the " +
                 "rosette continuous. Below ~40 the blades nest and stop reading as separate units.")]
        public float FanEndDeg;

        [Tooltip("Easing on the fan sweep. >1 holds the blades near the pair axis longer before " +
                 "flaring them out.")]
        public float FanShape;

        [Tooltip("Gap between the shoulder and the first blade's root.")]
        public float RootOffsetStart;

        [Tooltip("Gap between the shoulder and the last blade's root — the roots walk outward as " +
                 "the fan opens, which is what gives the wing its leading edge.")]
        public float RootOffsetEnd;

        [Header("Blades")]
        [Tooltip("Length of the innermost blade.")]
        public float BladeLengthStart;

        [Tooltip("Length of the outermost blade. This sets the dais's reach and is far past the " +
                 "prism pool's authored scale ceiling — the lay path widens it per prism.")]
        public float BladeLengthEnd;

        [Tooltip("In-plane width of the innermost blade.")]
        public float BladeWidthStart;

        [Tooltip("In-plane width of the outermost blade.")]
        public float BladeWidthEnd;

        [Tooltip("Easing on length AND width together, so a blade's aspect stays constant along " +
                 "the wing. >1 back-loads the growth into the outer blades.")]
        public float BladeGrowthShape;

        [Tooltip("Out-of-plane thickness. A blade is a PLATE — this is the cheapest volume dial " +
                 "there is, since it is the axis nobody looks along.")]
        public float BladeThickness;

        [Tooltip("Roll of the outermost blade about its own length, in degrees (mirrored per wing). " +
                 "Shingles the fan the way a real wing's feathers overlap.")]
        public float BladeRollEndDeg;

        [Header("Sun core")]
        [Tooltip("Radius at which each pair's super-shielded sun core sits, on the pair axis.")]
        public float SunRadius;

        [Tooltip("APPARENT tip-to-tip size of the sun core's eight-pointed star. The authored cube " +
                 "is derived from it (the stellation reaches 3x the cube), so state what you want " +
                 "to SEE.")]
        public float SunApparentDiameter;

        [Header("Dish")]
        [Tooltip("How far the rosette's rim rises out of the switch's plane, along the placement " +
                 "axis — a shallow bowl opening back toward the vessel. 0 is dead flat.")]
        public float DishRise;

        [Tooltip("Profile of the dish. 2 is a paraboloid; 1 is a cone.")]
        public float DishPower;

        /// <summary>The shipped motif — the numbers the rosette was designed and previewed at.</summary>
        public static ScarabWingDaisSettings Default => new()
        {
            PairCount = 10,
            FeathersPerWing = 9,
            TierCycleOffset = 0,
            ShoulderRadius = 1.30f,
            ShoulderOffsetDeg = 5f,
            FanStartDeg = 6f,
            FanEndDeg = 78f,
            FanShape = 1.10f,
            RootOffsetStart = 0.10f,
            RootOffsetEnd = 0.35f,
            BladeLengthStart = 0.50f,
            BladeLengthEnd = 1.90f,
            BladeWidthStart = 0.10f,
            BladeWidthEnd = 0.23f,
            BladeGrowthShape = 1.30f,
            BladeThickness = 0.05f,
            BladeRollEndDeg = 22f,
            SunRadius = 1.95f,
            SunApparentDiameter = 1.25f,
            DishRise = 0.40f,
            DishPower = 2f,
        };

        /// <summary>Prisms this shape lays: two fans per pair, plus one sun core per pair.</summary>
        public int PrismCount => Mathf.Max(1, PairCount) * (2 * Mathf.Max(1, FeathersPerWing) + 1);
    }
}
