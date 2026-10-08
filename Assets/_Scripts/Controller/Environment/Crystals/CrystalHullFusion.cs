using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A collected elemental crystal FUSING onto the collecting vessel's hull — the per-(vessel,
    /// element) replacement for the generic capture flourish. The crystal is pulled in whole, opens,
    /// and its rigid plates slide round the hull and lie flush on the skin, flare, then sink in.
    /// Full record: <c>Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md</c>.
    ///
    /// ── It draws the crystal's own geometry ───────────────────────────────────────────────────
    /// Each crystal model's mesh is copied vertex for vertex (every UV channel included — the
    /// charge crystal's crease-edge discharge data rides there) and drawn with the crystal's own
    /// shared materials and property block, so frame 0 IS the crystal. Only positions and normals
    /// are rewritten, per plate, as rigid transforms: a plate never deforms, so its faces stay
    /// planar and the charge shader's bolts keep running along its edges on the hull.
    ///
    /// ── The plates ride the BONES, not the vessel root ────────────────────────────────────────
    /// The hull is a skinned, puppeteered mesh. Each plate's spot is read off a bake of the hull
    /// in the pose it had at collection, then pinned to the bone that dominates that vertex, so a
    /// plate on a wing stays on the wing while it flaps. That needs the hull mesh CPU-readable
    /// (bone weights); an unreadable hull still fuses, pinned to the renderer instead.
    ///
    /// ── Cost ──────────────────────────────────────────────────────────────────────────────────
    /// Once per pickup: one <c>BakeMesh</c> of the hull, a farthest-point spread of 60 spots over
    /// ≤4k candidate vertices, and a 60×60 optimal assignment. Per frame, for ~0.6-1 s: 60 plate poses and one vertex/normal
    /// upload of the crystal's ~2.9k vertices. A one-shot, per pickup, not a standing cost — which
    /// is why this is CPU rather than a shader stamp (the ScarabCrystalMorph route); see the doc §5.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrystalHullFusion : MonoBehaviour
    {
        // Every crystal shader exposes the tint pair and the dissolve (Crystal.cs drives them);
        // only the charge shader exposes the discharge pair, and those writes are skipped elsewhere.
        static readonly int OpacityId = Shader.PropertyToID("_opacity");
        static readonly int DullId = Shader.PropertyToID("_DullCrystalColor");
        static readonly int BrightId = Shader.PropertyToID("_BrightCrystalColor");
        static readonly int ArcIntensityId = Shader.PropertyToID("_ArcIntensity");
        static readonly int ArcDutyId = Shader.PropertyToID("_ArcDuty");

        /// <summary>Hull vertices the spot search samples at most. The Squirrel's hull is ~13k.</summary>
        const int MaxSpotCandidates = 4096;

        static readonly Dictionary<Mesh, CrystalHullFusionGeometry.PlateSet> s_plates = new();
        static readonly Dictionary<Mesh, int[]> s_dominantBones = new();
        static readonly HashSet<string> s_warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            s_plates.Clear();
            s_dominantBones.Clear();
            s_warned.Clear();
        }

        sealed class Shell
        {
            public Transform Source;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public MaterialPropertyBlock Block;
            public CrystalHullFusionGeometry.PlateSet Plates;
            public int FirstPlate;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Color StartDull, StartBright;
            public bool HasTint;
            public float BaseArcIntensity, BaseArcDuty;
            public bool HasArcIntensity, HasArcDuty;
        }

        struct Plate
        {
            // Crystal-relative, in HULL space (rotation only), at collection scale.
            public Vector3 Offset;
            public Quaternion StartRotation;
            /// <summary>Plate-local units → world units at collection.</summary>
            public float StartScale;
            public float LandScale;

            // The spot, in hull space as baked.
            public Vector3 TargetHull;
            public Quaternion TargetRotationHull;
            public float Delay01;

            // The same spot pinned to the bone that carries it.
            public Transform Bone;
            public Vector3 TargetBone;
            public Quaternion TargetRotationBone;
            public Vector3 NormalBone;

            // Written each frame.
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        CrystalHullFusionConfigSO.Entry _entry;
        SkinnedMeshRenderer _hull;
        readonly List<Shell> _shells = new();
        Plate[] _plates;

        Vector3 _crystalCentreWorld;     // where the crystal was collected
        Vector3 _landHull;               // where it lands, hull space
        Vector3 _spinAxisHull;
        Vector3 _hullCentre, _hullExtents;
        Vector3 _invExtents;
        float _footScale;                // plate-local units → world, landed
        float _landedThickness;          // world units
        Color _targetDull, _targetBright;
        bool _haveTargetColour;
        float _startTime;

        /// <summary>Seconds from start to the clamp beat — when the pickup sound belongs.</summary>
        public float ClampDelaySeconds => _entry.ClampSeconds;

        /// <summary>Where the crystal touches the hull, live.</summary>
        public Vector3 LandingWorldPosition => _hull ? HullToWorld(_landHull) : _crystalCentreWorld;

        Vector3 HullToWorld(Vector3 p) => _hull.transform.position + _hull.transform.rotation * p;

        /// <summary>
        /// Starts a fusion of <paramref name="crystal"/> onto the hull of the vessel described by
        /// <paramref name="vesselStatus"/>, and hides the crystal's own renderers (it is still the
        /// caller's to retire). Returns null — leaving the crystal untouched, so the caller falls
        /// back to the generic capture — whenever the fusion cannot land honestly, and every such
        /// exit is warned ONCE per reason: they all look identical on screen (the old capture
        /// plays) and the difference is not recoverable afterwards.
        /// </summary>
        public static CrystalHullFusion Begin(Crystal crystal, IVesselStatus vesselStatus,
                                              CrystalHullFusionConfigSO.Entry entry)
        {
            if (crystal == null || vesselStatus == null || entry == null) return null;

            var animation = vesselStatus.VesselAnimation;
            var hull = animation ? FindHullRenderer(animation.transform) : null;
            if (!hull)
            {
                WarnOnce($"nohull:{vesselStatus.VesselType}",
                    $"[CrystalHullFusion] {vesselStatus.VesselType} has no visible SkinnedMeshRenderer " +
                    "under its VesselAnimation, so there is no hull to fuse onto - the generic capture plays.");
                return null;
            }

            var baked = new Mesh { name = "CrystalHullFusion_HullBake" };
            hull.BakeMesh(baked, true);
            if (baked.vertexCount == 0)
            {
                Destroy(baked);
                string meshName = hull.sharedMesh ? hull.sharedMesh.name : "(none)";
                WarnOnce($"bake:{meshName}",
                    $"[CrystalHullFusion] baking '{hull.name}' produced no vertices, so the hull cannot " +
                    $"be measured - the generic capture plays. Check Read/Write on the model importer of '{meshName}'.");
                return null;
            }

            var go = new GameObject($"CrystalHullFusion_{crystal.name}");
            var fusion = go.AddComponent<CrystalHullFusion>();
            fusion._entry = entry;
            fusion._hull = hull;

            bool ok = fusion.AdoptShells(crystal) && fusion.Plan(crystal, vesselStatus, baked);
            Destroy(baked);
            if (!ok)
            {
                Destroy(go);
                return null;
            }

            // The crystal is now drawn by the fusion. It stays alive (hidden) so its owner can
            // retire it on the clamp beat - the pickup sound and the cell bookkeeping are its own.
            foreach (var renderer in crystal.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            fusion._startTime = Time.time;
            fusion.LateUpdate(); // frame 0 is drawn THIS frame - the crystal is already hidden

            if (CSDebug.IsVerbose(CSLogChannel.CrystalMorph))
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalHullFusion] {vesselStatus.VesselType}/{entry.element}: '{crystal.name}' " +
                $"fusing {fusion._plates.Length} plates onto '{hull.name}' over {entry.TotalSeconds:F2}s " +
                $"(footprint x{fusion._footScale:F3}, domain colour {(fusion._haveTargetColour ? "read" : "NOT FOUND")}).");
            return fusion;
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

        /// <summary>
        /// Copies each crystal model onto a child of this object — the crystal's mesh (cloned, so
        /// every UV channel comes along), its shared materials and its property block, which is
        /// where <c>Crystal.ApplyColorSetTint</c> paints the collectability colour.
        /// </summary>
        bool AdoptShells(Crystal crystal)
        {
            var models = crystal.CrystalModels;
            if (models == null) return false;

            int plateTotal = 0;
            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (model == null) continue;
                if (!model.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                if (!model.TryGetComponent<MeshRenderer>(out var source)) continue;

                var sourceMesh = filter.sharedMesh;
                var plates = PlatesFor(sourceMesh);
                if (plates == null) continue;

                var shellObject = new GameObject($"Shell{i}");
                shellObject.transform.SetParent(transform, false);
                shellObject.layer = model.layer;

                var mesh = Instantiate(sourceMesh);
                mesh.name = $"{sourceMesh.name} (Fusion)";
                mesh.MarkDynamic();
                shellObject.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = shellObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;

                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block);

                var shell = new Shell
                {
                    Source = model.transform,
                    Renderer = renderer,
                    Mesh = mesh,
                    Block = block,
                    Plates = plates,
                    FirstPlate = plateTotal,
                    Vertices = new Vector3[sourceMesh.vertexCount],
                    Normals = new Vector3[sourceMesh.vertexCount],
                };

                // Start colours: what the crystal is DRAWING, i.e. the block over the material.
                var material = source.sharedMaterial;
                bool materialTint = material && material.HasProperty(DullId) && material.HasProperty(BrightId);
                shell.HasTint = materialTint || (block.HasColor(DullId) && block.HasColor(BrightId));
                shell.StartDull = block.HasColor(DullId) ? block.GetColor(DullId)
                                : materialTint ? material.GetColor(DullId) : Color.white;
                shell.StartBright = block.HasColor(BrightId) ? block.GetColor(BrightId)
                                  : materialTint ? material.GetColor(BrightId) : Color.white;
                shell.HasArcIntensity = material && material.HasProperty(ArcIntensityId);
                shell.HasArcDuty = material && material.HasProperty(ArcDutyId);
                if (shell.HasArcIntensity) shell.BaseArcIntensity = material.GetFloat(ArcIntensityId);
                if (shell.HasArcDuty) shell.BaseArcDuty = material.GetFloat(ArcDutyId);

                _shells.Add(shell);
                plateTotal += plates.PlateCount;
            }

            if (_shells.Count > 0) return true;

            WarnOnce($"shells:{crystal.name}",
                $"[CrystalHullFusion] '{crystal.name}' exposed no CPU-readable model with a MeshFilter " +
                "and MeshRenderer, so there is nothing to fuse - the generic capture plays.");
            return false;
        }

        static CrystalHullFusionGeometry.PlateSet PlatesFor(Mesh mesh)
        {
            if (s_plates.TryGetValue(mesh, out var cached)) return cached;
            CrystalHullFusionGeometry.PlateSet plates = null;
            if (mesh.isReadable)
            {
                var triangles = new List<int[]>(mesh.subMeshCount);
                for (int s = 0; s < mesh.subMeshCount; s++) triangles.Add(mesh.GetTriangles(s));
                plates = CrystalHullFusionGeometry.BuildPlates(mesh.vertices, mesh.normals, triangles);
            }
            s_plates[mesh] = plates;
            return plates;
        }

        /// <summary>
        /// Lays the whole fusion out against a bake of the hull taken THIS frame: where the crystal
        /// lands, which hull spot each plate takes, the bone that will carry it, and the sizes.
        /// </summary>
        bool Plan(Crystal crystal, IVesselStatus vesselStatus, Mesh baked)
        {
            var hullVertices = baked.vertices;
            var hullNormals = baked.normals;
            var bounds = baked.bounds;
            _hullCentre = bounds.center;
            _hullExtents = bounds.extents;
            _invExtents = CrystalHullFusionGeometry.InverseExtents(_hullExtents);
            float hullMeanRadius = (_hullExtents.x + _hullExtents.y + _hullExtents.z) / 3f;

            Quaternion hullRotation = _hull.transform.rotation;
            Quaternion toHull = Quaternion.Inverse(hullRotation);

            // The pose the crystal HAD when it was collected - a host respawn may already have moved
            // the transform this frame (Crystal.CollectPose).
            var pose = crystal.CollectPose;
            Matrix4x4 crystalWorld = Matrix4x4.TRS(pose.position, pose.rotation, crystal.CollectScale);
            Matrix4x4 worldToCrystal = crystal.transform.worldToLocalMatrix;

            int plateCount = 0;
            foreach (var shell in _shells) plateCount += shell.Plates.PlateCount;
            _plates = new Plate[plateCount];

            // Crystal centre and radius at collection, from the first shell (every model of a crystal
            // shares the crystal's centre).
            Matrix4x4 firstModel = ModelMatrix(crystalWorld, worldToCrystal, _shells[0].Source);
            _crystalCentreWorld = firstModel.MultiplyPoint3x4(_shells[0].Plates.Centre);

            // Where the crystal lands: the outermost hull spot facing the side it came from.
            Vector3 toCrystalHull = toHull * (_crystalCentreWorld - HullToWorld(_hullCentre));
            Vector3 pole = Vector3.Scale(toCrystalHull, _invExtents);
            pole = pole.sqrMagnitude > 1e-10f ? pole.normalized : Vector3.up;

            int stride = Mathf.Max(1, Mathf.CeilToInt(hullVertices.Length / (float)MaxSpotCandidates));
            float coneCos = Mathf.Cos(_entry.spotConeDegrees * Mathf.Deg2Rad);
            int landIndex = CrystalHullFusionGeometry.SelectHullSpot(hullVertices, hullNormals, _hullCentre,
                _hullExtents, pole, coneCos, stride);
            if (landIndex < 0) return false;

            float landedRadius = hullMeanRadius * _entry.landRadiusFraction;
            Vector3 landNormal = SafeNormal(hullNormals, landIndex, hullVertices[landIndex] - _hullCentre);
            _landHull = hullVertices[landIndex] + landNormal * landedRadius;

            _spinAxisHull = Vector3.Cross(pole, Mathf.Abs(pole.y) < 0.9f ? Vector3.up : Vector3.right).normalized;

            // Pass 1 - each plate's start, and the direction it wraps toward: plates keep their
            // angular distance from the contact, so the one that touched stays and the far side
            // closes round the back. The crystal's sphere is read AS the hull's normalised sphere.
            var wraps = new Vector3[plateCount];
            for (int s = 0; s < _shells.Count; s++)
            {
                var shell = _shells[s];
                var plates = shell.Plates;
                Matrix4x4 model = ModelMatrix(crystalWorld, worldToCrystal, shell.Source);
                Quaternion modelRotation = model.rotation;
                float modelScale = UniformScale(model);
                float landScale = landedRadius / Mathf.Max(1e-5f, plates.Radius);

                for (int p = 0; p < plates.PlateCount; p++)
                {
                    int index = shell.FirstPlate + p;
                    Vector3 radialHull = (toHull * (modelRotation * plates.Radials[p])).normalized;
                    wraps[index] = CrystalHullFusionGeometry.WrapDirection(radialHull, pole);
                    _plates[index] = new Plate
                    {
                        Offset = toHull * (model.MultiplyPoint3x4(plates.Centroids[p]) - _crystalCentreWorld),
                        StartRotation = toHull * (modelRotation * plates.Frames[p]),
                        StartScale = modelScale,
                        LandScale = landScale,
                        // Contact plate first, antipode last: the crystal opens from where it touched.
                        Delay01 = 0.5f * (1f - Vector3.Dot(radialHull, -pole)),
                    };
                }
            }

            // Pass 2 - spots spread evenly over the skin from the contact, then the optimal match of
            // plates to spots by how far each would have to turn (1 - cos).
            var spots = CrystalHullFusionGeometry.FarthestPointSpots(hullVertices, hullNormals, _hullCentre,
                landIndex, plateCount, stride, out float spacing);
            var cost = new float[plateCount, plateCount];
            for (int k = 0; k < plateCount; k++)
            {
                Vector3 q = Vector3.Scale(hullVertices[spots[k]] - _hullCentre, _invExtents);
                q = q.sqrMagnitude > 1e-12f ? q.normalized : pole;
                for (int i = 0; i < plateCount; i++) cost[i, k] = 1f - Vector3.Dot(wraps[i], q);
            }
            var assignment = CrystalHullFusionGeometry.AssignMinCost(cost);

            // Plate size: neighbours meet at the tightest spacing (tileFill 1), so a bigger hull gets
            // bigger plates rather than gaps.
            var firstPlates = _shells[0].Plates;
            float footprintWorld = 0.5f * spacing * _entry.tileFill;
            if (footprintWorld <= 1e-5f) footprintWorld = hullMeanRadius * 0.1f;
            _footScale = footprintWorld / Mathf.Max(1e-5f, firstPlates.FootprintRadius);
            _landedThickness = firstPlates.Thickness * _footScale * _entry.flatten;

            var dominantBones = DominantBones(_hull.sharedMesh);
            var bones = _hull.bones;
            Transform fallbackBone = _hull.rootBone ? _hull.rootBone : _hull.transform;

            // Pass 3 - each plate's landed pose, pinned to the bone that carries its spot.
            for (int i = 0; i < plateCount; i++)
            {
                ref var plate = ref _plates[i];
                int spot = spots[assignment[i]];

                Vector3 normal = SafeNormal(hullNormals, spot, hullVertices[spot] - _hullCentre);
                plate.TargetHull = hullVertices[spot] + normal * (_landedThickness * (0.5f + _entry.surfaceLift));
                plate.TargetRotationHull =
                    Quaternion.FromToRotation(plate.StartRotation * Vector3.forward, normal) * plate.StartRotation;

                Transform bone = fallbackBone;
                if (dominantBones != null && spot < dominantBones.Length)
                {
                    int boneIndex = dominantBones[spot];
                    if (bones != null && boneIndex >= 0 && boneIndex < bones.Length && bones[boneIndex])
                        bone = bones[boneIndex];
                }

                plate.Bone = bone;
                plate.TargetBone = bone.InverseTransformPoint(HullToWorld(plate.TargetHull));
                plate.TargetRotationBone = Quaternion.Inverse(bone.rotation) * (hullRotation * plate.TargetRotationHull);
                plate.NormalBone = bone.InverseTransformDirection(hullRotation * normal);
            }

            _haveTargetColour = _entry.convergeToDomainColour
                && crystal.TryGetDomainCrystalColors(vesselStatus.Domain, out _targetBright, out _targetDull);
            return true;
        }

        /// <summary>The model's world matrix with the crystal at its COLLECT pose rather than its
        /// live one: model-relative-to-crystal is pose-invariant, the crystal's pose is not.</summary>
        static Matrix4x4 ModelMatrix(Matrix4x4 crystalWorld, Matrix4x4 worldToCrystal, Transform model) =>
            model ? crystalWorld * (worldToCrystal * model.localToWorldMatrix) : crystalWorld;

        static float UniformScale(Matrix4x4 m) =>
            (m.GetColumn(0).magnitude + m.GetColumn(1).magnitude + m.GetColumn(2).magnitude) / 3f;

        static Vector3 SafeNormal(Vector3[] normals, int index, Vector3 fallback)
        {
            Vector3 n = normals != null && index < normals.Length ? normals[index] : fallback;
            if (n.sqrMagnitude < 1e-10f) n = fallback;
            return n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
        }

        /// <summary>Per vertex, the bone with the greatest weight. Needs the mesh readable; null
        /// otherwise, and every plate then rides the renderer instead.</summary>
        static int[] DominantBones(Mesh mesh)
        {
            if (!mesh) return null;
            if (s_dominantBones.TryGetValue(mesh, out var cached)) return cached;

            int[] dominant = null;
            if (mesh.isReadable)
            {
                var weights = mesh.boneWeights;
                if (weights is { Length: > 0 })
                {
                    dominant = new int[weights.Length];
                    for (int v = 0; v < weights.Length; v++)
                    {
                        var w = weights[v];
                        int bone = w.boneIndex0; float best = w.weight0;
                        if (w.weight1 > best) { bone = w.boneIndex1; best = w.weight1; }
                        if (w.weight2 > best) { bone = w.boneIndex2; best = w.weight2; }
                        if (w.weight3 > best) { bone = w.boneIndex3; }
                        dominant[v] = bone;
                    }
                }
            }
            else
            {
                WarnOnce($"bones:{mesh.name}",
                    $"[CrystalHullFusion] '{mesh.name}' is not CPU-readable, so fused plates cannot be " +
                    "pinned to the bones that carry them and will ride the renderer instead - a plate on " +
                    "a moving limb will drift off it. Enable Read/Write on the model importer.");
            }
            s_dominantBones[mesh] = dominant;
            return dominant;
        }

        void LateUpdate()
        {
            if (!_hull || _plates == null) { Destroy(gameObject); return; }

            float elapsed = Time.time - _startTime;
            var phase = _entry.Resolve(elapsed, out float u);
            if (phase == CrystalHullFusionConfigSO.Phase.Done) { Destroy(gameObject); return; }

            Transform hullTransform = _hull.transform;
            Quaternion hullRotation = hullTransform.rotation;
            Vector3 anchor = hullTransform.position;
            transform.SetPositionAndRotation(anchor, Quaternion.identity);
            transform.localScale = Vector3.one;

            for (int i = 0; i < _plates.Length; i++)
            {
                ref var plate = ref _plates[i];
                if (!plate.Bone) { Destroy(gameObject); return; }
                PosePlate(ref plate, phase, u, hullRotation);
            }

            foreach (var shell in _shells) WriteShell(shell, anchor);
            WriteMaterial(phase, u);
        }

        void PosePlate(ref Plate plate, CrystalHullFusionConfigSO.Phase phase, float u, Quaternion hullRotation)
        {
            var e = _entry;
            Vector3 boneTarget = plate.Bone.TransformPoint(plate.TargetBone);
            Quaternion boneRotation = plate.Bone.rotation * plate.TargetRotationBone;
            Vector3 landed = new(_footScale, _footScale, _footScale * e.flatten);

            switch (phase)
            {
                case CrystalHullFusionConfigSO.Phase.Approach:
                {
                    // The crystal, whole, pulled in on an accelerating curve and turning WITH the
                    // vessel (its orientation is held in hull space) so it lands in the orientation
                    // the wrap was planned against. Whole turns only - see approachSpinTurns.
                    float pull = Mathf.Pow(u, e.approachAcceleration);
                    float pop = 1f + e.approachPop * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 2.5f));
                    float scale = Mathf.Lerp(plate.StartScale, plate.LandScale, CrystalHullFusionConfigSO.Smooth(u)) * pop;
                    Quaternion spin = Quaternion.AngleAxis(e.approachSpinTurns * 360f * CrystalHullFusionConfigSO.Smooth(u), _spinAxisHull);
                    Vector3 centre = Vector3.LerpUnclamped(_crystalCentreWorld, HullToWorld(_landHull), pull);

                    plate.Position = centre + hullRotation * (spin * plate.Offset * (scale / plate.StartScale));
                    plate.Rotation = hullRotation * (spin * plate.StartRotation);
                    plate.Scale = Vector3.one * scale;
                    break;
                }

                case CrystalHullFusionConfigSO.Phase.Wrap:
                {
                    float p = e.PlateWrapProgress(u, plate.Delay01);
                    float s = CrystalHullFusionConfigSO.Smooth(p);

                    // Slide round the hull in its NORMALISED space - a great circle there, bowed
                    // outward mid-flight, is a path that hugs the skin instead of cutting through it.
                    Vector3 startHull = _landHull + plate.Offset * (plate.LandScale / plate.StartScale);
                    Vector3 qs = Vector3.Scale(startHull - _hullCentre, _invExtents);
                    Vector3 qt = Vector3.Scale(plate.TargetHull - _hullCentre, _invExtents);
                    float rs = qs.magnitude, rt = qt.magnitude;
                    Vector3 dir = CrystalHullFusionGeometry.SlerpDirection(
                        rs > 1e-6f ? qs / rs : Vector3.up, rt > 1e-6f ? qt / rt : Vector3.up, s, plate.Offset);
                    float radius = Mathf.Lerp(rs, rt, s) + e.wrapLift * Mathf.Sin(Mathf.PI * s);
                    Vector3 pathHull = _hullCentre + Vector3.Scale(dir * radius, _hullExtents);

                    // The path is planned against the hull as baked; the bone has moved since. Hand
                    // the plate over to the live bone as it arrives, so it lands ON the limb.
                    Vector3 drift = boneTarget - HullToWorld(plate.TargetHull);
                    plate.Position = HullToWorld(pathHull) + drift * s;

                    Quaternion pathRotation = hullRotation * Quaternion.Slerp(plate.StartRotation, plate.TargetRotationHull, s);
                    Quaternion correction = boneRotation * Quaternion.Inverse(hullRotation * plate.TargetRotationHull);
                    plate.Rotation = Quaternion.Slerp(Quaternion.identity, correction, s) * pathRotation;
                    plate.Scale = Vector3.Lerp(Vector3.one * plate.LandScale, landed, s);
                    break;
                }

                case CrystalHullFusionConfigSO.Phase.Hold:
                {
                    float swell = 1f + (e.clampPulse - 1f) * Mathf.Sin(Mathf.PI * u);
                    plate.Position = boneTarget;
                    plate.Rotation = boneRotation;
                    plate.Scale = landed * swell;
                    break;
                }

                default: // Sink - flatten into the skin.
                {
                    float s = CrystalHullFusionConfigSO.EaseIn(u);
                    Vector3 normal = plate.Bone.TransformDirection(plate.NormalBone).normalized;
                    plate.Position = boneTarget - normal * (e.sinkDepth * _landedThickness * s);
                    plate.Rotation = boneRotation;
                    plate.Scale = new Vector3(landed.x * (1f - 0.15f * s), landed.y * (1f - 0.15f * s),
                                              landed.z * Mathf.Lerp(1f, 0.1f, s));
                    break;
                }
            }
        }

        void WriteShell(Shell shell, Vector3 anchor)
        {
            var plates = shell.Plates;
            var local = plates.LocalPositions;
            var localNormals = plates.LocalNormals;
            var vertexPlate = plates.VertexPlate;

            for (int v = 0; v < local.Length; v++)
            {
                ref var plate = ref _plates[shell.FirstPlate + vertexPlate[v]];
                Vector3 scale = plate.Scale;
                shell.Vertices[v] = plate.Position + plate.Rotation * Vector3.Scale(scale, local[v]) - anchor;

                // Inverse-transpose of a rotation times a diagonal scale: divide, rotate, renormalise.
                Vector3 n = localNormals[v];
                n = new Vector3(n.x / Mathf.Max(1e-5f, scale.x), n.y / Mathf.Max(1e-5f, scale.y), n.z / Mathf.Max(1e-5f, scale.z));
                shell.Normals[v] = (plate.Rotation * n).normalized;
            }

            shell.Mesh.vertices = shell.Vertices;
            shell.Mesh.normals = shell.Normals;
            shell.Mesh.RecalculateBounds();
        }

        void WriteMaterial(CrystalHullFusionConfigSO.Phase phase, float u)
        {
            var e = _entry;

            // Colour: the crystal's pair carried onto the pilot's over the wrap, so it has become
            // theirs by the time it clamps.
            float converge = phase switch
            {
                CrystalHullFusionConfigSO.Phase.Approach => 0f,
                CrystalHullFusionConfigSO.Phase.Wrap => CrystalHullFusionConfigSO.Smooth(u),
                _ => 1f,
            };
            float hold = Mathf.Lerp(e.flareGain, 1.4f, CrystalHullFusionConfigSO.EaseOut(u));
            float flare = phase switch
            {
                CrystalHullFusionConfigSO.Phase.Approach => Mathf.Lerp(1f, 1.3f, u),
                CrystalHullFusionConfigSO.Phase.Wrap => Mathf.Lerp(1.3f, 1.6f, u),
                CrystalHullFusionConfigSO.Phase.Hold => hold,
                _ => Mathf.Lerp(1.4f, 1f, u),
            };
            float discharge = phase switch
            {
                CrystalHullFusionConfigSO.Phase.Hold => 1f,
                CrystalHullFusionConfigSO.Phase.Sink => 1f - CrystalHullFusionConfigSO.Smooth(u),
                _ => 0f,
            };
            float opacity = phase == CrystalHullFusionConfigSO.Phase.Sink ? 1f - CrystalHullFusionConfigSO.EaseIn(u) : 1f;

            foreach (var shell in _shells)
            {
                var renderer = shell.Renderer;
                if (!renderer) continue;
                var block = shell.Block;
                renderer.GetPropertyBlock(block);

                if (shell.HasTint)
                {
                    Color dull = shell.StartDull, bright = shell.StartBright;
                    if (_haveTargetColour)
                    {
                        dull = Color.Lerp(dull, _targetDull, converge);
                        bright = Color.Lerp(bright, _targetBright, converge);
                    }
                    block.SetColor(DullId, dull.ScaleRGB(flare));
                    block.SetColor(BrightId, bright.ScaleRGB(flare));
                }
                if (shell.HasArcIntensity)
                    block.SetFloat(ArcIntensityId, shell.BaseArcIntensity * Mathf.Lerp(1f, e.arcBoost, discharge));
                if (shell.HasArcDuty)
                    block.SetFloat(ArcDutyId, Mathf.Lerp(shell.BaseArcDuty, e.holdArcDuty, discharge));
                block.SetFloat(OpacityId, opacity);

                renderer.SetPropertyBlock(block);
            }
        }

        void OnDestroy()
        {
            foreach (var shell in _shells)
                if (shell.Mesh) Destroy(shell.Mesh);
            _shells.Clear();
        }

        static void WarnOnce(string key, string message)
        {
            if (s_warned.Add(key)) CSDebug.LogWarning(message);
        }
    }
}
