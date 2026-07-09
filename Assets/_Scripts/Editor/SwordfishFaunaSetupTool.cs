using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Builds the flagship swordfish fauna (MassSwordfishFauna) from the raw SwordFish_A model,
    /// mirroring how the hand-authored MassSharkFauna sits its prisms on the skeleton — the shark's
    /// blocks are parented to bones (Wing_2.L, MouthTop_2, Body_3, …) and each is oriented + sized
    /// to the body part it wraps (a flat 2×12×6 wing slab, a needle-thin 1×1×15 tooth on the jaw),
    /// NOT axis-aligned boxes clamped to arbitrary sizes. We reproduce that from geometry instead of
    /// by hand:
    ///
    ///   • Per bone-cluster fitting — the tool runs INSIDE Unity, so it reads the live
    ///     SkinnedMeshRenderer (bones + bindposes + boneWeights) and, for each bone, PCA-fits an
    ///     oriented box to the rest-pose vertices that bone actually skins. The prism inherits that
    ///     box's principal axes (so the dorsal slab stands vertical, the pectorals cant outward —
    ///     real per-part orientation, not one boring root rotation) and is scaled to the real local
    ///     silhouette, thinned to a slab, so nothing is oversized.
    ///   • Bill = danger needles — the forward-most cluster (the sword) is tiled with thin DangerBlock
    ///     needles laid END-TO-END along the bill so they don't overlap (each needle spans one
    ///     segment of the bill length). The sword IS the weapon and danger prisms hit everyone, per
    ///     the locked design.
    ///   • Body = DynamicHealthBlock slabs on the other clusters (long clusters split in two for
    ///     coverage), parented to their bone so they ride the swim animation.
    ///   • Prisms bloom in via PrismScaleAnimator (continuity: nothing pops in) and are registered
    ///     mass (Fauna.NotifyBodyPrismsMoved keeps the spatial index honest).
    ///   • One Spindle per SkinnedMeshRenderer (RenderedObject wired) so death runs the sealed
    ///     extremity-first wither dissolve and spawn gets the condense-in.
    ///   • A dormant authored CrystalMass child (Crystal + pickup collider disabled, shark parity) —
    ///     the locked "every lifeform drops one elemental crystal" invariant; Fauna.Die activates it.
    ///   • Root LightFauna — diet Predator, 45s starvation clock, Runtime Cell Data + a new
    ///     MassSwordfishFaunaDataSO (fastest fauna in the sea: out-swims the shark).
    ///   • Blob Swordfish Fauna Config Data (apex numbers 1:1 with the shark slot) swapped into the
    ///     Blob Cell Spawn Profile. The shark assets stay authored for other biomes.
    ///
    /// Run via Tools ▸ Cosmic Shore ▸ Build Swordfish Flagship Fauna, then run
    /// Tools ▸ Cosmic Shore ▸ Validate Lifeform Crystals. Idempotent (rebuilds in place, keeps
    /// human-tuned SO values). Placement is geometry-driven; open the prefab and nudge any prism —
    /// they're parented to the bones exactly like the shark's, so hand-tuning is straightforward.
    /// </summary>
    public static class SwordfishFaunaSetupTool
    {
        // --- Source / output paths ------------------------------------------------------------
        const string FbxPath = "Assets/_Models/Fauna/SwordFish_A.fbx";
        const string PrefabPath = "Assets/_Models/Fauna/MassSwordfishFauna.prefab";
        const string ControllerPath = "Assets/_Models/Fauna/SwordFish_A.controller";
        const string FaunaDataPath = "Assets/_SO_Assets/Light Fauna Data/MassSwordfishFaunaDataSO.asset";
        const string BlobConfigPath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Swordfish Fauna Config Data.asset";
        const string BlobSpawnProfilePath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset";
        const string BlobSharkConfigPath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset";
        const string CellDataPath = "Assets/_SO_Assets/Cell Data/Runtime Cell Data.asset";
        const string HealthBlockPrefabPath = "Assets/_Prefabs/Trails/DynamicHealthBlock.prefab";
        const string DangerBlockPrefabPath = "Assets/_Prefabs/Trails/DangerBlock.prefab";
        const string CrystalPrefabPath = "Assets/_Prefabs/Environment/CrystalMass.prefab";
        const string BodyMaterialPath = "Assets/_Graphics/Materials/SpindleMaterial.mat";

        // --- Tuning ---------------------------------------------------------------------------
        // Apex-sized creature. The model is scaled so its long axis spans TargetBodyLength; every
        // prism is then fitted to the real local silhouette (never clamped to a guessed size).
        const float TargetBodyLength = 34f;      // shark-class length
        const float SlabFitFactor = 0.72f;       // fill ~0.72 of the local silhouette (leaves the mesh proud)
        const float SlabFlatnessFraction = 0.32f;// thinnest axis capped to this fraction of the mid axis → a slab
        const float SplitAspect = 2.6f;          // a non-bill cluster longer than this (major/mid) becomes 2 slabs
        const float MinPrismDim = 0.6f;          // matches DynamicHealthBlock minScale
        const int MinClusterVerts = 8;           // ignore near-empty bones
        const float BillNeedleLength = 9f;        // target length per bill needle → count = billLen / this
        const float BillNeedleGap = 0.85f;        // needle length = segment × this, so consecutive needles don't touch
        const int MaxBillNeedles = 4;

        [MenuItem("Tools/Cosmic Shore/Build Swordfish Flagship Fauna")]
        public static void Build()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath))
            { Debug.LogError($"[SwordfishFauna] Missing model at {FbxPath}"); return; }

            ConfigureImporter();
            var controller = BuildAnimatorController();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath); // reload post-reimport

            var root = new GameObject("MassSwordfishFauna");
            try
            {
                var model = AssembleModel(root, fbx, controller);

                // 1) Analyse rest-pose clusters, orient the model nose(+Z), scale to length,
                //    then re-analyse so placement uses the final world transforms.
                var clusters = AnalyzeBoneClusters(model);
                if (clusters.Count == 0)
                {
                    Debug.LogError("[SwordfishFauna] No skinned bone clusters found — is SwordFish_A rigged? Aborting.");
                    return;
                }
                OrientAndScaleModel(model, clusters);
                clusters = AnalyzeBoneClusters(model);

                PlacePrisms(root, clusters);
                AttachSpindles(root, model);
                AttachDormantCrystal(root);
                ConfigureLightFauna(root, EnsureFaunaData());

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                WireBlobSpawnProfile(EnsureBlobConfig(prefab.GetComponent<LightFauna>()));

                AssetDatabase.SaveAssets();
                Debug.Log($"[SwordfishFauna] Built {PrefabPath} — {clusters.Count} bone clusters fitted; " +
                          "bill rendered as tiled DangerBlock needles. Open the prefab to eyeball the fit " +
                          "(prisms are parented to the bones like the shark; nudge any that read off). " +
                          "Then run Tools ▸ Cosmic Shore ▸ Validate Lifeform Crystals.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // --- FBX import: loop the swim take -----------------------------------------------------
        static void ConfigureImporter()
        {
            if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer) return;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var clip in clips)
                clip.loopTime = clip.name.Contains("Move"); // swim loops; charge stays a one-shot
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static RuntimeAnimatorController BuildAnimatorController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;

            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            var move = clips.FirstOrDefault(c => c.name.Contains("Move"));
            var charge = clips.FirstOrDefault(c => c.name.Contains("Charge"));

            if (move)
            {
                var swim = sm.AddState("Swim");
                swim.motion = move;
                swim.speed = 2f;
                sm.defaultState = swim;
            }
            else Debug.LogWarning("[SwordfishFauna] No SwrdFsh_Move clip — the fish will T-pose.");

            if (charge) sm.AddState("Charge").motion = charge; // authored, unwired, for future attack juice
            return controller;
        }

        // --- Model: instantiate, material, animator (orientation/scale applied later) ------------
        static GameObject AssembleModel(GameObject root, GameObject fbx, RuntimeAnimatorController controller)
        {
            var model = Object.Instantiate(fbx, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var animator = model.GetComponent<Animator>() ? model.GetComponent<Animator>() : model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // LightFauna owns locomotion; the clip only sways the body

            // SpindleMaterial carries the _DeathAnimation dissolve the Spindle wither/condense drives,
            // so the mesh blooms in on spawn and evaporates on death instead of popping.
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
            if (bodyMaterial)
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(bodyMaterial, r.sharedMaterials.Length).ToArray();
            else
                Debug.LogWarning($"[SwordfishFauna] Missing {BodyMaterialPath} — keeping raw FBX materials " +
                                 "(the spindle dissolve will be invisible).");
            return model;
        }

        // --- A fitted oriented box for one bone's rest-pose vertex cluster ------------------------
        class BoneCluster
        {
            public Transform Bone;
            public Vector3 Center;         // world
            public Vector3[] Axes;         // world principal axes, longest→shortest
            public Vector3 Extents;        // span along each axis (Axes[0]..[2])
        }

        /// <summary>
        /// Per bone, gathers the rest-pose world positions of the vertices it dominantly skins and
        /// PCA-fits an oriented box. Rest-pose world vertex = bone.localToWorld · bindpose · vertex
        /// (single-dominant-bone approximation of linear-blend skinning at rest), so it works in the
        /// editor with no Animator playing.
        /// </summary>
        static List<BoneCluster> AnalyzeBoneClusters(GameObject model)
        {
            var result = new List<BoneCluster>();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (!mesh || smr.bones == null || smr.bones.Length == 0) continue;

                var verts = mesh.vertices;
                var weights = mesh.boneWeights;
                var binds = mesh.bindposes;
                var bones = smr.bones;

                var perBone = new Dictionary<int, List<Vector3>>();
                for (int i = 0; i < verts.Length; i++)
                {
                    int b = weights[i].boneIndex0; // dominant bone
                    if (b < 0 || b >= bones.Length || !bones[b]) continue;
                    var world = bones[b].localToWorldMatrix.MultiplyPoint3x4(binds[b].MultiplyPoint3x4(verts[i]));
                    if (!perBone.TryGetValue(b, out var list)) perBone[b] = list = new List<Vector3>();
                    list.Add(world);
                }

                foreach (var kv in perBone)
                {
                    if (kv.Value.Count < MinClusterVerts) continue;
                    var cluster = FitOrientedBox(bones[kv.Key], kv.Value);
                    if (cluster != null) result.Add(cluster);
                }
            }
            return result;
        }

        static BoneCluster FitOrientedBox(Transform bone, List<Vector3> pts)
        {
            Vector3 c = Vector3.zero;
            foreach (var p in pts) c += p;
            c /= pts.Count;

            // Symmetric covariance of the centered cloud.
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p in pts)
            {
                var d = p - c;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            var axes = JacobiEigenvectors(xx, xy, xz, yy, yz, zz);

            // Extent along each principal axis, then sort longest→shortest.
            var withExt = axes.Select(a =>
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (var p in pts) { float t = Vector3.Dot(p - c, a); if (t < min) min = t; if (t > max) max = t; }
                return (axis: a, ext: max - min);
            }).OrderByDescending(t => t.ext).ToArray();

            return new BoneCluster
            {
                Bone = bone,
                Center = c,
                Axes = new[] { withExt[0].axis, withExt[1].axis, withExt[2].axis },
                Extents = new Vector3(withExt[0].ext, withExt[1].ext, withExt[2].ext),
            };
        }

        // --- Orientation & scale: nose → +Z, long axis → TargetBodyLength ------------------------
        static void OrientAndScaleModel(GameObject model, List<BoneCluster> clusters)
        {
            // Body = the cluster with the largest bounding volume; bill = the most elongated cluster
            // farthest from the body centre (the forward spike). Forward = body → bill.
            var body = clusters.OrderByDescending(c => c.Extents.x * c.Extents.y * c.Extents.z).First();
            var bill = clusters.OrderByDescending(c => Elongation(c) * (c.Center - body.Center).magnitude).First();

            Vector3 forward = (bill.Center - body.Center);
            if (forward.sqrMagnitude < 1e-4f) forward = body.Axes[0]; // degenerate: fall back to body long axis
            forward.Normalize();

            // Up = the world-vertical-most principal axis of the body (keeps the dorsal fin upright).
            Vector3 up = body.Axes.OrderByDescending(a => Mathf.Abs(a.y)).First();
            up = (up - Vector3.Dot(up, forward) * forward).normalized;
            if (up.sqrMagnitude < 1e-4f) up = Vector3.up;

            // Rotate the model so its nose faces +Z and the dorsal faces +Y.
            var current = Quaternion.LookRotation(forward, up);
            model.transform.rotation = Quaternion.Inverse(current) * model.transform.rotation;

            // Uniform-scale to the target length using the whole rendered bounds along +Z.
            var b = EncapsulateRenderers(model);
            float rawLength = b.size.z > 1e-3f ? b.size.z : Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (rawLength > 1e-3f)
                model.transform.localScale *= TargetBodyLength / rawLength;
        }

        static float Elongation(BoneCluster c) => c.Extents.x / Mathf.Max(1e-3f, c.Extents.y);

        static Bounds EncapsulateRenderers(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // --- Prism placement: slabs on the body, needles on the bill -----------------------------
        static void PlacePrisms(GameObject root, List<BoneCluster> clusters)
        {
            var healthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBlockPrefabPath);
            var dangerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DangerBlockPrefabPath);
            if (!healthPrefab || !dangerPrefab)
            {
                Debug.LogError("[SwordfishFauna] Missing DynamicHealthBlock/DangerBlock prefabs — no body prisms.");
                return;
            }

            // The bill is the forward-most cluster (after orientation, largest Center.z).
            var bill = clusters.OrderByDescending(c => c.Center.z).First();

            int idx = 0;
            foreach (var c in clusters)
            {
                if (c == bill)
                {
                    PlaceBillNeedles(root, dangerPrefab, c, ref idx);
                    continue;
                }

                // A long body part (e.g. the trunk) gets split into two slabs for coverage; the shark
                // spreads several blocks along its body the same way.
                int segments = Elongation(c) > SplitAspect ? 2 : 1;
                for (int s = 0; s < segments; s++)
                {
                    float f = segments == 1 ? 0f : (s == 0 ? -0.25f : 0.25f);
                    Vector3 center = c.Center + c.Axes[0] * (f * c.Extents.x);
                    float lenZ = c.Extents.x / segments;
                    // World box dims: Z = along the part (major), Y = mid, X = thinnest → a flat slab.
                    float thin = Mathf.Min(c.Extents.z, SlabFlatnessFraction * c.Extents.y);
                    Vector3 worldDims = new Vector3(thin, c.Extents.y, lenZ) * SlabFitFactor;
                    SpawnPrism(healthPrefab, $"BodyBlock {idx++}", root, c.Bone, center,
                        c.Axes[0], c.Axes[1], worldDims);
                }
            }
        }

        static void PlaceBillNeedles(GameObject root, GameObject dangerPrefab, BoneCluster bill, ref int idx)
        {
            float billLen = bill.Extents.x;
            int count = Mathf.Clamp(Mathf.RoundToInt(billLen / BillNeedleLength), 1, MaxBillNeedles);
            float seg = billLen / count;
            // Needle cross-section = the bill's own (thin) minor extents, so it reads as the sword,
            // not a box. Length = one segment × gap, laid END-TO-END so needles never overlap.
            float cross = Mathf.Max(MinPrismDim, Mathf.Min(bill.Extents.y, bill.Extents.z) * SlabFitFactor);
            float needleLen = seg * BillNeedleGap;

            for (int s = 0; s < count; s++)
            {
                // Segment centres from tail-end of the bill to the tip along the major axis.
                float t = -0.5f * billLen + (s + 0.5f) * seg;
                Vector3 center = bill.Center + bill.Axes[0] * t;
                Vector3 worldDims = new Vector3(cross, cross, needleLen);
                SpawnPrism(dangerPrefab, $"BillNeedle {idx++}", root, bill.Bone, center,
                    bill.Axes[0], bill.Axes[1], worldDims);
            }
        }

        /// <summary>
        /// Instantiates a block, parents it to its bone (so it rides the swim animation like the
        /// shark's blocks), orients its local +Z along <paramref name="forwardAxis"/> with +Y along
        /// <paramref name="upAxis"/>, and sets a localScale that realises <paramref name="worldDims"/>
        /// (X,Y,Z) given the bone's world scale. Enables grow-in and lifts the animator's max-scale so
        /// a large slab isn't clamped.
        /// </summary>
        static void SpawnPrism(GameObject prefab, string name, GameObject root, Transform bone,
            Vector3 center, Vector3 forwardAxis, Vector3 upAxis, Vector3 worldDims)
        {
            var block = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            block.name = name;

            var parent = bone ? bone : root.transform;
            block.transform.SetParent(parent, worldPositionStays: true);
            block.transform.position = center;
            block.transform.rotation = SafeLook(forwardAxis, upAxis);

            // worldDims is measured along the block's own local axes; divide out the parent's world
            // scale so localScale realises those world sizes (bones are ~uniformly scaled).
            Vector3 ls = parent.lossyScale;
            Vector3 local = new Vector3(
                Mathf.Max(MinPrismDim, worldDims.x) / SafeAxis(ls.x),
                Mathf.Max(MinPrismDim, worldDims.y) / SafeAxis(ls.y),
                Mathf.Max(MinPrismDim, worldDims.z) / SafeAxis(ls.z));
            block.transform.localScale = local;

            var animator = block.GetComponentInChildren<PrismScaleAnimator>(true);
            if (animator)
            {
                var so = new SerializedObject(animator);
                so.FindProperty("usePrefabScaleAsDefaultTarget").boolValue = true;

                // PrismScaleAnimator.SetTargetScale clamps the authored target (= this localScale) to
                // [minScale, maxScale] (default 0.5–10). The swordfish rig bakes globalScale 100 into
                // the bones, so a body-parented prism's LOCAL scale can land outside that band and get
                // clamped (blown up by min, or truncated by max). Bracket both around the real value so
                // the fitted slab survives verbatim.
                var abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
                var minProp = so.FindProperty("minScale");
                var maxProp = so.FindProperty("maxScale");
                minProp.vector3Value = Vector3.Min(minProp.vector3Value, abs * 0.5f);
                maxProp.vector3Value = Vector3.Max(maxProp.vector3Value, abs + Vector3.one);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static Quaternion SafeLook(Vector3 forward, Vector3 up)
        {
            if (forward.sqrMagnitude < 1e-6f) return Quaternion.identity;
            if (Mathf.Abs(Vector3.Dot(forward.normalized, up.normalized)) > 0.999f)
                up = Mathf.Abs(forward.normalized.y) < 0.9f ? Vector3.up : Vector3.right;
            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        // --- Spindles: one per renderer so the wither path dissolves the mesh, never pops it -----
        static void AttachSpindles(GameObject root, GameObject model)
        {
            var container = new GameObject("Spindles");
            container.transform.SetParent(root.transform, false);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var go = new GameObject($"Spindle {renderer.name}");
                go.transform.SetParent(container.transform, false);
                go.transform.position = renderer.bounds.center;
                go.AddComponent<Spindle>().RenderedObject = renderer;
            }
        }

        // --- Crystal: authored, carried dormant, activated by the sealed Fauna.Die ---------------
        static void AttachDormantCrystal(GameObject root)
        {
            var crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPrefabPath);
            if (!crystalPrefab)
            {
                Debug.LogError("[SwordfishFauna] Missing CrystalMass prefab — the lifeform-crystal invariant is unmet.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(crystalPrefab, root.transform);
            go.name = "CrystalMass";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 5f;

            var crystal = go.GetComponentInChildren<Crystal>(true);
            if (crystal)
            {
                crystal.enabled = false;
                if (crystal.TryGetComponent(out SphereCollider pickup)) pickup.enabled = false;
            }
        }

        // --- Root LightFauna ----------------------------------------------------------------------
        static void ConfigureLightFauna(GameObject root, LightFaunaDataSO faunaData)
        {
            var fauna = root.AddComponent<LightFauna>();
            var so = new SerializedObject(fauna);
            so.FindProperty("cellData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CellDataPath);
            so.FindProperty("diet").intValue = (int)FaunaDiet.Predator;
            so.FindProperty("starvationSeconds").floatValue = 45f; // apex clock, shark parity
            so.FindProperty("data").objectReferenceValue = faunaData;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static LightFaunaDataSO EnsureFaunaData()
        {
            var data = AssetDatabase.LoadAssetAtPath<LightFaunaDataSO>(FaunaDataPath);
            if (data) return data; // keep human-tuned values on re-run

            data = ScriptableObject.CreateInstance<LightFaunaDataSO>();
            data.detectionRadius = 100f;
            data.separationRadius = 100f;
            data.consumeRadius = 40f;
            data.behaviorUpdateRate = 2f;
            data.separationWeight = 20f;
            data.goalWeight = 1.5f;
            data.minSpeed = 30f; // fastest fauna in the sea — out-swims the shark's 25/35
            data.maxSpeed = 45f;
            data.rotationLerpSpeed = 6f;
            data.witherRingInterval = 0.25f;
            AssetDatabase.CreateAsset(data, FaunaDataPath);
            return data;
        }

        static FaunaConfigurationSO EnsureBlobConfig(LightFauna prefabFauna)
        {
            var config = AssetDatabase.LoadAssetAtPath<FaunaConfigurationSO>(BlobConfigPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<FaunaConfigurationSO>();
                // Apex-tier numbers, 1:1 with the shark slot it replaces (Docs/ECOSYSTEM.md §7.2).
                config.InitialSpawnCount = 1;
                config.PopulationSize = 1;
                config.SpawnProbability = 1f;
                config.FeedsPerOffspring = 6;
                config.OffspringPerBirth = 1;
                config.ReproductionCooldownSeconds = 30f;
                config.MaxLivePopulation = 2;
                AssetDatabase.CreateAsset(config, BlobConfigPath);
            }
            config.FaunaPrefab = prefabFauna; // always re-point at the rebuilt prefab
            EditorUtility.SetDirty(config);
            return config;
        }

        static void WireBlobSpawnProfile(FaunaConfigurationSO swordfishConfig)
        {
            var profile = AssetDatabase.LoadAssetAtPath<SpawnProfileSO>(BlobSpawnProfilePath);
            if (!profile)
            {
                Debug.LogError($"[SwordfishFauna] Missing spawn profile at {BlobSpawnProfilePath} — not wired.");
                return;
            }
            if (profile.SupportedFaunas.Contains(swordfishConfig)) return;

            var sharkConfig = AssetDatabase.LoadAssetAtPath<FaunaConfigurationSO>(BlobSharkConfigPath);
            int slot = sharkConfig ? profile.SupportedFaunas.IndexOf(sharkConfig) : -1;
            if (slot >= 0) profile.SupportedFaunas[slot] = swordfishConfig; // flagship takes the apex slot
            else profile.SupportedFaunas.Add(swordfishConfig);
            EditorUtility.SetDirty(profile);
        }

        // --- Symmetric 3×3 eigenvectors (Jacobi) — principal axes of the vertex cloud ------------
        static Vector3[] JacobiEigenvectors(float xx, float xy, float xz, float yy, float yz, float zz)
        {
            // a = symmetric matrix; v accumulates the eigenvector basis.
            double[,] a = { { xx, xy, xz }, { xy, yy, yz }, { xz, yz, zz } };
            double[,] v = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

            for (int sweep = 0; sweep < 32; sweep++)
            {
                // Largest off-diagonal magnitude.
                int p = 0, q = 1;
                double off = System.Math.Abs(a[0, 1]);
                if (System.Math.Abs(a[0, 2]) > off) { off = System.Math.Abs(a[0, 2]); p = 0; q = 2; }
                if (System.Math.Abs(a[1, 2]) > off) { off = System.Math.Abs(a[1, 2]); p = 1; q = 2; }
                if (off < 1e-10) break;

                double app = a[p, p], aqq = a[q, q], apq = a[p, q];
                double phi = 0.5 * System.Math.Atan2(2 * apq, aqq - app);
                double c = System.Math.Cos(phi), s = System.Math.Sin(phi);

                for (int k = 0; k < 3; k++)
                {
                    double akp = a[k, p], akq = a[k, q];
                    a[k, p] = c * akp - s * akq;
                    a[k, q] = s * akp + c * akq;
                }
                for (int k = 0; k < 3; k++)
                {
                    double apk = a[p, k], aqk = a[q, k];
                    a[p, k] = c * apk - s * aqk;
                    a[q, k] = s * apk + c * aqk;
                    double vkp = v[k, p], vkq = v[k, q];
                    v[k, p] = c * vkp - s * vkq;
                    v[k, q] = s * vkp + c * vkq;
                }
            }

            Vector3 Col(int j) => new Vector3((float)v[0, j], (float)v[1, j], (float)v[2, j]).normalized;
            return new[] { Col(0), Col(1), Col(2) };
        }

        static float SafeAxis(float value) => Mathf.Abs(value) < 1e-4f ? 1f : value;
    }
}
