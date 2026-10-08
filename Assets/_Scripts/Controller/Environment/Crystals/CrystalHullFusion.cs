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
    /// counterpart of the Squirrel's omni morph (the omni cage's panels landing on the eight ring
    /// shields, <c>SQUIRREL_CRYSTAL_MORPH.md</c>). Full record:
    /// <c>Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md</c>.
    ///
    /// ── Panels and filler ─────────────────────────────────────────────────────────────────────
    /// Every solid of the crystal gives up ONE face, its outermost (on the charge crystal: 60
    /// pentagon caps). That face flies; the solid's other faces fold into it during the peel, so
    /// what leaves the crystal is a cloud of loose faces.
    ///
    /// ── A face lands ON the hull's surface, bent to its shape ────────────────────────────────
    /// Each face is drawn as a subdivided fan (<see cref="CrystalHullFusionGeometry.FusionTemplate"/>)
    /// and takes a patch of the skin, where every point of it lies on the hull's own triangles,
    /// wearing the hull's interpolated normal, pinned to the bone that carries it - so a face on a
    /// wing stays on the wing while it flaps.
    ///
    /// ── The expensive half is done ONCE, off the main thread ──────────────────────────────────
    /// Where the faces land depends only on the hull mesh and the crystal mesh, never on the
    /// pickup, so it is a <see cref="CrystalHullFusionGeometry.HullLayout"/> built once per pair on
    /// a worker thread (<see cref="Prewarm"/>, called when the vessel initialises) from plain
    /// arrays captured on the main thread. The first playtest of this effect projected every face
    /// point on the main thread during the pickup itself - 80 ms of work even in warm .NET, more in
    /// the Editor - and the effect was over before the frame caught up. A pickup now chooses which
    /// face takes which patch (a 60×60 assignment) and clones a prototype mesh; nothing else.
    ///
    /// The worker touches no <c>UnityEngine.Object</c> and the main thread never awaits it: it
    /// POLLS a volatile ready flag, so none of the UniTask main-thread hazards in
    /// <c>Docs/THREADING.md</c> can arise. A pickup that arrives before the layout is ready plays
    /// the generic capture.
    ///
    /// ── It draws the crystal's own geometry ───────────────────────────────────────────────────
    /// The crystal's materials and property block are copied, and the mesh carries every channel
    /// of the drawn one (the charge discharge rides TEXCOORD1-3). The DRAWN charge mesh is an
    /// uploaded, unreadable twin, so the readable copy comes from
    /// <see cref="CrystalEdgeArcMeshBaker.TryGetReadable"/> - the first cut of this class checked
    /// <c>isReadable</c> on the drawn mesh, refused every charge crystal, and fell back to the
    /// generic capture, which looked exactly like the effect it was meant to replace.
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
        /// 45 triangles; every point still lands on the Squirrel's skin (measured).</summary>
        const int FaceSubdivisions = 3;

        static readonly ProfilerMarker s_beginMarker = new("CrystalHullFusion.Begin");
        static readonly ProfilerMarker s_prewarmMarker = new("CrystalHullFusion.Prewarm");
        static readonly ProfilerMarker s_frameMarker = new("CrystalHullFusion.Frame");

        /// <summary>One crystal mesh cut into faces. Arrays captured on the main thread, the cut
        /// itself made on a worker, the prototype mesh made on the main thread at first use.</summary>
        sealed class CrystalCut
        {
            public Vector3[] Vertices, Normals, Bary, EdgeH, EdgeSeed;
            public List<int[]> Triangles;
            public float ModelRadius;

            public CrystalHullFusionGeometry.PanelSet Panels;
            public CrystalHullFusionGeometry.FusionTemplate Template;
            public Mesh Prototype;
        }

        /// <summary>One (hull mesh, crystal mesh) layout, built on a worker.</summary>
        sealed class LayoutJob
        {
            public volatile bool Ready;
            public volatile string Failure;
            public CrystalCut Cut;
            public CrystalHullFusionGeometry.HullLayout Layout;
        }

        static readonly Dictionary<Mesh, CrystalCut> s_cuts = new();
        static readonly Dictionary<(Mesh hull, Mesh crystal), LayoutJob> s_layouts = new();
        static readonly HashSet<string> s_warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            s_cuts.Clear();
            s_layouts.Clear();
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
        CrystalHullFusionGeometry.HullLayout _layout;
        CrystalHullFusionGeometry.FusionTemplate _template;
        Transform[] _bones;                 // the hull's bones, then the fallback (root bone / renderer)
        Matrix4x4[] _boneToWorld;

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

        // ══ Prewarm (vessel spawn) ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Starts building the layout for every (this vessel, element) pair the config fuses, so it
        /// is ready before the first pickup. Called from <c>VesselAnimation.Initialize</c>; does
        /// nothing for a vessel with no entry, or a pair already built. Never throws into vessel
        /// initialisation - a failure is warned and that pair simply plays the generic capture.
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
                        if (!prefab || !TryGetDrawnMesh(prefab, out var drawn, out _, out _)) continue;
                        if (CrystalEdgeArcMeshBaker.TryGetReadable(drawn, out var readable))
                            GetOrStartLayout(hull, readable, entry);
                    }
                }
                catch (Exception e)
                {
                    WarnOnce($"prewarm:{vesselStatus.VesselType}",
                        $"[CrystalHullFusion] prewarming {vesselStatus.VesselType} failed ({e.Message}) - its " +
                        "crystals will play the generic capture until a pickup builds the layout.");
                }
            }
        }

        /// <summary>
        /// The mesh a crystal prefab or instance actually DRAWS: on an instance, its filter's mesh
        /// (already swapped for the edge-arc twin by <see cref="CrystalEdgeArcs"/>); on a prefab, the
        /// same cached twin the baker hands every instance.
        /// </summary>
        static bool TryGetDrawnMesh(Crystal crystal, out Mesh drawn, out GameObject model, out MeshRenderer renderer)
        {
            drawn = null;
            model = null;
            renderer = null;
            var models = crystal.CrystalModels;
            if (models == null) return false;

            foreach (var data in models)
            {
                var candidate = data?.model;
                if (candidate == null) continue;
                if (!candidate.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                if (!candidate.TryGetComponent<MeshRenderer>(out renderer)) continue;

                drawn = filter.sharedMesh;
                if (!drawn.name.EndsWith(CrystalEdgeArcMeshBaker.BakedSuffix)
                    && candidate.TryGetComponent<CrystalEdgeArcs>(out var arcs))
                {
                    var baked = CrystalEdgeArcMeshBaker.GetOrBake(drawn, arcs.PlateCorners);
                    if (baked != null) drawn = baked;
                }
                model = candidate;
                return true;
            }
            return false;
        }

        static LayoutJob GetOrStartLayout(SkinnedMeshRenderer hull, Mesh readableCrystal,
                                           CrystalHullFusionConfigSO.Entry entry)
        {
            var key = (hull.sharedMesh, readableCrystal);
            if (s_layouts.TryGetValue(key, out var job)) return job;

            job = new LayoutJob { Cut = GetCut(readableCrystal) };
            s_layouts[key] = job;

            // Everything the worker reads is captured HERE, on the main thread, as plain data.
            var baked = new Mesh { name = "CrystalHullFusion_HullBake", hideFlags = HideFlags.DontSave };
            hull.BakeMesh(baked, true);
            if (baked.vertexCount == 0)
            {
                Destroy(baked);
                job.Failure = $"baking '{hull.name}' produced no vertices - check Read/Write on the model importer " +
                              $"of '{(hull.sharedMesh ? hull.sharedMesh.name : "(none)")}'";
                return job;
            }

            var bones = hull.bones ?? Array.Empty<Transform>();
            var fallback = hull.rootBone ? hull.rootBone : hull.transform;
            var toBone = new Matrix4x4[bones.Length + 1];
            for (int b = 0; b < bones.Length; b++)
                toBone[b] = bones[b] ? bones[b].worldToLocalMatrix : fallback.worldToLocalMatrix;
            toBone[bones.Length] = fallback.worldToLocalMatrix;

            var input = new CrystalHullFusionGeometry.HullLayoutInput
            {
                HullVertices = baked.vertices,
                HullNormals = baked.normals,
                HullTriangles = baked.triangles,
                DominantBones = DominantBones(hull.sharedMesh),
                FallbackBone = bones.Length,
                HullToWorld = Matrix4x4.TRS(hull.transform.position, hull.transform.rotation, Vector3.one),
                BoneWorldToLocal = toBone,
            };
            Destroy(baked);

            // Patch size and skin gap are the pair's own tuning, read when its layout is built.
            input.TileFill = entry.tileFill;
            input.SurfaceLift = entry.surfaceLift;

            var cut = job.Cut;
            Task.Run(() =>
            {
                try
                {
                    lock (cut)
                    {
                        if (cut.Template == null)
                        {
                            cut.Panels = CrystalHullFusionGeometry.BuildPanels(cut.Vertices, cut.Triangles);
                            cut.Template = cut.Panels == null ? null : CrystalHullFusionGeometry.BuildTemplate(
                                cut.Panels, cut.Vertices, cut.Normals, cut.Bary, cut.EdgeH, cut.EdgeSeed,
                                cut.Triangles, FaceSubdivisions, cut.ModelRadius);
                        }
                    }
                    if (cut.Template == null) { job.Failure = "the crystal mesh has no faces"; return; }

                    input.Panels = cut.Panels;
                    input.Template = cut.Template;
                    var layout = CrystalHullFusionGeometry.BuildHullLayout(input, out string failure);
                    if (layout == null) { job.Failure = failure; return; }
                    job.Layout = layout;
                    job.Ready = true;
                }
                catch (Exception e)
                {
                    job.Failure = $"{e.GetType().Name}: {e.Message}";
                }
            });
            return job;
        }

        static CrystalCut GetCut(Mesh readable)
        {
            if (s_cuts.TryGetValue(readable, out var cut)) return cut;

            var bary = new List<Vector3>();
            var edgeH = new List<Vector3>();
            var edgeSeed = new List<Vector3>();
            readable.GetUVs(1, bary);
            readable.GetUVs(2, edgeH);
            readable.GetUVs(3, edgeSeed);
            var vertices = readable.vertices;
            var triangles = new List<int[]>(readable.subMeshCount);
            for (int s = 0; s < readable.subMeshCount; s++) triangles.Add(readable.GetTriangles(s));
            var extents = readable.bounds.extents;

            cut = new CrystalCut
            {
                Vertices = vertices,
                Normals = readable.normals,
                Bary = bary.Count == vertices.Length ? bary.ToArray() : null,
                EdgeH = edgeH.Count == vertices.Length ? edgeH.ToArray() : null,
                EdgeSeed = edgeSeed.Count == vertices.Length ? edgeSeed.ToArray() : null,
                Triangles = triangles,
                ModelRadius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)),
            };
            s_cuts[readable] = cut;
            return cut;
        }

        static Mesh PrototypeFor(CrystalCut cut, string name)
        {
            if (cut.Prototype) return cut.Prototype;
            var t = cut.Template;
            var mesh = new Mesh
            {
                name = $"{name} (Fusion)",
                hideFlags = HideFlags.DontSave,
                indexFormat = t.Vertices.Length > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(t.Vertices);
            mesh.SetNormals(t.Normals);
            mesh.SetUVs(1, t.Bary);
            mesh.SetUVs(2, t.EdgeH);
            mesh.SetUVs(3, t.EdgeSeed);
            mesh.subMeshCount = t.SubmeshTriangles.Length;
            for (int s = 0; s < t.SubmeshTriangles.Length; s++) mesh.SetTriangles(t.SubmeshTriangles[s], s, false);
            mesh.RecalculateBounds();
            cut.Prototype = mesh;
            return mesh;
        }

        // ══ A pickup ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Starts a fusion of <paramref name="crystal"/> onto the hull of the vessel described by
        /// <paramref name="vesselStatus"/>, and hides the crystal's own renderers (it is still the
        /// caller's to retire). Returns null — leaving the crystal untouched, so the caller plays the
        /// generic capture — while the layout is still building, and whenever the fusion cannot land
        /// honestly. Every refusal is a WARNING, once per reason: on screen they all look like the
        /// old capture, so the console is the only place the difference shows.
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

                if (!TryGetDrawnMesh(crystal, out var drawn, out var model, out var source)
                    || !CrystalEdgeArcMeshBaker.TryGetReadable(drawn, out var readable))
                {
                    WarnOnce($"mesh:{crystal.name}",
                        $"[CrystalHullFusion] '{crystal.name}' has no model whose faces can be read (not " +
                        "CPU-readable and not made by CrystalEdgeArcMeshBaker) - the generic capture plays.");
                    return null;
                }

                var job = GetOrStartLayout(hull, readable, entry);
                if (job.Failure != null)
                {
                    WarnOnce($"layout:{hull.name}:{readable.name}",
                        $"[CrystalHullFusion] no layout for '{readable.name}' on '{hull.name}': {job.Failure} - " +
                        "the generic capture plays.");
                    return null;
                }
                if (!job.Ready)
                {
                    if (CSDebug.IsVerbose(CSLogChannel.CrystalMorph))
                        CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                            $"[CrystalHullFusion] layout for '{hull.name}' still building - this pickup plays the generic capture.");
                    return null;
                }

                var go = new GameObject($"CrystalHullFusion_{crystal.name}");
                go.layer = model.layer;
                var fusion = go.AddComponent<CrystalHullFusion>();
                fusion._entry = entry;
                fusion._hull = hull;
                fusion._layout = job.Layout;
                fusion._template = job.Cut.Template;
                fusion.Adopt(PrototypeFor(job.Cut, drawn.name), source);
                fusion.Plan(crystal, vesselStatus, job.Cut.Panels, model.transform);

                // The crystal is now drawn by the fusion. It stays alive (hidden) so its owner can
                // retire it when the faces are down - the pickup sound and the cell bookkeeping are its own.
                foreach (var renderer in crystal.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;

                fusion._startTime = Time.time;
                fusion.LateUpdate(); // frame 0 is drawn THIS frame - the crystal is already hidden

                if (CSDebug.IsVerbose(CSLogChannel.CrystalMorph))
                    CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                        $"[CrystalHullFusion] {vesselStatus.VesselType}/{entry.element}: '{crystal.name}' " +
                        $"peeling {fusion._faces.Length} faces onto '{hull.name}' over {entry.TotalSeconds:F2}s " +
                        $"(patch radius {job.Layout.PatchRadius:F2}, {job.Layout.Unprojected} of " +
                        $"{job.Layout.Projected + job.Layout.Unprojected} points off the skin, domain colour " +
                        $"{(fusion._haveTargetColour ? "read" : "NOT FOUND")}).");
                return fusion;
            }
        }

        /// <summary>
        /// The hull is the renderer the vessel's ELEMENT display lives on — the skinned mesh carrying
        /// the element blend shapes, which by the fleet's own contract is the visible hull
        /// (<see cref="VesselAnimation.CollectElementShapes"/>). Failing that, the largest visible
        /// skinned mesh under the animation root.
        /// </summary>
        static SkinnedMeshRenderer FindHullRenderer(Transform root)
        {
            var shapes = new List<VesselAnimation.ElementShapeTarget>();
            VesselAnimation.CollectElementShapes(root, shapes);

            SkinnedMeshRenderer best = null;
            int bestVertices = -1;
            foreach (var shape in shapes) Consider(shape.Renderer);
            if (best) return best;

            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(false)) Consider(renderer);
            return best;

            void Consider(SkinnedMeshRenderer renderer)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return;
                var mesh = renderer.sharedMesh;
                if (!mesh || mesh.vertexCount <= bestVertices) return;
                best = renderer;
                bestVertices = mesh.vertexCount;
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
        /// crystal is read at its COLLECT pose (a host respawn may already have moved it).
        /// </summary>
        void Plan(Crystal crystal, IVesselStatus vesselStatus, CrystalHullFusionGeometry.PanelSet panels, Transform model)
        {
            var layout = _layout;
            var template = _template;

            var bones = _hull.bones ?? Array.Empty<Transform>();
            _bones = new Transform[bones.Length + 1];
            Array.Copy(bones, _bones, bones.Length);
            _bones[bones.Length] = _hull.rootBone ? _hull.rootBone : _hull.transform;
            _boneToWorld = new Matrix4x4[_bones.Length];
            _bowDistance = layout.HullMeanRadius * _entry.flightBow;

            var pose = crystal.CollectPose;
            Matrix4x4 crystalWorld = Matrix4x4.TRS(pose.position, pose.rotation, crystal.CollectScale);
            Matrix4x4 modelWorld = crystalWorld * (crystal.transform.worldToLocalMatrix * model.localToWorldMatrix);

            int vertexCount = template.Vertices.Length;
            _startPositions = new Vector3[vertexCount];
            _startNormals = new Vector3[vertexCount];
            _vertices = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                _startPositions[v] = modelWorld.MultiplyPoint3x4(template.Vertices[v]);
                _startNormals[v] = modelWorld.MultiplyVector(template.Normals[v]).normalized;
            }

            Vector3 crystalCentre = modelWorld.MultiplyPoint3x4(panels.Centre);
            float crystalRadius = panels.Radius * (modelWorld.GetColumn(0).magnitude + modelWorld.GetColumn(1).magnitude
                                                   + modelWorld.GetColumn(2).magnitude) / 3f;

            // The pole: where the crystal is, seen from the hull's centre, in normalised hull space.
            Transform hullTransform = _hull.transform;
            Quaternion toHull = Quaternion.Inverse(hullTransform.rotation);
            Vector3 crystalHull = toHull * (crystalCentre - hullTransform.position);
            Vector3 pole = Vector3.Scale(crystalHull - layout.HullCentre, layout.InvExtents);
            pole = pole.sqrMagnitude > 1e-10f ? pole.normalized : Vector3.up;

            int count = panels.PanelCount;
            int perFace = layout.PointsPerPatch;
            _faces = new Face[count];
            _pointStart = new Vector3[count * perFace];
            _pointPosition = new Vector3[count * perFace];
            _pointNormal = new Vector3[count * perFace];

            var cost = new float[count, count];
            float bestDelay = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 radial = modelWorld.MultiplyVector(panels.Radials[i]).normalized;
                Vector3 radialHull = (toHull * radial).normalized;
                Vector3 wrap = CrystalHullFusionGeometry.WrapDirection(radialHull, pole);
                for (int k = 0; k < count; k++) cost[i, k] = 1f - Vector3.Dot(wrap, layout.PatchDirection[k]);

                Vector3 centroid = panels.PanelCentroids[i];
                _faces[i] = new Face
                {
                    Lift = radial * (crystalRadius * _entry.peelDistance),
                    StartCentroid = modelWorld.MultiplyPoint3x4(centroid),
                    StartNormal = modelWorld.MultiplyVector(panels.PanelNormals[i]).normalized,
                    // The face nearest the hull lands first; the far side closes last.
                    Delay01 = 0.5f * (1f + Vector3.Dot(radialHull, pole)),
                };
                if (_faces[i].Delay01 < bestDelay) { bestDelay = _faces[i].Delay01; _contactFace = i; }

                int first = template.PointStart[i];
                for (int k = 0; k < perFace; k++)
                {
                    Vector2 q = template.Points[first + k];
                    _pointStart[i * perFace + k] = modelWorld.MultiplyPoint3x4(
                        centroid + template.AxisU[i] * q.x + template.AxisV[i] * q.y);
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
                ? e.sinkDepth * layout.PatchRadius * CrystalHullFusionConfigSO.EaseIn(u)
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
            var template = _template;
            var vertexPanel = template.VertexPanel;
            var vertexPoint = template.VertexPoint;
            int perFace = _layout.PointsPerPatch;
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

        /// <summary>Per vertex, the bone with the greatest weight. Needs the mesh readable; null
        /// otherwise, and every point then rides the renderer instead.</summary>
        static int[] DominantBones(Mesh mesh)
        {
            if (!mesh) return null;
            if (!mesh.isReadable)
            {
                WarnOnce($"bones:{mesh.name}",
                    $"[CrystalHullFusion] '{mesh.name}' is not CPU-readable, so fused faces cannot be " +
                    "pinned to the bones that carry them and will ride the renderer instead - a face on " +
                    "a moving limb will drift off it. Enable Read/Write on the model importer.");
                return null;
            }

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

        static void WarnOnce(string key, string message)
        {
            if (s_warned.Add(key)) CSDebug.LogWarning(message);
        }
    }
}
