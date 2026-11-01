using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Scenes/Voyage.unity</c>, the voyage's own scene, from <c>Menu_Main</c>: the same flight stack
    /// (network spawn chain, scene camera, flight HUD, a cell to clone corridor worlds from) with every piece of freestyle
    /// and app shell taken out: no toybox, no freestyle toggle, no lava-lamp flora, no screens or modals, no quest runner.
    /// The menu's spawn points become the harbour's berths, the cell boots the empty Barren world, and
    /// <see cref="VoyageScene"/> directs it. Re-run it after a change to Menu_Main's flight stack; it overwrites the scene.
    /// <code>Unity.exe -batchmode -quit -projectPath . -executeMethod CosmicShore.Editor.VoyageSceneBuilder.Build</code>
    /// </summary>
    public static class VoyageSceneBuilder
    {
        const string MenuScenePath = "Assets/_Scenes/Menu_Main.unity";
        const string BarrenConfigPath = "Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset";

        /// <summary>Under the UI's safe area, the only children a voyage keeps: the flight HUD and the toasts.</summary>
        static readonly string[] KeptSafeAreaChildren = { "Game UI", "ToastNotificationContainer" };

        /// <summary>Scene roots that are the menu's alone.</summary>
        static readonly string[] DroppedRoots = { "Quest Runner", "GameModeProgressionService" };

        /// <summary>Components that are freestyle or app shell, removed wherever they stand (dependents first).</summary>
        static readonly Type[] DroppedComponents =
        {
            typeof(ToyboxController),
            typeof(ModePreviewSession),
            typeof(MenuGodModeCamera),
            typeof(ArcadeConfigSyncManager),
            typeof(MenuCrystalClickHandler),
            typeof(MainMenuController),
        };

        [MenuItem("FrogletTools/Scenes/Build Voyage Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>The <c>-executeMethod</c> entry.</summary>
        public static void Build()
        {
            var log = new StringBuilder("[VoyageSceneBuilder] ");
            var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

            var game = Root(scene, "Game") ?? throw new InvalidOperationException("Menu_Main has no 'Game' root.");
            var toggle = game.GetComponent<MenuCrystalClickHandler>();
            var menu = game.GetComponent<MainMenuController>();
            var flightGroups = ReadArray<CanvasGroup>(toggle, "freestyleCanvasGroups");
            var camera = Read<MainMenuCameraController>(toggle, "cameraController");
            var berths = ReadArray<Transform>(menu, "_playerOrigins");
            int intensity = menu ? new SerializedObject(menu).FindProperty("menuIntensity")?.intValue ?? 1 : 1;

            var director = game.AddComponent<VoyageScene>();
            var so = new SerializedObject(director);
            WriteArray(so.FindProperty("berths"), berths);
            WriteArray(so.FindProperty("flightGroups"), flightGroups);
            so.FindProperty("cameraController").objectReferenceValue = camera;
            so.FindProperty("intensity").intValue = Mathf.Max(1, intensity);
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Append($"director: {berths.Length} berths, {flightGroups.Length} flight groups, camera {(camera ? "yes" : "no")}. ");

            foreach (var hud in Object.FindObjectsByType<MenuMiniGameHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var hso = new SerializedObject(hud);
                hso.FindProperty("crystalClickHandler").objectReferenceValue = null;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }

            int removed = RemoveComponents();
            log.Append($"{removed} freestyle/shell components removed. ");

            foreach (var name in DroppedRoots)
                if (Root(scene, name) is { } root) { Object.DestroyImmediate(root); log.Append($"dropped {name}. "); }

            log.Append(StripEnvironment(scene));
            log.Append(StripUi(scene));

            var spawn = game.transform.Find("Spawn Points");
            if (spawn) spawn.name = "Harbour Berths";

            if (!EditorSceneManager.SaveScene(scene, VoyageLaunch.ScenePath))
                throw new InvalidOperationException($"Could not save {VoyageLaunch.ScenePath}.");

            // Reopened and saved again, so every in-scene NetworkObject takes this scene's ids rather than Menu_Main's.
            var voyage = EditorSceneManager.OpenScene(VoyageLaunch.ScenePath, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(voyage);
            EditorSceneManager.SaveScene(voyage);

            log.Append(AddToBuildSettings());
            Debug.Log(log.ToString());
        }

        static int RemoveComponents()
        {
            int removed = 0;
            // A component another one requires cannot go first; a few passes settle the order.
            for (int pass = 0; pass < 4; pass++)
            {
                bool any = false;
                foreach (var type in DroppedComponents)
                    foreach (var c in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (!c) continue;
                        try { Object.DestroyImmediate(c); removed++; any = true; }
                        catch (Exception e) { Debug.LogWarning($"[VoyageSceneBuilder] {type.Name} stays this pass: {e.Message}"); }
                    }
                if (!any) break;
            }
            return removed;
        }

        static string StripEnvironment(Scene scene)
        {
            var env = Root(scene, "Environment");
            if (!env) return "no Environment root. ";
            var sb = new StringBuilder();
            for (int i = env.transform.childCount - 1; i >= 0; i--)
            {
                var child = env.transform.GetChild(i).gameObject;
                if (child.GetComponentInChildren<Cell>(true)) continue;
                sb.Append($"dropped Environment/{child.name}. ");
                Object.DestroyImmediate(child);
            }

            // The harbour's world is empty water (Barren: no environment, no spawn profile). The corridor clones its
            // worlds from the cell's other configs, which stay, in order.
            var barren = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BarrenConfigPath);
            foreach (var cell in env.GetComponentsInChildren<Cell>(true))
            {
                var cso = new SerializedObject(cell);
                var configs = cso.FindProperty("CellConfigs");
                if (!barren || configs == null) continue;
                int at = -1;
                for (int i = 0; i < configs.arraySize; i++)
                    if (configs.GetArrayElementAtIndex(i).objectReferenceValue == barren) at = i;
                if (at < 0)
                {
                    configs.InsertArrayElementAtIndex(0);
                    configs.GetArrayElementAtIndex(0).objectReferenceValue = barren;
                }
                else if (at > 0) configs.MoveArrayElement(at, 0);
                cso.ApplyModifiedPropertiesWithoutUndo();
                sb.Append($"cell '{cell.name}' boots Barren. ");
            }
            return sb.ToString();
        }

        static string StripUi(Scene scene)
        {
            var sb = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
            {
                var safe = root.transform.Find("Safe Area");
                if (!safe) continue;
                for (int i = safe.childCount - 1; i >= 0; i--)
                {
                    var child = safe.GetChild(i).gameObject;
                    if (KeptSafeAreaChildren.Contains(child.name)) continue;
                    sb.Append($"dropped UI/{child.name}. ");
                    Object.DestroyImmediate(child);
                }
            }
            return sb.ToString();
        }

        static string AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == VoyageLaunch.ScenePath)) return "already in the build list.";
            int menu = scenes.FindIndex(s => s.path == MenuScenePath);
            scenes.Insert(menu >= 0 ? menu + 1 : scenes.Count, new EditorBuildSettingsScene(VoyageLaunch.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            return "added to the build list after Menu_Main.";
        }

        static GameObject Root(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);

        static T Read<T>(Object owner, string field) where T : Object =>
            owner ? new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as T : null;

        static T[] ReadArray<T>(Object owner, string field) where T : Object
        {
            var list = new List<T>();
            var p = owner ? new SerializedObject(owner).FindProperty(field) : null;
            if (p is { isArray: true })
                for (int i = 0; i < p.arraySize; i++)
                    if (p.GetArrayElementAtIndex(i).objectReferenceValue is T t) list.Add(t);
            return list.ToArray();
        }

        static void WriteArray<T>(SerializedProperty p, IReadOnlyList<T> values) where T : Object
        {
            if (p == null) return;
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
