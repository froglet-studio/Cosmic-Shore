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
    /// <para><b>The motif.</b> <see cref="ScarabWingDaisSettings.PairCount"/> super-shielded
    /// SUN CORES ride a circle around the spent ring. Each is CRADLED by a mirrored pair of
    /// wings — TILED chains of blade prisms laid root-to-root — which start on the sun's orbit
    /// with their blades pointing AWAY from it, wrap around it, and grow outward. Every blade
    /// in the rosette points away from the switch, and no wing sprouts from its sun: the sun
    /// sits in the crook. Nothing overlaps and nothing clips — that is a property of the
    /// construction, not of tuning, and <c>ScarabWingDaisTests</c> proves it with an exact
    /// separating-axis test over the real silhouettes (a rectangle per plain/danger blade, a
    /// RHOMBUS per shielded one, the stella's eight-point hull per sun).</para>
    ///
    /// <para><b>The octahedra are the hinges, and that is geometry rather than decoration.</b>
    /// A plain blade presents a flat root EDGE, so two of them laid flush are parallel and the
    /// run cannot turn. A shielded blade's octahedron presents a root POINT with two faces
    /// sloping at <c>atan(w/L)</c> from its axis, so the blades either side lie flush against
    /// those faces while sitting at <c>2·atan(w/L)</c> to each other. All three share ONE pivot —
    /// the rhombus's root tip — and the rhombus's axis BISECTS the two runs. So the wing's
    /// curvature is <i>placed</i> (wherever a shielded blade goes) and never tuned, and the tier
    /// pattern the eye reads is the same thing as the shape it reads. Shielded blades also cap
    /// both ends of every wing, which is what gives the curve a beginning and an end.</para>
    ///
    /// <para><b>Everything else alternates plain → danger</b>, so the run reads as groups of
    /// feathers separated by diamond joints, and every tier is doing a different job to a ball
    /// (SCARAB.md §5.1). A hinge carries its OWN aspect
    /// (<see cref="ScarabWingDaisSettings.HingeAspect"/>) rather than the blade ramp's, because
    /// it pivots instead of advancing the chain — so a fat wedge buys curvature without
    /// lengthening the wing, which is what keeps the rosette tight around the switch.</para>
    ///
    /// <para><b>Sizes are stated, not grown</b>, so every consumer must widen the prism's scale
    /// window (<c>Prism.AdmitTargetScale</c>) before assigning <c>TargetScale</c> — see
    /// <see cref="ScarabSwitch"/>.</para>
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
        /// its identity, and the hinge angle it produces — is exact.</para>
        /// </summary>
        public static readonly float ShieldedFit = 1f / OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        /// <summary>
        /// Authored cube edge → the sun core's <b>apparent</b> size: the diameter of the sphere
        /// its spikes reach.
        ///
        /// <para>This is the number that was got wrong once and is worth stating twice. The
        /// octahedron's vertices sit ON THE AXES, so a shielded prism's apparent size IS its axis
        /// extent (<c>3 × the box</c>). The stella octangula's spikes sit at the <b>cube
        /// corners</b>, so its axis extent is also <c>3 ×</c> the box while the sphere it fills is
        /// <c>√3</c> larger again. Sizing a sun core by its bounding box therefore understates
        /// what the player sees by 73%, and no axis-extent measurement can see the error.</para>
        /// </summary>
        public static readonly float SunApparentFactor =
            StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE * Mathf.Sqrt(3f);

        /// <summary>
        /// Authored cube edge → the sun's reach IN THE DAIS PLANE, i.e. toward the four spikes
        /// whose corners lie in that plane. This is the clearance a wing must start outside, and
        /// it is neither the axis extent nor the full circumsphere.
        /// </summary>
        public static readonly float SunInPlaneReach =
            StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE * Mathf.Sqrt(2f) * 0.5f;

        /// <summary>One prism of the dais, in world space, ready to lay.</summary>
        public readonly struct Element
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public readonly PrismKind Kind;
            /// <summary>Which sun core this belongs to, 0..PairCount-1.</summary>
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
        /// Fills <paramref name="into"/> with the whole dais, ordered <b>outward along the
        /// wings</b> (every wing's blade 0, then every wing's blade 1, …) and the sun cores LAST —
        /// so a budgeted lay draws each wing's curve out from its root and ignites the suns at the
        /// end, rather than filling one wing at a time.
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
            int blades = Mathf.Max(1, settings.BladesPerWing);
            float R = Mathf.Max(0.01f, ringRadius);

            float reach = OuterReach(settings, R);
            float thickness = R * settings.BladeThickness;
            float sunEdge = SunEdge(settings, R);
            float sunRadius = R * settings.SunRadius;
            float orbit = sunEdge * SunInPlaneReach + R * settings.WingOrbit;
            float rootRadius = R * settings.RootRadius;
            float gap = R * settings.BladeGap;

            // Walk every wing in lockstep so the emitted order is "blade 0 of all wings, blade 1
            // of all wings, …" — the lay budget then draws the whole rosette outward at once.
            int wings = pairs * 2;
            var dir = new Vector2[wings];
            var chain = new Vector2[wings];
            for (int p = 0; p < pairs; p++)
            {
                float pairRad = p * Mathf.PI * 2f / pairs;
                Vector2 sun = new(Mathf.Cos(pairRad) * sunRadius, Mathf.Sin(pairRad) * sunRadius);
                for (int w = 0; w < 2; w++)
                {
                    int s = w == 0 ? 1 : -1;
                    int idx = p * 2 + w;
                    // The blade points AWAY from its sun, and the chain is seeded on the sun's
                    // ORBIT. That single choice is what makes the wing wrap AROUND the sun and
                    // grow outward, instead of sprouting from it and curling back inward.
                    dir[idx] = Rotate(new Vector2(Mathf.Cos(pairRad), Mathf.Sin(pairRad)),
                                      s * settings.WingStartDeg * Mathf.Deg2Rad);
                    chain[idx] = sun + dir[idx] * orbit;
                }
            }

            for (int b = 0; b < blades; b++)
            {
                float t = blades > 1 ? b / (float)(blades - 1) : 0.5f;
                PrismKind kind = KindAt(settings, b);
                float length = BladeLength(settings, R, t);
                float width = BladeWidth(settings, R, t, kind);

                for (int p = 0; p < pairs; p++)
                for (int w = 0; w < 2; w++)
                {
                    int s = w == 0 ? 1 : -1;
                    int idx = p * 2 + w;
                    Vector2 d = dir[idx];
                    Vector2 planar;
                    Vector2 bladeDir;

                    if (kind == PrismKind.Shielded)
                    {
                        // The hinge. One pivot serves all three prisms: the incoming run's far
                        // root corner, the rhombus's root tip, and the outgoing run's near root
                        // corner. The rhombus's axis bisects the two runs, so each neighbour lies
                        // flush against one of its sloping faces.
                        Vector2 pivot = chain[idx] + d * rootRadius;
                        float phi = Mathf.Atan2(width * 0.5f, length * 0.5f)
                                    - settings.HingeEaseDeg * Mathf.Deg2Rad;
                        bladeDir = Rotate(d, s * phi);
                        planar = pivot + bladeDir * (gap + length * 0.5f);

                        Vector2 outgoing = Rotate(bladeDir, s * phi);
                        dir[idx] = outgoing;
                        chain[idx] = pivot - outgoing * rootRadius + Perp(outgoing, s) * gap;
                    }
                    else
                    {
                        // A flat root edge of its own width: it advances the run and cannot turn it.
                        bladeDir = d;
                        planar = chain[idx] + Perp(d, s) * (width * 0.5f) + d * (rootRadius + length * 0.5f);
                        chain[idx] += Perp(d, s) * (width + gap);
                    }

                    float fit = kind == PrismKind.Shielded ? ShieldedFit : 1f;
                    if (!TryEmit(into, settings, center, axis, basisU, basisV, R, reach,
                                 planar, bladeDir, new Vector3(width, thickness, length) * fit,
                                 kind, p, s, b))
                        continue;
                }
            }

            for (int p = 0; p < pairs; p++)
            {
                float pairRad = p * Mathf.PI * 2f / pairs;
                Vector2 radial = new(Mathf.Cos(pairRad), Mathf.Sin(pairRad));
                TryEmit(into, settings, center, axis, basisU, basisV, R, reach,
                        radial * sunRadius, radial, Vector3.one * sunEdge,
                        PrismKind.SuperShielded, p, 0, -1);
            }
        }

        /// <summary>A blade's length: the shared growth ramp, tier-independent so the wing keeps
        /// one silhouette.</summary>
        static float BladeLength(in ScarabWingDaisSettings settings, float R, float t) =>
            R * Mathf.LerpUnclamped(settings.BladeLengthStart, settings.BladeLengthEnd,
                                    Shape(t, settings.BladeGrowthShape));

        /// <summary>
        /// A blade's width. A HINGE takes its own aspect instead of the growth ramp's, and that is
        /// what lets the rosette be both tight and strongly curved: the hinge angle is
        /// <c>2·atan(width/length)</c>, while a hinge costs NOTHING in chain length because it
        /// pivots rather than advancing the run. So the plain blades stay thin (short chain, small
        /// footprint, small void around the switch) and the octahedra stay fat (a wide wedge, a
        /// strong turn) — two dials that would otherwise fight each other.
        /// </summary>
        static float BladeWidth(in ScarabWingDaisSettings settings, float R, float t, PrismKind kind) =>
            kind == PrismKind.Shielded
                ? settings.HingeAspect * BladeLength(settings, R, t)
                : R * Mathf.LerpUnclamped(settings.BladeWidthStart, settings.BladeWidthEnd,
                                          Shape(t, settings.BladeGrowthShape));

        static bool TryEmit(List<Element> into, in ScarabWingDaisSettings settings, Vector3 center,
                            Vector3 axis, Vector3 basisU, Vector3 basisV, float R, float reach,
                            Vector2 planar, Vector2 planarDir, Vector3 scale, PrismKind kind,
                            int pair, int wingSign, int blade)
        {
            Vector3 pos = center + basisU * planar.x + basisV * planar.y
                        + axis * Dish(settings, R, planar.magnitude, reach);
            Vector3 forward = (basisU * planarDir.x + basisV * planarDir.y).normalized;
            if (!SafeLookRotation.TryGet(forward, axis, out Quaternion rot, null, false)) return false;
            into.Add(new Element(pos, rot, scale, kind, pair, wingSign, blade));
            return true;
        }

        /// <summary>
        /// The tier a blade wears. Shielded caps BOTH ends of the wing (the accents that give the
        /// curve a beginning and an end) and recurs every
        /// <see cref="ScarabWingDaisSettings.HingeEvery"/> blades (the joints the curve turns at);
        /// everything else alternates plain → danger.
        /// </summary>
        public static PrismKind KindAt(in ScarabWingDaisSettings settings, int blade)
        {
            int n = Mathf.Max(1, settings.BladesPerWing);
            int every = Mathf.Max(0, settings.HingeEvery);
            if (blade <= 0 || blade >= n - 1) return PrismKind.Shielded;
            if (every > 0 && blade % every == 0) return PrismKind.Shielded;

            // Position in the plain/danger alternation, counting only the blades that are not hinges.
            int ordinal = 0;
            for (int i = 1; i < blade; i++)
                if (!(every > 0 && i % every == 0)) ordinal++;
            return (ordinal & 1) == 0 ? PrismKind.Plain : PrismKind.Danger;
        }

        /// <summary>The authored cube edge of a sun core, derived from the APPARENT size the
        /// designer stated (see <see cref="SunApparentFactor"/>).</summary>
        public static float SunEdge(in ScarabWingDaisSettings settings, float ringRadius) =>
            Mathf.Max(0.01f, ringRadius) * settings.SunApparentDiameter / SunApparentFactor;

        /// <summary>
        /// Planar radius the rosette actually reaches — the dish's reference, and the number the
        /// mode's arena has to accommodate. EXACT, not a bound: every wing is the same walk, so
        /// one traversal of one wing answers it. A loose bound would be safe for clearance and
        /// wrong for the dish, which is keyed on this and would flatten out.
        /// </summary>
        public static float OuterReach(in ScarabWingDaisSettings settings, float ringRadius)
        {
            float R = Mathf.Max(0.01f, ringRadius);
            float sunEdge = SunEdge(settings, R);
            float reach = R * settings.SunRadius + sunEdge * SunApparentFactor * 0.5f;

            Vector2 sun = new(R * settings.SunRadius, 0f);
            Vector2 d = Rotate(new Vector2(1f, 0f), settings.WingStartDeg * Mathf.Deg2Rad);
            Vector2 chain = sun + d * (sunEdge * SunInPlaneReach + R * settings.WingOrbit);
            float rootRadius = R * settings.RootRadius;
            float gap = R * settings.BladeGap;
            int blades = Mathf.Max(1, settings.BladesPerWing);

            for (int b = 0; b < blades; b++)
            {
                float t = blades > 1 ? b / (float)(blades - 1) : 0.5f;
                PrismKind kind = KindAt(settings, b);
                float length = BladeLength(settings, R, t);
                float width = BladeWidth(settings, R, t, kind);

                if (kind == PrismKind.Shielded)
                {
                    Vector2 pivot = chain + d * rootRadius;
                    float phi = Mathf.Atan2(width * 0.5f, length * 0.5f)
                                - settings.HingeEaseDeg * Mathf.Deg2Rad;
                    Vector2 bladeDir = Rotate(d, phi);
                    // The octahedron's far vertex sits on its axis at 3 x the fitted half-length,
                    // which is exactly the plain blade's half-length — so the tip is the reach.
                    reach = Mathf.Max(reach, (pivot + bladeDir * (gap + length)).magnitude);
                    d = Rotate(bladeDir, phi);
                    chain = pivot - d * rootRadius + Perp(d, 1) * gap;
                }
                else
                {
                    Vector2 root = chain + Perp(d, 1) * (width * 0.5f) + d * rootRadius;
                    Vector2 tip = root + d * length;
                    Vector2 across = Perp(d, 1) * (width * 0.5f);
                    reach = Mathf.Max(reach, Mathf.Max((tip + across).magnitude, (tip - across).magnitude));
                    chain += Perp(d, 1) * (width + gap);
                }
            }
            return reach;
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

        static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>The chain direction: perpendicular to the blade, mirrored per wing.</summary>
        static Vector2 Perp(Vector2 v, int wingSign) => new(-v.y * wingSign, v.x * wingSign);
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
        [Tooltip("Sun cores around the switch; each carries a mirrored pair of wings. Ten is the " +
                 "authored motif.")]
        [Range(3, 24)] public int PairCount;

        [Tooltip("Blades per wing. THIS IS THE COST DIAL: prisms = PairCount x (2 x this + 1).")]
        [Range(4, 32)] public int BladesPerWing;

        [Tooltip("A shielded HINGE every N blades (both ends are always shielded accents). This " +
                 "is the curvature dial: the wing turns 2 x atan(width/length) at each hinge and " +
                 "nowhere else, because only an octahedron's faces slope away from its axis. " +
                 "Smaller = a tighter curl.")]
        [Range(2, 8)] public int HingeEvery;

        [Header("Wing (distances are multiples of the ring radius)")]
        [Tooltip("Radius at which the sun cores sit. The wings hang off them, so this sets the " +
                 "rosette's whole scale — and it is what has to grow for the wings to TILE " +
                 "instead of overlapping.")]
        public float SunRadius;

        [Tooltip("Angle between a wing's first blade and its sun's outward radial, in degrees. " +
                 "Small values start both wings of a pair almost on the axis, so they cradle the " +
                 "sun in a tight crook before fanning apart.")]
        public float WingStartDeg;

        [Tooltip("How far a wing's chain starts from its sun, ON TOP of the sun's own in-plane " +
                 "spike reach (which is computed, not authored). The blade points AWAY from the " +
                 "sun and the chain rides this orbit, so the wing wraps AROUND the sun and grows " +
                 "outward — it does not sprout from it.")]
        public float WingOrbit;

        [Tooltip("How far a blade's body starts from the run's root line. Larger values fan the " +
                 "blades further apart at their tips for the same hinge angle.")]
        public float RootRadius;

        [Tooltip("Clearance between neighbouring blades. This is what keeps a flush-tiled run " +
                 "from touching; the no-overlap test is run against the real silhouettes.")]
        public float BladeGap;

        [Tooltip("Shaves the hinge angle so a rhombus's neighbours sit just clear of its faces " +
                 "rather than exactly on them, in degrees.")]
        public float HingeEaseDeg;

        [Header("Blades")]
        [Tooltip("Length of a wing's first blade.")]
        public float BladeLengthStart;

        [Tooltip("Length of a wing's last blade. Longer blades turn LESS at a hinge " +
                 "(atan(width/length)), so this and the width together set the curl.")]
        public float BladeLengthEnd;

        [Tooltip("Width of a wing's first blade.")]
        public float BladeWidthStart;

        [Tooltip("Width of a wing's last blade.")]
        public float BladeWidthEnd;

        [Tooltip("Easing on length AND width together, so a blade's aspect stays consistent " +
                 "along the wing.")]
        public float BladeGrowthShape;

        [Tooltip("Width:length of a HINGE blade, which sets its wedge angle (the run turns " +
                 "2 x atan(this) there). Independent of the blade ramp on purpose: a hinge pivots " +
                 "rather than advancing the chain, so fattening it buys curvature for free while " +
                 "the plain blades stay thin and the rosette stays tight around the switch.")]
        public float HingeAspect;

        [Tooltip("Out-of-plane thickness. A blade is a PLATE — this is the cheapest volume dial " +
                 "there is, since it is the axis nobody looks along.")]
        public float BladeThickness;

        [Header("Sun core")]
        [Tooltip("APPARENT diameter of the sun core's eight-pointed star — the sphere its spikes " +
                 "reach, which is sqrt(3) LARGER than its bounding box because a stella " +
                 "octangula's spikes point at the cube's CORNERS. The authored cube is derived " +
                 "from it, so state what you want to SEE.")]
        public float SunApparentDiameter;

        [Header("Dish")]
        [Tooltip("How far the rosette's rim rises out of the switch's plane, along the placement " +
                 "axis — a shallow bowl opening back toward the vessel. 0 is dead flat.")]
        public float DishRise;

        [Tooltip("Profile of the dish. 2 is a paraboloid; 1 is a cone.")]
        public float DishPower;

        /// <summary>
        /// The shipped motif. Solved rather than eyeballed: the wing's footprint was measured and
        /// <see cref="SunRadius"/> raised until an exact separating-axis test over every prism
        /// silhouette reported ZERO overlaps, with the innermost prism still clear of the ring.
        /// Changing any blade dial moves that solution — re-run <c>ScarabWingDaisTests</c>.
        /// </summary>
        public static ScarabWingDaisSettings Default => new()
        {
            PairCount = 10,
            BladesPerWing = 13,
            HingeEvery = 4,
            SunRadius = 6.40f,
            WingStartDeg = 4f,
            WingOrbit = 1.10f,
            RootRadius = 0.45f,
            BladeGap = 0.028f,
            HingeEaseDeg = 1f,
            BladeLengthStart = 0.80f,
            BladeLengthEnd = 1.70f,
            BladeWidthStart = 0.068f,
            BladeWidthEnd = 0.124f,
            BladeGrowthShape = 1.15f,
            HingeAspect = 0.25f,
            BladeThickness = 0.05f,
            SunApparentDiameter = 1.90f,
            DishRise = 0.60f,
            DishPower = 2f,
        };

        /// <summary>Prisms this shape lays: two wings per pair, plus one sun core per pair.</summary>
        public int PrismCount => Mathf.Max(1, PairCount) * (2 * Mathf.Max(1, BladesPerWing) + 1);
    }
}
