using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click setup for the freestyle <b>Toybox</b> in Menu_Main. It:
    ///   1. authors the four built-in toy definitions (Connect-the-Dots painting, Vessel Changer,
    ///      Domain Changer, Wanderway conveyor) under <c>Assets/_SO_Assets/Toys/</c> plus the
    ///      painting gallery under <c>Assets/_SO_Assets/Toys/Paintings/</c>,
    ///   2. creates/loads a <see cref="ToyboxSO"/> at <c>Assets/Resources/Toybox.asset</c> and
    ///      registers the toys on it, and
    ///   3. adds a <see cref="ToyboxController"/> to the Menu_Main scene (on the object carrying
    ///      <c>MenuCrystalClickHandler</c>, else a new root) and points it at the toybox.
    ///
    /// Idempotent - safe to re-run (re-runs also fill newly-added unset content fields and append
    /// missing gallery paintings). The painting toy spawns one <see cref="PaintingToy"/> station
    /// per painting, each driving a multi-stroke <see cref="PaintingRunner"/>.
    /// See Docs/ToySystem/ARCHITECTURE.md.
    /// </summary>
    public static class ToyboxSetupTool
    {
        const string ToysFolder = "Assets/_SO_Assets/Toys";
        const string PaintingsFolder = "Assets/_SO_Assets/Toys/Paintings";
        const string ResourcesFolder = "Assets/Resources";
        const string ToyboxAssetPath = "Assets/Resources/Toybox.asset";
        const string MenuScenePath = "Assets/_Scenes/Menu_Main.unity";

        [MenuItem("FrogletTools/Scene Setup/Setup Freestyle Toybox")]
        [FrogletTool(FrogletToolCategory.SceneSetup, Importance = 4,
            Description = "Author the freestyle toybox assets and wire them into Menu_Main.")]
        static void SetupToybox()
        {
            var gallery = CreatePaintingGallery();
            var painting = LoadOrCreateToy<PaintingToyDefinitionSO>(
                "Toy_Painting", "painting", "Connect the Dots", "Connect the dots to paint 3D masterpieces with your trail.",
                new Color(0.20f, 0.90f, 1.00f), 0f);
            AssignPaintings(painting, gallery); // always - migrates pre-gallery Toy_Painting assets too
            var vessel = LoadOrCreateToy<VesselChangerToyDefinitionSO>(
                "Toy_VesselChanger", "vessel_changer", "Vessel Changer", "Fly through to swap your ship.",
                new Color(1.00f, 0.85f, 0.20f), 120f);
            var domain = LoadOrCreateToy<DomainChangerToyDefinitionSO>(
                "Toy_DomainChanger", "domain_changer", "Domain Changer", "Fly through to change your team colour.",
                new Color(0.85f, 0.30f, 0.90f), 240f);
            var conveyor = LoadOrCreateToy<ConveyorToyDefinitionSO>(
                "Toy_Conveyor", "conveyor", "Wanderway", "Fly through to summon an endless trail of little worlds.",
                new Color(0.35f, 1.00f, 0.55f), 60f, AssignConveyorContent);
            // No content wiring: with no cells authored the toy reads the containing Cell's own
            // CellConfigs rotation, which is the single source of truth for this scene's cell.
            var cellSelector = LoadOrCreateToy<CellSelectorToyDefinitionSO>(
                "Toy_CellSelector", "cell_selector", "Cell Selector",
                "Fly through to pick the world you fly in - or reset it.",
                new Color(0.55f, 0.75f, 1.00f), 300f);

            var toybox = LoadOrCreateToybox();
            RegisterToys(toybox, new ToyDefinitionSO[] { painting, vessel, domain, conveyor, cellSelector });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool wiredScene = AddControllerToMenuScene(toybox);

            EditorUtility.DisplayDialog("Setup Freestyle Toybox",
                "Toybox ready with 5 toys (Connect the Dots, Vessel Changer, Domain Changer, Wanderway, " +
                "Cell Selector).\n\n" +
                $"• Toy assets:  {ToysFolder}/\n" +
                $"• Paintings:   {PaintingsFolder}/ (16 masterpieces: Star → Taj Mahal → Torus Knot, " +
                "Buckyball, Double Helix, Nautilus, Lotus, Rose, Spiral Galaxy, Phoenix, Almighty " +
                "Mountain, Starry Night, Lion's Head, Peacock)\n" +
                $"• Toybox:      {ToyboxAssetPath}\n" +
                (wiredScene
                    ? "• ToyboxController added to Menu_Main and saved.\n"
                    : "• Could not auto-add the ToyboxController - add it to the Menu_Main 'Game' object manually.\n") +
                "\nAll five toys work as-is. The vessel changer shows mini ship models; the domain " +
                "changer shows the two colours you're not; the painting toy spawns one station per " +
                "painting (multi-stroke, multi-domain connect-the-dots with start gates that recolour " +
                "your trail); the Wanderway conveyor streams shuffled microscenes ahead of your flight " +
                "path; the Cell Selector blooms a matrix of mini-cells that swap (or reset) the world " +
                "you fly in.\n\n" +
                "REMINDER: set the Menu_Main Cell's 'Cell Type Choice Options' to EnvironmentFree so " +
                "freestyle boots empty and the heavy worlds stay opt-in.\n" +
                "See Docs/ToySystem/ARCHITECTURE.md.",
                "OK");
        }

        // ── Toy definition assets ────────────────────────────────────────────

        static T LoadOrCreateToy<T>(string fileName, string id, string displayName, string description,
            Color accent, float angleDeg, System.Action<SerializedObject> extra = null) where T : ToyDefinitionSO
        {
            EnsureFolder(ToysFolder);
            string path = $"{ToysFolder}/{fileName}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = false;
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                created = true;
            }

            if (created)
            {
                var so = new SerializedObject(asset);
                SetString(so, "id", id);
                SetString(so, "displayName", displayName);
                SetString(so, "description", description);
                SetColor(so, "accentColor", accent);
                SetBool(so, "unlockedByDefault", true);
                SetFloat(so, "placementAngleDegrees", angleDeg);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }

            // Always fill any UNSET content references (each 'extra' assignment guards for unset), so
            // re-running the tool wires newly-added fields - e.g. the conveyor's omniCrystalPrefab -
            // onto an already-authored asset without clobbering user customisations.
            if (extra != null)
            {
                var so = new SerializedObject(asset);
                extra.Invoke(so);
                if (so.ApplyModifiedProperties())
                    EditorUtility.SetDirty(asset);
            }

            return asset;
        }

        // ── Painting gallery assets ──────────────────────────────────────────

        static List<PaintingDefinitionSO> CreatePaintingGallery()
        {
            EnsureFolder(PaintingsFolder);
            // One source of truth: the runtime catalog on PaintingToyDefinitionSO.
            var gallery = new List<PaintingDefinitionSO>();
            foreach (var spec in PaintingToyDefinitionSO.DefaultGalleryCatalog)
                gallery.Add(LoadOrCreatePainting($"Painting_{spec.Name.Replace(" ", "")}",
                    spec.Id, spec.Name, spec.Description, spec.Preset, spec.Size, spec.Reach));
            return gallery;
        }

        static PaintingDefinitionSO LoadOrCreatePainting(string fileName, string id, string displayName,
            string description, PaintingPreset preset, float size, float reachThreshold)
        {
            string path = $"{PaintingsFolder}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PaintingDefinitionSO>(path);
            if (asset) return asset;

            asset = ScriptableObject.CreateInstance<PaintingDefinitionSO>();
            AssetDatabase.CreateAsset(asset, path);

            var so = new SerializedObject(asset);
            SetString(so, "paintingId", id);
            SetString(so, "displayName", displayName);
            SetString(so, "description", description);
            SetInt(so, "preset", (int)preset);
            SetFloat(so, "presetSize", size);
            SetFloat(so, "reachThreshold", reachThreshold);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void AssignPaintings(PaintingToyDefinitionSO toy, IReadOnlyList<PaintingDefinitionSO> gallery)
        {
            var so = new SerializedObject(toy);
            var list = so.FindProperty("paintings");
            if (list == null) return;

            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
                existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

            foreach (var painting in gallery)
            {
                if (!painting || existing.Contains(painting)) continue;
                int idx = list.arraySize;
                list.arraySize = idx + 1;
                list.GetArrayElementAtIndex(idx).objectReferenceValue = painting;
                existing.Add(painting);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(toy);
        }

        static void AssignConveyorContent(SerializedObject so)
        {
            // Prism prefab: the plain environment prism the Spawnable shapes use.
            var prismProp = so.FindProperty("prismPrefab");
            if (prismProp != null && !prismProp.objectReferenceValue)
            {
                var prism = AssetDatabase.LoadAssetAtPath<Prism>("Assets/_Prefabs/Trails/SpawnablePrism.prefab");
                if (prism) prismProp.objectReferenceValue = prism;
            }

            // Omni crystal prefab: the body-collected jackpot pickup (fuel + speed buff).
            var omniProp = so.FindProperty("omniCrystalPrefab");
            if (omniProp != null && !omniProp.objectReferenceValue)
            {
                var omni = AssetDatabase.LoadAssetAtPath<Crystal>("Assets/_Prefabs/Environment/Crystal.prefab");
                if (omni) omniProp.objectReferenceValue = omni;
            }

            // Crystal-side collection effect: the standard element-level powerup.
            var effectsProp = so.FindProperty("crystalCollectionEffects");
            if (effectsProp != null && effectsProp.arraySize == 0)
            {
                var effect = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/_SO_Assets/Effects/Skimmer Crystal Effects/SkimmerAdjustElementLevelByCrystalEffect.asset");
                if (effect)
                {
                    effectsProp.arraySize = 1;
                    effectsProp.GetArrayElementAtIndex(0).objectReferenceValue = effect;
                }
            }
        }

        // ── Toybox asset ─────────────────────────────────────────────────────

        static ToyboxSO LoadOrCreateToybox()
        {
            EnsureFolder(ResourcesFolder);
            var toybox = AssetDatabase.LoadAssetAtPath<ToyboxSO>(ToyboxAssetPath);
            if (!toybox)
            {
                toybox = ScriptableObject.CreateInstance<ToyboxSO>();
                AssetDatabase.CreateAsset(toybox, ToyboxAssetPath);
            }
            return toybox;
        }

        static void RegisterToys(ToyboxSO toybox, IReadOnlyList<ToyDefinitionSO> toys)
        {
            var so = new SerializedObject(toybox);
            var list = so.FindProperty("toys");

            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
                existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

            foreach (var toy in toys)
            {
                if (!toy || existing.Contains(toy)) continue;
                int idx = list.arraySize;
                list.arraySize = idx + 1;
                list.GetArrayElementAtIndex(idx).objectReferenceValue = toy;
                existing.Add(toy);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(toybox);
        }

        // ── Scene wiring ─────────────────────────────────────────────────────

        static bool AddControllerToMenuScene(ToyboxSO toybox)
        {
            if (!System.IO.File.Exists(MenuScenePath)) return false;

            Scene scene = default;
            bool wasOpen = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == MenuScenePath) { scene = s; wasOpen = true; break; }
            }
            if (!wasOpen)
                scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);
            if (!scene.IsValid()) return false;

            var host = FindControllerHost(scene);
            var controller = host ? host.GetComponent<ToyboxController>() : null;
            if (!controller)
            {
                if (!host)
                {
                    host = new GameObject("FreestyleToybox");
                    SceneManager.MoveGameObjectToScene(host, scene);
                    Undo.RegisterCreatedObjectUndo(host, "Create FreestyleToybox");
                }
                controller = Undo.AddComponent<ToyboxController>(host);
            }

            var so = new SerializedObject(controller);
            var prop = so.FindProperty("toybox");
            if (prop != null && !prop.objectReferenceValue)
            {
                prop.objectReferenceValue = toybox;
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);
            return true;
        }

        /// <summary>Prefer the object carrying MenuCrystalClickHandler (the freestyle hub). Null if none.</summary>
        static GameObject FindControllerHost(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var handler = root.GetComponentInChildren<MenuCrystalClickHandler>(true);
                if (handler) return handler.gameObject;
            }
            return null;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void SetString(SerializedObject so, string field, string value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.stringValue = value;
        }

        static void SetBool(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
        }

        static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
        }

        static void SetColor(SerializedObject so, string field, Color value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.colorValue = value;
        }
    }
}
