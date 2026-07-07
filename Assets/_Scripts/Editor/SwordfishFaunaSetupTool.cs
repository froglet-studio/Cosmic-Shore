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
    /// mirroring the MassSharkFauna assembly it succeeds as the Blob (menu) apex predator:
    ///
    ///   • FBX import pass — loops the SwrdFsh_Move take (continuous swim) and imports the
    ///     SwrdFsh_Charge take alongside it.
    ///   • Animator controller (SwordFish_A.controller) — default looping Swim state (+ a Charge
    ///     state, unwired, for future attack juice).
    ///   • Root LightFauna — diet Predator, 45s starvation clock, Runtime Cell Data + a new
    ///     MassSwordfishFaunaDataSO (fastest fauna in the sea: the swordfish out-swims the shark).
    ///   • Prism body — DynamicHealthBlocks along the body and DangerBlocks along the bill (the
    ///     sword IS the weapon; danger prisms are dangerous to everyone, per the locked design),
    ///     parented under the nearest skeleton bones so they ride the swim animation. Body prisms
    ///     bloom in via PrismScaleAnimator (continuity: nothing pops in) and are registered mass
    ///     (Fauna.NotifyBodyPrismsMoved keeps the spatial index honest).
    ///   • One Spindle per SkinnedMeshRenderer (RenderedObject wired) so death runs the sealed
    ///     wither path — extremity-first spindle dissolve, never a pop — and spawn gets the
    ///     dissolve-in (Spindle.CondenseCoroutine).
    ///   • A dormant authored CrystalMass child (Crystal + SphereCollider disabled, exactly like
    ///     the shark's) — the locked "every lifeform drops one elemental crystal" invariant;
    ///     the sealed Fauna.Die activates it on any death path.
    ///   • Blob Swordfish Fauna Config Data (FaunaConfigurationSO, apex-tier numbers mirroring
    ///     the shark: seed floor 1, cap 2, births on 6 kills) and swaps the shark entry for the
    ///     swordfish in the Blob Cell Spawn Profile. The shark assets stay authored for other
    ///     biomes; only the menu flagship slot changes hands.
    ///
    /// Run via Tools ▸ Cosmic Shore ▸ Build Swordfish Flagship Fauna. Idempotent: the prefab is
    /// rebuilt in place (same GUID), existing SO assets keep their human-tuned values and only
    /// re-point at the rebuilt prefab. After running, run Tools ▸ Cosmic Shore ▸ Validate
    /// Lifeform Crystals.
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
        const string AccentMaterialPath = "Assets/_Graphics/Materials/CrystalMaterials/BlueMassCrystalMaterial.mat";

        // --- Body layout ------------------------------------------------------------------------
        // Shark-scale creature. Stations are fractions of body length measured from the NOSE
        // (bill tip): the sword carries DangerBlocks, the body carries DynamicHealthBlocks.
        // 3 + 5 = 8 body prisms vs the shark's 10 — collider budget strictly ≤ the slot it takes.
        const float TargetBodyLength = 30f;
        static readonly float[] BillStations = { 0.03f, 0.10f, 0.18f };
        static readonly float[] BodyStations = { 0.32f, 0.45f, 0.58f, 0.71f, 0.85f };
        const float SlabHalfWidthFraction = 0.05f; // vertex slab sampled around each station

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
                var model = AssembleModel(root, fbx, controller, out var bodyLength);
                AttachBodyPrisms(root, model, bodyLength);
                AttachSpindles(root, model);
                AttachDormantCrystal(root);
                var faunaData = EnsureFaunaData();
                ConfigureLightFauna(root, faunaData);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var faunaConfig = EnsureBlobConfig(prefab.GetComponent<LightFauna>());
                WireBlobSpawnProfile(faunaConfig);

                AssetDatabase.SaveAssets();
                Debug.Log($"[SwordfishFauna] Built {PrefabPath} (body length {bodyLength:F1}), " +
                          $"wired {BlobConfigPath} into the Blob spawn profile apex slot. " +
                          "Verify: bill (DangerBlocks) points +Z / nose-forward — if the heuristic guessed the " +
                          "wrong end, rotate the Model child 180° about Y and re-save. Then run " +
                          "Tools ▸ Cosmic Shore ▸ Validate Lifeform Crystals.");
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
            {
                // The swim cycle must loop; the charge/attack take stays a one-shot.
                clip.loopTime = clip.name.Contains("Move");
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static RuntimeAnimatorController BuildAnimatorController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing) AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var stateMachine = controller.layers[0].stateMachine;

            var fbxClips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToList();

            var moveClip = fbxClips.FirstOrDefault(c => c.name.Contains("Move"));
            var chargeClip = fbxClips.FirstOrDefault(c => c.name.Contains("Charge"));

            if (moveClip)
            {
                var swim = stateMachine.AddState("Swim");
                swim.motion = moveClip;
                swim.speed = 2f; // brisk cruise — the flagship reads fast even at Calm
                stateMachine.defaultState = swim;
            }
            else
            {
                Debug.LogWarning("[SwordfishFauna] No SwrdFsh_Move clip found on the FBX — the fish will T-pose.");
            }

            if (chargeClip)
            {
                // Authored but unwired: future attack juice can transition to it without re-authoring.
                var charge = stateMachine.AddState("Charge");
                charge.motion = chargeClip;
            }

            return controller;
        }

        // --- Model: orient nose-forward (+Z), scale to shark-class length ------------------------
        static GameObject AssembleModel(GameObject root, GameObject fbx, RuntimeAnimatorController controller, out float bodyLength)
        {
            var model = Object.Instantiate(fbx, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // LightFauna owns locomotion; the clip only sways the body

            // Replace the raw FBX materials with the lifeform look: SpindleMaterial carries the
            // _DeathAnimation dissolve the Spindle wither/condense path drives, so the mesh
            // blooms in on spawn and evaporates on death instead of popping.
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
            var accentMaterial = AssetDatabase.LoadAssetAtPath<Material>(AccentMaterialPath);
            if (bodyMaterial)
            {
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = i == 0 || !accentMaterial ? bodyMaterial : accentMaterial;
                    renderer.sharedMaterials = materials;
                }
            }
            else
            {
                Debug.LogWarning($"[SwordfishFauna] Missing {BodyMaterialPath} — keeping the raw FBX materials " +
                                 "(no _DeathAnimation dissolve; the spindle wither/condense will be invisible).");
            }

            // Orient: find the mesh's long axis, then decide which end is the nose — the bill is
            // a long thin spike, so the end with the smaller mean cross-section radius is the front.
            var vertices = CollectLocalVertices(model.transform);
            if (vertices.Count == 0)
            {
                Debug.LogWarning("[SwordfishFauna] No mesh vertices found — skipping orientation/scale.");
                bodyLength = TargetBodyLength;
                return model;
            }

            var (axis, min, max) = LongestAxis(vertices);
            var noseDir = NoseDirection(vertices, axis, min, max);
            model.transform.localRotation = RotationToForward(noseDir);

            // Uniform-scale so nose→tail spans TargetBodyLength, measured in ROOT space so any
            // FBX import scale on the model root is already factored in.
            var rootVertices = CollectRootSpaceVertices(root.transform, model.transform);
            float rawLength = rootVertices.Max(v => v.z) - rootVertices.Min(v => v.z);
            if (rawLength > 0.001f)
                model.transform.localScale *= TargetBodyLength / rawLength;
            bodyLength = TargetBodyLength;
            return model;
        }

        // --- Body prisms: DangerBlocks on the sword, DynamicHealthBlocks along the body ----------
        static void AttachBodyPrisms(GameObject root, GameObject model, float bodyLength)
        {
            var healthBlockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBlockPrefabPath);
            var dangerBlockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DangerBlockPrefabPath);
            if (!healthBlockPrefab || !dangerBlockPrefab)
            {
                Debug.LogError("[SwordfishFauna] Missing DynamicHealthBlock/DangerBlock prefabs — no body prisms placed.");
                return;
            }

            // Vertices in ROOT space (model is now oriented nose = +Z and scaled).
            var vertices = CollectRootSpaceVertices(root.transform, model.transform);
            if (vertices.Count == 0) return;

            float zMax = vertices.Max(v => v.z); // nose
            float zMin = vertices.Min(v => v.z); // tail
            float length = Mathf.Max(0.001f, zMax - zMin);
            var bones = CollectBones(model);

            foreach (float t in BillStations)
                PlaceBlock(dangerBlockPrefab, $"DangerBlock Bill {t:F2}", root, bones, vertices,
                    zMax - t * length, length, minCross: 1f, maxCross: 2.5f);

            foreach (float t in BodyStations)
                PlaceBlock(healthBlockPrefab, $"DynamicHealthBlock Body {t:F2}", root, bones, vertices,
                    zMax - t * length, length, minCross: 1.5f, maxCross: 7f);
        }

        static void PlaceBlock(GameObject blockPrefab, string name, GameObject root,
            List<Transform> bones, List<Vector3> vertices, float zStation, float bodyLength,
            float minCross, float maxCross)
        {
            float halfSlab = SlabHalfWidthFraction * bodyLength;
            var slab = vertices.Where(v => Mathf.Abs(v.z - zStation) <= halfSlab).ToList();
            if (slab.Count == 0) return; // gap in the silhouette — nothing to wrap a prism around

            Vector3 center = slab.Aggregate(Vector3.zero, (acc, v) => acc + v) / slab.Count;
            float extentX = slab.Max(v => v.x) - slab.Min(v => v.x);
            float extentY = slab.Max(v => v.y) - slab.Min(v => v.y);
            var worldScale = new Vector3(
                Mathf.Clamp(extentX * 0.8f, minCross, maxCross),
                Mathf.Clamp(extentY * 0.8f, minCross, maxCross),
                bodyLength * 2f * SlabHalfWidthFraction);

            var block = (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab);
            block.name = name;
            block.transform.SetPositionAndRotation(root.transform.TransformPoint(center), root.transform.rotation);

            var parent = NearestBone(bones, block.transform.position) ?? root.transform;
            block.transform.SetParent(parent, worldPositionStays: true);

            // Bones inherit the model's uniform scale — divide it back out so the authored
            // block size is a world-space size (mirrors the shark's hand-authored blocks).
            var parentScale = parent.lossyScale;
            block.transform.localScale = new Vector3(
                worldScale.x / SafeAxis(parentScale.x),
                worldScale.y / SafeAxis(parentScale.y),
                worldScale.z / SafeAxis(parentScale.z));

            // Grow-to-authored-scale on Initialize: the body blooms in, nothing pops (continuity).
            var scaleAnimator = block.GetComponentInChildren<PrismScaleAnimator>(true);
            if (scaleAnimator)
            {
                var so = new SerializedObject(scaleAnimator);
                so.FindProperty("usePrefabScaleAsDefaultTarget").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // --- Spindles: one per renderer so the wither path dissolves the mesh, never pops it -----
        static void AttachSpindles(GameObject root, GameObject model)
        {
            var container = new GameObject("Spindles");
            container.transform.SetParent(root.transform, false);

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var spindleGo = new GameObject($"Spindle {renderer.name}");
                spindleGo.transform.SetParent(container.transform, false);
                spindleGo.transform.position = renderer.bounds.center;
                var spindle = spindleGo.AddComponent<Spindle>();
                spindle.RenderedObject = renderer;
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

            var crystalGo = (GameObject)PrefabUtility.InstantiatePrefab(crystalPrefab, root.transform);
            crystalGo.name = "CrystalMass";
            crystalGo.transform.localPosition = Vector3.zero;
            crystalGo.transform.localRotation = Quaternion.identity;
            crystalGo.transform.localScale = Vector3.one * 5f;

            // Same dormant state the shark authors: component + pickup collider off while carried;
            // Crystal.ActivateCrystal re-enables both and reparents to the cell on death.
            var crystal = crystalGo.GetComponentInChildren<Crystal>(true);
            if (crystal)
            {
                crystal.enabled = false;
                if (crystal.TryGetComponent(out SphereCollider pickup))
                    pickup.enabled = false;
            }
        }

        // --- Root LightFauna ----------------------------------------------------------------------
        static void ConfigureLightFauna(GameObject root, LightFaunaDataSO faunaData)
        {
            var fauna = root.AddComponent<LightFauna>();
            var so = new SerializedObject(fauna);
            so.FindProperty("cellData").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(CellDataPath);
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
            data.minSpeed = 30f; // the swordfish is the fastest thing in the sea — out-swims the shark (25/35)
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
                // Apex-tier numbers, 1:1 with the shark slot it replaces (Docs/ECOSYSTEM.md §7.2):
                // the taming balance and predator pressure on the herbivores are unchanged.
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

            // The flagship takes the shark's apex slot; the shark stays authored for other biomes.
            var sharkConfig = AssetDatabase.LoadAssetAtPath<FaunaConfigurationSO>(BlobSharkConfigPath);
            int slot = sharkConfig ? profile.SupportedFaunas.IndexOf(sharkConfig) : -1;
            if (slot >= 0)
                profile.SupportedFaunas[slot] = swordfishConfig;
            else
                profile.SupportedFaunas.Add(swordfishConfig);
            EditorUtility.SetDirty(profile);
        }

        // --- Mesh analysis helpers -----------------------------------------------------------------

        static List<Vector3> CollectLocalVertices(Transform modelRoot)
        {
            var vertices = new List<Vector3>();
            foreach (var smr in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AppendVertices(vertices, smr.sharedMesh, smr.transform, modelRoot);
            foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>(true))
                AppendVertices(vertices, mf.sharedMesh, mf.transform, modelRoot);
            return vertices;
        }

        static List<Vector3> CollectRootSpaceVertices(Transform root, Transform model)
        {
            var vertices = new List<Vector3>();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AppendVertices(vertices, smr.sharedMesh, smr.transform, root);
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                AppendVertices(vertices, mf.sharedMesh, mf.transform, root);
            return vertices;
        }

        static void AppendVertices(List<Vector3> into, Mesh mesh, Transform meshTransform, Transform space)
        {
            if (!mesh) return;
            var toSpace = space.worldToLocalMatrix * meshTransform.localToWorldMatrix;
            foreach (var v in mesh.vertices)
                into.Add(toSpace.MultiplyPoint3x4(v));
        }

        static (int axis, float min, float max) LongestAxis(List<Vector3> vertices)
        {
            Vector3 min = vertices[0], max = vertices[0];
            foreach (var v in vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            var size = max - min;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            return (axis, min[axis], max[axis]);
        }

        /// <summary>
        /// The bill is a long thin spike, so the body end with the smaller mean cross-section
        /// radius is the nose. Returns the model-local direction the nose points in.
        /// </summary>
        static Vector3 NoseDirection(List<Vector3> vertices, int axis, float min, float max)
        {
            float band = (max - min) * 0.15f;
            int a = (axis + 1) % 3, b = (axis + 2) % 3;

            // Mean cross-section radius of the end band, measured from the band's own
            // centroid so a centerline offset from the origin doesn't skew the comparison.
            float RadiusNear(float station)
            {
                var slab = vertices.Where(v => Mathf.Abs(v[axis] - station) <= band).ToList();
                if (slab.Count == 0) return 0f;
                float ca = slab.Average(v => v[a]);
                float cb = slab.Average(v => v[b]);
                return slab.Average(v => Mathf.Sqrt((v[a] - ca) * (v[a] - ca) + (v[b] - cb) * (v[b] - cb)));
            }

            bool noseAtMax = RadiusNear(max) <= RadiusNear(min);
            var dir = Vector3.zero;
            dir[axis] = noseAtMax ? 1f : -1f;
            return dir;
        }

        static Quaternion RotationToForward(Vector3 noseDir)
        {
            if (noseDir == Vector3.forward) return Quaternion.identity;
            if (noseDir == Vector3.back) return Quaternion.Euler(0f, 180f, 0f);
            if (noseDir == Vector3.right) return Quaternion.Euler(0f, -90f, 0f);
            if (noseDir == Vector3.left) return Quaternion.Euler(0f, 90f, 0f);
            if (noseDir == Vector3.up) return Quaternion.Euler(90f, 0f, 0f);
            return Quaternion.Euler(-90f, 0f, 0f); // Vector3.down
        }

        static List<Transform> CollectBones(GameObject model)
        {
            return model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(smr => smr.bones)
                .Where(b => b)
                .Distinct()
                .ToList();
        }

        static Transform NearestBone(List<Transform> bones, Vector3 worldPosition)
        {
            Transform best = null;
            float bestSqr = float.PositiveInfinity;
            foreach (var bone in bones)
            {
                float sqr = (bone.position - worldPosition).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = bone; }
            }
            return best;
        }

        static float SafeAxis(float value) => Mathf.Abs(value) < 0.0001f ? 1f : value;
    }
}
