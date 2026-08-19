using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pose/scale/tier set of the <b>scarab-wing dais</b> — the sun-disc rosette a
    /// <see cref="ScarabSwitch"/> lays when a ball threads it (SCARAB.md §5.1). Pure closed-form
    /// geometry with no Unity scene dependency, so it is edit-mode testable
    /// (<c>ScarabWingDaisTests</c>) and bit-identical on every peer, which is the whole
    /// requirement for a prism structure that is re-built locally rather than replicated.
    ///
    /// <para><b>The motif.</b> <see cref="ScarabWingDaisSettings.PairCount"/> super-shielded SUN
    /// CORES ride a circle around the spent ring, and each is WRAPPED by a mirrored pair of wings.
    /// A wing is a <b>fan about its own sun</b>: every blade is a radial spoke from the sun, the
    /// pair's two wings start together on the inboard axis — their first blades are long spars
    /// whose tips land ON THE SWITCH RING — and they sweep apart around the sun, closing into a C
    /// that opens away from the switch. So the wing BEGINS where the ball threaded the switch and
    /// GROWS outward until it has the sun in its crook.</para>
    ///
    /// <para><b>Nothing overlaps and nothing clips, by construction rather than by tuning</b>, and
    /// it is two separate arguments. WITHIN a wing the blades are spokes from one centre whose
    /// angular steps are the sum of their own root half-angles plus
    /// <see cref="ScarabWingDaisSettings.BladeGapDeg"/>, so consecutive silhouettes cannot meet.
    /// BETWEEN pairs, every blade is clipped to <see cref="SectorLimit"/> — the longest it can be
    /// and still lie inside its pair's own <c>360/PairCount</c> wedge — so a pair physically
    /// cannot reach its neighbour whatever the dials say. <c>ScarabWingDaisTests</c> proves both
    /// with an exact separating-axis test over the real silhouettes (a rectangle per plain/danger
    /// blade, a RHOMBUS per shielded one, the stella's eight-point hull per sun).</para>
    ///
    /// <para><b>The octahedra open the fan, and that is geometry rather than decoration.</b> A
    /// plain blade is a rectangle, so its angular footprint at the root circle is
    /// <c>atan((w/2)/hole)</c> — a few degrees. A shielded blade's octahedron presents a root
    /// POINT with two faces sloping at <c>atan(w/L)</c> from its axis, so its neighbours must
    /// stand off by that whole angle instead. The wing therefore takes a big visible step at every
    /// hinge and small ones everywhere else: the tier pattern the eye reads IS the shape it reads.
    /// Shielded blades also cap both ends of every wing, which is what gives the curve a beginning
    /// and an end.</para>
    ///
    /// <para><b>Everything else alternates plain → danger</b>, so the run reads as groups of
    /// feathers separated by diamond joints, and every tier is doing a different job to a ball
    /// (SCARAB.md §5.1).</para>
    ///
    /// <para><b>Sizes are stated, not grown</b>, so every consumer must widen the prism's scale
    /// window (<c>Prism.AdmitTargetScale</c>) before assigning <c>TargetScale</c> — see
    /// <see cref="ScarabSwitch"/>. Tune it in <b>FrogletTools &gt; Vessels &gt; Scarab Wing Dais
    /// Lab</b>, which draws the rosette and runs these checks live.</para>
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
        /// its identity, and the stand-off angle it produces — is exact.</para>
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
        /// whose corners lie in that plane. This is the clearance the wing's hole must exceed, and
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
        /// One blade of a wing, in the wing's own polar frame about its sun: the angle it stands
        /// at (0 = pointing straight back at the switch), the tier it wears, and its size. Both
        /// wings of every pair are this same table mirrored and rotated, which is why the whole
        /// rosette can be reasoned about — and tested — one wing at a time.
        /// </summary>
        public readonly struct Blade
        {
            /// <summary>Angle from the INBOARD pair axis, radians, always positive.</summary>
            public readonly float Theta;
            public readonly PrismKind Kind;
            /// <summary>Length along the spoke (the plain-blade envelope, before <see cref="ShieldedFit"/>).</summary>
            public readonly float Length;
            /// <summary>Width across the spoke (likewise the envelope).</summary>
            public readonly float Width;
            /// <summary>Half the angular footprint this blade denies its neighbours.</summary>
            public readonly float StandOff;
            public readonly int Index;

            public Blade(float theta, PrismKind kind, float length, float width, float standOff, int index)
            {
                Theta = theta; Kind = kind; Length = length; Width = width;
                StandOff = standOff; Index = index;
            }
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the whole dais, ordered <b>outward along the
        /// wings</b> (every wing's blade 0, then every wing's blade 1, …) and the sun cores LAST —
        /// so a budgeted lay draws every wing's spar from the ring first and ignites the suns once
        /// their cradles are standing, rather than filling one wing at a time.
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
            float R = Mathf.Max(0.01f, ringRadius);

            var blades = BuildWing(settings, R);
            float reach = OuterReach(settings, R);
            float thickness = R * settings.BladeThickness;
            float sunEdge = SunEdge(settings, R);
            float sunRadius = R * settings.SunRadius;
            float hole = R * settings.WingHoleRadius;

            for (int b = 0; b < blades.Count; b++)
            {
                Blade blade = blades[b];
                float fit = blade.Kind == PrismKind.Shielded ? ShieldedFit : 1f;
                var scale = new Vector3(blade.Width, thickness, blade.Length) * fit;

                for (int p = 0; p < pairs; p++)
                for (int w = 0; w < 2; w++)
                {
                    int s = w == 0 ? 1 : -1;
                    // Blade 0 stands on the INBOARD axis (pointing back at the switch) and the
                    // wing sweeps away from it, so theta is measured from pi and mirrored per wing.
                    float a = p * Mathf.PI * 2f / pairs + Mathf.PI + s * blade.Theta;
                    Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 sun = SunPlanar(settings, R, p);
                    Vector2 planar = sun + dir * (hole + blade.Length * 0.5f);
                    TryEmit(into, settings, center, axis, basisU, basisV, R, reach,
                            planar, dir, scale, blade.Kind, p, s, blade.Index);
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

        /// <summary>
        /// The one wing every wing in the rosette is a copy of: each blade's stand-off angle, the
        /// angle it ends up at, and the size it gets there.
        ///
        /// <para>Angle and length are mutually dependent — a shielded blade's stand-off is
        /// <c>atan(w/L)</c> while its length is a function of where it lands — so this resolves
        /// them with ONE deliberate fixed-point pass: the stand-off uses the length the blade
        /// would have had at its predecessor's angle, then the final length is taken at the angle
        /// that produces. It is a fixed rule rather than an iteration on purpose; determinism is
        /// worth more here than the last fraction of a degree, because every peer rebuilds this
        /// locally and they must agree exactly.</para>
        /// </summary>
        public static List<Blade> BuildWing(in ScarabWingDaisSettings settings, float ringRadius)
        {
            float R = Mathf.Max(0.01f, ringRadius);
            int n = Mathf.Max(1, settings.BladesPerWing);
            float hole = R * settings.WingHoleRadius;
            float sunRadius = R * settings.SunRadius;
            float spar = Mathf.Max(0.01f, sunRadius - hole - R * settings.WingRootReach);
            float tip = R * settings.BladeTipLength;
            float floor = R * settings.BladeMinLength;
            float gap = settings.BladeGapDeg * Mathf.Deg2Rad;
            float sector = Mathf.PI / Mathf.Max(1, settings.PairCount);
            float lateral = R * settings.SectorMargin;

            var wing = new List<Blade>(n);
            float theta = settings.WingHalfGapDeg * Mathf.Deg2Rad;
            float previousStandOff = -1f;

            for (int j = 0; j < n; j++)
            {
                PrismKind kind = KindAt(settings, j);
                float t = n > 1 ? j / (float)(n - 1) : 0.5f;
                float width = R * Mathf.LerpUnclamped(settings.BladeWidthStart, settings.BladeWidthEnd,
                                                      Shape(t, settings.BladeWidthShape));
                if (kind == PrismKind.Shielded) width *= settings.HingeWidthScale;

                float provisional = LengthAt(settings, R, sunRadius, hole, spar, tip, floor,
                                             sector, lateral, theta, width);
                float standOff = kind == PrismKind.Shielded
                    ? Mathf.Atan2(width, provisional)          // the octahedron's sloping face
                    : Mathf.Atan2(width * 0.5f, hole);         // the rectangle's root corner
                if (previousStandOff >= 0f) theta += previousStandOff + standOff + gap;
                previousStandOff = standOff;

                float length = LengthAt(settings, R, sunRadius, hole, spar, tip, floor,
                                        sector, lateral, theta, width);
                wing.Add(new Blade(theta, kind, length, width, standOff, j));
            }
            return wing;
        }

        /// <summary>
        /// A blade's length: the wing's own silhouette, clipped so the pair can never reach out of
        /// its sector.
        ///
        /// <para>The silhouette is a cardioid in the fan angle — longest on the inboard axis (the
        /// spar that reaches the ring) and easing to
        /// <see cref="ScarabWingDaisSettings.BladeTipLength"/> as the wing closes around the sun.
        /// That is what makes the tips trace a curve instead of a straight edge: a length that
        /// only ever hits the sector clip would draw the wedge, not the wing.</para>
        ///
        /// <para><b>The sector clip is applied LAST, after the length floor</b> — order that reads
        /// like a detail and is the whole invariant. Clamping up to
        /// <see cref="ScarabWingDaisSettings.BladeMinLength"/> afterwards lets a generous floor
        /// push a blade straight back out of its own sector, and the confinement argument (and
        /// with it the no-overlap proof) quietly stops holding for reasons no dial announces.</para>
        /// </summary>
        static float LengthAt(in ScarabWingDaisSettings settings, float R, float sunRadius, float hole,
                              float spar, float tip, float floor, float sector, float lateral,
                              float theta, float width)
        {
            float profile = Mathf.Pow(Mathf.Clamp01((1f + Mathf.Cos(theta)) * 0.5f),
                                      Mathf.Max(0.01f, settings.BladeTaper));
            float wanted = Mathf.Max(floor, tip + (spar - tip) * profile);
            return Mathf.Min(wanted, SectorLimit(sunRadius, hole, theta, width, sector, lateral));
        }

        /// <summary>
        /// The longest a blade at fan angle <paramref name="theta"/> may be and still lie entirely
        /// inside its pair's own wedge of the dais — the invariant that makes inter-pair overlap
        /// impossible rather than merely unobserved.
        ///
        /// <para>The wedge is the pair of rays from the dais centre at <c>±sector</c> to the pair
        /// axis. A blade is a spoke from the sun, so the test is one ray-vs-half-plane clip per
        /// boundary: project the sun, the hole and the blade's own half-width onto the boundary's
        /// inward normal, and solve for the length at which the far corner would cross it. A
        /// boundary the spoke points AWAY from cannot bind and is skipped.</para>
        /// </summary>
        public static float SectorLimit(float sunRadius, float hole, float theta, float width,
                                        float sector, float lateral)
        {
            // The pair's own frame: axis along +x, sun at (sunRadius, 0), blade pointing inboard
            // at theta off the axis.
            float a = Mathf.PI + theta;
            Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 across = new(-d.y, d.x);
            float best = float.MaxValue;

            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? 1f : -1f;
                float normalAngle = sign * sector - sign * Mathf.PI * 0.5f;
                Vector2 nrm = new(Mathf.Cos(normalAngle), Mathf.Sin(normalAngle));
                float dn = Vector2.Dot(d, nrm);
                float room = sunRadius * nrm.x + hole * dn
                             - Mathf.Abs(Vector2.Dot(across, nrm)) * width * 0.5f - lateral;
                if (dn >= -1e-6f)
                {
                    if (room < 0f) best = 0f;      // even the root is outside — nothing fits
                    continue;
                }
                best = Mathf.Min(best, room / -dn);
            }
            return Mathf.Max(0f, best);
        }

        static Vector2 SunPlanar(in ScarabWingDaisSettings settings, float R, int pair)
        {
            float pairRad = pair * Mathf.PI * 2f / Mathf.Max(1, settings.PairCount);
            float sunRadius = R * settings.SunRadius;
            return new Vector2(Mathf.Cos(pairRad) * sunRadius, Mathf.Sin(pairRad) * sunRadius);
        }

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
        /// <see cref="ScarabWingDaisSettings.HingeEvery"/> blades (the joints the fan opens at);
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
        /// How much room is left between a sun core's in-plane spikes and the ring of blade roots
        /// that wraps it. Negative means the wings are growing through their own sun.
        /// </summary>
        public static float SunClearance(in ScarabWingDaisSettings settings, float ringRadius)
        {
            float R = Mathf.Max(0.01f, ringRadius);
            return R * settings.WingHoleRadius - SunEdge(settings, R) * SunInPlaneReach;
        }

        /// <summary>
        /// Planar radius the wings BEGIN at — the closest any prism comes to the switch, which is
        /// the tip of the inboard spar. <c>WingRootReach = 1</c> lands it on the ring.
        /// </summary>
        public static float InnerReach(in ScarabWingDaisSettings settings, float ringRadius)
        {
            float R = Mathf.Max(0.01f, ringRadius);
            float best = float.MaxValue;
            var wing = BuildWing(settings, R);
            Vector2 sun = new(R * settings.SunRadius, 0f);
            for (int i = 0; i < wing.Count; i++)
            {
                Blade b = wing[i];
                float a = Mathf.PI + b.Theta;
                Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 across = new(-d.y, d.x);
                for (int c = 0; c < 4; c++)
                {
                    float along = (c & 1) == 0 ? 0f : b.Length;
                    float side = (c & 2) == 0 ? b.Width * 0.5f : -b.Width * 0.5f;
                    if (b.Kind == PrismKind.Shielded) { along = (c & 1) == 0 ? 0f : b.Length; side = 0f; }
                    best = Mathf.Min(best, (sun + d * (R * settings.WingHoleRadius + along) + across * side).magnitude);
                }
            }
            return best;
        }

        /// <summary>
        /// Planar radius the rosette actually reaches — the dish's reference, and the number the
        /// mode's arena has to accommodate. EXACT, not a bound: every wing is the same table, so
        /// one pass over it answers it. A loose bound would be safe for clearance and wrong for
        /// the dish, which is keyed on this and would flatten out.
        /// </summary>
        public static float OuterReach(in ScarabWingDaisSettings settings, float ringRadius)
        {
            float R = Mathf.Max(0.01f, ringRadius);
            float hole = R * settings.WingHoleRadius;
            float sunEdge = SunEdge(settings, R);
            float reach = R * settings.SunRadius + sunEdge * SunApparentFactor * 0.5f;

            Vector2 sun = new(R * settings.SunRadius, 0f);
            var wing = BuildWing(settings, R);
            for (int i = 0; i < wing.Count; i++)
            {
                Blade b = wing[i];
                float a = Mathf.PI + b.Theta;
                Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 across = new(-d.y, d.x);
                Vector2 tip = sun + d * (hole + b.Length);
                // The octahedron's far vertex sits on the axis, so a shielded blade's corners are
                // its tip; a rectangle's are the two tip corners.
                float half = b.Kind == PrismKind.Shielded ? 0f : b.Width * 0.5f;
                reach = Mathf.Max(reach, Mathf.Max((tip + across * half).magnitude,
                                                   (tip - across * half).magnitude));
            }
            return reach;
        }

        /// <summary>Total angle a pair's two wings sweep around their sun, in degrees. Under 360
        /// it reads as a C opening away from the switch; at 360 the sun is ringed.</summary>
        public static float WrapDegrees(in ScarabWingDaisSettings settings, float ringRadius)
        {
            var wing = BuildWing(settings, Mathf.Max(0.01f, ringRadius));
            return wing.Count == 0 ? 0f : 2f * wing[wing.Count - 1].Theta * Mathf.Rad2Deg;
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
    }

    /// <summary>
    /// Authored shape of the <see cref="ScarabWingDais"/>. Every distance is a MULTIPLE OF THE
    /// SWITCH RING RADIUS, so the Mass element grows the rosette with the ring it surrounds and
    /// there is exactly one size dial (SCARAB.md §7's one-parameter-per-element contract).
    ///
    /// <para>Tune it in <b>FrogletTools &gt; Vessels &gt; Scarab Wing Dais Lab</b>: it draws the
    /// rosette from these numbers, runs the overlap / reach / wrap checks, and writes the result
    /// back into <c>PlaceSwitchAction.asset</c>.</para>
    /// </summary>
    [Serializable]
    public struct ScarabWingDaisSettings
    {
        [Header("Rosette")]
        [Tooltip("Sun cores around the switch; each is wrapped by a mirrored pair of wings. Ten " +
                 "is the authored motif.")]
        [Range(3, 24)] public int PairCount;

        [Tooltip("Blades per wing. THIS IS THE COST DIAL: prisms = PairCount x (2 x this + 1). It " +
                 "is also the wrap dial — every blade adds its own stand-off to the sweep, so more " +
                 "blades close the C further around the sun.")]
        [Range(4, 32)] public int BladesPerWing;

        [Tooltip("A shielded hinge every N blades (both ends are always shielded accents). This is " +
                 "the curvature dial: a rectangle stands its neighbour off by atan(halfWidth/hole) " +
                 "— a couple of degrees — while an octahedron's sloping face stands it off by " +
                 "atan(width/length), several times more. Smaller = a fan that opens faster.")]
        [Range(2, 8)] public int HingeEvery;

        [Header("Wing (distances are multiples of the ring radius)")]
        [Tooltip("Radius the sun cores sit at. The wings wrap them, so this sets the rosette's " +
                 "whole scale — and it is what has to grow for ten pairs to fit side by side.")]
        public float SunRadius;

        [Tooltip("Radius of the circle a wing's blade ROOTS stand on, about its sun. Must clear " +
                 "the sun's own in-plane spikes; larger also means tighter tiling, because a " +
                 "rectangle's angular footprint is atan(halfWidth/this).")]
        public float WingHoleRadius;

        [Tooltip("Where the inboard spar's TIP lands, as a multiple of the ring radius. 1 puts it " +
                 "ON the switch ring — the wings then literally begin where the ball threaded the " +
                 "switch. The spar's length is derived from it, never authored.")]
        public float WingRootReach;

        [Tooltip("Half the gap between a pair's two wings on the inboard axis, in degrees. Both " +
                 "spars point back at the switch, so this is what stops them from occupying the " +
                 "same spoke — and it is also how far off-axis their tips land.")]
        public float WingHalfGapDeg;

        [Tooltip("Angular clearance between neighbouring blades, in degrees, ON TOP of their own " +
                 "footprints. This is what keeps a tiled fan from touching; the no-overlap test " +
                 "runs against the real silhouettes.")]
        public float BladeGapDeg;

        [Tooltip("Lateral clearance a blade keeps from its pair's sector boundary. This is the " +
                 "clip that makes inter-pair overlap impossible rather than merely unobserved.")]
        public float SectorMargin;

        [Header("Blades")]
        [Tooltip("Length of the blades that close the wrap, out where the wing has turned away " +
                 "from the switch. The inboard spar's length is DERIVED from WingRootReach.")]
        public float BladeTipLength;

        [Tooltip("Floor a blade's length is never clipped below, so a wing keeps its feathers even " +
                 "where the sector is tight.")]
        public float BladeMinLength;

        [Tooltip("Profile of the wing's silhouette: length falls as ((1+cos(theta))/2)^this from " +
                 "the inboard spar to the tip. Higher holds the long feathers nearer the axis, so " +
                 "the wing reads as a narrow spearhead; lower spreads them around the wrap.")]
        public float BladeTaper;

        [Tooltip("Width of a wing's first blade.")]
        public float BladeWidthStart;

        [Tooltip("Width of a wing's last blade.")]
        public float BladeWidthEnd;

        [Tooltip("Easing on the width ramp along the wing.")]
        public float BladeWidthShape;

        [Tooltip("How much wider a HINGE blade is than the plain blade beside it. It sets the " +
                 "wedge the fan opens by, since an octahedron stands its neighbours off by " +
                 "atan(width/length) — the one place in the wing where the tier IS the shape.")]
        public float HingeWidthScale;

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
        /// The shipped motif: ten suns, each cradled in a 240° C of feathers whose spars land on
        /// the switch ring. Solved rather than eyeballed — an exact separating-axis test over
        /// every prism silhouette reports ZERO overlaps, the inner reach lands within a unit of
        /// the ring, and the sun keeps 6.2 units of clearance inside its hole. Changing any dial
        /// moves that solution: re-run <c>ScarabWingDaisTests</c>, or open the Dais Lab.
        /// </summary>
        public static ScarabWingDaisSettings Default => new()
        {
            PairCount = 10,
            BladesPerWing = 19,
            HingeEvery = 5,
            SunRadius = 7.00f,
            WingHoleRadius = 1.25f,
            WingRootReach = 0.80f,
            WingHalfGapDeg = 2.0f,
            BladeGapDeg = 0.8f,
            SectorMargin = 0.06f,
            BladeTipLength = 0.55f,
            BladeMinLength = 0.30f,
            BladeTaper = 1.80f,
            BladeWidthStart = 0.070f,
            BladeWidthEnd = 0.090f,
            BladeWidthShape = 1f,
            HingeWidthScale = 1.80f,
            BladeThickness = 0.05f,
            SunApparentDiameter = 2.30f,
            DishRise = 0.60f,
            DishPower = 2f,
        };

        /// <summary>Prisms this shape lays: two wings per pair, plus one sun core per pair.</summary>
        public int PrismCount => Mathf.Max(1, PairCount) * (2 * Mathf.Max(1, BladesPerWing) + 1);
    }
}
