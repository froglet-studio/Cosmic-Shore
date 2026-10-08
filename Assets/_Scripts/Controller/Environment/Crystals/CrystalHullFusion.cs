using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A collected elemental crystal's FACES coming off it and mating with the collecting vessel's
    /// hull — the per-(vessel, element) replacement for the generic capture flourish, and the hull
    /// counterpart of the Squirrel's omni morph (<c>SQUIRREL_CRYSTAL_MORPH.md</c>). Full record:
    /// <c>Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md</c>.
    ///
    /// ── What flies, and where it lands ────────────────────────────────────────────────────────
    /// Every solid of the crystal gives up its outermost face (60 pentagons on the charge crystal);
    /// the rest folds into it during the peel. Each face is drawn as a subdivided fan and lands on
    /// a patch of the hull with every point ON the hull's own triangles, wearing its normal, pinned
    /// to the bone that carries it.
    ///
    /// ── The geometry is SOLVED AT EDIT TIME ───────────────────────────────────────────────────
    /// Where the faces land is a property of two assets - the hull mesh in its bind pose and the
    /// crystal mesh - so <b>FrogletTools > Vessels > Bake Crystal Hull Fusions</b> solves it once
    /// (<see cref="CrystalHullFusionGeometry.Solve"/>) and writes a
    /// <see cref="CrystalHullFusionBakeSO"/> holding the answer and the mesh to draw. With a current
    /// bake a pickup does no geometry at all: it matches faces to patches (a 60×60 assignment),
    /// clones the baked mesh, and from then on every frame is one matrix read per bone plus managed
    /// maths.
    ///
    /// A missing or STALE bake (the hull, the crystal, the entry's tuning or the solver changed)
    /// warns once, names the tool, and falls back to running the SAME solve on a worker thread from
    /// arrays captured on the main thread - no <c>UnityEngine.Object</c> off the main thread, and the
    /// main thread polls a volatile flag rather than awaiting it (<c>Docs/THREADING.md</c>). A pickup
    /// that beats the worker plays the generic capture.
    ///
    /// History, kept because each cut failed in a way the next one is shaped by (doc §0): the first
    /// never ran (the drawn charge mesh is unreadable - <see cref="CrystalEdgeArcMeshBaker.TryGetReadable"/>);
    /// the second projected every point on the main thread DURING the pickup and was over before the
    /// frame caught up; the third moved that to a worker; this one moves it out of the game.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrystalHullFusion : MonoBehaviour
    {
        // The four elementals are drawn by three shader families, and the fusion speaks to each in
        // its own properties - a write a material has no property for is skipped:
        //   CHARGE  (ChargeCrystal)            tint _Dull/_BrightCrystalColor, dissolve _opacity,
        //                                      discharge _ArcIntensity/_ArcDuty.
        //   MASS    (OmniShepardFresnelShader) colour _BrightColor/_DarkColor, dissolve _Opacity, and a
        //                                      VERTEX scale band (_Start/_Stop/_ScaleDistance) that
        //                                      scales the mesh about its object origin - the fusion's
        //                                      origin is the hull, so the band is frozen (Adopt).
        //   SPACE / TIME (SpreadFresnelShader) colour _BrightColor/_DarkColor, OPAQUE - no dissolve
        //                                      property, so the faces shrink into the skin instead.
        static readonly int OpacityId = Shader.PropertyToID("_opacity");
        static readonly int ShepardOpacityId = Shader.PropertyToID("_Opacity");
        static readonly int DullId = Shader.PropertyToID("_DullCrystalColor");
        static readonly int BrightId = Shader.PropertyToID("_BrightCrystalColor");
        static readonly int FresnelBrightId = Shader.PropertyToID("_BrightColor");
        static readonly int FresnelDarkId = Shader.PropertyToID("_DarkColor");
        static readonly int ArcIntensityId = Shader.PropertyToID("_ArcIntensity");
        static readonly int ArcDutyId = Shader.PropertyToID("_ArcDuty");
        static readonly int BandStartId = Shader.PropertyToID("_Start");
        static readonly int BandStopId = Shader.PropertyToID("_Stop");
        static readonly int BandScaleId = Shader.PropertyToID("_ScaleDistance");

        /// <summary>Where a Shepard band is frozen for a fusion: alpha = (1.05 - s), so 0.05 draws the
        /// faces at full opacity, and the dissolve then rides <c>_Opacity</c>.</summary>
        const float FrozenShepardBand = 0.05f;

        /// <summary>Levels each fan triangle of a face is cut into. 3 gives a pentagon 31 points and
        /// 45 triangles; every point still lands on the Squirrel's skin (measured). Part of a bake's
        /// fingerprint - change it and every bake reads stale.</summary>
        public const int FaceSubdivisions = 3;

        /// <summary>The bake tool's menu path, named in every stale/missing-bake warning.</summary>
        public const string BakeToolMenu = "FrogletTools > Vessels > Bake Crystal Hull Fusions";

        static readonly ProfilerMarker s_beginMarker = new("CrystalHullFusion.Begin");
        static readonly ProfilerMarker s_prewarmMarker = new("CrystalHullFusion.Prewarm");
        static readonly ProfilerMarker s_frameMarker = new("CrystalHullFusion.Frame");

        /// <summary>A solution ready to draw: the solve plus the mesh every pickup clones.</summary>
        sealed class Ready
        {
            public CrystalHullFusionGeometry.FusionSolution Solution;
            public Mesh Prototype;
        }

        /// <summary>A runtime fallback solve in flight on a worker.</summary>
        sealed class Job
        {
            public volatile bool Done;
            public volatile string Failure;
            public CrystalHullFusionGeometry.FusionSolution Solution;
            public Ready Result;   // main thread, once Done
        }

        static readonly Dictionary<CrystalHullFusionBakeSO, Ready> s_baked = new();
        static readonly Dictionary<(Mesh hull, int hullVertices, Mesh crystal, int plates), Job> s_jobs = new();
        static readonly HashSet<string> s_warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            s_baked.Clear();
            s_jobs.Clear();
            s_warned.Clear();
            s_anchorSkins.Clear();
        }

        struct Face
        {
            public Vector3 Lift;            // world offset the peel lifts the face by
            public Vector3 StartCentroid;   // world, at collection
            public Vector3 StartNormal;     // world, at collection
            public float Delay01;
            public int Patch;

            // Written each frame.
            public Vector3 Centroid;
        }

        CrystalHullFusionConfigSO.Entry _entry;
        HullRig _hull;
        CrystalHullFusionGeometry.FusionSolution _solution;
        CrystalHullFusionGeometry.HullLayout _layout;
        Transform[] _bones;                 // the rig's pins, then its space
        Matrix4x4[] _boneToWorld;
        float _hullScale;

        MeshRenderer _renderer;
        Mesh _mesh;
        MaterialPropertyBlock _block;
        Vector3[] _startPositions;          // per vertex, world, at collection
        Vector3[] _startNormals;
        Vector3[] _vertices;                // written each frame, relative to this object
        Vector3[] _normals;

        Face[] _faces;
        Vector3[] _pointStart;              // per face × point, world, at collection (before the lift)
        Vector3[] _pointPosition;
        Vector3[] _pointNormal;
        int _contactFace;

        float _bowDistance;
        Color _startDull, _startBright, _targetDull, _targetBright;
        bool _hasTint, _haveTargetColour;
        int _tintDullId, _tintBrightId;     // the crystal pair, or the fresnel pair
        bool _tintHoldsDull;                // fresnel: the dark body stays dark, only the rim converges
        int _opacityId;                     // -1: an opaque shader, so the faces shrink away instead
        float _baseArcIntensity, _baseArcDuty;
        bool _hasArcIntensity, _hasArcDuty;
        float _startTime;

        // The approach: a crystal collected far from the hull flies in whole before it peels.
        float _approachSeconds;             // 0 = it peels where it was taken
        Vector3 _approachDirLocal;          // hull centre -> crystal, in the hull's rotation
        float _standoffDistance;            // world
        Vector3 _crystalCentreStart;        // world, at collection
        Vector3 _carry;                     // world offset applied to every start this frame

        Renderer[] _companions;             // crystal shells fading in place, then hidden
        MaterialPropertyBlock[] _companionBlocks;
        int[] _companionOpacityId;
        float[] _companionStartOpacity;

        /// <summary>Seconds from start until every face is down — when the pickup sound belongs.</summary>
        public float MateDelaySeconds => _approachSeconds + _entry.MateSecondsFromStart;

        /// <summary>The contact patch, live, in world space.</summary>
        public Vector3 LandingWorldPosition
        {
            get
            {
                if (_layout == null || _faces == null || !_hull?.Space) return transform.position;
                int patch = _faces[_contactFace].Patch;
                var bone = _bones[_layout.PatchBone[patch]];
                return bone ? bone.TransformPoint(_layout.PatchPositionLocal[patch]) : transform.position;
            }
        }

        // ══ Shared with the bake tool ═════════════════════════════════════════════════════════

        /// <summary>
        /// The hull a fusion lands on, as a RIG: a space the layout is solved in and the transforms
        /// every landed point is pinned to. Two shapes of hull fly in the fleet, and both reduce to
        /// skinning:
        ///
        ///   SKINNED (Squirrel, Manta, Dolphin, Serpent, Sparrow, Scarab...) - the renderer's bones
        ///     and bind poses, as authored; the renderer itself is the last pin.
        ///   STATIC  (Rhino, Urchin, Grizzly) - a body MeshRenderer and every mesh part under it
        ///     (wings, jets, guns, shrouds), each a rigid "bone" whose bind pose is where it sat
        ///     against the body when the layout was solved. A puppeted wing carries its patches with it.
        ///
        /// <see cref="FindHull"/> resolves it identically from the vessel PREFAB (the bake) and from a
        /// live vessel (a pickup), so a bake's bone indices name the same transforms in both.
        /// </summary>
        public sealed class HullRig
        {
            /// <summary>The space the layout lives in: the skinned renderer, or the static body.</summary>
            public Transform Space;
            /// <summary>Every pin, indexed as the solution's bone indices; the last is <see cref="Space"/>.</summary>
            public Transform[] Bones;
            /// <summary>The mesh that names this hull in a bake's fingerprint: the skinned mesh, or the body's.</summary>
            public Mesh KeyMesh;
            /// <summary>Vertices across every part - the cheap runtime check that the parts are the bake's.</summary>
            public int VertexCount;
            public int PartCount;
            /// <summary>Non-null for a skinned hull.</summary>
            public SkinnedMeshRenderer Skinned;
            /// <summary>A static hull's parts, body first. Null for a skinned hull.</summary>
            public MeshFilter[] Parts;

            public string Name => Space ? Space.name : "(none)";
        }

        /// <summary>
        /// The vessel's hull under <paramref name="root"/>: its skinned hull
        /// (<see cref="FindHullRenderer"/>) when it has one, else its largest mesh renderer as the
        /// BODY plus every mesh part under that body. Null when there is neither - the Butterfly's
        /// hull mesh is generated at runtime and has no asset to solve against.
        /// </summary>
        public static HullRig FindHull(Transform root, bool requireActive = true, Mesh bakedKey = null)
        {
            if (!root) return null;

            // A bake names the mesh it was solved on: find THAT one first, whatever else the vessel
            // is drawing at the moment (a larger effect mesh, a renderer an ability switched off), so
            // the runtime and the bake cannot disagree about which hull this is.
            if (bakedKey)
            {
                foreach (var candidate in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (candidate && candidate.sharedMesh == bakedKey) return SkinnedRig(candidate);
                foreach (var candidate in root.GetComponentsInChildren<MeshFilter>(true))
                    if (IsHullPart(candidate) && candidate.sharedMesh == bakedKey) return StaticRig(candidate);
            }

            var skinned = FindHullRenderer(root, requireActive);
            if (skinned) return SkinnedRig(skinned);

            MeshFilter body = null;
            int bodyVertices = -1;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(!requireActive))
            {
                if (!IsHullPart(filter)) continue;
                if (requireActive && (!filter.gameObject.activeInHierarchy || !filter.GetComponent<MeshRenderer>().enabled)) continue;
                if (filter.sharedMesh.vertexCount <= bodyVertices) continue;
                body = filter;
                bodyVertices = filter.sharedMesh.vertexCount;
            }
            return body ? StaticRig(body) : null;
        }

        static HullRig SkinnedRig(SkinnedMeshRenderer skinned)
        {
            var bones = skinned.bones ?? Array.Empty<Transform>();
            var pins = new Transform[bones.Length + 1];
            Array.Copy(bones, pins, bones.Length);
            pins[bones.Length] = skinned.transform;
            return new HullRig
            {
                Space = skinned.transform,
                Bones = pins,
                KeyMesh = skinned.sharedMesh,
                VertexCount = skinned.sharedMesh.vertexCount,
                PartCount = 1,
                Skinned = skinned,
            };
        }

        static HullRig StaticRig(MeshFilter body)
        {
            // Parts are taken regardless of their active or enabled state, in hierarchy order, so the
            // prefab the bake read and the live vessel always list the SAME transforms at the same
            // indices - a part an ability hides mid-flight does not renumber the rest.
            var parts = new List<MeshFilter>();
            foreach (var filter in body.GetComponentsInChildren<MeshFilter>(true))
                if (IsHullPart(filter)) parts.Add(filter);

            var rig = new HullRig
            {
                Space = body.transform,
                Bones = new Transform[parts.Count + 1],
                KeyMesh = body.sharedMesh,
                PartCount = parts.Count,
                Parts = parts.ToArray(),
            };
            for (int p = 0; p < parts.Count; p++)
            {
                rig.Bones[p] = parts[p].transform;
                rig.VertexCount += parts[p].sharedMesh.vertexCount;
            }
            rig.Bones[parts.Count] = body.transform;
            return rig;
        }

        static bool IsHullPart(MeshFilter filter) =>
            filter && filter.sharedMesh && filter.TryGetComponent<MeshRenderer>(out _);

        /// <summary>
        /// The hull is the renderer the vessel's ELEMENT display lives on — the skinned mesh carrying
        /// the element blend shapes, which by the fleet's own contract is the visible hull
        /// (<see cref="VesselAnimation.CollectElementShapes"/>). Failing that, the largest skinned
        /// mesh under the root. <paramref name="requireActive"/> is false for a PREFAB asset, which
        /// is in no scene and so is never active in a hierarchy.
        /// </summary>
        public static SkinnedMeshRenderer FindHullRenderer(Transform root, bool requireActive = true)
        {
            var shapes = new List<VesselAnimation.ElementShapeTarget>();
            VesselAnimation.CollectElementShapes(root, shapes);

            SkinnedMeshRenderer best = null;
            int bestVertices = -1;
            foreach (var shape in shapes) Consider(shape.Renderer);
            if (best) return best;

            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(!requireActive)) Consider(renderer);
            return best;

            void Consider(SkinnedMeshRenderer renderer)
            {
                if (!renderer || !renderer.enabled) return;
                if (requireActive && !renderer.gameObject.activeInHierarchy) return;
                var mesh = renderer.sharedMesh;
                if (!mesh || mesh.vertexCount <= bestVertices) return;
                best = renderer;
                bestVertices = mesh.vertexCount;
            }
        }

        /// <summary>
        /// The mesh a crystal (prefab or instance) DRAWS, the source asset it was baked from, and
        /// the renderer drawing it (<paramref name="model"/> is that renderer's GameObject - the
        /// space the mesh lives in):
        ///
        ///   CHARGE - a MeshRenderer whose filter holds <see cref="CrystalEdgeArcs"/>' twin on an
        ///            instance; on a prefab the baker hands back the same cached twin.
        ///   MASS   - four nested MeshRenderer shells of ONE mesh. Three ride a Shepard band that
        ///            SHRINKS them (<c>_ScaleDistance</c> on); the outer one holds its size. The outer
        ///            one is the body that flies (<see cref="ShellRank"/>); the other three fade in
        ///            place (<see cref="IsCompanionShell"/>).
        ///   SPACE  - a SkinnedMeshRenderer spinning its blocks on blend shapes.
        ///   TIME   - a SkinnedMeshRenderer on a CHILD of the model, flipping its blocks on bones.
        /// </summary>
        public static bool TryResolveCrystal(Crystal crystal, out Mesh drawn, out Mesh source, out int plateCorners,
                                             out GameObject model, out Renderer renderer)
        {
            drawn = source = null;
            plateCorners = 0;
            model = null;
            renderer = null;
            var models = crystal ? crystal.CrystalModels : null;
            if (models == null) return false;

            GameObject best = null;
            int bestRank = int.MinValue;
            foreach (var data in models)
            {
                var candidate = data?.model;
                if (candidate == null) continue;
                Renderer candidateRenderer = null;
                if (candidate.TryGetComponent<MeshFilter>(out var candidateFilter) && candidateFilter.sharedMesh &&
                    candidate.TryGetComponent<MeshRenderer>(out var meshRenderer))
                    candidateRenderer = meshRenderer;
                else
                {
                    var skinned = candidate.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (skinned && skinned.sharedMesh) candidateRenderer = skinned;
                }
                if (!candidateRenderer) continue;

                int rank = ShellRank(candidateRenderer);
                if (rank <= bestRank) continue;   // ties keep the first
                best = candidate;
                bestRank = rank;
                renderer = candidateRenderer;
            }
            if (!best) return false;

            if (renderer is SkinnedMeshRenderer skin)
                drawn = source = skin.sharedMesh;
            else
            {
                drawn = best.GetComponent<MeshFilter>().sharedMesh;
                if (drawn.name.EndsWith(CrystalEdgeArcMeshBaker.BakedSuffix))
                    CrystalEdgeArcMeshBaker.TryGetSource(drawn, out source, out plateCorners);
                else
                {
                    source = drawn;
                    if (best.TryGetComponent<CrystalEdgeArcs>(out var arcs))
                    {
                        plateCorners = arcs.PlateCorners;
                        var baked = CrystalEdgeArcMeshBaker.GetOrBake(source, plateCorners);
                        if (baked != null) drawn = baked;
                    }
                }
            }
            model = renderer.gameObject;
            return source != null;
        }

        /// <summary>
        /// Which crystal model flies: one whose shader holds its size (a Shepard shell with
        /// <c>_ScaleDistance</c> off - the Mass crystal's big outer shell) beats an ordinary model,
        /// which beats a shell whose band shrinks it. Every Mass shell is the same mesh, so the pick
        /// never changes what a bake was solved against.
        /// </summary>
        static int ShellRank(Renderer renderer)
        {
            var material = renderer ? renderer.sharedMaterial : null;
            if (!material || !material.HasProperty(BandScaleId)) return 1;
            return material.GetFloat(BandScaleId) > 0.5f ? 0 : 2;
        }

        /// <summary>A crystal renderer that is NOT the one flying but can fade where it is - the
        /// Mass crystal's three shrinking shells.</summary>
        static bool IsCompanionShell(Renderer renderer, Renderer flying)
        {
            if (!renderer || renderer == flying || !renderer.enabled) return false;
            var material = renderer.sharedMaterial;
            return material && (material.HasProperty(ShepardOpacityId) || material.HasProperty(OpacityId));
        }

        /// <summary>
        /// Captures everything <see cref="CrystalHullFusionGeometry.Solve"/> needs as plain arrays,
        /// on the main thread: the hull in its BIND POSE (mesh asset + bind poses + heaviest bone per
        /// vertex) and the crystal's drawn mesh with its discharge channels. Null, named, when either
        /// mesh cannot be read.
        /// </summary>
        public static CrystalHullFusionGeometry.SolveInput CaptureSolveInput(HullRig hull, Mesh drawnCrystal,
            CrystalHullFusionConfigSO.Entry entry, out string failure)
        {
            if (!TryCaptureHull(hull, out var hullVertices, out var hullNormals, out var hullTriangles,
                                out var dominantBones, out var bindPoses, out failure))
                return null;
            if (!CrystalEdgeArcMeshBaker.TryGetReadable(drawnCrystal, out var crystal))
            {
                failure = $"'{(drawnCrystal ? drawnCrystal.name : "(none)")}' is not CPU-readable and was not made by " +
                          "CrystalEdgeArcMeshBaker - enable Read/Write on its model importer";
                return null;
            }

            var bary = new List<Vector3>();
            var edgeH = new List<Vector3>();
            var edgeSeed = new List<Vector3>();
            crystal.GetUVs(1, bary);
            crystal.GetUVs(2, edgeH);
            crystal.GetUVs(3, edgeSeed);
            var crystalVertices = crystal.vertices;
            var crystalTriangles = new List<int[]>(crystal.subMeshCount);
            for (int s = 0; s < crystal.subMeshCount; s++) crystalTriangles.Add(crystal.GetTriangles(s));
            var extents = crystal.bounds.extents;

            return new CrystalHullFusionGeometry.SolveInput
            {
                CrystalVertices = crystalVertices,
                CrystalNormals = crystal.normals,
                CrystalBary = bary.Count == crystalVertices.Length ? bary.ToArray() : null,
                CrystalEdgeH = edgeH.Count == crystalVertices.Length ? edgeH.ToArray() : null,
                CrystalEdgeSeed = edgeSeed.Count == crystalVertices.Length ? edgeSeed.ToArray() : null,
                CrystalTriangles = crystalTriangles,
                CrystalModelRadius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)),

                HullVertices = hullVertices,
                HullNormals = hullNormals,
                HullTriangles = hullTriangles,
                HullDominantBones = dominantBones,
                HullBindPoses = bindPoses,

                TileFill = entry.tileFill,
                SurfaceLift = entry.surfaceLift,
                Subdivisions = FaceSubdivisions,
            };
        }

        /// <summary>
        /// The hull in its bind pose as one triangle soup in <see cref="HullRig.Space"/>, with each
        /// vertex's pin and every pin's bind pose (rig space → pin space). A skinned hull is its mesh
        /// asset as authored. A static hull's parts are carried into the body's space through where
        /// they sit against it NOW (the prefab's rest pose, for a bake), and each part's bind pose is
        /// the inverse of that placement - so a landed point rides its part, wherever it moves.
        /// </summary>
        public static bool TryCaptureHull(HullRig hull, out Vector3[] vertices, out Vector3[] normals, out int[] triangles,
                                          out int[] dominantBones, out Matrix4x4[] bindPoses, out string failure)
        {
            vertices = normals = null;
            triangles = dominantBones = null;
            bindPoses = null;
            failure = null;
            if (hull == null || !hull.KeyMesh) { failure = "there is no hull mesh"; return false; }

            if (hull.Skinned)
            {
                var mesh = hull.KeyMesh;
                if (!mesh.isReadable) { failure = Unreadable(mesh); return false; }
                vertices = mesh.vertices;
                normals = mesh.normals;
                triangles = mesh.triangles;
                dominantBones = DominantBones(mesh);
                bindPoses = mesh.bindposes;
                return true;
            }

            var allVertices = new List<Vector3>(hull.VertexCount);
            var allNormals = new List<Vector3>(hull.VertexCount);
            var allTriangles = new List<int>();
            var allBones = new List<int>(hull.VertexCount);
            bindPoses = new Matrix4x4[hull.Parts.Length];
            Matrix4x4 spaceFromWorld = hull.Space.worldToLocalMatrix;
            for (int p = 0; p < hull.Parts.Length; p++)
            {
                var mesh = hull.Parts[p].sharedMesh;
                if (!mesh.isReadable) { failure = Unreadable(mesh); return false; }

                Matrix4x4 place = spaceFromWorld * hull.Parts[p].transform.localToWorldMatrix;
                Matrix4x4 placeNormal = place.inverse.transpose;
                bindPoses[p] = place.inverse;

                int offset = allVertices.Count;
                foreach (var v in mesh.vertices) { allVertices.Add(place.MultiplyPoint3x4(v)); allBones.Add(p); }
                var partNormals = mesh.normals;
                for (int v = 0; v < mesh.vertexCount; v++)
                    allNormals.Add(v < partNormals.Length ? placeNormal.MultiplyVector(partNormals[v]).normalized : Vector3.up);
                foreach (int t in mesh.triangles) allTriangles.Add(offset + t);
            }

            vertices = allVertices.ToArray();
            normals = allNormals.ToArray();
            triangles = allTriangles.ToArray();
            dominantBones = allBones.ToArray();
            return true;

            static string Unreadable(Mesh mesh) =>
                $"'{mesh.name}' is not CPU-readable - enable Read/Write on its model importer";
        }

        /// <summary>
        /// The mesh a solution is drawn with: its vertices, normals and the charge discharge channels
        /// (UV1-3), plus (face, point) packed into UV0 - which the charge shader never reads - so a
        /// baked mesh carries its own per-vertex bookkeeping and the bake asset need not repeat it.
        /// </summary>
        public static Mesh BuildTemplateMesh(CrystalHullFusionGeometry.FusionSolution solution, string name)
        {
            int count = solution.Vertices.Length;
            var mesh = new Mesh
            {
                name = name,
                indexFormat = count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            var ids = new Vector2[count];
            for (int v = 0; v < count; v++) ids[v] = new Vector2(solution.VertexPanel[v], solution.VertexPoint[v]);

            mesh.SetVertices(solution.Vertices);
            mesh.SetNormals(solution.Normals);
            mesh.SetUVs(0, ids);
            if (solution.Bary != null) mesh.SetUVs(1, solution.Bary);
            if (solution.EdgeH != null) mesh.SetUVs(2, solution.EdgeH);
            if (solution.EdgeSeed != null) mesh.SetUVs(3, solution.EdgeSeed);
            mesh.subMeshCount = solution.SubmeshTriangles.Length;
            for (int s = 0; s < solution.SubmeshTriangles.Length; s++)
                mesh.SetTriangles(solution.SubmeshTriangles[s], s, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Per vertex, the bone with the greatest weight. Null for an unreadable mesh or one
        /// with no weights; every point then rides the renderer instead.</summary>
        public static int[] DominantBones(Mesh mesh)
        {
            if (!mesh || !mesh.isReadable) return null;
            var weights = mesh.boneWeights;
            if (weights is not { Length: > 0 }) return null;
            var dominant = new int[weights.Length];
            for (int v = 0; v < weights.Length; v++)
            {
                var w = weights[v];
                int bone = w.boneIndex0; float best = w.weight0;
                if (w.weight1 > best) { bone = w.boneIndex1; best = w.weight1; }
                if (w.weight2 > best) { bone = w.boneIndex2; best = w.weight2; }
                if (w.weight3 > best) { bone = w.boneIndex3; }
                dominant[v] = bone;
            }
            return dominant;
        }

        // ══ Finding a solution: the bake, or the fallback ════════════════════════════════════

        /// <summary>
        /// Readies every (this vessel, element) pair the config fuses, so the first pickup pays
        /// nothing: a current bake is loaded, anything else starts the worker fallback. Called from
        /// <c>VesselAnimation.Initialize</c>; never throws into vessel initialisation.
        /// </summary>
        public static void Prewarm(IVesselStatus vesselStatus, Transform animationRoot)
        {
            if (vesselStatus == null || !animationRoot) return;
            var config = CrystalHullFusionConfigSO.Load();
            if (!config) return;

            using (s_prewarmMarker.Auto())
            {
                try
                {
                    foreach (var entry in config.Entries)
                    {
                        if (entry == null || entry.vessel != vesselStatus.VesselType) continue;
                        var hull = FindHull(animationRoot, bakedKey: entry.bake ? entry.bake.HullMesh : null);
                        if (hull == null) return;

                        var set = ElementalCrystalSetSO.Load();
                        var prefab = set ? set.GetPrefab(entry.element) : null;
                        if (prefab && TryResolveCrystal(prefab, out var drawn, out var source, out int plates, out _, out _))
                            TryGetReady(hull, drawn, source, plates, entry, out _);
                    }
                }
                catch (Exception e)
                {
                    WarnOnce($"prewarm:{vesselStatus.VesselType}",
                        $"[CrystalHullFusion] prewarming {vesselStatus.VesselType} failed ({e.Message}) - its " +
                        "crystals will play the generic capture until a pickup readies the fusion.");
                }
            }
        }

        /// <summary>
        /// The solution for this pair, if one is ready NOW. A current bake always is. Otherwise the
        /// worker fallback is started (or polled) and this answers false until it lands - with a
        /// reason when it never will.
        /// </summary>
        static bool TryGetReady(HullRig hull, Mesh drawn, Mesh source, int plates,
                                CrystalHullFusionConfigSO.Entry entry, out Ready ready)
        {
            ready = null;
            var bake = entry.bake;
            if (bake)
            {
                if (bake.Matches(hull.KeyMesh, hull.VertexCount, hull.PartCount, source, plates, entry, FaceSubdivisions, out string why))
                {
                    ready = FromBake(bake);
                    if (ready != null) return true;
                    why = "its template mesh is not CPU-readable";
                }
                WarnOnce($"stale:{bake.name}",
                    $"[CrystalHullFusion] '{bake.name}' is STALE ({why}). Solving {entry.vessel}/{entry.element} " +
                    $"at runtime on a worker instead - re-run {BakeToolMenu} and push its output.");
            }
            else
            {
                WarnOnce($"unbaked:{entry.vessel}:{entry.element}",
                    $"[CrystalHullFusion] {entry.vessel}/{entry.element} has no bake. Solving it at runtime on a " +
                    $"worker instead - run {BakeToolMenu} and push its output.");
            }

            var job = GetOrStartJob(hull, drawn, source, plates, entry);
            if (job.Failure != null)
            {
                WarnOnce($"job:{hull.Name}:{source.name}",
                    $"[CrystalHullFusion] cannot solve {entry.vessel}/{entry.element}: {job.Failure} - the generic capture plays.");
                return false;
            }
            if (!job.Done) return false;

            job.Result ??= new Ready
            {
                Solution = job.Solution,
                Prototype = BuildTemplateMesh(job.Solution, $"{source.name} (Fusion)"),
            };
            if (job.Result.Prototype) job.Result.Prototype.hideFlags = HideFlags.DontSave;
            ready = job.Result;
            return true;
        }

        /// <summary>A bake's solution with its per-vertex arrays read back out of the template mesh,
        /// once per bake per session.</summary>
        static Ready FromBake(CrystalHullFusionBakeSO bake)
        {
            if (s_baked.TryGetValue(bake, out var cached)) return cached;

            var mesh = bake.TemplateMesh;
            if (!mesh || !mesh.isReadable) return null;

            var solution = bake.Solution;
            solution.Vertices = mesh.vertices;
            solution.Normals = mesh.normals;
            var ids = new List<Vector2>(mesh.vertexCount);
            mesh.GetUVs(0, ids);
            solution.VertexPanel = new int[ids.Count];
            solution.VertexPoint = new int[ids.Count];
            for (int v = 0; v < ids.Count; v++)
            {
                solution.VertexPanel[v] = Mathf.RoundToInt(ids[v].x);
                solution.VertexPoint[v] = Mathf.RoundToInt(ids[v].y);
            }

            cached = new Ready { Solution = solution, Prototype = mesh };
            s_baked[bake] = cached;
            return cached;
        }

        static Job GetOrStartJob(HullRig hull, Mesh drawn, Mesh source, int plates,
                                 CrystalHullFusionConfigSO.Entry entry)
        {
            var key = (hull.KeyMesh, hull.VertexCount, source, plates);
            if (s_jobs.TryGetValue(key, out var job)) return job;

            job = new Job();
            s_jobs[key] = job;

            var input = CaptureSolveInput(hull, drawn, entry, out string failure);
            if (input == null) { job.Failure = failure; return job; }

            Task.Run(() =>
            {
                try
                {
                    var solution = CrystalHullFusionGeometry.Solve(input, out string why);
                    if (solution == null) { job.Failure = why; return; }
                    job.Solution = solution;
                    job.Done = true;
                }
                catch (Exception e)
                {
                    job.Failure = $"{e.GetType().Name}: {e.Message}";
                }
            });
            return job;
        }

        // ══ A pickup ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Starts a fusion of <paramref name="crystal"/> onto the hull of the vessel described by
        /// <paramref name="vesselStatus"/>, and hides the crystal's own renderers (it is still the
        /// caller's to retire). Returns null — leaving the crystal untouched, so the caller plays the
        /// generic capture — when no solution is ready or the fusion cannot land honestly; every
        /// refusal is a warning, once per reason, because on screen they all look like the old
        /// capture.
        /// </summary>
        public static CrystalHullFusion Begin(Crystal crystal, IVesselStatus vesselStatus,
                                              CrystalHullFusionConfigSO.Entry entry)
        {
            if (crystal == null || vesselStatus == null || entry == null) return null;
            using (s_beginMarker.Auto())
            {
                var animation = vesselStatus.VesselAnimation;
                var hull = animation ? FindHull(animation.transform, bakedKey: entry.bake ? entry.bake.HullMesh : null) : null;
                if (hull == null)
                {
                    WarnOnce($"nohull:{vesselStatus.VesselType}",
                        $"[CrystalHullFusion] {vesselStatus.VesselType} has no visible hull mesh under its " +
                        "VesselAnimation, so there is nothing to fuse onto - the generic capture plays.");
                    return null;
                }

                if (!TryResolveCrystal(crystal, out var drawn, out var source, out int plates, out var model, out var renderer))
                {
                    WarnOnce($"mesh:{crystal.name}",
                        $"[CrystalHullFusion] '{crystal.name}' has no model with a mesh to fuse - the generic capture plays.");
                    return null;
                }

                if (!TryGetReady(hull, drawn, source, plates, entry, out var ready))
                {
                    if (CSDebug.IsVerbose(CSLogChannel.CrystalMorph))
                        CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                            $"[CrystalHullFusion] no solution ready for {entry.vessel}/{entry.element} yet - this pickup plays the generic capture.");
                    return null;
                }

                var go = new GameObject($"CrystalHullFusion_{crystal.name}") { layer = model.layer };
                var fusion = go.AddComponent<CrystalHullFusion>();
                fusion._entry = entry;
                fusion._hull = hull;
                fusion._solution = ready.Solution;
                fusion._layout = ready.Solution.Layout;
                fusion.Adopt(ready.Prototype, renderer);
                fusion.Plan(crystal, vesselStatus, model.transform, renderer);

                // The crystal is now drawn by the fusion. It stays alive (hidden) so its owner can
                // retire it when the faces are down - the pickup sound and the cell bookkeeping are its own.
                // Its other fadeable shells (the Mass crystal's three shrinking ones) stay where they are
                // and fade out while the faces fly; the tint block is the fusion's from here on.
                crystal.BeginCaptureVisual();
                var companions = new List<Renderer>();
                foreach (var r in crystal.GetComponentsInChildren<Renderer>(true))
                {
                    if (IsCompanionShell(r, renderer)) companions.Add(r);
                    else r.enabled = false;
                }
                fusion.AdoptCompanions(companions);

                fusion._startTime = Time.time;
                fusion.LateUpdate(); // frame 0 is drawn THIS frame - the crystal is already hidden

                if (CSDebug.IsVerbose(CSLogChannel.CrystalMorph))
                    CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                        $"[CrystalHullFusion] {vesselStatus.VesselType}/{entry.element}: '{crystal.name}' " +
                        $"peeling {fusion._faces.Length} faces onto '{hull.Name}' over {entry.TotalSeconds:F2}s " +
                        $"({(entry.bake && ready.Prototype == entry.bake.TemplateMesh ? "baked" : "runtime-solved")}, " +
                        $"domain colour {(fusion._haveTargetColour ? "read" : "NOT FOUND")}, " +
                        $"taken {(fusion._crystalCentreStart - fusion.HullCentreWorld(out float hullRadius)).magnitude:F1} from a hull " +
                        $"of radius {hullRadius:F1}, {(fusion._approachSeconds > 0f ? $"flying in {fusion._approachSeconds:F2}s first" : "peeling where taken")}).");
                return fusion;
            }
        }

        /// <summary>Clones the prototype and wears the crystal's own materials and property block,
        /// which is where <c>Crystal.ApplyColorSetTint</c> paints the collectability colour - then
        /// reads which of the three shader families it is wearing (see the property IDs above).</summary>
        void Adopt(Mesh prototype, Renderer source)
        {
            _mesh = Instantiate(prototype);
            _mesh.hideFlags = HideFlags.DontSave;
            _mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterials = source.sharedMaterials;
            _renderer.shadowCastingMode = source.shadowCastingMode;
            _renderer.receiveShadows = source.receiveShadows;

            _block = new MaterialPropertyBlock();
            source.GetPropertyBlock(_block);

            var material = source.sharedMaterial;
            bool Has(int id) => material && material.HasProperty(id);

            // The Mass shells' band scales the mesh about the object origin every frame; this object's
            // origin is the hull, so a live band would fling the faces across the sky. Pin it.
            if (Has(BandScaleId)) _block.SetFloat(BandScaleId, 0f);
            if (Has(BandStartId) && Has(BandStopId))
            {
                _block.SetFloat(BandStartId, FrozenShepardBand);
                _block.SetFloat(BandStopId, FrozenShepardBand);
            }

            bool crystalPair = Has(DullId) && Has(BrightId);
            bool fresnelPair = !crystalPair && Has(FresnelBrightId) && Has(FresnelDarkId);
            _tintDullId = fresnelPair ? FresnelDarkId : DullId;
            _tintBrightId = fresnelPair ? FresnelBrightId : BrightId;
            _tintHoldsDull = fresnelPair;
            _hasTint = crystalPair || fresnelPair || (_block.HasColor(DullId) && _block.HasColor(BrightId));
            _startDull = _block.HasColor(_tintDullId) ? _block.GetColor(_tintDullId) : Has(_tintDullId) ? material.GetColor(_tintDullId) : Color.white;
            _startBright = _block.HasColor(_tintBrightId) ? _block.GetColor(_tintBrightId) : Has(_tintBrightId) ? material.GetColor(_tintBrightId) : Color.white;

            _opacityId = Has(OpacityId) ? OpacityId : Has(ShepardOpacityId) ? ShepardOpacityId : -1;
            _hasArcIntensity = material && material.HasProperty(ArcIntensityId);
            _hasArcDuty = material && material.HasProperty(ArcDutyId);
            if (_hasArcIntensity) _baseArcIntensity = material.GetFloat(ArcIntensityId);
            if (_hasArcDuty) _baseArcDuty = material.GetFloat(ArcDutyId);
        }

        void AdoptCompanions(List<Renderer> companions)
        {
            _companions = companions.ToArray();
            _companionBlocks = new MaterialPropertyBlock[_companions.Length];
            _companionOpacityId = new int[_companions.Length];
            _companionStartOpacity = new float[_companions.Length];
            for (int c = 0; c < _companions.Length; c++)
            {
                var material = _companions[c].sharedMaterial;
                int id = material.HasProperty(ShepardOpacityId) ? ShepardOpacityId : OpacityId;
                var block = new MaterialPropertyBlock();
                _companions[c].GetPropertyBlock(block);
                _companionBlocks[c] = block;
                _companionOpacityId[c] = id;
                _companionStartOpacity[c] = block.HasFloat(id) ? block.GetFloat(id) : material.GetFloat(id);
            }
        }

        /// <summary>The companion shells fade over the approach, the peel and the flight - gone by
        /// the time the faces are down, which is when the crystal itself is retired.</summary>
        void FadeCompanions(float elapsed)
        {
            if (_companions == null || _companions.Length == 0) return;
            float span = _approachSeconds + (_entry.peelSeconds + _entry.flightSeconds) * _entry.Scale;
            float keep = 1f - CrystalHullFusionConfigSO.Smooth(span > 0f ? elapsed / span : 1f);
            for (int c = 0; c < _companions.Length; c++)
            {
                var shell = _companions[c];
                if (!shell || !shell.enabled) continue;
                if (keep <= 0f) { shell.enabled = false; continue; }
                _companionBlocks[c].SetFloat(_companionOpacityId[c], _companionStartOpacity[c] * keep);
                shell.SetPropertyBlock(_companionBlocks[c]);
            }
        }

        /// <summary>
        /// The per-pickup half: each face's start in the world, and which patch it takes. The
        /// crystal is read at its COLLECT pose (a host respawn may already have moved it), and an
        /// ANIMATED crystal is read at the pose it is holding (<see cref="ReadFacePoses"/>), so a
        /// block caught mid-flip leaves from where it is. The hull's layout lives in the rig's space.
        /// </summary>
        void Plan(Crystal crystal, IVesselStatus vesselStatus, Transform model, Renderer crystalRenderer)
        {
            var solution = _solution;
            var layout = _layout;
            Transform hullTransform = _hull.Space;

            _bones = _hull.Bones;
            _boneToWorld = new Matrix4x4[_bones.Length];
            for (int b = 0; b < _bones.Length; b++)
                _boneToWorld[b] = _bones[b] ? _bones[b].localToWorldMatrix : hullTransform.localToWorldMatrix;

            Vector3 lossy = hullTransform.lossyScale;
            _hullScale = (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.y) + Mathf.Abs(lossy.z)) / 3f;
            _bowDistance = layout.HullMeanRadius * _hullScale * _entry.flightBow;

            var pose = crystal.CollectPose;
            Matrix4x4 crystalWorld = Matrix4x4.TRS(pose.position, pose.rotation, crystal.CollectScale);
            Matrix4x4 modelWorld = crystalWorld * (crystal.transform.worldToLocalMatrix * model.localToWorldMatrix);

            int count = solution.FaceCount;
            var faceWorld = new Matrix4x4[count];
            var facePoses = ReadFacePoses(crystalRenderer, solution);
            for (int i = 0; i < count; i++) faceWorld[i] = facePoses != null ? modelWorld * facePoses[i] : modelWorld;

            int vertexCount = solution.Vertices.Length;
            _startPositions = new Vector3[vertexCount];
            _startNormals = new Vector3[vertexCount];
            _vertices = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                Matrix4x4 m = faceWorld[solution.VertexPanel[v]];
                _startPositions[v] = m.MultiplyPoint3x4(solution.Vertices[v]);
                _startNormals[v] = m.MultiplyVector(solution.Normals[v]).normalized;
            }

            Vector3 crystalCentre = modelWorld.MultiplyPoint3x4(solution.CrystalCentre);
            float crystalRadius = solution.CrystalRadius * (modelWorld.GetColumn(0).magnitude
                + modelWorld.GetColumn(1).magnitude + modelWorld.GetColumn(2).magnitude) / 3f;

            // The pole: where the crystal is, seen from the hull's centre, in normalised hull space.
            Vector3 pole = Vector3.Scale(hullTransform.InverseTransformPoint(crystalCentre) - layout.HullCentre, layout.InvExtents);
            pole = pole.sqrMagnitude > 1e-10f ? pole.normalized : Vector3.up;

            int perFace = solution.PointsPerFace;
            _faces = new Face[count];
            _pointStart = new Vector3[count * perFace];
            _pointPosition = new Vector3[count * perFace];
            _pointNormal = new Vector3[count * perFace];

            var cost = new float[count, count];
            float bestDelay = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                // The radial is the block's SLOT - a flipping block turns about its own centre and
                // stays on it - so it is read through the model, not through the block's pose.
                Vector3 radial = modelWorld.MultiplyVector(solution.FaceRadial[i]).normalized;
                Vector3 radialHull = hullTransform.InverseTransformDirection(radial).normalized;
                Vector3 wrap = CrystalHullFusionGeometry.WrapDirection(radialHull, pole);
                for (int k = 0; k < count; k++) cost[i, k] = 1f - Vector3.Dot(wrap, layout.PatchDirection[k]);

                Matrix4x4 m = faceWorld[i];
                Vector3 centroid = solution.FaceCentroid[i];
                _faces[i] = new Face
                {
                    Lift = radial * (crystalRadius * _entry.peelDistance),
                    StartCentroid = m.MultiplyPoint3x4(centroid),
                    StartNormal = m.MultiplyVector(solution.FaceNormal[i]).normalized,
                    // The face nearest the hull lands first; the far side closes last.
                    Delay01 = 0.5f * (1f + Vector3.Dot(radialHull, pole)),
                };
                if (_faces[i].Delay01 < bestDelay) { bestDelay = _faces[i].Delay01; _contactFace = i; }

                for (int k = 0; k < perFace; k++)
                {
                    Vector2 q = solution.FacePoints[i * perFace + k];
                    _pointStart[i * perFace + k] = m.MultiplyPoint3x4(
                        centroid + solution.FaceAxisU[i] * q.x + solution.FaceAxisV[i] * q.y);
                }
            }

            var assignment = CrystalHullFusionGeometry.AssignMinCost(cost);
            for (int i = 0; i < count; i++) _faces[i].Patch = assignment[i];

            // A crystal taken far from the hull (a 60x skimmer reaches 30 units out) would peel out
            // there, and its faces streaking 30 units into the ship read as the old capture. It flies
            // in whole first, to a standoff that keeps pace with the hull, and peels beside it.
            _crystalCentreStart = crystalCentre;
            Vector3 hullCentre = HullCentreWorld(out float hullRadius);
            _standoffDistance = _entry.approachStandoff * hullRadius;
            Vector3 fromHull = crystalCentre - hullCentre;
            if (hullRadius > 0f && fromHull.magnitude > _standoffDistance)
            {
                _approachSeconds = _entry.approachSeconds * _entry.Scale;
                _approachDirLocal = Quaternion.Inverse(hullTransform.rotation) * fromHull.normalized;
            }

            _haveTargetColour = _entry.convergeToDomainColour
                && crystal.TryGetDomainCrystalColors(vesselStatus.Domain, out _targetBright, out _targetDull);
        }

        /// <summary>A skinned crystal's anchor corners, read once per (mesh, solution): each
        /// anchor's rest position, its blend-shape deltas and its bone weights.</summary>
        sealed class AnchorSkin
        {
            public int[] Shapes;            // blend shapes that move any anchor
            public float[] ShapeFullWeight; // each shape's last frame weight (100 for an FBX key)
            public Vector3[][] ShapeDelta;  // per shape, per anchor
            public BoneWeight[] Weights;    // per anchor; null = not bone-skinned
            public Matrix4x4[] BindPoses;
        }

        static readonly Dictionary<(Mesh, CrystalHullFusionGeometry.FusionSolution), AnchorSkin> s_anchorSkins = new();

        /// <summary>
        /// Per face, the rigid map from its rest pose to the pose a SKINNED crystal is holding right
        /// now (mesh space), read off the face's three anchor corners. Only the anchors are skinned -
        /// blend shapes (the Space crystal's spin) and bones (the Time crystal's flip wave) exactly as
        /// the renderer would - so this is a few dozen vertices per pickup, and exact rather than
        /// measured. Null - every face at rest - for a static crystal (charge, mass) or an unreadable mesh.
        /// </summary>
        static Matrix4x4[] ReadFacePoses(Renderer renderer, CrystalHullFusionGeometry.FusionSolution solution)
        {
            if (renderer is not SkinnedMeshRenderer skinned || solution.FaceAnchor == null) return null;
            const int per = CrystalHullFusionGeometry.AnchorsPerFace;
            var anchors = solution.FaceAnchor;
            var rest = solution.FaceAnchorRest;
            if (rest == null || rest.Length != anchors.Length || anchors.Length != solution.FaceCount * per) return null;

            var mesh = skinned.sharedMesh;
            var skin = GetAnchorSkin(mesh, solution);
            if (skin == null) return null;

            var live = new Vector3[anchors.Length];
            for (int a = 0; a < anchors.Length; a++) live[a] = rest[a];
            for (int s = 0; s < skin.Shapes.Length; s++)
            {
                float w = skinned.GetBlendShapeWeight(skin.Shapes[s]) / skin.ShapeFullWeight[s];
                if (Mathf.Approximately(w, 0f)) continue;
                var delta = skin.ShapeDelta[s];
                for (int a = 0; a < anchors.Length; a++) live[a] += delta[a] * w;
            }

            var bones = skinned.bones;
            if (skin.Weights != null && bones is { Length: > 0 })
            {
                // Mesh space at bind is the renderer's local space, so the skinned point is brought
                // back into it through the renderer: at rest this is the identity.
                Matrix4x4 toLocal = skinned.transform.worldToLocalMatrix;
                for (int a = 0; a < anchors.Length; a++)
                {
                    var bw = skin.Weights[a];
                    Vector3 p = live[a];
                    Vector3 world = Skin(bw.boneIndex0, bw.weight0) + Skin(bw.boneIndex1, bw.weight1)
                                  + Skin(bw.boneIndex2, bw.weight2) + Skin(bw.boneIndex3, bw.weight3);
                    float total = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
                    if (total > 1e-6f) live[a] = toLocal.MultiplyPoint3x4(world / total);

                    Vector3 Skin(int bone, float weight) =>
                        weight > 0f && bone >= 0 && bone < bones.Length && bone < skin.BindPoses.Length && bones[bone]
                            ? (bones[bone].localToWorldMatrix * skin.BindPoses[bone]).MultiplyPoint3x4(p) * weight
                            : Vector3.zero;
                }
            }

            var poses = new Matrix4x4[solution.FaceCount];
            for (int f = 0; f < solution.FaceCount; f++)
            {
                int o = f * per;
                poses[f] = CrystalHullFusionGeometry.AnchorMap(rest[o], rest[o + 1], rest[o + 2], live[o], live[o + 1], live[o + 2]);
            }
            return poses;
        }

        static AnchorSkin GetAnchorSkin(Mesh mesh, CrystalHullFusionGeometry.FusionSolution solution)
        {
            if (!mesh) return null;
            var key = (mesh, solution);
            if (s_anchorSkins.TryGetValue(key, out var skin)) return skin;
            s_anchorSkins[key] = null;
            if (!mesh.isReadable)
            {
                WarnOnce($"anchors:{mesh.name}",
                    $"[CrystalHullFusion] '{mesh.name}' is not CPU-readable, so its faces leave from their REST " +
                    "pose rather than the pose the crystal is holding - enable Read/Write on its model importer.");
                return null;
            }

            var anchors = solution.FaceAnchor;
            foreach (int a in anchors)
                if (a < 0 || a >= mesh.vertexCount) return null;

            var shapes = new List<int>();
            var fullWeights = new List<float>();
            var deltas = new List<Vector3[]>();
            var frame = new Vector3[mesh.vertexCount];
            for (int s = 0; s < mesh.blendShapeCount; s++)
            {
                int last = mesh.GetBlendShapeFrameCount(s) - 1;
                if (last < 0) continue;
                mesh.GetBlendShapeFrameVertices(s, last, frame, null, null);
                var perAnchor = new Vector3[anchors.Length];
                bool moves = false;
                for (int a = 0; a < anchors.Length; a++)
                {
                    perAnchor[a] = frame[anchors[a]];
                    moves |= perAnchor[a].sqrMagnitude > 0f;
                }
                if (!moves) continue;
                shapes.Add(s);
                fullWeights.Add(Mathf.Max(1e-4f, mesh.GetBlendShapeFrameWeight(s, last)));
                deltas.Add(perAnchor);
            }

            BoneWeight[] weights = null;
            var bindPoses = mesh.bindposes;
            if (bindPoses is { Length: > 0 })
            {
                var all = mesh.boneWeights;
                if (all.Length == mesh.vertexCount)
                {
                    weights = new BoneWeight[anchors.Length];
                    for (int a = 0; a < anchors.Length; a++) weights[a] = all[anchors[a]];
                }
            }

            skin = new AnchorSkin
            {
                Shapes = shapes.ToArray(),
                ShapeFullWeight = fullWeights.ToArray(),
                ShapeDelta = deltas.ToArray(),
                Weights = weights,
                BindPoses = bindPoses,
            };
            s_anchorSkins[key] = skin;
            return skin;
        }

        /// <summary>The posed hull's centre and mean radius in the world, read off its patches
        /// through this frame's pin matrices - true for a skinned and a static hull alike, whatever
        /// scale their mesh space carries.</summary>
        Vector3 HullCentreWorld(out float meanRadius)
        {
            var layout = _layout;
            int count = layout.PatchCount;
            Vector3 centre = Vector3.zero;
            for (int k = 0; k < count; k++)
                centre += _boneToWorld[layout.PatchBone[k]].MultiplyPoint3x4(layout.PatchPositionLocal[k]);
            centre /= Mathf.Max(1, count);
            float sum = 0f;
            for (int k = 0; k < count; k++)
                sum += (_boneToWorld[layout.PatchBone[k]].MultiplyPoint3x4(layout.PatchPositionLocal[k]) - centre).magnitude;
            meanRadius = sum / Mathf.Max(1, count);
            return centre;
        }

        /// <summary>The approach's offset this frame: nothing for a crystal taken near the hull;
        /// otherwise the way from where it was taken to the standoff beside the hull NOW, eased in
        /// over the approach and held (tracking the hull) through the peel and the flight's start.</summary>
        Vector3 Carry(float approach01)
        {
            if (_approachSeconds <= 0f) return Vector3.zero;
            Vector3 centre = HullCentreWorld(out _);
            Vector3 standoff = centre + _hull.Space.rotation * _approachDirLocal * _standoffDistance;
            return (standoff - _crystalCentreStart) * CrystalHullFusionConfigSO.Smooth(approach01);
        }

        // ══ Per frame ═════════════════════════════════════════════════════════════════════════

        void LateUpdate()
        {
            if (_hull == null || !_hull.Space || _faces == null) { Destroy(gameObject); return; }

            float elapsed = Time.time - _startTime;
            float approach01 = 1f;
            CrystalHullFusionConfigSO.Phase phase;
            float u;
            if (elapsed < _approachSeconds)
            {
                approach01 = elapsed / _approachSeconds;
                phase = CrystalHullFusionConfigSO.Phase.Approach;
                u = approach01;
            }
            else
            {
                phase = _entry.Resolve(elapsed - _approachSeconds, out u);
                if (phase == CrystalHullFusionConfigSO.Phase.Done) { Destroy(gameObject); return; }
            }

            using (s_frameMarker.Auto())
            {
                Vector3 anchor = _hull.Space.position;
                transform.SetPositionAndRotation(anchor, Quaternion.identity);
                transform.localScale = Vector3.one;

                // One native read per bone, then every point is managed matrix maths.
                // A pin destroyed mid-fusion (a hull part an ability removed) holds its last pose:
                // only the faces on it stop following, the fusion does not end.
                for (int b = 0; b < _bones.Length; b++)
                    if (_bones[b]) _boneToWorld[b] = _bones[b].localToWorldMatrix;
                _carry = Carry(approach01);

                // The approach draws the crystal whole: the peel's first frame, carried.
                var drawn = phase == CrystalHullFusionConfigSO.Phase.Approach ? CrystalHullFusionConfigSO.Phase.Peel : phase;
                float drawnU = phase == CrystalHullFusionConfigSO.Phase.Approach ? 0f : u;
                if (drawn != CrystalHullFusionConfigSO.Phase.Peel) PosePoints(drawn, drawnU);
                WriteMesh(drawn, drawnU, anchor);
                FadeCompanions(elapsed);
                WriteMaterial(drawn, drawnU);
            }
        }

        void PosePoints(CrystalHullFusionConfigSO.Phase phase, float u)
        {
            var e = _entry;
            var layout = _layout;
            int perFace = layout.PointsPerPatch;
            bool dissolving = phase == CrystalHullFusionConfigSO.Phase.Dissolve;
            float sink = dissolving
                ? e.sinkDepth * layout.PatchRadius * _hullScale * CrystalHullFusionConfigSO.EaseIn(u)
                : 0f;
            // An opaque shader has nothing to fade, so its faces dissolve by drawing in to nothing.
            float shrink = dissolving && _opacityId < 0 ? CrystalHullFusionConfigSO.EaseIn(u) : 0f;

            for (int i = 0; i < _faces.Length; i++)
            {
                ref var face = ref _faces[i];
                int patch = face.Patch;
                Vector3 spotNormal = _boneToWorld[layout.PatchBone[patch]].MultiplyVector(layout.PatchNormalLocal[patch]).normalized;
                float f = phase == CrystalHullFusionConfigSO.Phase.Flight
                    ? CrystalHullFusionConfigSO.Smooth(e.FaceFlightProgress(u, face.Delay01))
                    : 1f;
                float g = 1f - f;
                Vector3 centroid = Vector3.zero;

                for (int k = 0; k < perFace; k++)
                {
                    int at = patch * perFace + k;
                    Matrix4x4 m = _boneToWorld[layout.PointBone[at]];
                    Vector3 target = m.MultiplyPoint3x4(layout.PointLocal[at]);
                    Vector3 targetNormal = m.MultiplyVector(layout.PointNormalLocal[at]).normalized;
                    int p = i * perFace + k;

                    if (phase == CrystalHullFusionConfigSO.Phase.Flight)
                    {
                        // A quadratic curve whose last leg runs straight down the patch normal: the
                        // face swings out over its patch and comes DOWN onto the skin, rather than
                        // arriving edge-on or through the hull.
                        Vector3 start = _pointStart[p] + face.Lift + _carry;
                        Vector3 control = target + spotNormal * _bowDistance;
                        _pointPosition[p] = g * g * start + 2f * g * f * control + f * f * target;
                        _pointNormal[p] = Vector3.Lerp(face.StartNormal, targetNormal, f).normalized;
                    }
                    else
                    {
                        _pointPosition[p] = target - targetNormal * sink;
                        _pointNormal[p] = targetNormal;
                    }
                    centroid += _pointPosition[p];
                }
                face.Centroid = centroid / Mathf.Max(1, perFace);
                if (shrink > 0f)
                    for (int k = 0; k < perFace; k++)
                    {
                        int p = i * perFace + k;
                        _pointPosition[p] = Vector3.LerpUnclamped(_pointPosition[p], face.Centroid, shrink);
                    }
            }
        }

        void WriteMesh(CrystalHullFusionConfigSO.Phase phase, float u, Vector3 anchor)
        {
            var vertexPanel = _solution.VertexPanel;
            var vertexPoint = _solution.VertexPoint;
            int perFace = _solution.PointsPerFace;
            bool peel = phase == CrystalHullFusionConfigSO.Phase.Peel;
            float lift = CrystalHullFusionConfigSO.EaseOut(u);
            float fold = CrystalHullFusionConfigSO.Smooth(u);

            for (int v = 0; v < vertexPanel.Length; v++)
            {
                int i = vertexPanel[v];
                int k = vertexPoint[v];

                if (peel)
                {
                    // Every face lifts off along its solid's radial; the filler folds into its face's
                    // centre as it goes, so what leaves the crystal is loose faces.
                    ref var face = ref _faces[i];
                    Vector3 start = _startPositions[v] + face.Lift * lift + _carry;
                    _vertices[v] = (k >= 0 ? start : Vector3.Lerp(start, face.StartCentroid + face.Lift * lift + _carry, fold)) - anchor;
                    _normals[v] = _startNormals[v];
                }
                else if (k >= 0)
                {
                    _vertices[v] = _pointPosition[i * perFace + k] - anchor;
                    _normals[v] = _pointNormal[i * perFace + k];
                }
                else
                {
                    // Folded away: a degenerate point riding the face, drawing nothing.
                    _vertices[v] = _faces[i].Centroid - anchor;
                    _normals[v] = _startNormals[v];
                }
            }

            _mesh.SetVertices(_vertices);   // recalculates the bounds
            _mesh.SetNormals(_normals);
        }

        void WriteMaterial(CrystalHullFusionConfigSO.Phase phase, float u)
        {
            var e = _entry;

            // Colour: the crystal's pair carried onto the pilot's over the flight, so the faces are
            // theirs by the time they touch the skin.
            float converge = phase switch
            {
                CrystalHullFusionConfigSO.Phase.Peel => 0f,
                CrystalHullFusionConfigSO.Phase.Flight => CrystalHullFusionConfigSO.Smooth(u),
                _ => 1f,
            };
            float flare = phase switch
            {
                CrystalHullFusionConfigSO.Phase.Peel => Mathf.Lerp(1f, 1.3f, u),
                CrystalHullFusionConfigSO.Phase.Flight => Mathf.Lerp(1.3f, 1.6f, u),
                CrystalHullFusionConfigSO.Phase.Mate => Mathf.Lerp(e.flareGain, 1.4f, CrystalHullFusionConfigSO.EaseOut(u)),
                _ => Mathf.Lerp(1.4f, 1f, u),
            };
            float discharge = phase switch
            {
                CrystalHullFusionConfigSO.Phase.Mate => 1f,
                CrystalHullFusionConfigSO.Phase.Dissolve => 1f - CrystalHullFusionConfigSO.Smooth(u),
                _ => 0f,
            };
            float opacity = phase == CrystalHullFusionConfigSO.Phase.Dissolve ? 1f - CrystalHullFusionConfigSO.EaseIn(u) : 1f;

            if (!_renderer) return;
            // The block is OURS and persistent: Adopt seeded it with the crystal's own block and the
            // frozen Shepard band. Re-reading it from the renderer each frame (as this once did) read
            // back an EMPTY block on frame 0 and threw the freeze away - the Mass faces then rode a
            // live band that scaled them about the hull's origin, and "shrank to a point".
            if (_hasTint)
            {
                Color dull = _startDull, bright = _startBright;
                if (_haveTargetColour)
                {
                    if (!_tintHoldsDull) dull = Color.Lerp(dull, _targetDull, converge);
                    bright = Color.Lerp(bright, _targetBright, converge);
                }
                // A fresnel crystal's dark body is what gives its rim contrast; only the rim flares.
                _block.SetColor(_tintDullId, _tintHoldsDull ? dull : dull.ScaleRGB(flare));
                _block.SetColor(_tintBrightId, bright.ScaleRGB(flare));
            }
            if (_hasArcIntensity)
                _block.SetFloat(ArcIntensityId, _baseArcIntensity * Mathf.Lerp(1f, e.arcBoost, discharge));
            if (_hasArcDuty)
                _block.SetFloat(ArcDutyId, Mathf.Lerp(_baseArcDuty, e.mateArcDuty, discharge));
            if (_opacityId >= 0) _block.SetFloat(_opacityId, opacity);
            _renderer.SetPropertyBlock(_block);
        }

        void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
            // A fusion cut short must not leave a half-faded shell hanging where the crystal was.
            if (_companions != null)
                foreach (var shell in _companions)
                    if (shell) shell.enabled = false;
        }

        static void WarnOnce(string key, string message)
        {
            if (s_warned.Add(key)) CSDebug.LogWarning(message);
        }
    }
}
