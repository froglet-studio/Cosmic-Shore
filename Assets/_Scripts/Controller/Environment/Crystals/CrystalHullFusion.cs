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
        // Every crystal shader exposes the tint pair and the dissolve (Crystal.cs drives them); only
        // the charge shader exposes the discharge pair, and those writes are skipped elsewhere.
        static readonly int OpacityId = Shader.PropertyToID("_opacity");
        static readonly int DullId = Shader.PropertyToID("_DullCrystalColor");
        static readonly int BrightId = Shader.PropertyToID("_BrightCrystalColor");
        static readonly int ArcIntensityId = Shader.PropertyToID("_ArcIntensity");
        static readonly int ArcDutyId = Shader.PropertyToID("_ArcDuty");

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
        static readonly Dictionary<(Mesh hull, Mesh crystal, int plates), Job> s_jobs = new();
        static readonly HashSet<string> s_warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            s_baked.Clear();
            s_jobs.Clear();
            s_warned.Clear();
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
        SkinnedMeshRenderer _hull;
        CrystalHullFusionGeometry.FusionSolution _solution;
        CrystalHullFusionGeometry.HullLayout _layout;
        Transform[] _bones;                 // the hull's bones, then the renderer itself
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
        float _baseArcIntensity, _baseArcDuty;
        bool _hasArcIntensity, _hasArcDuty;
        float _startTime;

        /// <summary>Seconds from start until every face is down — when the pickup sound belongs.</summary>
        public float MateDelaySeconds => _entry.MateSecondsFromStart;

        /// <summary>The contact patch, live, in world space.</summary>
        public Vector3 LandingWorldPosition
        {
            get
            {
                if (_layout == null || _faces == null || !_hull) return transform.position;
                int patch = _faces[_contactFace].Patch;
                var bone = _bones[_layout.PatchBone[patch]];
                return bone ? bone.TransformPoint(_layout.PatchPositionLocal[patch]) : transform.position;
            }
        }

        // ══ Shared with the bake tool ═════════════════════════════════════════════════════════

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
        /// the model it is on. On an instance the filter already holds <see cref="CrystalEdgeArcs"/>'
        /// twin; on a prefab the baker hands back the same cached twin every instance draws.
        /// </summary>
        public static bool TryResolveCrystal(Crystal crystal, out Mesh drawn, out Mesh source, out int plateCorners,
                                             out GameObject model, out MeshRenderer renderer)
        {
            drawn = source = null;
            plateCorners = 0;
            model = null;
            renderer = null;
            var models = crystal ? crystal.CrystalModels : null;
            if (models == null) return false;

            foreach (var data in models)
            {
                var candidate = data?.model;
                if (candidate == null) continue;
                if (!candidate.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                if (!candidate.TryGetComponent<MeshRenderer>(out renderer)) continue;

                drawn = filter.sharedMesh;
                if (drawn.name.EndsWith(CrystalEdgeArcMeshBaker.BakedSuffix))
                    CrystalEdgeArcMeshBaker.TryGetSource(drawn, out source, out plateCorners);
                else
                {
                    source = drawn;
                    if (candidate.TryGetComponent<CrystalEdgeArcs>(out var arcs))
                    {
                        plateCorners = arcs.PlateCorners;
                        var baked = CrystalEdgeArcMeshBaker.GetOrBake(source, plateCorners);
                        if (baked != null) drawn = baked;
                    }
                }
                model = candidate;
                return source != null;
            }
            return false;
        }

        /// <summary>
        /// Captures everything <see cref="CrystalHullFusionGeometry.Solve"/> needs as plain arrays,
        /// on the main thread: the hull in its BIND POSE (mesh asset + bind poses + heaviest bone per
        /// vertex) and the crystal's drawn mesh with its discharge channels. Null, named, when either
        /// mesh cannot be read.
        /// </summary>
        public static CrystalHullFusionGeometry.SolveInput CaptureSolveInput(SkinnedMeshRenderer hull, Mesh drawnCrystal,
            CrystalHullFusionConfigSO.Entry entry, out string failure)
        {
            failure = null;
            var hullMesh = hull ? hull.sharedMesh : null;
            if (!hullMesh) { failure = "the hull renderer has no mesh"; return null; }
            if (!hullMesh.isReadable)
            {
                failure = $"'{hullMesh.name}' is not CPU-readable - enable Read/Write on its model importer";
                return null;
            }
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

                HullVertices = hullMesh.vertices,
                HullNormals = hullMesh.normals,
                HullTriangles = hullMesh.triangles,
                HullDominantBones = DominantBones(hullMesh),
                HullBindPoses = hullMesh.bindposes,

                TileFill = entry.tileFill,
                SurfaceLift = entry.surfaceLift,
                Subdivisions = FaceSubdivisions,
            };
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
                    SkinnedMeshRenderer hull = null;
                    foreach (var entry in config.Entries)
                    {
                        if (entry == null || entry.vessel != vesselStatus.VesselType) continue;
                        if (!hull) hull = FindHullRenderer(animationRoot);
                        if (!hull) return;

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
        static bool TryGetReady(SkinnedMeshRenderer hull, Mesh drawn, Mesh source, int plates,
                                CrystalHullFusionConfigSO.Entry entry, out Ready ready)
        {
            ready = null;
            var bake = entry.bake;
            if (bake)
            {
                if (bake.Matches(hull.sharedMesh, source, plates, entry, FaceSubdivisions, out string why))
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
                WarnOnce($"job:{hull.name}:{source.name}",
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

        static Job GetOrStartJob(SkinnedMeshRenderer hull, Mesh drawn, Mesh source, int plates,
                                 CrystalHullFusionConfigSO.Entry entry)
        {
            var key = (hull.sharedMesh, source, plates);
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
                var hull = animation ? FindHullRenderer(animation.transform) : null;
                if (!hull)
                {
                    WarnOnce($"nohull:{vesselStatus.VesselType}",
                        $"[CrystalHullFusion] {vesselStatus.VesselType} has no visible SkinnedMeshRenderer " +
                        "under its VesselAnimation, so there is no hull to fuse onto - the generic capture plays.");
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
                fusion.Plan(crystal, vesselStatus, model.transform);

                // The crystal is now drawn by the fusion. It stays alive (hidden) so its owner can
                // retire it when the faces are down - the pickup sound and the cell bookkeeping are its own.
                foreach (var r in crystal.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;

                fusion._startTime = Time.time;
                fusion.LateUpdate(); // frame 0 is drawn THIS frame - the crystal is already hidden

                if (CSDebug.IsVerbose(CSLogChannel.CrystalMorph))
                    CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                        $"[CrystalHullFusion] {vesselStatus.VesselType}/{entry.element}: '{crystal.name}' " +
                        $"peeling {fusion._faces.Length} faces onto '{hull.name}' over {entry.TotalSeconds:F2}s " +
                        $"({(entry.bake && ready.Prototype == entry.bake.TemplateMesh ? "baked" : "runtime-solved")}, " +
                        $"domain colour {(fusion._haveTargetColour ? "read" : "NOT FOUND")}).");
                return fusion;
            }
        }

        /// <summary>Clones the prototype and wears the crystal's own materials and property block,
        /// which is where <c>Crystal.ApplyColorSetTint</c> paints the collectability colour.</summary>
        void Adopt(Mesh prototype, MeshRenderer source)
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
            bool materialTint = material && material.HasProperty(DullId) && material.HasProperty(BrightId);
            _hasTint = materialTint || (_block.HasColor(DullId) && _block.HasColor(BrightId));
            _startDull = _block.HasColor(DullId) ? _block.GetColor(DullId) : materialTint ? material.GetColor(DullId) : Color.white;
            _startBright = _block.HasColor(BrightId) ? _block.GetColor(BrightId) : materialTint ? material.GetColor(BrightId) : Color.white;
            _hasArcIntensity = material && material.HasProperty(ArcIntensityId);
            _hasArcDuty = material && material.HasProperty(ArcDutyId);
            if (_hasArcIntensity) _baseArcIntensity = material.GetFloat(ArcIntensityId);
            if (_hasArcDuty) _baseArcDuty = material.GetFloat(ArcDutyId);
        }

        /// <summary>
        /// The per-pickup half: each face's start in the world, and which patch it takes. The
        /// crystal is read at its COLLECT pose (a host respawn may already have moved it). The hull's
        /// layout lives in its bind-pose mesh space, which is the renderer's local space.
        /// </summary>
        void Plan(Crystal crystal, IVesselStatus vesselStatus, Transform model)
        {
            var solution = _solution;
            var layout = _layout;
            Transform hullTransform = _hull.transform;

            var bones = _hull.bones ?? Array.Empty<Transform>();
            _bones = new Transform[bones.Length + 1];
            Array.Copy(bones, _bones, bones.Length);
            _bones[bones.Length] = hullTransform;
            _boneToWorld = new Matrix4x4[_bones.Length];

            Vector3 lossy = hullTransform.lossyScale;
            _hullScale = (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.y) + Mathf.Abs(lossy.z)) / 3f;
            _bowDistance = layout.HullMeanRadius * _hullScale * _entry.flightBow;

            var pose = crystal.CollectPose;
            Matrix4x4 crystalWorld = Matrix4x4.TRS(pose.position, pose.rotation, crystal.CollectScale);
            Matrix4x4 modelWorld = crystalWorld * (crystal.transform.worldToLocalMatrix * model.localToWorldMatrix);

            int vertexCount = solution.Vertices.Length;
            _startPositions = new Vector3[vertexCount];
            _startNormals = new Vector3[vertexCount];
            _vertices = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                _startPositions[v] = modelWorld.MultiplyPoint3x4(solution.Vertices[v]);
                _startNormals[v] = modelWorld.MultiplyVector(solution.Normals[v]).normalized;
            }

            Vector3 crystalCentre = modelWorld.MultiplyPoint3x4(solution.CrystalCentre);
            float crystalRadius = solution.CrystalRadius * (modelWorld.GetColumn(0).magnitude
                + modelWorld.GetColumn(1).magnitude + modelWorld.GetColumn(2).magnitude) / 3f;

            // The pole: where the crystal is, seen from the hull's centre, in normalised hull space.
            Vector3 pole = Vector3.Scale(hullTransform.InverseTransformPoint(crystalCentre) - layout.HullCentre, layout.InvExtents);
            pole = pole.sqrMagnitude > 1e-10f ? pole.normalized : Vector3.up;

            int count = solution.FaceCount;
            int perFace = solution.PointsPerFace;
            _faces = new Face[count];
            _pointStart = new Vector3[count * perFace];
            _pointPosition = new Vector3[count * perFace];
            _pointNormal = new Vector3[count * perFace];

            var cost = new float[count, count];
            float bestDelay = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 radial = modelWorld.MultiplyVector(solution.FaceRadial[i]).normalized;
                Vector3 radialHull = hullTransform.InverseTransformDirection(radial).normalized;
                Vector3 wrap = CrystalHullFusionGeometry.WrapDirection(radialHull, pole);
                for (int k = 0; k < count; k++) cost[i, k] = 1f - Vector3.Dot(wrap, layout.PatchDirection[k]);

                Vector3 centroid = solution.FaceCentroid[i];
                _faces[i] = new Face
                {
                    Lift = radial * (crystalRadius * _entry.peelDistance),
                    StartCentroid = modelWorld.MultiplyPoint3x4(centroid),
                    StartNormal = modelWorld.MultiplyVector(solution.FaceNormal[i]).normalized,
                    // The face nearest the hull lands first; the far side closes last.
                    Delay01 = 0.5f * (1f + Vector3.Dot(radialHull, pole)),
                };
                if (_faces[i].Delay01 < bestDelay) { bestDelay = _faces[i].Delay01; _contactFace = i; }

                for (int k = 0; k < perFace; k++)
                {
                    Vector2 q = solution.FacePoints[i * perFace + k];
                    _pointStart[i * perFace + k] = modelWorld.MultiplyPoint3x4(
                        centroid + solution.FaceAxisU[i] * q.x + solution.FaceAxisV[i] * q.y);
                }
            }

            var assignment = CrystalHullFusionGeometry.AssignMinCost(cost);
            for (int i = 0; i < count; i++) _faces[i].Patch = assignment[i];

            _haveTargetColour = _entry.convergeToDomainColour
                && crystal.TryGetDomainCrystalColors(vesselStatus.Domain, out _targetBright, out _targetDull);
        }

        // ══ Per frame ═════════════════════════════════════════════════════════════════════════

        void LateUpdate()
        {
            if (!_hull || _faces == null) { Destroy(gameObject); return; }

            float elapsed = Time.time - _startTime;
            var phase = _entry.Resolve(elapsed, out float u);
            if (phase == CrystalHullFusionConfigSO.Phase.Done) { Destroy(gameObject); return; }

            using (s_frameMarker.Auto())
            {
                Vector3 anchor = _hull.transform.position;
                transform.SetPositionAndRotation(anchor, Quaternion.identity);
                transform.localScale = Vector3.one;

                if (phase != CrystalHullFusionConfigSO.Phase.Peel)
                {
                    // One native read per bone, then every point is managed matrix maths.
                    for (int b = 0; b < _bones.Length; b++)
                    {
                        if (!_bones[b]) { Destroy(gameObject); return; }
                        _boneToWorld[b] = _bones[b].localToWorldMatrix;
                    }
                    PosePoints(phase, u);
                }
                WriteMesh(phase, u, anchor);
                WriteMaterial(phase, u);
            }
        }

        void PosePoints(CrystalHullFusionConfigSO.Phase phase, float u)
        {
            var e = _entry;
            var layout = _layout;
            int perFace = layout.PointsPerPatch;
            float sink = phase == CrystalHullFusionConfigSO.Phase.Dissolve
                ? e.sinkDepth * layout.PatchRadius * _hullScale * CrystalHullFusionConfigSO.EaseIn(u)
                : 0f;

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
                        Vector3 start = _pointStart[p] + face.Lift;
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
                    Vector3 start = _startPositions[v] + face.Lift * lift;
                    _vertices[v] = (k >= 0 ? start : Vector3.Lerp(start, face.StartCentroid + face.Lift * lift, fold)) - anchor;
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
            _renderer.GetPropertyBlock(_block);
            if (_hasTint)
            {
                Color dull = _startDull, bright = _startBright;
                if (_haveTargetColour)
                {
                    dull = Color.Lerp(dull, _targetDull, converge);
                    bright = Color.Lerp(bright, _targetBright, converge);
                }
                _block.SetColor(DullId, dull.ScaleRGB(flare));
                _block.SetColor(BrightId, bright.ScaleRGB(flare));
            }
            if (_hasArcIntensity)
                _block.SetFloat(ArcIntensityId, _baseArcIntensity * Mathf.Lerp(1f, e.arcBoost, discharge));
            if (_hasArcDuty)
                _block.SetFloat(ArcDutyId, Mathf.Lerp(_baseArcDuty, e.mateArcDuty, discharge));
            _block.SetFloat(OpacityId, opacity);
            _renderer.SetPropertyBlock(_block);
        }

        void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
        }

        static void WarnOnce(string key, string message)
        {
            if (s_warned.Add(key)) CSDebug.LogWarning(message);
        }
    }
}
