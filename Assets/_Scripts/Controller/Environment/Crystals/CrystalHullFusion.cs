using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
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
    /// pentagon caps). That face is a panel and flies; the solid's other faces are filler and fold
    /// into it during the peel, so what leaves the crystal is a cloud of loose faces.
    ///
    /// ── A panel lands ON the hull's surface, bent to its shape ───────────────────────────────
    /// Each panel takes a patch of the skin (patches farthest-point spread over the hull, panels
    /// matched to them optimally). The panel is drawn as a SUBDIVIDED fan
    /// (<see cref="CrystalHullFusionGeometry.FusionTemplate"/>); every point of it is laid in the
    /// patch's tangent plane at the patch's size and then projected onto the closest point of the
    /// hull's own triangles, wearing the hull's interpolated normal there. So the landed face lies
    /// on the model's real surface with the model's real shading - it reads as part of the hull,
    /// not as a tile on top of it. (A flat three-triangle pentagon cannot do this: on the Squirrel
    /// its corners sat a median 0.31 patch radii off the skin.)
    ///
    /// ── Every point rides its own bone ────────────────────────────────────────────────────────
    /// The hull is skinned and puppeteered. Targets are read off a bake of the hull taken at
    /// collection and pinned to the bone that dominates the nearest hull vertex, so a face on a wing
    /// stays on the wing while it flaps. That needs the hull mesh CPU-readable (bone weights); an
    /// unreadable hull still fuses, pinned to the renderer. The projection is spread over the peel's
    /// frames against the bake-time pose, so the pickup frame pays only for the layout.
    ///
    /// ── It draws the crystal's own geometry ───────────────────────────────────────────────────
    /// The crystal's mesh is cloned vertex for vertex (every UV channel - the charge crystal's
    /// crease-edge discharge rides there) and drawn with its own shared materials and property
    /// block, so frame 0 IS the crystal. The DRAWN charge mesh is an uploaded, unreadable twin
    /// (<see cref="CrystalEdgeArcMeshBaker"/>); the readable copy comes from
    /// <see cref="CrystalEdgeArcMeshBaker.TryGetReadable"/>. The first cut of this class checked
    /// <c>isReadable</c> on the drawn mesh, refused every charge crystal, and fell back to the
    /// generic capture - which looked exactly like the effect it was meant to replace.
    ///
    /// ── Why CPU ───────────────────────────────────────────────────────────────────────────────
    /// The target is a skinned hull at flight speed, so a target stamped into a UV channel (the
    /// omni morph's GPU route) would be stale the frame after it was written, and the charge shader
    /// already spends TEXCOORD1-3 on its discharge. This is a one-shot per pickup: ~1.2 s of ~16k
    /// vertex writes a frame.
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

        /// <summary>Hull vertices the patch spread samples at most. The Squirrel's hull is ~13k.</summary>
        const int MaxSpotCandidates = 4096;

        /// <summary>Levels each fan triangle of a panel is cut into. 4 gives a pentagon 41 points and
        /// 80 triangles - enough to bend over the Squirrel at its patch size (every point projected,
        /// measured), few enough that 60 panels stay ~16k vertices.</summary>
        const int PanelSubdivisions = 4;

        /// <summary>How far a laid point may be from the skin and still be projected onto it, in
        /// patch radii.</summary>
        const float ProjectionReach = 1.5f;

        /// <summary>Panels projected per frame during the peel; the rest are finished the frame the
        /// flight starts.</summary>
        const int PanelsPlannedPerFrame = 8;

        static readonly Dictionary<Mesh, CrystalHullFusionGeometry.PanelSet> s_panels = new();
        static readonly Dictionary<Mesh, CrystalHullFusionGeometry.FusionTemplate> s_templates = new();
        static readonly Dictionary<Mesh, int[]> s_dominantBones = new();
        static readonly HashSet<string> s_warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            s_panels.Clear();
            s_templates.Clear();
            s_dominantBones.Clear();
            s_warned.Clear();
        }

        sealed class Shell
        {
            public Transform Source;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public MaterialPropertyBlock Block;
            public CrystalHullFusionGeometry.PanelSet Panels;
            public CrystalHullFusionGeometry.FusionTemplate Template;
            public int FirstPanel;
            public int FirstPoint;
            public float PanelScale;          // mesh units → world, at collection
            public Vector3[] StartPositions;  // world, at collection
            public Vector3[] StartNormals;    // world, at collection
            public Vector3[] Vertices;        // written each frame, relative to this object
            public Vector3[] Normals;
            public Color StartDull, StartBright;
            public bool HasTint;
            public float BaseArcIntensity, BaseArcDuty;
            public bool HasArcIntensity, HasArcDuty;
        }

        struct Panel
        {
            public int Shell;
            public int Local;               // index within its shell's panel set
            public Vector3 Lift;            // world offset the peel lifts the face by
            public Vector3 StartCentroid;   // world, at collection
            public Vector3 StartNormal;     // world, at collection
            public float Delay01;
            public int FirstPoint;
            public int PointCount;

            // The patch, in hull space as baked.
            public Vector3 SpotPosition;
            public Vector3 SpotNormal;
            public Vector3 AxisU, AxisV;    // the panel's own axes laid on the patch
            public float LaidScale;         // panel mesh units → patch world units
            public int SpotBone;
            public bool Planned;

            // Written each frame.
            public Vector3 Centroid;
        }

        struct Point
        {
            public Vector3 Start;           // world, at collection (before the peel's lift)
            public int Bone;                // index into _bones
            public Vector3 TargetLocal;     // on the skin, in that bone's frame
            public Vector3 NormalLocal;

            // Written each frame.
            public Vector3 Position;
            public Vector3 Normal;
        }

        CrystalHullFusionConfigSO.Entry _entry;
        SkinnedMeshRenderer _hull;
        readonly List<Shell> _shells = new();
        Panel[] _panels;
        Point[] _points;

        // The hull as baked, for the panels still to be projected.
        CrystalHullFusionGeometry.HullSurface _surface;
        Vector3 _bakePosition;
        Quaternion _bakeRotation;
        Transform[] _bones;                 // last entry is the fallback (root bone / renderer)
        Matrix4x4[] _boneWorldToLocalAtBake;
        Quaternion[] _boneInverseRotationAtBake;
        int[] _dominantBones;
        int _planned;

        Vector3 _landingWorld;
        float _bowDistance;
        float _patchRadius;
        Color _targetDull, _targetBright;
        bool _haveTargetColour;
        float _startTime;

        /// <summary>Seconds from start until every face is down — when the pickup sound belongs.</summary>
        public float MateDelaySeconds => _entry.MateSecondsFromStart;

        /// <summary>The contact patch, in world space, as it was at collection.</summary>
        public Vector3 LandingWorldPosition => _landingWorld;

        /// <summary>
        /// Starts a fusion of <paramref name="crystal"/> onto the hull of the vessel described by
        /// <paramref name="vesselStatus"/>, and hides the crystal's own renderers (it is still the
        /// caller's to retire). Returns null — leaving the crystal untouched, so the caller falls
        /// back to the generic capture — whenever the fusion cannot land honestly. Every such exit
        /// is a WARNING, once per reason: on screen they all look like the old capture, so the
        /// console is the only place the difference shows.
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

            // The crystal is now drawn by the fusion. It stays alive (hidden) so its owner can retire
            // it when the faces are down - the pickup sound and the cell bookkeeping are its own.
            foreach (var renderer in crystal.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            fusion._startTime = Time.time;
            fusion.LateUpdate(); // frame 0 is drawn THIS frame - the crystal is already hidden

            if (CSDebug.IsVerbose(CSLogChannel.CrystalMorph))
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalHullFusion] {vesselStatus.VesselType}/{entry.element}: '{crystal.name}' " +
                    $"peeling {fusion._panels.Length} faces ({fusion._points.Length} points) onto '{hull.name}' " +
                    $"over {entry.TotalSeconds:F2}s (patch radius {fusion._patchRadius:F2}, domain colour " +
                    $"{(fusion._haveTargetColour ? "read" : "NOT FOUND")}).");
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
        /// Builds one shell per crystal model: the fusion template (filler copied, panels subdivided)
        /// drawn with the model's shared materials and property block, which is where
        /// <c>Crystal.ApplyColorSetTint</c> paints the collectability colour.
        /// </summary>
        bool AdoptShells(Crystal crystal)
        {
            var models = crystal.CrystalModels;
            if (models == null) return false;

            string refusal = null;
            int panelTotal = 0, pointTotal = 0;
            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (model == null) continue;
                if (!model.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                if (!model.TryGetComponent<MeshRenderer>(out var source)) continue;

                var drawn = filter.sharedMesh;
                if (!CrystalEdgeArcMeshBaker.TryGetReadable(drawn, out var readable))
                {
                    refusal = $"'{drawn.name}' is not CPU-readable and was not made by CrystalEdgeArcMeshBaker, " +
                              "so its faces cannot be read. Enable Read/Write on its model importer.";
                    continue;
                }

                if (!TryGetTemplate(readable, out var panels, out var template))
                {
                    refusal = $"'{drawn.name}' has no triangles.";
                    continue;
                }

                var shellObject = new GameObject($"Shell{i}");
                shellObject.transform.SetParent(transform, false);
                shellObject.layer = model.layer;

                var mesh = new Mesh
                {
                    name = $"{drawn.name} (Fusion)",
                    hideFlags = HideFlags.DontSave,
                    indexFormat = template.Vertices.Length > 65535
                        ? UnityEngine.Rendering.IndexFormat.UInt32
                        : UnityEngine.Rendering.IndexFormat.UInt16,
                };
                mesh.MarkDynamic();
                mesh.vertices = template.Vertices;
                mesh.normals = template.Normals;
                mesh.SetUVs(1, template.Bary);
                mesh.SetUVs(2, template.EdgeH);
                mesh.SetUVs(3, template.EdgeSeed);
                mesh.subMeshCount = template.SubmeshTriangles.Length;
                for (int s = 0; s < template.SubmeshTriangles.Length; s++)
                    mesh.SetTriangles(template.SubmeshTriangles[s], s, false);
                mesh.RecalculateBounds();
                shellObject.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = shellObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;

                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block);

                int vertexCount = template.Vertices.Length;
                var shell = new Shell
                {
                    Source = model.transform,
                    Renderer = renderer,
                    Mesh = mesh,
                    Block = block,
                    Panels = panels,
                    Template = template,
                    FirstPanel = panelTotal,
                    FirstPoint = pointTotal,
                    StartPositions = new Vector3[vertexCount],
                    StartNormals = new Vector3[vertexCount],
                    Vertices = new Vector3[vertexCount],
                    Normals = new Vector3[vertexCount],
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
                panelTotal += panels.PanelCount;
                pointTotal += template.Points.Length;
            }

            if (_shells.Count > 0) return true;

            WarnOnce($"shells:{crystal.name}",
                $"[CrystalHullFusion] '{crystal.name}' exposed no model whose faces can be read " +
                $"({refusal ?? "no model with a MeshFilter and a MeshRenderer"}) - the generic capture plays.");
            return false;
        }

        static bool TryGetTemplate(Mesh mesh, out CrystalHullFusionGeometry.PanelSet panels,
                                   out CrystalHullFusionGeometry.FusionTemplate template)
        {
            if (s_templates.TryGetValue(mesh, out template))
            {
                panels = s_panels[mesh];
                return template != null;
            }

            var triangles = new List<int[]>(mesh.subMeshCount);
            for (int s = 0; s < mesh.subMeshCount; s++) triangles.Add(mesh.GetTriangles(s));
            var vertices = mesh.vertices;
            panels = CrystalHullFusionGeometry.BuildPanels(vertices, triangles);

            var bary = new List<Vector3>(); var edgeH = new List<Vector3>(); var edgeSeed = new List<Vector3>();
            mesh.GetUVs(1, bary);
            mesh.GetUVs(2, edgeH);
            mesh.GetUVs(3, edgeSeed);
            float modelRadius = Mathf.Max(mesh.bounds.extents.x, Mathf.Max(mesh.bounds.extents.y, mesh.bounds.extents.z));

            template = panels == null ? null : CrystalHullFusionGeometry.BuildTemplate(panels, vertices, mesh.normals,
                bary.Count == vertices.Length ? bary.ToArray() : null,
                edgeH.Count == vertices.Length ? edgeH.ToArray() : null,
                edgeSeed.Count == vertices.Length ? edgeSeed.ToArray() : null,
                triangles, PanelSubdivisions, modelRadius);

            s_panels[mesh] = panels;
            s_templates[mesh] = template;
            return template != null;
        }

        /// <summary>
        /// Lays the fusion out against a bake of the hull taken THIS frame: each face's start in the
        /// world, the patch it takes and how it is laid there, and a snapshot of the hull and its
        /// bones so the projection of every point can be spread over the next frames.
        /// </summary>
        bool Plan(Crystal crystal, IVesselStatus vesselStatus, Mesh baked)
        {
            var hullVertices = baked.vertices;
            var hullNormals = baked.normals;
            var bounds = baked.bounds;
            Vector3 hullCentre = bounds.center;
            Vector3 hullExtents = bounds.extents;
            Vector3 invExtents = CrystalHullFusionGeometry.InverseExtents(hullExtents);
            float hullMeanRadius = (hullExtents.x + hullExtents.y + hullExtents.z) / 3f;
            _bowDistance = hullMeanRadius * _entry.flightBow;

            Transform hullTransform = _hull.transform;
            _bakePosition = hullTransform.position;
            _bakeRotation = hullTransform.rotation;
            Quaternion toHull = Quaternion.Inverse(_bakeRotation);

            // The pose the crystal HAD when it was collected - a host respawn may already have moved
            // the transform this frame (Crystal.CollectPose).
            var pose = crystal.CollectPose;
            Matrix4x4 crystalWorld = Matrix4x4.TRS(pose.position, pose.rotation, crystal.CollectScale);
            Matrix4x4 worldToCrystal = crystal.transform.worldToLocalMatrix;

            int panelCount = 0, pointCount = 0;
            foreach (var shell in _shells)
            {
                panelCount += shell.Panels.PanelCount;
                pointCount += shell.Template.Points.Length;
            }
            _panels = new Panel[panelCount];
            _points = new Point[pointCount];

            Matrix4x4 firstModel = ModelMatrix(crystalWorld, worldToCrystal, _shells[0].Source);
            Vector3 crystalCentre = firstModel.MultiplyPoint3x4(_shells[0].Panels.Centre);
            float crystalRadius = _shells[0].Panels.Radius * UniformScale(firstModel);

            // The contact patch: the outermost skin facing the side the crystal came from.
            Vector3 pole = Vector3.Scale(toHull * (crystalCentre - (_bakePosition + _bakeRotation * hullCentre)), invExtents);
            pole = pole.sqrMagnitude > 1e-10f ? pole.normalized : Vector3.up;
            int stride = Mathf.Max(1, Mathf.CeilToInt(hullVertices.Length / (float)MaxSpotCandidates));
            int contact = CrystalHullFusionGeometry.SelectHullSpot(hullVertices, hullNormals, hullCentre,
                hullExtents, pole, Mathf.Cos(_entry.spotConeDegrees * Mathf.Deg2Rad), stride);
            if (contact < 0) return false;
            _landingWorld = _bakePosition + _bakeRotation * hullVertices[contact];

            // Pass 1 - every face's start in the world and the direction it heads for on the hull
            // (the crystal's sphere read as the hull's normalised one, reflected through the contact
            // so the face nearest the hull stays nearest).
            var wraps = new Vector3[panelCount];
            for (int si = 0; si < _shells.Count; si++)
            {
                var shell = _shells[si];
                var set = shell.Panels;
                var template = shell.Template;
                Matrix4x4 model = ModelMatrix(crystalWorld, worldToCrystal, shell.Source);
                shell.PanelScale = UniformScale(model);

                for (int v = 0; v < template.Vertices.Length; v++)
                {
                    shell.StartPositions[v] = model.MultiplyPoint3x4(template.Vertices[v]);
                    shell.StartNormals[v] = model.MultiplyVector(template.Normals[v]).normalized;
                }

                for (int p = 0; p < set.PanelCount; p++)
                {
                    int index = shell.FirstPanel + p;
                    Vector3 radial = model.MultiplyVector(set.Radials[p]).normalized;
                    Vector3 radialHull = (toHull * radial).normalized;
                    Vector3 centroidMesh = set.PanelCentroids[p];
                    wraps[index] = CrystalHullFusionGeometry.WrapDirection(radialHull, pole);

                    _panels[index] = new Panel
                    {
                        Shell = si,
                        Local = p,
                        Lift = radial * (crystalRadius * _entry.peelDistance),
                        StartCentroid = model.MultiplyPoint3x4(centroidMesh),
                        StartNormal = model.MultiplyVector(set.PanelNormals[p]).normalized,
                        // The face nearest the hull lands first; the far side closes last.
                        Delay01 = 0.5f * (1f + Vector3.Dot(radialHull, pole)),
                        FirstPoint = shell.FirstPoint + template.PointStart[p],
                        PointCount = template.PointCount[p],
                    };

                    for (int k = 0; k < template.PointCount[p]; k++)
                    {
                        Vector2 q = template.Points[template.PointStart[p] + k];
                        _points[shell.FirstPoint + template.PointStart[p] + k].Start = model.MultiplyPoint3x4(
                            centroidMesh + template.AxisU[p] * q.x + template.AxisV[p] * q.y);
                    }
                }
            }

            // Pass 2 - patches spread evenly over the skin from the contact, then the optimal match of
            // faces to patches by how far each would have to turn (1 - cos).
            var spots = CrystalHullFusionGeometry.FarthestPointSpots(hullVertices, hullNormals, hullCentre,
                contact, panelCount, stride, out float spacing);
            var cost = new float[panelCount, panelCount];
            for (int k = 0; k < panelCount; k++)
            {
                Vector3 q = Vector3.Scale(hullVertices[spots[k]] - hullCentre, invExtents);
                q = q.sqrMagnitude > 1e-12f ? q.normalized : pole;
                for (int i = 0; i < panelCount; i++) cost[i, k] = 1f - Vector3.Dot(wraps[i], q);
            }
            var assignment = CrystalHullFusionGeometry.AssignMinCost(cost);

            _patchRadius = 0.5f * spacing * _entry.tileFill;
            if (_patchRadius <= 1e-5f) _patchRadius = hullMeanRadius * 0.1f;

            // Snapshot the bones at the bake, so a panel projected two frames from now still pins
            // against the pose its targets were read in.
            _dominantBones = DominantBones(_hull.sharedMesh);
            var bones = _hull.bones ?? new Transform[0];
            _bones = new Transform[bones.Length + 1];
            for (int b = 0; b < bones.Length; b++) _bones[b] = bones[b];
            _bones[bones.Length] = _hull.rootBone ? _hull.rootBone : hullTransform;
            _boneWorldToLocalAtBake = new Matrix4x4[_bones.Length];
            _boneInverseRotationAtBake = new Quaternion[_bones.Length];
            for (int b = 0; b < _bones.Length; b++)
            {
                if (!_bones[b]) continue;
                _boneWorldToLocalAtBake[b] = _bones[b].worldToLocalMatrix;
                _boneInverseRotationAtBake[b] = Quaternion.Inverse(_bones[b].rotation);
            }

            // Pass 3 - lay each face on its patch (the projection itself is deferred).
            for (int i = 0; i < panelCount; i++)
            {
                ref var panel = ref _panels[i];
                int spot = spots[assignment[i]];
                Vector3 spotNormal = SafeNormal(hullNormals, spot, hullVertices[spot] - hullCentre);
                var shell = _shells[panel.Shell];

                // The panel's own axes, carried onto the patch by the smallest turn that takes its
                // normal to the skin's, so it keeps its twist from the crystal.
                Quaternion turn = Quaternion.FromToRotation(toHull * panel.StartNormal, spotNormal);
                Vector3 u = turn * (toHull * (shell.Source ? ModelRotation(crystalWorld, worldToCrystal, shell.Source) : Quaternion.identity)
                                     * shell.Template.AxisU[panel.Local]);
                u = (u - Vector3.Dot(u, spotNormal) * spotNormal).normalized;
                if (u.sqrMagnitude < 0.5f) u = Vector3.Cross(spotNormal, Mathf.Abs(spotNormal.x) < 0.9f ? Vector3.right : Vector3.up).normalized;

                panel.SpotPosition = hullVertices[spot];
                panel.SpotNormal = spotNormal;
                panel.AxisU = u;
                panel.AxisV = Vector3.Cross(spotNormal, u);
                panel.LaidScale = _patchRadius / Mathf.Max(1e-6f, shell.Panels.PanelRadius[panel.Local]);
                panel.SpotBone = BoneIndexFor(spot);
            }

            _surface = new CrystalHullFusionGeometry.HullSurface(hullVertices, hullNormals, baked.triangles, _patchRadius);

            _haveTargetColour = _entry.convergeToDomainColour
                && crystal.TryGetDomainCrystalColors(vesselStatus.Domain, out _targetBright, out _targetDull);
            return true;
        }

        int BoneIndexFor(int vertex)
        {
            int fallback = _bones.Length - 1;
            if (_dominantBones == null || vertex < 0 || vertex >= _dominantBones.Length) return fallback;
            int b = _dominantBones[vertex];
            return b >= 0 && b < fallback && _bones[b] ? b : fallback;
        }

        /// <summary>
        /// Projects one face's points onto the skin as baked and pins each to its bone. Runs during
        /// the peel, a few faces a frame, against the bake-time snapshot.
        /// </summary>
        void PlanPanel(int index)
        {
            ref var panel = ref _panels[index];
            if (panel.Planned) return;
            panel.Planned = true;

            var template = _shells[panel.Shell].Template;
            int firstTemplatePoint = template.PointStart[panel.Local];
            float lift = _entry.surfaceLift * _patchRadius;
            float reach = ProjectionReach * _patchRadius;

            for (int k = 0; k < panel.PointCount; k++)
            {
                Vector2 q = template.Points[firstTemplatePoint + k];
                Vector3 laid = panel.SpotPosition + (panel.AxisU * q.x + panel.AxisV * q.y) * panel.LaidScale;

                Vector3 surface, normal;
                int bone;
                if (_surface.TryProject(laid, panel.SpotNormal, reach, out surface, out normal, out int nearest))
                    bone = BoneIndexFor(nearest);
                else
                {
                    surface = laid;
                    normal = panel.SpotNormal;
                    bone = panel.SpotBone;
                }

                Vector3 worldAtBake = _bakePosition + _bakeRotation * (surface + normal * lift);
                ref var point = ref _points[panel.FirstPoint + k];
                point.Bone = bone;
                point.TargetLocal = _boneWorldToLocalAtBake[bone].MultiplyPoint3x4(worldAtBake);
                point.NormalLocal = _boneInverseRotationAtBake[bone] * (_bakeRotation * normal);
            }
        }

        /// <summary>The model's world matrix with the crystal at its COLLECT pose rather than its
        /// live one: model-relative-to-crystal is pose-invariant, the crystal's pose is not.</summary>
        static Matrix4x4 ModelMatrix(Matrix4x4 crystalWorld, Matrix4x4 worldToCrystal, Transform model) =>
            model ? crystalWorld * (worldToCrystal * model.localToWorldMatrix) : crystalWorld;

        static Quaternion ModelRotation(Matrix4x4 crystalWorld, Matrix4x4 worldToCrystal, Transform model) =>
            ModelMatrix(crystalWorld, worldToCrystal, model).rotation;

        static float UniformScale(Matrix4x4 m) =>
            (m.GetColumn(0).magnitude + m.GetColumn(1).magnitude + m.GetColumn(2).magnitude) / 3f;

        static Vector3 SafeNormal(Vector3[] normals, int index, Vector3 fallback)
        {
            Vector3 n = normals != null && index >= 0 && index < normals.Length ? normals[index] : fallback;
            if (n.sqrMagnitude < 1e-10f) n = fallback;
            return n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
        }

        /// <summary>Per vertex, the bone with the greatest weight. Needs the mesh readable; null
        /// otherwise, and every point then rides the renderer instead.</summary>
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
                    $"[CrystalHullFusion] '{mesh.name}' is not CPU-readable, so fused faces cannot be " +
                    "pinned to the bones that carry them and will ride the renderer instead - a face on " +
                    "a moving limb will drift off it. Enable Read/Write on the model importer.");
            }
            s_dominantBones[mesh] = dominant;
            return dominant;
        }

        void LateUpdate()
        {
            if (!_hull || _panels == null) { Destroy(gameObject); return; }

            float elapsed = Time.time - _startTime;
            var phase = _entry.Resolve(elapsed, out float u);
            if (phase == CrystalHullFusionConfigSO.Phase.Done) { Destroy(gameObject); return; }

            // Spread the projection over the peel; whatever is left is finished before any face flies.
            int budget = phase == CrystalHullFusionConfigSO.Phase.Peel ? PanelsPlannedPerFrame : int.MaxValue;
            while (_planned < _panels.Length && budget-- > 0) PlanPanel(_planned++);
            if (_planned >= _panels.Length) _surface = null; // the bake is spent - let it go

            for (int b = 0; b < _bones.Length; b++)
                if (!_bones[b]) { Destroy(gameObject); return; }

            Vector3 anchor = _hull.transform.position;
            transform.SetPositionAndRotation(anchor, Quaternion.identity);
            transform.localScale = Vector3.one;

            if (phase != CrystalHullFusionConfigSO.Phase.Peel) PosePoints(phase, u);
            foreach (var shell in _shells) WriteShell(shell, phase, u, anchor);
            WriteMaterial(phase, u);
        }

        void PosePoints(CrystalHullFusionConfigSO.Phase phase, float u)
        {
            var e = _entry;
            for (int i = 0; i < _panels.Length; i++)
            {
                ref var panel = ref _panels[i];
                var spotBone = _bones[panel.SpotBone];
                Vector3 spotNormal = spotBone.rotation * (_boneInverseRotationAtBake[panel.SpotBone] * (_bakeRotation * panel.SpotNormal));
                float f = phase == CrystalHullFusionConfigSO.Phase.Flight
                    ? CrystalHullFusionConfigSO.Smooth(e.FaceFlightProgress(u, panel.Delay01))
                    : 1f;
                Vector3 centroid = Vector3.zero;

                for (int k = 0; k < panel.PointCount; k++)
                {
                    ref var point = ref _points[panel.FirstPoint + k];
                    var bone = _bones[point.Bone];
                    Vector3 target = bone.TransformPoint(point.TargetLocal);
                    Vector3 targetNormal = (bone.rotation * point.NormalLocal).normalized;

                    switch (phase)
                    {
                        case CrystalHullFusionConfigSO.Phase.Flight:
                        {
                            // A quadratic curve whose last leg runs straight down the patch normal: the
                            // face swings out over its patch and comes DOWN onto the skin, rather than
                            // arriving edge-on or through the hull.
                            Vector3 start = point.Start + panel.Lift;
                            Vector3 control = target + spotNormal * _bowDistance;
                            float g = 1f - f;
                            point.Position = g * g * start + 2f * g * f * control + f * f * target;
                            point.Normal = Vector3.Lerp(panel.StartNormal, targetNormal, f).normalized;
                            break;
                        }

                        case CrystalHullFusionConfigSO.Phase.Mate:
                            point.Position = target;
                            point.Normal = targetNormal;
                            break;

                        default: // Dissolve - a hair into the skin.
                            point.Position = target - targetNormal *
                                (e.sinkDepth * _patchRadius * CrystalHullFusionConfigSO.EaseIn(u));
                            point.Normal = targetNormal;
                            break;
                    }
                    centroid += point.Position;
                }
                panel.Centroid = centroid / Mathf.Max(1, panel.PointCount);
            }
        }

        void WriteShell(Shell shell, CrystalHullFusionConfigSO.Phase phase, float u, Vector3 anchor)
        {
            var template = shell.Template;
            var vertexPanel = template.VertexPanel;
            var vertexPoint = template.VertexPoint;
            bool peel = phase == CrystalHullFusionConfigSO.Phase.Peel;
            float lift = CrystalHullFusionConfigSO.EaseOut(u);
            float fold = CrystalHullFusionConfigSO.Smooth(u);

            for (int v = 0; v < vertexPanel.Length; v++)
            {
                ref var panel = ref _panels[shell.FirstPanel + vertexPanel[v]];
                int k = vertexPoint[v];

                if (peel)
                {
                    // Every face lifts off along its solid's radial; the filler folds into its face's
                    // centre as it goes, so what leaves the crystal is loose faces.
                    Vector3 start = shell.StartPositions[v] + panel.Lift * lift;
                    shell.Vertices[v] = (k >= 0 ? start
                        : Vector3.Lerp(start, panel.StartCentroid + panel.Lift * lift, fold)) - anchor;
                    shell.Normals[v] = shell.StartNormals[v];
                    continue;
                }

                if (k >= 0)
                {
                    ref var point = ref _points[shell.FirstPoint + template.PointStart[panel.Local] + k];
                    shell.Vertices[v] = point.Position - anchor;
                    shell.Normals[v] = point.Normal;
                }
                else
                {
                    // Folded away: a degenerate point riding the face, drawing nothing.
                    shell.Vertices[v] = panel.Centroid - anchor;
                    shell.Normals[v] = shell.StartNormals[v];
                }
            }

            shell.Mesh.vertices = shell.Vertices;
            shell.Mesh.normals = shell.Normals;
            shell.Mesh.RecalculateBounds();
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
                    block.SetFloat(ArcDutyId, Mathf.Lerp(shell.BaseArcDuty, e.mateArcDuty, discharge));
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
