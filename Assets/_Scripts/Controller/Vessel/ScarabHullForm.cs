using System.Collections.Generic;
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
            };
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

                // The wing cases hinge about the seam, so their pivot is the centreline.
                Begin("elytron.r", Vector3.zero, carapace: true); BuildShell(+1f, seamHalf, halfWidth);
                Begin("elytron.l", Vector3.zero, carapace: true); BuildShell(-1f, seamHalf, halfWidth);

                Begin("pronotum", Vector3.zero, carapace: true); BuildPronotum(halfWidth);

                if (s.HornLength > 0.001f) BuildHorn();
                if (s.LegLength > 0.001f) BuildLegs(halfWidth);

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
            /// rounded skirt. Starting at 0.10 leaves the tail at ~2/3 width.
            /// </summary>
            static float ShellU(float t) => Mathf.Lerp(0.10f, 0.995f, Mathf.Clamp01(t));

            /// <summary>Half-width of the shell at longitudinal position t (0 = tail, 1 = nose),
            /// as a fraction of the half-width.</summary>
            static float WidthAt(float t)
            {
                float sn = Mathf.Sin(Mathf.Pow(ShellU(t), 0.80f) * Mathf.PI);
                return Mathf.Pow(Mathf.Max(0f, sn), 0.55f);
            }

            /// <summary>Dome height factor at t. Peaks behind the middle so the shell looks
            /// loaded at the back, then falls away toward the head shield.</summary>
            static float HeightAt(float t)
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

                    for (int j = 0; j <= s.WidthSegments; j++)
                    {
                        float v = j / (float)s.WidthSegments;               // 0 = seam, 1 = outer edge
                        float x = Mathf.Lerp(inner, w, v) * side;
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

                    for (int j = 0; j < row; j++)
                    {
                        float sAcross = j / (float)(row - 1) * 2f - 1f;      // -1 .. +1 across
                        _verts.Add(new Vector3(w * sAcross, h * Mathf.Cos(sAcross * Mathf.PI * 0.5f), z));
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
                    float t = anchors[i];
                    float w = WidthAt(t) * halfWidth;
                    Vector3 root = new(w * side * 0.94f, -s.BellyDepth * HeightAt(t) * 0.45f, ZAt(t));
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
                    AddQuad(_chassisTris, b + i, b + i2, b + 4 + i2, b + 4 + i, flip: false);
                }
                AddQuad(_chassisTris, b + 0, b + 1, b + 2, b + 3, flip: true);
                AddQuad(_chassisTris, b + 4, b + 5, b + 6, b + 7, flip: false);
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
