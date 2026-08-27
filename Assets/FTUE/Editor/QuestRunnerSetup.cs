#if !LINUX_BUILD
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Adds a <see cref="QuestGraphRunner"/> to the currently-open scene (intended for
    /// Menu_Main) and auto-resolves as many of its references as it can from the open scene
    /// and the project. Anything it can't resolve is logged so it can be wired by hand.
    /// </summary>
    public static class QuestRunnerSetup
    {
        [MenuItem("FrogletTools/Quest Graph/Setup Runner In Scene")]
        public static void SetupRunner()
        {
            var existing = Object.FindFirstObjectByType<QuestGraphRunner>(FindObjectsInactive.Include);
            var runner = existing;
            if (runner == null)
            {
                var go = new GameObject("Quest Runner");
                Undo.RegisterCreatedObjectUndo(go, "Create Quest Runner");
                runner = go.AddComponent<QuestGraphRunner>();
            }

            var so = new SerializedObject(runner);

            AssignObject(so, "quest", FindAsset<QuestSO>());
            AssignObject(so, "gameData", FindAsset<GameDataSO>());
            AssignObject(so, "freestyleEvents", FindAsset<MenuFreestyleEventsContainerSO>());
            AssignObject(so, "onButtonPressed", FindInputPressedEvent());
            AssignObject(so, "skimBoostEvent", FindAsset<ScriptableEventBoostChanged>());

            AssignObject(so, "crystalHandler", Object.FindFirstObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include));
            AssignObject(so, "instructionView", Object.FindFirstObjectByType<QuestInstructionView>(FindObjectsInactive.Include));
            AssignObject(so, "dialoguePanel", FindDialoguePanel());
            AssignObject(so, "screenSwitcher", Object.FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include));

            AssignGameCards(so);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runner);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);

            Selection.activeObject = runner.gameObject;
            LogUnresolved(so);
            Debug.Log("[Quest] Runner setup complete. Review the 'Quest Runner' object and assign anything still marked missing.");
        }

        static void AssignObject(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null)
            {
                Debug.LogWarning($"[Quest] Runner has no serialized field '{prop}'.");
                return;
            }
            if (p.objectReferenceValue == null && value != null)
                p.objectReferenceValue = value;
        }

        /// <summary>
        /// The runner's single dialogue slot accepts a scene instance OR a prefab asset —
        /// prefer the scene panel, fall back to a prefab carrying QuestDialoguePanelView.
        /// </summary>
        static QuestDialoguePanelView FindDialoguePanel()
        {
            var sceneInstance = Object.FindFirstObjectByType<QuestDialoguePanelView>(FindObjectsInactive.Include);
            if (sceneInstance != null) return sceneInstance;

            foreach (var guid in AssetDatabase.FindAssets("DialogueSetUI t:Prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.TryGetComponent(out QuestDialoguePanelView view))
                    return view;
            }

            return null;
        }

        static void AssignGameCards(SerializedObject so)
        {
            var prop = so.FindProperty("gameCards");
            if (prop == null) return;
            if (prop.arraySize > 0) return; // don't clobber a manual wiring

            // Game cards are CallToActionTargets whose TargetID is in the PlayGame range (400s).
            var cards = Object.FindObjectsByType<CallToActionTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => (int)t.TargetID >= 400 && (int)t.TargetID < 500)
                .ToArray();

            prop.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        }

        static void LogUnresolved(SerializedObject so)
        {
            string[] required =
            {
                "quest", "gameData", "freestyleEvents", "onButtonPressed", "skimBoostEvent", "instructionView",
                "crystalHandler", "dialoguePanel", "screenSwitcher",
            };
            var missing = required
                .Select(so.FindProperty)
                .Where(p => p != null && p.objectReferenceValue == null)
                .Select(p => p.name)
                .ToArray();

            if (missing.Length > 0)
                Debug.LogWarning($"[Quest] Runner still needs manual wiring for: {string.Join(", ", missing)}");
        }

        internal static T FindAsset<T>() where T : Object
        {
            var guid = AssetDatabase.FindAssets($"t:{typeof(T).Name}").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static ScriptableEventInputEvents FindInputPressedEvent()
        {
            // Prefer the "pressed" channel over the "released" one when both exist.
            var all = AssetDatabase.FindAssets("t:ScriptableEventInputEvents")
                .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableEventInputEvents>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .ToList();

            return all.FirstOrDefault(a => a.name.ToLowerInvariant().Contains("press"))
                   ?? all.FirstOrDefault();
        }
    }
}
#endif
