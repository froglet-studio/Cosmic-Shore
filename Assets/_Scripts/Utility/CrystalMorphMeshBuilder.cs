using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Builds the mesh that carries a collected crystal's BODY onto another shape — the geometry
    /// behind a vessel's bespoke omni-crystal retirement, the per-hull replacement for the shared
    /// husk spray.
    ///
    /// The mesh is the SOURCE mesh, vertex for vertex, plus TWO extra attributes per vertex:
    /// TEXCOORD2 = (target position, phase) and TEXCOORD3 = (target NORMAL, the same phase). A
    /// vertex shader lerps both off one stamped clock (<c>CrystalMorph.hlsl</c>), so the animation
    /// costs zero CPU per frame and — critically — at t = 0 the mesh renders EXACTLY as the crystal
    /// did, because it IS the crystal's geometry with the crystal's own normals, tangents and UVs.
    /// That identity is what makes the hand-off seamless; do not "optimise" it by re-generating a
    /// simplified cage.
    ///
    /// **The normal is not decoration.** Both the crystal's shader and the shape it lands on derive
    /// their base colour from <c>(1 − N·V)⁴</c> through the same <c>FresnelColors</c> subgraph, so a
    /// morph that carried only POSITION would arrive with the crystal cage's normals sitting on the
    /// target's faces: the right shape wearing the wrong surface, which is a seam no colour match
    /// can close.
    ///
    /// ── The target is a CONVEX HULL, and the mapping is per-vertex ────────────────────────────
    /// Every source vertex slides along its own ray from the hull's centre until it meets the
    /// hull's surface, and takes that facet's normal. This needs no census, no assignment and no
    /// 1:1 correspondence between the crystal's panels and the target's faces — which matters,
    /// because the crystal's cage (122 disjoint solids, 64 non-quad panels) has no arithmetic
    /// relationship to a subdivided icosphere's 320 facets. What it gives up is the "every panel
    /// becomes exactly one face" reading; what it buys is that the last frame lies EXACTLY on the
    /// target's real surface, with the target's real per-facet normals, for any convex target.
    ///
    /// A hull's surface is a continuous function of direction, so adjacent source vertices landing
    /// either side of a crease land on the crease — the cage folds around the shape rather than
    /// tearing.
    ///
    /// ── PHASE is per SOLID, not per vertex ────────────────────────────────────────────────────
    /// A solid is found by welded POSITION (connected components over shared vertices), which is
    /// what keeps one strut's faces travelling together instead of stretching it between two
    /// schedules. Each solid's phase is its centroid's rank by distance from the centre, mapped
    /// into <c>[phaseStart, phaseEnd]</c> — so authoring <c>start &gt; end</c> inverts the cascade
    /// (outermost-first becomes innermost-first) with no code change.
    ///
    /// ── The second mapping: a PANEL CENSUS onto octahedra (the Squirrel) ─────────────────────
    /// <see cref="TryBuild(Mesh, IReadOnlyList{OctahedronTarget}, float, float, float, out string)"/>
    /// is the other reading, and it rests on an exact coincidence rather than on convexity: the
    /// omni body's cage is 122 disjoint solids — 90 box struts, 20 triangular prisms, 12 pentagonal
    /// prisms — so its NON-QUAD faces are 20×2 + 12×2 = <b>64</b>, and the eight shielded prisms of
    /// the Squirrel's boost ring show 8 × 8 = <b>64</b> octahedron faces. Every panel becomes exactly
    /// one face, with nothing invented and nothing spare; the 660 quads (struts and panel rims) are
    /// the leftovers, and each collapses into the octahedron its own solid was assigned to. Eight
    /// separate octahedra are not one convex hull, which is why this is a census and not a cast.
    /// Proven against the shipped FBX by <c>Tools/Build/measure_omni_crystal_morph.py</c>.
    ///
    /// Two traps that mapping is written around:
    /// 1. <b>A face is found STRUCTURALLY, never by coplanarity.</b> 60 of the cage's quads are
    ///    non-planar (a ~5° twist), so a plane test cuts them in half and reports 160 triangle
    ///    panels where there are 40 — measured. Triangles cut from one imported polygon share
    ///    vertex INDICES and triangles from different polygons cannot, because a hard-edged import
    ///    splits those corners apart.
    /// 2. <b>A panel must BECOME its face, not sit inside it.</b> A raw perimeter map put only 83
    ///    of 336 panel corners on a target corner, so every octahedron would have landed as
    ///    shrunken plates with gaps; three corners are ANCHORED to the face's three corners and
    ///    the rest ride its edges (<see cref="MapPanel"/>).
    /// </summary>
    public static class CrystalMorphMeshBuilder
    {
        /// <summary>UV channel carrying (target position .xyz, phase .w). Read by CrystalMorph.hlsl.</summary>
        public const int TargetUVChannel = 2;

        /// <summary>UV channel carrying (target normal .xyz, phase .w). Read by CrystalMorphNormal
        /// in CrystalMorph.hlsl. The phase is duplicated rather than shared because a Custom
        /// Function node reads one input: position and normal must travel on the SAME schedule or a
        /// face's shading arrives before or after its shape.</summary>
        public const int TargetNormalUVChannel = 3;

        /// <summary>Weld tolerance for the solid grouping, in the source mesh's own local units.</summary>
        public const float WeldEpsilon = 1e-4f;

        /// <summary>
        /// The shape a crystal is morphing into, as a closed convex hull in the morph object's own
        /// local space: a centre strictly inside it, and its triangles flat in <see cref="Corners"/>
        /// (face f owns [3f, 3f+2]) with one outward <see cref="Normals"/> entry each.
        ///
        /// Built from the target's OWN shipped mesh — never a re-derived approximation — so the
        /// morph's last frame and the target's first frame are the same geometry and there is no
        /// second authority to drift from.
        /// </summary>
        public readonly struct ConvexHullTarget
        {
            public readonly Vector3 Centre;
            /// <summary>Faces × 3 corners, flat. Face f owns [3f, 3f+2].</summary>
            public readonly Vector3[] Corners;
            /// <summary>One outward normal per face.</summary>
            public readonly Vector3[] Normals;

            public ConvexHullTarget(Vector3 centre, Vector3[] corners, Vector3[] normals)
            {
                Centre = centre;
                Corners = corners;
                Normals = normals;
            }

            public int FaceCount => Corners == null ? 0 : Corners.Length / 3;
            public bool IsValid => Corners != null && Normals != null
                                   && Corners.Length >= 3 && Corners.Length % 3 == 0
                                   && Normals.Length == Corners.Length / 3;

            /// <summary>
            /// Reads a target hull straight out of a mesh, transformed by <paramref name="toLocal"/>
            /// into the morph object's frame. The normal is recomputed from the transformed corners
            /// rather than carried from the mesh, because a transform can mirror (and a flat-shaded
            /// icosphere's authored normals are per-corner duplicates of the same face normal
            /// anyway) — deriving it keeps the outward sense correct by construction, which is
            /// checked against the centre.
            /// </summary>
            public static bool TryFromMesh(Mesh mesh, Matrix4x4 toLocal, Vector3 centreLocal,
                                           out ConvexHullTarget target, out string diagnosis)
            {
                target = default;
                diagnosis = null;

                if (mesh == null) { diagnosis = "the target mesh is null"; return false; }
                if (!mesh.isReadable)
                {
                    diagnosis = $"the target mesh '{mesh.name}' is not readable — a generated mesh " +
                                "is readable by default, so an unreadable one came from an importer " +
                                "with Read/Write off";
                    return false;
                }

                var verts = mesh.vertices;
                var tris = mesh.triangles;
                if (tris.Length < 3 || tris.Length % 3 != 0)
                {
                    diagnosis = $"the target mesh '{mesh.name}' has {tris.Length} indices — not a " +
                                "whole number of triangles";
                    return false;
                }

                int faces = tris.Length / 3;
                var corners = new Vector3[tris.Length];
                var normals = new Vector3[faces];

                for (int f = 0; f < faces; f++)
                {
                    Vector3 a = toLocal.MultiplyPoint3x4(verts[tris[3 * f]]);
                    Vector3 b = toLocal.MultiplyPoint3x4(verts[tris[3 * f + 1]]);
                    Vector3 c = toLocal.MultiplyPoint3x4(verts[tris[3 * f + 2]]);
                    corners[3 * f] = a;
                    corners[3 * f + 1] = b;
                    corners[3 * f + 2] = c;

                    Vector3 n = Vector3.Cross(b - a, c - a);
                    float len = n.magnitude;
                    // A degenerate facet cannot state a direction; point it outward from the centre
                    // so it can never flip a vertex's shading inside out.
                    Vector3 outward = (a + b + c) / 3f - centreLocal;
                    n = len > 1e-8f ? n / len : outward.normalized;
                    if (Vector3.Dot(n, outward) < 0f) n = -n;
                    normals[f] = n;
                }

                target = new ConvexHullTarget(centreLocal, corners, normals);
                return true;
            }
        }

        /// <summary>
        /// Emits the morph mesh: the source's geometry unshared (one vertex per triangle corner),
        /// with every vertex's hull destination in TEXCOORD2 and its destination NORMAL in
        /// TEXCOORD3, both stamped with its solid's phase.
        ///
        /// Returns null with a <paramref name="diagnosis"/> naming the fix rather than throwing —
        /// this runs inside an impact-effect dispatch, and an exception there unwinds a caller that
        /// has already minted the thing being morphed into.
        /// </summary>
        /// <param name="source">The crystal's own cage mesh. MUST be Read/Write enabled.</param>
        /// <param name="target">The hull to land on, in the same local space as the morph object.</param>
        /// <param name="phaseStart">Phase of the solid nearest the centre.</param>
        /// <param name="phaseEnd">Phase of the solid furthest from it. Author below
        /// <paramref name="phaseStart"/> to invert the cascade.</param>
        public static Mesh TryBuild(Mesh source, in ConvexHullTarget target,
                                    float phaseStart, float phaseEnd, out string diagnosis)
        {
            diagnosis = null;

            if (source == null) { diagnosis = "the crystal exposed no source mesh"; return null; }
            if (!source.isReadable)
            {
                diagnosis = $"'{source.name}' is not Read/Write enabled, so its vertices cannot be " +
                            "read on the CPU (an imported mesh THROWS rather than returning empty). " +
                            "Fix it on the model importer: select the FBX, tick Read/Write, apply.";
                return null;
            }
            if (!target.IsValid)
            {
                diagnosis = "the target hull is empty or malformed (corners must be a whole number " +
                            "of triangles with one normal each)";
                return null;
            }

            var srcVerts = source.vertices;
            var srcTris = source.triangles;
            if (srcTris.Length < 3)
            {
                diagnosis = $"'{source.name}' has no triangles to carry";
                return null;
            }

            // ── Solids, by welded position ────────────────────────────────────────────────────
            // Union-find over the source's own index buffer: two corners at the same POSITION are
            // the same point of the same solid, whichever triangles reference them. This is the
            // opposite grouping to anything face-based, and it is what keeps a strut's six faces
            // travelling on one schedule instead of stretching between two.
            int[] weld = WeldMap(srcVerts, WeldEpsilon);
            var parent = new int[srcVerts.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int i = 0; i < srcVerts.Length; i++) Union(parent, i, weld[i]);
            for (int t = 0; t < srcTris.Length; t += 3)
            {
                Union(parent, srcTris[t], srcTris[t + 1]);
                Union(parent, srcTris[t + 1], srcTris[t + 2]);
            }

            // Solid id per source vertex, plus each solid's centroid distance from the centre.
            var solidOf = new Dictionary<int, int>();
            var solidSumR = new List<float>();
            var solidCount = new List<int>();
            var vertSolid = new int[srcVerts.Length];
            for (int i = 0; i < srcVerts.Length; i++)
            {
                int root = Find(parent, i);
                if (!solidOf.TryGetValue(root, out int id))
                {
                    id = solidSumR.Count;
                    solidOf[root] = id;
                    solidSumR.Add(0f);
                    solidCount.Add(0);
                }
                vertSolid[i] = id;
                solidSumR[id] += (srcVerts[i] - target.Centre).magnitude;
                solidCount[id]++;
            }

            int solids = solidSumR.Count;
            var solidRadius = new float[solids];
            for (int s = 0; s < solids; s++)
                solidRadius[s] = solidCount[s] > 0 ? solidSumR[s] / solidCount[s] : 0f;

            float minR = float.MaxValue, maxR = float.MinValue;
            for (int s = 0; s < solids; s++)
            {
                if (solidRadius[s] < minR) minR = solidRadius[s];
                if (solidRadius[s] > maxR) maxR = solidRadius[s];
            }
            float span = Mathf.Max(1e-5f, maxR - minR);

            var solidPhase = new float[solids];
            for (int s = 0; s < solids; s++)
                solidPhase[s] = Mathf.Lerp(phaseStart, phaseEnd, (solidRadius[s] - minR) / span);

            // ── Per-unique-position hull landing ──────────────────────────────────────────────
            // Cached by welded index so the ~2.9k distinct points of a cage are cast once each,
            // not once per triangle corner.
            var landedPos = new Vector3[srcVerts.Length];
            var landedNrm = new Vector3[srcVerts.Length];
            var landed = new bool[srcVerts.Length];

            var faceCentroidDir = new Vector3[target.FaceCount];
            for (int f = 0; f < target.FaceCount; f++)
            {
                Vector3 c = (target.Corners[3 * f] + target.Corners[3 * f + 1] + target.Corners[3 * f + 2]) / 3f;
                faceCentroidDir[f] = (c - target.Centre).normalized;
            }

            for (int i = 0; i < srcVerts.Length; i++)
            {
                int w = weld[i];
                if (!landed[w])
                {
                    LandOnHull(srcVerts[w], in target, faceCentroidDir,
                               out landedPos[w], out landedNrm[w]);
                    landed[w] = true;
                }
                landedPos[i] = landedPos[w];
                landedNrm[i] = landedNrm[w];
            }

            var vertexTarget = new Vector4[srcVerts.Length];
            var vertexTargetNormal = new Vector4[srcVerts.Length];
            for (int i = 0; i < srcVerts.Length; i++)
            {
                float phase = Mathf.Clamp01(solidPhase[vertSolid[i]]);
                vertexTarget[i] = new Vector4(landedPos[i].x, landedPos[i].y, landedPos[i].z, phase);
                vertexTargetNormal[i] = new Vector4(landedNrm[i].x, landedNrm[i].y, landedNrm[i].z, phase);
            }

            return Emit(source, srcVerts, srcTris, vertexTarget, vertexTargetNormal, target.Corners);
        }

        // ══ The panel census: a crystal onto a SET of octahedra ════════════════════════════════

        /// <summary>
        /// One octahedron the crystal is morphing into, in the morph object's local space: its
        /// centre and its eight faces as three corner POSITIONS each (face f owns [3f, 3f+2]).
        ///
        /// Corner positions carry no winding, which is deliberate — the outward sense of each face
        /// is recovered from <see cref="Centre"/>, so the face set can come from anything that
        /// knows the octahedron's six apexes without agreeing with
        /// <see cref="OctahedronMeshGenerator"/>'s triangle order.
        /// </summary>
        public readonly struct OctahedronTarget
        {
            public readonly Vector3 Centre;
            /// <summary>8 faces × 3 corners, flat. Face f owns [3f, 3f+2].</summary>
            public readonly Vector3[] FaceCorners;

            public OctahedronTarget(Vector3 centre, Vector3[] faceCorners)
            {
                Centre = centre;
                FaceCorners = faceCorners;
            }

            public int FaceCount => FaceCorners == null ? 0 : FaceCorners.Length / 3;

            /// <summary>
            /// The eight octant faces of the octahedron whose apexes sit at
            /// <paramref name="centre"/> ± each semi-axis, all mapped through
            /// <paramref name="toLocal"/>. This is exactly the face set a prism's shield draws
            /// (<see cref="OctahedronMeshGenerator"/>: one face per octant of ±x, ±y, ±z).
            /// </summary>
            public static OctahedronTarget FromSemiAxes(Matrix4x4 toLocal, Vector3 centre, Vector3 semiAxes)
            {
                Vector3 px = toLocal.MultiplyPoint3x4(centre + new Vector3(semiAxes.x, 0f, 0f));
                Vector3 nx = toLocal.MultiplyPoint3x4(centre - new Vector3(semiAxes.x, 0f, 0f));
                Vector3 py = toLocal.MultiplyPoint3x4(centre + new Vector3(0f, semiAxes.y, 0f));
                Vector3 ny = toLocal.MultiplyPoint3x4(centre - new Vector3(0f, semiAxes.y, 0f));
                Vector3 pz = toLocal.MultiplyPoint3x4(centre + new Vector3(0f, 0f, semiAxes.z));
                Vector3 nz = toLocal.MultiplyPoint3x4(centre - new Vector3(0f, 0f, semiAxes.z));

                var corners = new Vector3[24];
                int w = 0;
                for (int sx = 0; sx < 2; sx++)
                    for (int sy = 0; sy < 2; sy++)
                        for (int sz = 0; sz < 2; sz++)
                        {
                            corners[w++] = sx == 0 ? px : nx;
                            corners[w++] = sy == 0 ? py : ny;
                            corners[w++] = sz == 0 ? pz : nz;
                        }
                return new OctahedronTarget(toLocal.MultiplyPoint3x4(centre), corners);
            }
        }

        // Source analysis, cached per mesh. Positions, normals and the face partition are
        // properties of the SOURCE alone — one cage for every omni crystal in the game — so they
        // are measured once per session; only the targets change per morph.
        sealed class PanelAnalysis
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public int[] Triangles;
            /// <summary>Each face's triangle indices (into <see cref="Triangles"/>/3).</summary>
            public List<int>[] FaceTriangles;
            /// <summary>Each face's unique source vertex indices, ordered around the polygon.</summary>
            public int[][] FaceCorners;
            public Vector3[] FaceCentroid;
            /// <summary>Solid id per face.</summary>
            public int[] FaceSolid;
            /// <summary>Faces that are NOT quads — the panels that become octahedron faces.</summary>
            public List<int> Panels;
            public List<int> Fillers;
            public Dictionary<int, List<int>> PanelsBySolid;
            public Dictionary<int, Vector3> SolidCentroid;
        }

        static readonly Dictionary<int, PanelAnalysis> s_panelAnalysis = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPanelCache() => s_panelAnalysis.Clear();

        /// <summary>
        /// Emits a morph mesh that starts as <paramref name="source"/> and ends as
        /// <paramref name="targets"/>: every non-quad PANEL lands exactly on one octahedron face,
        /// 1:1, and every quad collapses into the octahedron its own solid was assigned to.
        ///
        /// Returns null with a <paramref name="diagnosis"/> when the census does not line up —
        /// a half-mapped morph is worse than none, because it reads as a broken shape rather than
        /// as a missing animation.
        /// </summary>
        /// <param name="fillerPhase">Phase of the leftover quads. 0 = absorbed FIRST, so nothing is
        /// left hanging around the shape when the panels land.</param>
        /// <param name="panelPhaseStart">Phase of each octahedron's first face to land.</param>
        /// <param name="panelPhaseEnd">Phase of each octahedron's last face to land.</param>
        public static Mesh TryBuild(Mesh source, IReadOnlyList<OctahedronTarget> targets,
                                    float fillerPhase, float panelPhaseStart, float panelPhaseEnd,
                                    out string diagnosis)
        {
            diagnosis = null;
            if (source == null) { diagnosis = "the crystal exposed no source mesh"; return null; }
            if (targets == null || targets.Count == 0) { diagnosis = "no octahedron targets"; return null; }
            for (int k = 0; k < targets.Count; k++)
                if (targets[k].FaceCount != 8)
                {
                    diagnosis = $"target {k} has {targets[k].FaceCount} faces, not an octahedron's 8";
                    return null;
                }

            // Read/Write is checked FIRST: without it `Mesh.vertices` does not return empty, it
            // THROWS, and the throw escapes through whatever raised the event that got us here.
            // That is how the Squirrel's morph first shipped dead — the only symptom was the ring
            // appearing normally while the crystal faded out.
            if (!source.isReadable)
            {
                diagnosis = $"'{source.name}' is not Read/Write enabled, so its vertices cannot be " +
                            "read on the CPU (an imported mesh THROWS rather than returning empty). " +
                            "Fix it on the model importer: select the FBX, tick Read/Write, apply.";
                return null;
            }

            var a = AnalysePanels(source);
            if (a == null) { diagnosis = $"'{source.name}' has no geometry (no vertices or no triangles)"; return null; }

            int targetFaces = targets.Count * 8;
            if (a.Panels.Count != targetFaces)
            {
                diagnosis = $"'{source.name}' has {a.Panels.Count} panel (non-quad) faces but " +
                            $"{targets.Count} octahedra need {targetFaces}. The morph maps panels to " +
                            "faces 1:1 — the omni cage's census is 64 = 8 × 8 " +
                            "(Tools/Build/measure_omni_crystal_morph.py).";
                return null;
            }

            var vertexTarget = new Vector4[a.Vertices.Length];
            var vertexTargetNormal = new Vector4[a.Vertices.Length];
            AssignPanels(a, targets, vertexTarget, vertexTargetNormal,
                         Mathf.Clamp01(fillerPhase), Mathf.Clamp01(panelPhaseStart),
                         Mathf.Clamp01(panelPhaseEnd));

            var extent = new Vector3[targets.Count * 25];
            int e = 0;
            for (int k = 0; k < targets.Count; k++)
            {
                extent[e++] = targets[k].Centre;
                for (int c = 0; c < 24; c++) extent[e++] = targets[k].FaceCorners[c];
            }
            return Emit(source, a.Vertices, a.Triangles, vertexTarget, vertexTargetNormal, extent);
        }

        static PanelAnalysis AnalysePanels(Mesh source)
        {
            if (s_panelAnalysis.TryGetValue(source.GetInstanceID(), out var cached)) return cached;

            var verts = source.vertices;
            var tris = source.triangles;
            if (verts.Length == 0 || tris.Length < 3) return null;

            var normals = source.normals;
            var a = new PanelAnalysis
            {
                Vertices = verts,
                Normals = normals != null && normals.Length == verts.Length ? normals : null,
                Triangles = tris,
            };

            // FACES: union-find over shared vertex INDICES — never by plane (class doc, trap 1).
            var faceOf = new int[verts.Length];
            for (int i = 0; i < faceOf.Length; i++) faceOf[i] = i;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(faceOf, tris[t], tris[t + 1]);
                Union(faceOf, tris[t], tris[t + 2]);
            }

            // SOLIDS: union-find over WELDED positions plus the index buffer.
            int[] weld = WeldMap(verts, WeldEpsilon);
            var solidOf = new int[verts.Length];
            for (int i = 0; i < solidOf.Length; i++) solidOf[i] = i;
            for (int i = 0; i < verts.Length; i++) Union(solidOf, i, weld[i]);
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(solidOf, tris[t], tris[t + 1]);
                Union(solidOf, tris[t], tris[t + 2]);
            }

            var faceIndex = new Dictionary<int, int>();
            var faceTris = new List<List<int>>();
            var faceVerts = new List<HashSet<int>>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int root = Find(faceOf, tris[t]);
                if (!faceIndex.TryGetValue(root, out int fi))
                {
                    fi = faceTris.Count;
                    faceIndex[root] = fi;
                    faceTris.Add(new List<int>());
                    faceVerts.Add(new HashSet<int>());
                }
                faceTris[fi].Add(t / 3);
                faceVerts[fi].Add(tris[t]);
                faceVerts[fi].Add(tris[t + 1]);
                faceVerts[fi].Add(tris[t + 2]);
            }

            int faceCount = faceTris.Count;
            a.FaceTriangles = faceTris.ToArray();
            a.FaceCorners = new int[faceCount][];
            a.FaceCentroid = new Vector3[faceCount];
            a.FaceSolid = new int[faceCount];
            a.Panels = new List<int>();
            a.Fillers = new List<int>();
            a.PanelsBySolid = new Dictionary<int, List<int>>();
            a.SolidCentroid = new Dictionary<int, Vector3>();
            var solidSum = new Dictionary<int, Vector3>();
            var solidN = new Dictionary<int, int>();

            for (int f = 0; f < faceCount; f++)
            {
                var corners = new int[faceVerts[f].Count];
                faceVerts[f].CopyTo(corners);

                Vector3 c = Vector3.zero;
                foreach (int v in corners) c += verts[v];
                c /= corners.Length;
                a.FaceCentroid[f] = c;

                // Ordered around the polygon so the anchor map can walk the outline by arc length.
                OrderAroundCentroid(verts, corners, c, TriangleNormal(verts, tris, faceTris[f][0]));
                a.FaceCorners[f] = corners;

                int solid = Find(solidOf, corners[0]);
                a.FaceSolid[f] = solid;
                solidSum[solid] = (solidSum.TryGetValue(solid, out var sum) ? sum : Vector3.zero) + c;
                solidN[solid] = (solidN.TryGetValue(solid, out int n) ? n : 0) + 1;

                if (corners.Length == 4) a.Fillers.Add(f);
                else
                {
                    a.Panels.Add(f);
                    if (!a.PanelsBySolid.TryGetValue(solid, out var list))
                        a.PanelsBySolid[solid] = list = new List<int>();
                    list.Add(f);
                }
            }
            foreach (var kv in solidSum) a.SolidCentroid[kv.Key] = kv.Value / solidN[kv.Key];

            s_panelAnalysis[source.GetInstanceID()] = a;
            return a;
        }

        static Vector3 TriangleNormal(Vector3[] verts, int[] tris, int triIndex)
        {
            int t = triIndex * 3;
            var n = Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);
            return n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;
        }

        static void OrderAroundCentroid(Vector3[] verts, int[] corners, Vector3 centre, Vector3 normal)
        {
            if (corners.Length < 3) return;
            Vector3 u = Vector3.Cross(normal, verts[corners[0]] - centre);
            u = u.sqrMagnitude > 1e-20f ? Vector3.Cross(u, normal).normalized : Vector3.right;
            Vector3 v = Vector3.Cross(normal, u);

            var keys = new float[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 d = verts[corners[i]] - centre;
                keys[i] = Mathf.Atan2(Vector3.Dot(d, v), Vector3.Dot(d, u));
            }
            System.Array.Sort(keys, corners);
        }

        static void AssignPanels(PanelAnalysis a, IReadOnlyList<OctahedronTarget> targets,
                                 Vector4[] vertexTarget, Vector4[] vertexTargetNormal,
                                 float fillerPhase, float panelPhaseStart, float panelPhaseEnd)
        {
            int octCount = targets.Count;

            // Directions are taken about the centre of the WHOLE target set, never the morph
            // object's origin: the ring is laid ahead of the hull, so measured from the crystal's
            // own centre every octahedron would sit in roughly the same direction and the
            // balanced pass below would be choosing between near-ties.
            Vector3 ringCentre = Vector3.zero;
            for (int k = 0; k < octCount; k++) ringCentre += targets[k].Centre;
            ringCentre /= octCount;
            Vector3 cageCentre = Vector3.zero;
            int solids = 0;
            foreach (var kv in a.SolidCentroid) { cageCentre += kv.Value; solids++; }
            if (solids > 0) cageCentre /= solids;

            var octDir = new Vector3[octCount];
            for (int k = 0; k < octCount; k++) octDir[k] = SafeDir(targets[k].Centre - ringCentre);

            // Panel-carrying solids spread EVENLY over the octahedra: each panel solid carries the
            // same number of panels (two caps), so an even split of solids is an even split of
            // faces. A solid's parts always travel together — the reason solids exist at all.
            var panelSolids = new List<int>(a.PanelsBySolid.Keys);
            panelSolids.Sort();
            int perOct = Mathf.Max(1, panelSolids.Count / octCount);

            var scored = new List<(float score, int solid, int oct)>(panelSolids.Count * octCount);
            foreach (int s in panelSolids)
            {
                Vector3 d = SafeDir(a.SolidCentroid[s] - cageCentre);
                for (int k = 0; k < octCount; k++)
                    scored.Add((-Vector3.Dot(d, octDir[k]), s, k));
            }
            scored.Sort((x, y) => x.score != y.score ? x.score.CompareTo(y.score)
                                                     : (x.solid != y.solid ? x.solid.CompareTo(y.solid)
                                                                           : x.oct.CompareTo(y.oct)));
            var solidOct = new Dictionary<int, int>(panelSolids.Count);
            var counts = new int[octCount];
            foreach (var (_, s, k) in scored)
            {
                if (solidOct.ContainsKey(s) || counts[k] >= perOct) continue;
                solidOct[s] = k;
                counts[k]++;
            }
            // A census that is not an exact multiple falls back to nearest so no panel is ever
            // left without an octahedron (the count check above makes this unreachable today).
            foreach (int s in panelSolids)
                if (!solidOct.ContainsKey(s))
                    solidOct[s] = NearestOct(a.SolidCentroid[s] - cageCentre, octDir);

            // Panels → the faces of THEIR solid's octahedron, greedy by angular fit. Each panel is
            // read about its own solid's centroid and each face about its octahedron's centre, so
            // the fit is "which side of the shield does this side of the plate become".
            var panelPairs = new List<(float score, int panel, int oct, int face)>();
            foreach (var kv in a.PanelsBySolid)
            {
                int k = solidOct[kv.Key];
                Vector3 solidCentre = a.SolidCentroid[kv.Key];
                foreach (int f in kv.Value)
                {
                    Vector3 pd = SafeDir(a.FaceCentroid[f] - solidCentre);
                    for (int fi = 0; fi < 8; fi++)
                        panelPairs.Add((-Vector3.Dot(pd, SafeDir(FaceCentre(targets[k], fi) - targets[k].Centre)),
                                        f, k, fi));
                }
            }
            panelPairs.Sort((x, y) => x.score != y.score ? x.score.CompareTo(y.score)
                                                         : (x.panel != y.panel ? x.panel.CompareTo(y.panel)
                                                                               : x.face.CompareTo(y.face)));
            var faceTaken = new HashSet<(int oct, int face)>();
            var panelFace = new Dictionary<int, (int oct, int face)>(a.Panels.Count);
            foreach (var (_, panel, oct, face) in panelPairs)
            {
                if (panelFace.ContainsKey(panel) || faceTaken.Contains((oct, face))) continue;
                panelFace[panel] = (oct, face);
                faceTaken.Add((oct, face));
            }

            // A face left unclaimed (its octahedron was handed panels it could not seat by fit)
            // takes the first unseated panel — the count check guarantees the two lists match.
            foreach (int f in a.Panels)
            {
                if (panelFace.ContainsKey(f)) continue;
                for (int k = 0; k < octCount && !panelFace.ContainsKey(f); k++)
                    for (int fi = 0; fi < 8; fi++)
                        if (faceTaken.Add((k, fi))) { panelFace[f] = (k, fi); break; }
            }

            foreach (var kv in panelFace)
            {
                var (oct, face) = kv.Value;
                // Spread across each octahedron's eight faces, so a shield ASSEMBLES face by face
                // rather than appearing whole.
                float phase = Mathf.Lerp(panelPhaseStart, panelPhaseEnd, face / 7f);
                MapPanel(a, kv.Key, targets[oct], face, phase, vertexTarget, vertexTargetNormal);
            }

            foreach (int f in a.Fillers)
            {
                int solid = a.FaceSolid[f];
                int k = solidOct.TryGetValue(solid, out int assigned)
                    ? assigned
                    : NearestOct(a.SolidCentroid[solid] - cageCentre, octDir);
                // Collapse to the octahedron's CENTRE: the quad becomes a point inside the shield
                // and is absorbed, rather than being left hanging as a face with no home.
                var c = targets[k].Centre;
                var target = new Vector4(c.x, c.y, c.z, fillerPhase);
                foreach (int tri in a.FaceTriangles[f])
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int v = a.Triangles[tri * 3 + corner];
                        vertexTarget[v] = target;
                        // A leftover collapses to a POINT and has no area left to shade, so it has
                        // no orientation to arrive at: its normal is held at its own, which makes
                        // the blend a no-op instead of a swing on the way to nothing.
                        Vector3 n = a.Normals != null ? a.Normals[v] : Vector3.up;
                        vertexTargetNormal[v] = new Vector4(n.x, n.y, n.z, fillerPhase);
                    }
            }
        }

        static int NearestOct(Vector3 from, Vector3[] octDir)
        {
            Vector3 d = SafeDir(from);
            int best = 0;
            float bestDot = float.NegativeInfinity;
            for (int k = 0; k < octDir.Length; k++)
            {
                float dot = Vector3.Dot(d, octDir[k]);
                if (dot > bestDot) { bestDot = dot; best = k; }
            }
            return best;
        }

        static Vector3 SafeDir(Vector3 v) => v.sqrMagnitude > 1e-12f ? v.normalized : Vector3.forward;

        static Vector3 FaceCentre(in OctahedronTarget t, int face) =>
            (t.FaceCorners[face * 3] + t.FaceCorners[face * 3 + 1] + t.FaceCorners[face * 3 + 2]) / 3f;

        /// <summary>
        /// Maps one panel's corners onto its target triangle by ANCHORING three of them to the
        /// triangle's three corners and sliding the rest along the edges between them (class doc,
        /// trap 2). Anchored, the panel's outline IS the face's outline and its area IS the face's
        /// area; a pentagon's two extra corners ride ON the edges, adding vertices without changing
        /// the shape.
        ///
        /// The alignment — which source corners anchor, which target corner each takes, which way
        /// round — is the one that moves the corners least: ≤ 6 candidates for a triangle, ≤ 60
        /// for a pentagon, once per panel.
        /// </summary>
        static void MapPanel(PanelAnalysis a, int face, in OctahedronTarget target, int targetFace,
                             float phase, Vector4[] vertexTarget, Vector4[] vertexTargetNormal)
        {
            var corners = a.FaceCorners[face];
            int n = corners.Length;

            var dst = new Vector3[3];
            for (int i = 0; i < 3; i++) dst[i] = target.FaceCorners[targetFace * 3 + i];
            Vector3 dstCentre = (dst[0] + dst[1] + dst[2]) / 3f;
            Vector3 srcCentre = a.FaceCentroid[face];

            var edge = new float[n];
            for (int i = 0; i < n; i++)
                edge[i] = (a.Vertices[corners[(i + 1) % n]] - a.Vertices[corners[i]]).magnitude;

            var mapped = new Vector3[n];
            var best = new Vector3[n];
            var arc = new float[n + 1];
            float bestScore = float.NegativeInfinity;
            bool haveBest = false;

            for (int dir = 1; dir >= -1; dir -= 2)
                for (int start = 0; start < n; start++)
                    for (int o1 = 1; o1 <= n - 2; o1++)
                        for (int o2 = o1 + 1; o2 <= n - 1; o2++)
                        {
                            ApplyAnchors(a, corners, edge, dst, n, dir, start, o1, o2, arc, mapped);

                            float score = 0f;
                            for (int k = 0; k < n; k++)
                                score += Vector3.Dot(SafeDir(a.Vertices[corners[k]] - srcCentre),
                                                     SafeDir(mapped[k] - dstCentre));
                            if (haveBest && score <= bestScore) continue;

                            bestScore = score;
                            haveBest = true;
                            for (int k = 0; k < n; k++) best[k] = mapped[k];
                        }

            // The face's OUTWARD normal, oriented by the octahedron's own centre rather than by
            // the corners' order, which carries no winding.
            Vector3 fn = Vector3.Cross(dst[1] - dst[0], dst[2] - dst[0]);
            fn = fn.sqrMagnitude > 1e-20f ? fn.normalized : SafeDir(dstCentre - target.Centre);
            if (Vector3.Dot(fn, dstCentre - target.Centre) < 0f) fn = -fn;

            for (int k = 0; k < n; k++)
            {
                vertexTarget[corners[k]] = new Vector4(best[k].x, best[k].y, best[k].z, phase);
                vertexTargetNormal[corners[k]] = new Vector4(fn.x, fn.y, fn.z, phase);
            }
        }

        /// <summary>
        /// One candidate alignment: walking the source polygon from <paramref name="start"/> in
        /// direction <paramref name="dir"/>, the corners at walk offsets 0, o1 and o2 become the
        /// target's corners 0, 1 and 2; every other corner lands on the target edge between the
        /// two anchors it sits between, at its own share of the arc length.
        /// </summary>
        static void ApplyAnchors(PanelAnalysis a, int[] corners, float[] edge, Vector3[] dst, int n,
                                 int dir, int start, int o1, int o2, float[] arc, Vector3[] mapped)
        {
            int Walk(int offset) => ((start + dir * offset) % n + n) % n;

            arc[0] = 0f;
            for (int step = 0; step < n; step++)
            {
                int from = Walk(step);
                // Walking backwards traverses the edge that ENDS at `from`, not the one that starts there.
                arc[step + 1] = arc[step] + edge[dir > 0 ? from : ((from - 1) % n + n) % n];
            }

            int a0 = 0, a1 = o1, a2 = o2, a3 = n;
            for (int seg = 0; seg < 3; seg++)
            {
                int fromStep = seg == 0 ? a0 : seg == 1 ? a1 : a2;
                int toStep = seg == 0 ? a1 : seg == 1 ? a2 : a3;
                float span = Mathf.Max(1e-6f, arc[toStep] - arc[fromStep]);
                for (int step = fromStep; step < toStep; step++)
                {
                    float u = (arc[step] - arc[fromStep]) / span;
                    mapped[Walk(step)] = Vector3.Lerp(dst[seg], dst[(seg + 1) % 3], u);
                }
            }
        }

        // ══ Emit (both mappings) ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Emits the morph mesh UNSHARED — one vertex per source triangle corner — carrying the
        /// source's own normals, tangents and UV0 verbatim (so frame 0 IS the crystal) plus the two
        /// target channels.
        ///
        /// Unsharing is what makes per-face targets possible at all: a vertex reachable from two
        /// faces would have two destinations and one slot. It is visually identical, because a
        /// hard-edged import already stores those corners apart.
        /// </summary>
        /// <param name="extent">Points the animation reaches that the per-vertex targets may not
        /// (a hull's corners, an octahedron's centre), folded into the culling bounds.</param>
        static Mesh Emit(Mesh source, Vector3[] srcVerts, int[] srcTris,
                         Vector4[] vertexTarget, Vector4[] vertexTargetNormal, Vector3[] extent)
        {
            var srcNormals = source.normals;
            var srcTangents = source.tangents;
            var srcUv0 = source.uv;
            bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
            bool hasTangents = srcTangents != null && srcTangents.Length == srcVerts.Length;
            bool hasUv0 = srcUv0 != null && srcUv0.Length == srcVerts.Length;

            int n = srcTris.Length;
            var verts = new Vector3[n];
            var normals = new Vector3[n];
            var tangents = new Vector4[n];
            var uv0 = new Vector2[n];
            var uv2 = new Vector4[n];
            var uv3 = new Vector4[n];
            var tris = new int[n];

            for (int k = 0; k < n; k++)
            {
                int si = srcTris[k];
                verts[k] = srcVerts[si];
                normals[k] = hasNormals ? srcNormals[si] : Vector3.up;
                tangents[k] = hasTangents ? srcTangents[si] : new Vector4(1f, 0f, 0f, 1f);
                uv0[k] = hasUv0 ? srcUv0[si] : Vector2.zero;
                uv2[k] = vertexTarget[si];
                uv3[k] = vertexTargetNormal[si];
                tris[k] = k;
            }

            var mesh = new Mesh
            {
                name = $"CrystalMorph_{source.name}",
                // Runtime-only: a generated mesh must never serialize into a scene. DontSave also
                // exempts it from Resources.UnloadUnusedAssets, so the runner's explicit Destroy is
                // what keeps one per pickup from accumulating.
                hideFlags = HideFlags.DontSave,
                indexFormat = n > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(TargetUVChannel, uv2);
            mesh.SetUVs(TargetNormalUVChannel, uv3);
            mesh.SetTriangles(tris, 0, false);

            // The morph DISPLACES vertices in the vertex stage, so the culling envelope has to
            // cover both ends of the animation or the mesh is frustum-culled mid-flight. Every
            // vertex travels a straight line from a point in the source to its target, so the
            // source's bounds plus every target (and the extent points) is exact.
            var bounds = source.bounds;
            for (int k = 0; k < n; k++) bounds.Encapsulate(new Vector3(uv2[k].x, uv2[k].y, uv2[k].z));
            if (extent != null)
                for (int k = 0; k < extent.Length; k++) bounds.Encapsulate(extent[k]);
            mesh.bounds = bounds;

            return mesh;
        }

        /// <summary>
        /// Slides one point along its own ray from the hull's centre until it meets the hull.
        ///
        /// The facet is picked by best angular fit (max dot against the facet centroids' directions)
        /// and then VERIFIED by barycentric containment, falling back to an exhaustive ray test when
        /// the cheap pick misses — which it can near a vertex of an irregular hull. The verify is
        /// what makes the fast path safe rather than merely usual.
        /// </summary>
        static void LandOnHull(Vector3 point, in ConvexHullTarget target, Vector3[] faceCentroidDir,
                               out Vector3 position, out Vector3 normal)
        {
            Vector3 d = point - target.Centre;
            float len = d.magnitude;
            if (len < 1e-6f)
            {
                // A point AT the centre has no direction to slide along, so it stays put and keeps
                // its own shading. It is inside the hull either way, which is all the morph needs.
                position = target.Centre;
                normal = Vector3.up;
                return;
            }
            d /= len;

            int best = -1;
            float bestDot = float.MinValue;
            for (int f = 0; f < faceCentroidDir.Length; f++)
            {
                float dot = Vector3.Dot(d, faceCentroidDir[f]);
                if (dot > bestDot) { bestDot = dot; best = f; }
            }

            if (best >= 0 && TryRayFace(target.Centre, d, in target, best, out position))
            {
                normal = target.Normals[best];
                return;
            }

            for (int f = 0; f < target.FaceCount; f++)
            {
                if (f == best) continue;
                if (!TryRayFace(target.Centre, d, in target, f, out position)) continue;
                normal = target.Normals[f];
                return;
            }

            // Unreachable for a closed hull containing the centre. Degrading to the best facet's
            // plane keeps the vertex on the surface rather than leaving it hanging in space.
            int fallback = Mathf.Max(0, best);
            normal = target.Normals[fallback];
            Vector3 a = target.Corners[3 * fallback];
            float denom = Vector3.Dot(d, normal);
            float t = Mathf.Abs(denom) > 1e-6f ? Vector3.Dot(a - target.Centre, normal) / denom : len;
            position = target.Centre + d * Mathf.Max(0f, t);
        }

        /// <summary>Möller–Trumbore, front and back faces alike — the ray starts inside the hull, so
        /// the only hit that exists is the one leaving through this facet.</summary>
        static bool TryRayFace(Vector3 origin, Vector3 dir, in ConvexHullTarget target, int face,
                               out Vector3 hit)
        {
            hit = default;
            Vector3 a = target.Corners[3 * face];
            Vector3 b = target.Corners[3 * face + 1];
            Vector3 c = target.Corners[3 * face + 2];

            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;

            float inv = 1f / det;
            Vector3 tv = origin - a;
            float u = Vector3.Dot(tv, p) * inv;
            if (u < -1e-4f || u > 1f + 1e-4f) return false;

            Vector3 q = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < -1e-4f || u + v > 1f + 1e-4f) return false;

            float t = Vector3.Dot(e2, q) * inv;
            if (t <= 0f) return false;

            hit = origin + dir * t;
            return true;
        }

        /// <summary>Maps every vertex index onto the lowest index sharing its position, so a solid's
        /// connectivity survives the importer having split corners apart for shading.</summary>
        static int[] WeldMap(Vector3[] verts, float epsilon)
        {
            var map = new int[verts.Length];
            var buckets = new Dictionary<Vector3Int, List<int>>(verts.Length);
            float inv = 1f / Mathf.Max(1e-6f, epsilon);

            for (int i = 0; i < verts.Length; i++)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(verts[i].x * inv),
                    Mathf.RoundToInt(verts[i].y * inv),
                    Mathf.RoundToInt(verts[i].z * inv));

                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<int>(4);
                    buckets[key] = list;
                }

                int hit = -1;
                for (int j = 0; j < list.Count; j++)
                {
                    if ((verts[list[j]] - verts[i]).sqrMagnitude <= epsilon * epsilon) { hit = list[j]; break; }
                }

                if (hit < 0) { list.Add(i); hit = i; }
                map[i] = hit;
            }
            return map;
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }
    }
}
