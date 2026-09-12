using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab hull's GEOMETRY, as a pure function: <see cref="Generate"/> takes a
    /// <see cref="Settings"/> and returns the named, pivoted, submeshed parts — no components,
    /// no scene, no side effects. <see cref="ScarabHullBuilder"/> is the MonoBehaviour shell
    /// that emits these parts as meshes and wires them into the vessel.
    ///
    /// WHY THE SPLIT. The 2026-08-15 NaN incident was found only because the shipped C# was
    /// compiled and RUN offline ("the previous Python validator silently clamped where the C#
    /// did not, which is why it passed a model the engine rejected"). A pure core makes that the
    /// cheap, standing posture: `ScarabHullFormTests` runs the exact shipped geometry in
    /// edit-mode NUnit (the `ScarabWingDais.Generate` pattern), and the offline harness runs the
    /// same file byte-for-byte. It is also what the elemental morphs need — the four element
    /// extremes are just <see cref="Generate"/> at transformed Settings, and deltas between
    /// builds are only meaningful when generation is pure.
    ///
    /// EVERY PROFILE FUNCTION CLAMPS BEFORE <c>Pow</c>. In float32 <c>Sin(PI)</c> is ≈ -8.74e-8 —
    /// negative — and <c>Pow(negative, fractional)</c> is NaN. One unclamped profile put a NaN Y
    /// on every vertex at t = 1, which Unity surfaced as a rejected <c>localPosition</c> on the
    /// horn and `abnormal mesh bounds ... -nan(ind)`, and which froze the puppetry because
    /// un-cullable renderers stop updating. Clamp at the source, every time.
    /// </summary>
    public static class ScarabHullForm
    {
        public const int ChassisSubmesh = 0;
        public const int ShellSubmesh = 1;

        /// <summary>
        /// Every number the geometry depends on. Mirrors <see cref="ScarabHullBuilder"/>'s
        /// serialized fields one-for-one (the builder is the authored home; this struct is how
        /// the values travel into the pure function). Integer fields decide TOPOLOGY and must
        /// never be touched by a morph; float fields move vertices only.
        /// </summary>
        public struct Settings
        {
            public float Length;
            public float Width;
            public float DomeHeight;
            public float BellyDepth;
            public float SeamFraction;
            public float ElytraFront;
            public float PronotumFront;
            public float PronotumSwell;
            public int StriaeCount;
            public float StriaeDepth;
            public int LengthSegments;
            public int WidthSegments;
            public float HornLength;
            public float HornCurve;
            public int HornSides;
            public float LegLength;
            public float LegThickness;
            public float AbdomenHeight;
            public float AntennaLength;
            public float AntennaThickness;

            // ---- morph channels -----------------------------------------------------------
            // Element-owned form channels: NOT mirrored onto ScarabHullBuilder's serialized
            // fields, because their author is ApplyElementExtreme (the element table is a
            // code-owned law, like the fleet's ElementalScaling curves), never the prefab. Each
            // defaults to a value that reproduces the pre-morph geometry BIT-EXACTLY (guards in
            // the consumers make the zero case a literal no-op), which the bake's topology
            // assert and the base-build fingerprint test both stand on.

            /// <summary>Height of the pronotum's centreline crest, as a fraction of DomeHeight.
            /// 0 = the plain shield. Charge's channel: armour reads as a keel.</summary>
            public float PronotumKeel;
            /// <summary>Depth of the wing cases' outer-rim serration, as a fraction of the
            /// half-width. 0 = smooth rim. Charge's channel: the silhouette grows teeth.</summary>
            public float ElytraSerration;
            /// <summary>Where the shell profile's arch starts being sampled (see ShellU). The
            /// form constant 0.10 leaves the tail at ~2/3 width; Time LOWERS it toward the
            /// arch's endpoint, pinching the tail into a faster, more tapered stern. (Lower =
            /// closer to the sine arch's zero = narrower — the first cut raised it, which walks
            /// the tail TOWARD the arch's peak and WIDENED the stern 20%; caught by the
            /// direction test below and the review pass.)</summary>
            public float ShellTailPinch;
            /// <summary>How far the leg sockets slide toward the tail (in t, 0 = tail). Time's
            /// channel: legs trail aft like a sprinter's.</summary>
            public float LegSocketAftShift;
            /// <summary>How far the leg sockets tuck toward the centreline (fraction of the
            /// half-width off the 0.94 rest factor). Time's channel.</summary>
            public float LegSocketInboard;

            /// <summary>The authored defaults — kept equal to the prefab's serialized values
            /// (the prefab is authoritative; field-parity holds the two together).</summary>
            public static Settings Default => new Settings
            {
                Length = 9f,
                Width = 7.4f,
                DomeHeight = 2.15f,
                BellyDepth = 0.8f,
                SeamFraction = 0.055f,
                ElytraFront = 0.63f,
                PronotumFront = 0.90f,
                PronotumSwell = 1.09f,
                StriaeCount = 4,
                StriaeDepth = 0.045f,
                LengthSegments = 22,
                WidthSegments = 10,
                HornLength = 0.42f,
                HornCurve = 1.25f,
                HornSides = 7,
                LegLength = 0.34f,
                LegThickness = 0.055f,
                AbdomenHeight = 0.55f,
                AntennaLength = 0.55f,
                AntennaThickness = 0.032f,
                PronotumKeel = 0f,
                ElytraSerration = 0f,
                ShellTailPinch = 0.10f,
                LegSocketAftShift = 0f,
                LegSocketInboard = 0f,
            };
        }

        /// <summary>
        /// The four elements a Scarab hull morphs for, in the order every weight array in this
        /// file uses. Kept equal (by an edit-mode test) to
        /// <c>VesselElementalMorphConfigSO.MorphElements</c> — the fleet's morph order — without
        /// referencing the SO here, so the pure core stays free of ScriptableObject/DOTween.
        /// </summary>
        public static readonly Element[] MorphElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>
        /// The element-extreme table: what this hull looks like at level 10 of ONE element.
        /// Floats only — topology is a function of the integer settings and the feature gates,
        /// and none of these can flip a gate (asserted by <see cref="BakeMorphSet"/>). The
        /// element conventions are the fleet's (CONTRACT §4): Charge = threat (keel + serrated
        /// silhouette), Mass = volume (dome/belly/width swell, sockets ride the fit), Space =
        /// reach (the horn — the identity feature — longer and higher), Time = rate (tail
        /// pinched for speed, legs trailed aft and inboard). Multiplicative where the base value
        /// is authored per-prefab (a retuned hull keeps morphing proportionally), absolute where
        /// the channel is a form constant the prefab never authors.
        /// </summary>
        public static Settings ApplyElementExtreme(Settings s, Element element)
        {
            switch (element)
            {
                case Element.Charge:
                    s.PronotumKeel = 0.34f;
                    s.ElytraSerration = 0.12f;
                    return s;
                case Element.Mass:
                    s.DomeHeight *= 1.25f;
                    s.BellyDepth *= 1.30f;
                    s.Width *= 1.08f;
                    return s;
                case Element.Space:
                    // 0.42 → 0.62 and 1.25 → 1.45 at the shipped defaults. Gate-invariant on
                    // purpose: a hull authored horn-less sits at or under the 0.001 feature
                    // gate, and multiplying it could carry it ACROSS the gate — the extreme
                    // would then grow a part the base build lacks and the bake's topology
                    // assert throws in Awake. A morph must never flip a feature gate.
                    if (s.HornLength > 0.001f)
                    {
                        s.HornLength *= 0.62f / 0.42f;
                        s.HornCurve *= 1.45f / 1.25f;
                    }
                    return s;
                case Element.Time:
                    // DOWN from 0.10: toward the arch endpoint = narrower (0.666 → 0.453 tail
                    // width factor, −32%; raising it widens — see the ShellTailPinch doc).
                    s.ShellTailPinch = 0.04f;
                    s.LegSocketAftShift = 0.06f;
                    s.LegSocketInboard = 0.10f;
                    return s;
                default:
                    return s;
            }
        }

        /// <summary>
        /// One movable piece of the ship. The hull is emitted as SEVERAL of these rather than
        /// one mesh so <see cref="ScarabAnimation"/> has something to puppet: a static ship
        /// reads as a prop no matter how good the flight model is. Each part carries its own
        /// PIVOT, because a wing case that hinges about the seam and a leg that swings from its
        /// socket cannot share one origin. Verts are in HULL space (pivot NOT yet subtracted —
        /// the emitter does that), so deltas between two builds of the same topology are
        /// directly comparable in one frame.
        /// </summary>
        public sealed class Part
        {
            public string Name;
            public Vector3 Pivot;            // hull-space; becomes the child's localPosition
            public bool IsCarapace;          // counts toward the authored width/length fit
            public readonly List<Vector3> Verts = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<int> Chassis = new();
            public readonly List<int> Shell = new();
        }

        /// <summary>
        /// Build the whole hull. Deterministic; topology (part list, vertex counts, triangle
        /// lists) is a function of the INTEGER settings plus the two `> 0.001` feature gates
        /// (horn, legs) — hold those fixed and any float retune produces vertex-wise comparable
        /// builds, which is the property the elemental morphs stand on.
        /// </summary>
        public static List<Part> Generate(Settings s)
        {
            var g = new Generator(s);
            g.Build();
            return g.Parts;
        }

        // ------------------------------------------------------------------ elemental morphs
        // The morph model: the four element extremes are Generate at ApplyElementExtreme'd
        // Settings, and because generation is pure and topology is a function of the integer
        // settings only, "level 7 Mass + level 3 Space" is just the base build plus a weighted
        // sum of per-vertex deltas. Everything below is pure so the whole lattice — 16 weight
        // corners, every two-element combination, the bounds interval — runs in edit-mode NUnit
        // and in the offline harness against the exact shipped arithmetic.

        /// <summary>One element's shape deltas for one part, in the part's own LOCAL frame
        /// (pivot-relative), plus the pivot's own travel in hull space. Local-frame deltas +
        /// blended pivot compose exactly: at weight 1 the blended part IS the extreme build.</summary>
        public sealed class PartMorphDelta
        {
            public Vector3[] VertDeltas;
            public Vector3[] NormalDeltas;
            public Vector3 PivotDelta;
            /// <summary>False when this element leaves the part untouched — the appliers skip it.</summary>
            public bool Any;
        }

        /// <summary>
        /// The baked morph state: the base build plus per-element per-part deltas and, per part,
        /// the LOCAL-frame bounds interval that contains every weight combination in [0,1]^4.
        /// Blending is multilinear in the weights, so per vertex per axis the reachable range is
        /// exactly [base + Σ min(0, δe), base + Σ max(0, δe)] — the interval union the mesh
        /// bounds are pinned to so animated writes never need (or shrink under) a recalculation.
        /// </summary>
        public sealed class MorphSet
        {
            public List<Part> BaseParts;
            /// <summary>Base verts per part, pivot-subtracted — the frame the meshes are emitted in.</summary>
            public Vector3[][] BaseLocalVerts;
            /// <summary>[element index (MorphElements order)][part index].</summary>
            public PartMorphDelta[][] Deltas;
            public Vector3[] BoundsMin;
            public Vector3[] BoundsMax;
            /// <summary>True when any element moves any vertex of the part or its pivot.</summary>
            public bool[] PartMorphs;
        }

        /// <summary>
        /// Build the base hull and all four element extremes, assert they are the SAME mesh
        /// (part roster, vertex counts, triangle lists — a divergence here means a float channel
        /// flipped a feature gate, which is a bug, and a silent one on the blend path), and
        /// bake the deltas + bounds intervals. Throws loudly on any topology mismatch.
        /// </summary>
        public static MorphSet BakeMorphSet(Settings s)
        {
            var baseParts = Generate(s);
            var set = new MorphSet
            {
                BaseParts = baseParts,
                BaseLocalVerts = new Vector3[baseParts.Count][],
                Deltas = new PartMorphDelta[MorphElements.Length][],
                BoundsMin = new Vector3[baseParts.Count],
                BoundsMax = new Vector3[baseParts.Count],
                PartMorphs = new bool[baseParts.Count],
            };

            for (int p = 0; p < baseParts.Count; p++)
            {
                var part = baseParts[p];
                var local = new Vector3[part.Verts.Count];
                for (int i = 0; i < local.Length; i++) local[i] = part.Verts[i] - part.Pivot;
                set.BaseLocalVerts[p] = local;
            }

            for (int e = 0; e < MorphElements.Length; e++)
            {
                var extremeParts = Generate(ApplyElementExtreme(s, MorphElements[e]));
                AssertSameTopology(baseParts, extremeParts, MorphElements[e]);

                var deltas = new PartMorphDelta[baseParts.Count];
                for (int p = 0; p < baseParts.Count; p++)
                {
                    var basePart = baseParts[p];
                    var extPart = extremeParts[p];
                    var d = new PartMorphDelta
                    {
                        VertDeltas = new Vector3[basePart.Verts.Count],
                        NormalDeltas = new Vector3[basePart.Verts.Count],
                        PivotDelta = extPart.Pivot - basePart.Pivot,
                    };
                    bool any = d.PivotDelta.sqrMagnitude > 1e-10f;
                    for (int i = 0; i < basePart.Verts.Count; i++)
                    {
                        d.VertDeltas[i] = (extPart.Verts[i] - extPart.Pivot)
                                          - (basePart.Verts[i] - basePart.Pivot);
                        d.NormalDeltas[i] = extPart.Normals[i] - basePart.Normals[i];
                        any |= d.VertDeltas[i].sqrMagnitude > 1e-10f;
                    }
                    d.Any = any;
                    deltas[p] = d;
                    set.PartMorphs[p] |= any;
                }
                set.Deltas[e] = deltas;
            }

            for (int p = 0; p < baseParts.Count; p++)
            {
                var local = set.BaseLocalVerts[p];
                if (local.Length == 0) continue;
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                for (int i = 0; i < local.Length; i++)
                {
                    Vector3 lo = local[i], hi = local[i];
                    for (int e = 0; e < MorphElements.Length; e++)
                    {
                        var d = set.Deltas[e][p].VertDeltas[i];
                        lo += Vector3.Min(Vector3.zero, d);
                        hi += Vector3.Max(Vector3.zero, d);
                    }
                    min = Vector3.Min(min, lo);
                    max = Vector3.Max(max, hi);
                }
                set.BoundsMin[p] = min;
                set.BoundsMax[p] = max;
            }

            return set;
        }

        /// <summary>
        /// Blend one part at the given element weights (MorphElements order, each in [0,1]) into
        /// caller-owned lists — LOCAL-frame verts and renormalized normals — and return the
        /// blended hull-space pivot for the part's transform. Lists are cleared and refilled, so
        /// a per-frame caller reuses its scratch allocations.
        /// </summary>
        public static Vector3 BlendPart(MorphSet set, int partIndex, float[] weights,
                                        List<Vector3> outVerts, List<Vector3> outNormals)
        {
            var basePart = set.BaseParts[partIndex];
            var baseLocal = set.BaseLocalVerts[partIndex];
            outVerts.Clear();
            outNormals.Clear();

            Vector3 pivot = basePart.Pivot;
            for (int e = 0; e < MorphElements.Length; e++)
                pivot += set.Deltas[e][partIndex].PivotDelta * weights[e];

            for (int i = 0; i < baseLocal.Length; i++)
            {
                Vector3 v = baseLocal[i];
                Vector3 n = basePart.Normals[i];
                for (int e = 0; e < MorphElements.Length; e++)
                {
                    var d = set.Deltas[e][partIndex];
                    v += d.VertDeltas[i] * weights[e];
                    n += d.NormalDeltas[i] * weights[e];
                }
                outVerts.Add(v);
                outNormals.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
            }

            return pivot;
        }

        static void AssertSameTopology(List<Part> a, List<Part> b, Element element)
        {
            if (a.Count != b.Count)
                throw new System.InvalidOperationException(
                    $"Scarab morph bake: {element} extreme changed the part roster ({a.Count} vs {b.Count}).");
            for (int p = 0; p < a.Count; p++)
            {
                if (a[p].Name != b[p].Name || a[p].Verts.Count != b[p].Verts.Count
                    || !SameTris(a[p].Chassis, b[p].Chassis) || !SameTris(a[p].Shell, b[p].Shell))
                    throw new System.InvalidOperationException(
                        $"Scarab morph bake: {element} extreme diverged topology on part '{a[p].Name}'.");
            }

            static bool SameTris(List<int> x, List<int> y)
            {
                if (x.Count != y.Count) return false;
                for (int i = 0; i < x.Count; i++) if (x[i] != y[i]) return false;
                return true;
            }
        }

        // ------------------------------------------------------------------ the generator
        // One instance per Generate call: no static scratch, so concurrent/preview builds can
        // never alias (the "non-reentrant static buffers" trap recorded in CLAUDE.md).

        sealed class Generator
        {
            readonly Settings s;
            public readonly List<Part> Parts = new();
            Part _part;

            List<Vector3> _verts => _part.Verts;
            List<Vector2> _uvs => _part.Uvs;
            List<int> _chassisTris => _part.Chassis;
            List<int> _shellTris => _part.Shell;

            public Generator(Settings settings) => s = settings;

            public void Build()
            {
                float halfWidth = s.Width * 0.5f;
                float seamHalf = halfWidth * s.SeamFraction;

                Begin("Core", Vector3.zero, carapace: true);
                BuildBelly(halfWidth);
                BuildClypeus(halfWidth);
                if (s.AbdomenHeight > 0.001f) BuildAbdomen(halfWidth);

                // The wing cases hinge about the seam, so their pivot is the centreline.
                Begin("elytron.r", Vector3.zero, carapace: true); BuildShell(+1f, seamHalf, halfWidth);
                Begin("elytron.l", Vector3.zero, carapace: true); BuildShell(-1f, seamHalf, halfWidth);

                Begin("pronotum", Vector3.zero, carapace: true); BuildPronotum(halfWidth);

                if (s.HornLength > 0.001f) BuildHorn();
                if (s.LegLength > 0.001f) BuildLegs(halfWidth);
                if (s.AntennaLength > 0.001f) BuildAntennae(halfWidth);

                FitToAuthoredExtents();
                ComputeNormals();
            }

            void Begin(string name, Vector3 pivot, bool carapace = false)
            {
                _part = new Part { Name = name, Pivot = pivot, IsCarapace = carapace };
                Parts.Add(_part);
            }

            // ---------------------------------------------------------------- shape profiles

            /// <summary>
            /// Remap the part parameter onto the interior of the profile's arch. Sampling the
            /// arch's literal endpoints pinches BOTH ends of the shell to a point, which is
            /// right for the head and wrong for the tail — a beetle's elytra end in a broad
            /// rounded skirt. The default start of 0.10 leaves the tail at ~2/3 width; Time's
            /// ShellTailPinch LOWERS it toward the endpoint, tapering the stern (an instance
            /// method for exactly that channel — everything sampling the arch tapers together).
            /// </summary>
            float ShellU(float t) => Mathf.Lerp(s.ShellTailPinch, 0.995f, Mathf.Clamp01(t));

            /// <summary>Half-width of the shell at longitudinal position t (0 = tail, 1 = nose),
            /// as a fraction of the half-width.</summary>
            float WidthAt(float t)
            {
                float sn = Mathf.Sin(Mathf.Pow(ShellU(t), 0.80f) * Mathf.PI);
                return Mathf.Pow(Mathf.Max(0f, sn), 0.55f);
            }

            /// <summary>Dome height factor at t. Peaks behind the middle so the shell looks
            /// loaded at the back, then falls away toward the head shield.</summary>
            float HeightAt(float t)
            {
                float sn = Mathf.Sin(Mathf.Pow(ShellU(t), 0.95f) * Mathf.PI);
                return Mathf.Pow(Mathf.Max(0f, sn), 0.65f);
            }

            float ZAt(float t) => (t - 0.5f) * s.Length;

            // ---------------------------------------------------------------- parts

            void BuildShell(float side, float seamHalf, float halfWidth)
            {
                int baseIndex = _verts.Count;

                for (int i = 0; i <= s.LengthSegments; i++)
                {
                    float t = i / (float)s.LengthSegments * s.ElytraFront;
                    float w = WidthAt(t) * halfWidth;
                    float h = HeightAt(t) * s.DomeHeight;
                    float z = ZAt(t);
                    float inner = Mathf.Min(seamHalf, w * 0.9f);

                    // Serration (Charge morph): the outer rim scallops inward on a fixed
                    // 6-tooth wave along the case. Weighted by v^4 so only the rim band moves
                    // and the seam-side closure with the belly is untouched. Value-only — the
                    // guard keeps the zero channel bit-exact, and no vertex is added.
                    float notch = 0f;
                    if (s.ElytraSerration > 0f)
                        notch = s.ElytraSerration * halfWidth
                                * (0.5f - 0.5f * Mathf.Cos(i / (float)s.LengthSegments * 6f * Mathf.PI * 2f));

                    for (int j = 0; j <= s.WidthSegments; j++)
                    {
                        float v = j / (float)s.WidthSegments;               // 0 = seam, 1 = outer edge
                        float x = Mathf.Lerp(inner, w, v) * side;
                        if (notch > 0f) x -= side * notch * Mathf.Pow(v, 4f);
                        // Elliptical cross-section: full dome height at the seam, falling to the rim.
                        float y = h * Mathf.Cos(v * Mathf.PI * 0.5f);
                        // Striae: shallow longitudinal ridges, faded at seam and rim so they never
                        // break the closure with the belly or the seam groove.
                        if (s.StriaeCount > 0 && s.StriaeDepth > 0f)
                        {
                            float edgeFade = Mathf.Sin(v * Mathf.PI);
                            y += Mathf.Cos(v * s.StriaeCount * Mathf.PI * 2f)
                                 * s.StriaeDepth * s.DomeHeight * edgeFade;
                        }
                        _verts.Add(new Vector3(x, y, z));
                        _uvs.Add(new Vector2(v, t));
                    }
                }

                // Winding fix (found by the port's orientation test, verified against
                // OctahedronMeshGenerator's proven convention): the original emission wound
                // every face INWARD, so under the hull materials' Cull Back the shell drew
                // only its interior. Outward-from-above needs the flipped order on the
                // RIGHT case (dx > 0) and the plain order on the mirrored left.
                bool flip = side > 0f;
                for (int i = 0; i < s.LengthSegments; i++)
                for (int j = 0; j < s.WidthSegments; j++)
                {
                    int row = s.WidthSegments + 1;
                    int a = baseIndex + i * row + j;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;
                    AddQuad(_shellTris, a, b, d, c, flip);
                }
            }

            void BuildPronotum(float halfWidth)
            {
                int baseIndex = _verts.Count;
                int row = s.WidthSegments * 2 + 1;
                // Overlap the elytra slightly: a butt joint at exactly ElytraFront shows
                // daylight the moment the wing cases flare.
                float tBack = s.ElytraFront - 0.04f;
                int segments = Mathf.Max(4, s.LengthSegments / 2);

                for (int i = 0; i <= segments; i++)
                {
                    float t = Mathf.Lerp(tBack, s.PronotumFront, i / (float)segments);
                    float w = WidthAt(t) * halfWidth * s.PronotumSwell;
                    float h = HeightAt(t) * s.DomeHeight * s.PronotumSwell;
                    float z = ZAt(t);

                    // Keel (Charge morph): a sharp centreline crest riding the shield, faded to
                    // nothing at both the elytra joint and the head so the closures stay exact.
                    float keel = 0f;
                    if (s.PronotumKeel > 0f)
                        keel = s.PronotumKeel * s.DomeHeight
                               * Mathf.Sin(Mathf.Clamp01(i / (float)segments) * Mathf.PI);

                    for (int j = 0; j < row; j++)
                    {
                        float sAcross = j / (float)(row - 1) * 2f - 1f;      // -1 .. +1 across
                        float y = h * Mathf.Cos(sAcross * Mathf.PI * 0.5f);
                        // pow 3 keeps the crest a RIDGE — a wide falloff reads as swelling,
                        // which is Mass's channel, not Charge's.
                        if (keel > 0f)
                            y += keel * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(sAcross)), 3f);
                        _verts.Add(new Vector3(w * sAcross, y, z));
                        _uvs.Add(new Vector2((sAcross + 1f) * 0.5f, t));
                    }
                }

                for (int i = 0; i < segments; i++)
                for (int j = 0; j < row - 1; j++)
                {
                    int a = baseIndex + i * row + j;
                    AddQuad(_shellTris, a, a + 1, a + row + 1, a + row, flip: true);
                }
            }

            void BuildBelly(float halfWidth)
            {
                int baseIndex = _verts.Count;
                int row = s.WidthSegments * 2 + 1;

                for (int i = 0; i <= s.LengthSegments; i++)
                {
                    float t = i / (float)s.LengthSegments;
                    float w = WidthAt(t) * halfWidth;
                    float z = ZAt(t);

                    for (int j = 0; j < row; j++)
                    {
                        float sAcross = j / (float)(row - 1) * 2f - 1f;      // -1 .. +1 across
                        float x = w * sAcross;
                        // Shallow keel — flat-ish, deepest on the centreline.
                        float y = -s.BellyDepth * HeightAt(t) * Mathf.Cos(sAcross * Mathf.PI * 0.5f);
                        _verts.Add(new Vector3(x, y, z));
                        _uvs.Add(new Vector2((sAcross + 1f) * 0.5f, t));
                    }
                }

                for (int i = 0; i < s.LengthSegments; i++)
                for (int j = 0; j < row - 1; j++)
                {
                    int a = baseIndex + i * row + j;
                    AddQuad(_chassisTris, a, a + 1, a + row + 1, a + row, flip: false);  // faces down
                }
            }

            /// <summary>
            /// The clypeus — the flat shovel a scarab pushes with. A SOLID wedge, not a plate:
            /// anything double-sided needs its own vertices or thickness; this has thickness
            /// (a doubled same-vertex quad averages +n and -n to zero and renders black).
            /// </summary>
            void BuildClypeus(float halfWidth)
            {
                float wBack = WidthAt(s.PronotumFront) * halfWidth * s.PronotumSwell;
                float wFront = Mathf.Max(wBack * 0.72f, halfWidth * 0.30f);
                float zBack = ZAt(s.PronotumFront);
                float zFront = ZAt(1f) + s.Length * 0.10f;

                float topBack = HeightAt(s.PronotumFront) * s.DomeHeight * s.PronotumSwell;
                float botBack = -s.BellyDepth * HeightAt(s.PronotumFront) * 0.6f;
                float topFront = topBack * 0.16f;
                float botFront = topFront - Mathf.Max(0.08f, s.DomeHeight * 0.10f);

                AddWedge(zBack, zFront, wBack, wFront, topBack, botBack, topFront, botFront,
                         _chassisTris);
            }

            /// <summary>The horn: a tapered spike off the head shield that sweeps up and
            /// forward. The single most identifying feature of the silhouette, so it rides the
            /// DOMAIN submesh.</summary>
            void BuildHorn()
            {
                float span = s.Length * s.HornLength;
                float rootRadius = s.Width * 0.075f;
                float zRoot = ZAt(s.PronotumFront) + s.Length * 0.04f;
                float yRoot = HeightAt(s.PronotumFront) * s.DomeHeight * 0.55f;

                Begin("horn", new Vector3(0f, yRoot, zRoot));   // hinges at the head

                const int rings = 8;
                int firstRing = _verts.Count;
                float sweepScale = Mathf.Max(0.05f, s.HornCurve);

                // Rings stop one short of the tip and fan to a single APEX vertex — a
                // zero-radius ring renders as nothing and validates as degenerate triangles.
                for (int r = 0; r < rings; r++)
                {
                    float u = r / (float)rings;
                    float sweep = u * s.HornCurve;
                    float z = zRoot + span * Mathf.Sin(sweep) / sweepScale;
                    float y = yRoot + span * (1f - Mathf.Cos(sweep)) / sweepScale;
                    float radius = rootRadius * Mathf.Pow(1f - u, 1.5f);

                    // The ring plane is perpendicular to the ARC TANGENT — rings held in the XY
                    // plane flatten the horn into a smear exactly where it curves most.
                    Vector3 centre = new(0f, y, z);
                    Vector3 axisX = Vector3.right;
                    Vector3 axisY = new(0f, Mathf.Cos(sweep), -Mathf.Sin(sweep));

                    for (int k = 0; k < s.HornSides; k++)
                    {
                        float a = k / (float)s.HornSides * Mathf.PI * 2f;
                        _verts.Add(centre + axisX * (Mathf.Cos(a) * radius)
                                          + axisY * (Mathf.Sin(a) * radius));
                        _uvs.Add(new Vector2(k / (float)s.HornSides, u));
                    }
                }

                int apex = AddVert(new Vector3(0f,
                                               yRoot + span * (1f - Mathf.Cos(s.HornCurve)) / sweepScale,
                                               zRoot + span * Mathf.Sin(s.HornCurve) / sweepScale),
                                   new Vector2(0.5f, 1f));

                for (int r = 0; r < rings - 1; r++)
                for (int k = 0; k < s.HornSides; k++)
                {
                    int k2 = (k + 1) % s.HornSides;
                    int a = firstRing + r * s.HornSides + k;
                    int b = firstRing + r * s.HornSides + k2;
                    int c = firstRing + (r + 1) * s.HornSides + k;
                    int d = firstRing + (r + 1) * s.HornSides + k2;
                    AddQuad(_shellTris, a, b, d, c, flip: false);
                }

                int lastRing = firstRing + (rings - 1) * s.HornSides;
                for (int k = 0; k < s.HornSides; k++)
                    AddTriangle(_shellTris, lastRing + k, lastRing + (k + 1) % s.HornSides, apex);
            }

            /// <summary>
            /// The abdomen dorsum — the soft body UNDER the wing cases. Without it the seam gap
            /// and every open-elytra pose (turn flare, juke splay, boost sweep) show straight
            /// through the ship to the background, and the beetle reads as a hollow shell prop.
            /// A low dome at a fraction of the shell profile, spanning the elytra region, on the
            /// CHASSIS submesh (a beetle's abdomen is the soft body, not armour). Part of Core:
            /// the body does not move when the wing cases do.
            /// </summary>
            void BuildAbdomen(float halfWidth)
            {
                int baseIndex = _verts.Count;
                int row = s.WidthSegments * 2 + 1;
                int segments = Mathf.Max(4, s.LengthSegments / 2);
                // Cover the whole elytra span plus the pronotum overlap, tucked just inside the
                // shell rim so the two never z-fight at the closed pose.
                float tFront = s.ElytraFront + 0.02f;

                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments * tFront;
                    float w = WidthAt(t) * halfWidth * 0.86f;
                    float h = HeightAt(t) * s.DomeHeight * s.AbdomenHeight;
                    float z = ZAt(t);

                    for (int j = 0; j < row; j++)
                    {
                        float sAcross = j / (float)(row - 1) * 2f - 1f;
                        _verts.Add(new Vector3(w * sAcross, h * Mathf.Cos(sAcross * Mathf.PI * 0.5f), z));
                        _uvs.Add(new Vector2((sAcross + 1f) * 0.5f, t));
                    }
                }

                for (int i = 0; i < segments; i++)
                for (int j = 0; j < row - 1; j++)
                {
                    int a = baseIndex + i * row + j;
                    AddQuad(_chassisTris, a, a + 1, a + row + 1, a + row, flip: true);   // faces up
                }
            }

            /// <summary>
            /// Two lamellate-club antennae — THE dung-beetle feature, and the hull's dedicated
            /// secondary-motion showcase (heavy under-damped springs in ScarabAnimation). Each is
            /// a two-segment shaft off the clypeus side sweeping UP and BACK, ending in a fan of
            /// three short lamellae on the DOMAIN submesh — swept high on purpose, so the clubs
            /// break the dome's silhouette from the chase camera astern and flick visibly with
            /// every impulse.
            /// </summary>
            void BuildAntennae(float halfWidth)
            {
                float reach = halfWidth * s.AntennaLength;
                float thick = halfWidth * s.AntennaThickness;
                float wHead = WidthAt(s.PronotumFront) * halfWidth * s.PronotumSwell;
                float zHead = ZAt(s.PronotumFront);
                float yHead = HeightAt(s.PronotumFront) * s.DomeHeight * 0.35f;

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 socket = new(wHead * side * 0.62f, yHead, zHead + s.Length * 0.03f);
                    // Scape: out and up. Funicle: up and back, which is what lifts the club over
                    // the dome line so it reads from astern.
                    Vector3 elbow = socket + new Vector3(side * reach * 0.42f, reach * 0.34f, reach * 0.10f);
                    Vector3 tip = elbow + new Vector3(side * reach * 0.18f, reach * 0.52f, -reach * 0.42f);

                    Begin($"antenna.{(side < 0 ? "l" : "r")}", socket);
                    AddSegment(socket, elbow, thick, thick * 0.8f, _chassisTris);
                    AddSegment(elbow, tip, thick * 0.8f, thick * 0.55f, _chassisTris);

                    // The club: three lamellae fanned off the tip, each a short flattened
                    // segment, in the domain colour so the fan glows the pilot's team.
                    Vector3 fanAxis = (tip - elbow).normalized;
                    Vector3 fanSide = new Vector3(side, 0f, 0f);
                    for (int plate = 0; plate < 3; plate++)
                    {
                        float spread = (plate - 1) * 0.45f;
                        Vector3 dir = (fanAxis + fanSide * (spread * 0.35f)
                                       + new Vector3(0f, 0.12f * plate, -0.18f * spread)).normalized;
                        Vector3 end = tip + dir * (reach * 0.34f);
                        AddSegment(tip, end, thick * 0.9f, thick * 0.5f, _shellTris);
                    }
                }
            }

            /// <summary>
            /// Six JOINTED legs, three per side: femur out to a knee, tibia down and back to the
            /// foot. The knee is what makes a swing read as a leg rather than a spike rotating.
            /// </summary>
            void BuildLegs(float halfWidth)
            {
                float reach = halfWidth * s.LegLength;
                float[] anchors = { 0.22f, 0.44f, 0.72f };
                float[] sweeps = { -0.85f, -0.15f, 0.55f };              // rear legs trail, front reach

                for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < anchors.Length; i++)
                {
                    // Time morph: sockets slide aft (toward t = 0) and tuck inboard — a
                    // sprinter's trailing stance. Value-only; the clamp keeps the rearmost
                    // socket on the body.
                    float t = Mathf.Max(0.06f, anchors[i] - s.LegSocketAftShift);
                    float w = WidthAt(t) * halfWidth;
                    float socketOut = 0.94f - s.LegSocketInboard;
                    Vector3 root = new(w * side * socketOut, -s.BellyDepth * HeightAt(t) * 0.45f, ZAt(t));
                    Vector3 knee = root + new Vector3(side * reach * 0.85f,
                                                      -reach * 0.16f,
                                                      sweeps[i] * reach * 0.55f);
                    Vector3 foot = knee + new Vector3(side * reach * 0.28f,
                                                      -reach * 0.70f,
                                                      sweeps[i] * reach * 0.45f);

                    Begin($"leg.{(side < 0 ? "l" : "r")}{i + 1}", root);
                    AddSegment(root, knee, halfWidth * s.LegThickness, halfWidth * s.LegThickness * 0.7f);
                    AddSegment(knee, foot, halfWidth * s.LegThickness * 0.7f, halfWidth * s.LegThickness * 0.25f);
                }
            }

            /// <summary>A capped four-sided tapered prism between two points. Capped because an
            /// open tube shows its own interior the moment a leg swings past the camera.</summary>
            void AddSegment(Vector3 from, Vector3 to, float radiusFrom, float radiusTo)
                => AddSegment(from, to, radiusFrom, radiusTo, _chassisTris);

            void AddSegment(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, List<int> tris)
            {
                Vector3 axis = (to - from).normalized;
                if (axis.sqrMagnitude < 1e-6f) return;
                Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                Vector3 nx = Vector3.Cross(up, axis).normalized;
                Vector3 ny = Vector3.Cross(axis, nx).normalized;

                int b = _verts.Count;
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 0.5f;
                    Vector3 offset = nx * Mathf.Cos(a) + ny * Mathf.Sin(a);
                    AddVert(from + offset * radiusFrom, new Vector2(i / 4f, 0f));
                }
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 0.5f;
                    Vector3 offset = nx * Mathf.Cos(a) + ny * Mathf.Sin(a);
                    AddVert(to + offset * radiusTo, new Vector2(i / 4f, 1f));
                }

                for (int i = 0; i < 4; i++)
                {
                    int i2 = (i + 1) % 4;
                    AddQuad(tris, b + i, b + i2, b + 4 + i2, b + 4 + i, flip: false);
                }
                AddQuad(tris, b + 0, b + 1, b + 2, b + 3, flip: true);
                AddQuad(tris, b + 4, b + 5, b + 6, b + 7, flip: false);
            }

            /// <summary>A solid six-faced wedge spanning two Z stations. Distinct vertices per
            /// face-pair, so normals stay hard at the edges.</summary>
            void AddWedge(float zBack, float zFront, float wBack, float wFront,
                          float topBack, float botBack, float topFront, float botFront,
                          List<int> tris)
            {
                int b = _verts.Count;
                AddVert(new Vector3(-wBack, topBack, zBack), new Vector2(0f, 0f));
                AddVert(new Vector3(+wBack, topBack, zBack), new Vector2(1f, 0f));
                AddVert(new Vector3(+wBack, botBack, zBack), new Vector2(1f, 0.2f));
                AddVert(new Vector3(-wBack, botBack, zBack), new Vector2(0f, 0.2f));
                AddVert(new Vector3(-wFront, topFront, zFront), new Vector2(0f, 1f));
                AddVert(new Vector3(+wFront, topFront, zFront), new Vector2(1f, 1f));
                AddVert(new Vector3(+wFront, botFront, zFront), new Vector2(1f, 0.8f));
                AddVert(new Vector3(-wFront, botFront, zFront), new Vector2(0f, 0.8f));

                AddQuad(tris, b + 0, b + 1, b + 5, b + 4, flip: true);    // top
                AddQuad(tris, b + 3, b + 2, b + 6, b + 7, flip: false);   // bottom
                AddQuad(tris, b + 0, b + 3, b + 7, b + 4, flip: false);   // left
                AddQuad(tris, b + 1, b + 2, b + 6, b + 5, flip: true);    // right
                AddQuad(tris, b + 4, b + 5, b + 6, b + 7, flip: true);    // front lip
            }

            /// <summary>
            /// Scale X/Z so the finished CARAPACE measures exactly Width × Length, and centre
            /// the whole hull on the origin. The fit measures the carapace ONLY: measuring the
            /// appendages let the legs and horn drive the divisor and squashed the body to ~70%
            /// of its authored size.
            /// </summary>
            void FitToAuthoredExtents()
            {
                bool any = false;
                Vector3 min = Vector3.zero, max = Vector3.zero;
                foreach (var part in Parts)
                {
                    if (!part.IsCarapace) continue;
                    foreach (var v in part.Verts)
                    {
                        if (!any) { min = max = v; any = true; continue; }
                        min = Vector3.Min(min, v);
                        max = Vector3.Max(max, v);
                    }
                }
                if (!any) return;

                Vector3 size = max - min;
                Vector3 centre = (min + max) * 0.5f;
                float sx = size.x > 1e-4f ? s.Width / size.x : 1f;
                float sz = size.z > 1e-4f ? s.Length / size.z : 1f;

                foreach (var part in Parts)
                {
                    for (int i = 0; i < part.Verts.Count; i++)
                        part.Verts[i] = Fit(part.Verts[i], centre, sx, sz);
                    part.Pivot = Fit(part.Pivot, centre, sx, sz);
                }
            }

            static Vector3 Fit(Vector3 p, Vector3 centre, float sx, float sz)
            {
                var q = p - centre;
                return new Vector3(q.x * sx, q.y, q.z * sz);
            }

            /// <summary>
            /// Area-weighted vertex normals, computed HERE rather than by
            /// <c>Mesh.RecalculateNormals</c> so the elemental morphs can lerp between two
            /// builds' normal streams instead of re-deriving normals per animated frame — and so
            /// the offline harness sees exactly the shading the game does. Hard edges fall out
            /// of the vertex-split topology (the wedge and leg caps own their vertices).
            /// </summary>
            void ComputeNormals()
            {
                foreach (var part in Parts)
                {
                    var normals = part.Normals;
                    normals.Clear();
                    for (int i = 0; i < part.Verts.Count; i++) normals.Add(Vector3.zero);
                    Accumulate(part, part.Chassis);
                    Accumulate(part, part.Shell);
                    for (int i = 0; i < normals.Count; i++)
                    {
                        var n = normals[i];
                        normals[i] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
                    }
                }

                static void Accumulate(Part part, List<int> tris)
                {
                    for (int i = 0; i < tris.Count; i += 3)
                    {
                        int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                        // Cross product magnitude = 2 × triangle area, so summing unnormalized
                        // face normals IS the area weighting.
                        Vector3 n = Vector3.Cross(part.Verts[b] - part.Verts[a],
                                                  part.Verts[c] - part.Verts[a]);
                        part.Normals[a] += n;
                        part.Normals[b] += n;
                        part.Normals[c] += n;
                    }
                }
            }

            // ---------------------------------------------------------------- primitives

            int AddVert(Vector3 position, Vector2 uv)
            {
                _verts.Add(position);
                _uvs.Add(uv);
                return _verts.Count - 1;
            }

            static void AddTriangle(List<int> tris, int a, int b, int c)
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
            }

            static void AddQuad(List<int> tris, int a, int b, int c, int d, bool flip)
            {
                if (flip)
                {
                    AddTriangle(tris, a, c, b);
                    AddTriangle(tris, a, d, c);
                }
                else
                {
                    AddTriangle(tris, a, b, c);
                    AddTriangle(tris, a, c, d);
                }
            }
        }
    }
}
