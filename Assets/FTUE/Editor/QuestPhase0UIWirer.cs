#if !LINUX_BUILD
using System.Linq;
using CosmicShore.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click wiring for the hand-built Phase 0 UI in Menu_Main:
    ///   • "InGameInstructionSet" (+ "Container"/Set1..SetN children) → QuestInstructionView
    ///     with the sets registered as keyed panels in flight-school order, plus a starter
    ///     ProgressText for the skim counter if none exists.
    ///   • "DialogueSetUI" → QuestDialoguePanelView (captain image / body text / next button
    ///     resolved by name+type heuristics) and plugged into DialogueViewResolver's
    ///     MainMenu override slot.
    ///   • QuestToastNotifier added next to the runner.
    /// Then re-runs the standard runner setup so all references resolve. Everything is
    /// logged — review the console mapping and adjust in the inspector if a guess is wrong.
    /// </summary>
    public static class QuestPhase0UIWirer
    {
        static readonly string[] PanelKeys =
            { "speed_up", "slow_down", "look_around", "drift", "skim", "exit_freestyle" };

        [MenuItem("FrogletTools/Quest Graph/Wire Phase 0 UI (Menu_Main)")]
        public static void Wire()
        {
            WireInstructionSet();
            WireDialoguePanel();
            QuestRunnerSetup.SetupRunner(); // resolves runner refs incl. the new instruction view
            WireToastNotifier();
            WireTrainingHiddenGroups();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Quest] Phase 0 UI wiring complete — review the mapping logs above, then SAVE THE SCENE.");
        }

        // ── Instruction set ────────────────────────────────────────────

        static void WireInstructionSet()
        {
            var root = FindSceneObject("InGameInstructionSet");
            if (root == null)
            {
                Debug.LogWarning("[Quest] Wirer: no 'InGameInstructionSet' object found in the open scene — skipped.");
                return;
            }

            var view = root.GetComponent<QuestInstructionView>();
            if (view == null) view = Undo.AddComponent<QuestInstructionView>(root);

            var rootGroup = root.GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = Undo.AddComponent<CanvasGroup>(root);

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = rootGroup;

            var container = root.transform.Find("Container") != null
                ? root.transform.Find("Container")
                : root.transform;

            var panelsProp = so.FindProperty("panels");
            panelsProp.arraySize = 0;
            int keyIndex = 0;
            for (int i = 0; i < container.childCount && keyIndex < PanelKeys.Length; i++)
            {
                var child = container.GetChild(i);
                var childGroup = child.GetComponent<CanvasGroup>();
                if (childGroup == null) childGroup = Undo.AddComponent<CanvasGroup>(child.gameObject);

                panelsProp.InsertArrayElementAtIndex(keyIndex);
                var element = panelsProp.GetArrayElementAtIndex(keyIndex);
                element.FindPropertyRelative("key").stringValue = PanelKeys[keyIndex];
                element.FindPropertyRelative("group").objectReferenceValue = childGroup;

                Debug.Log($"[Quest] Wirer: instruction panel '{child.name}' → key '{PanelKeys[keyIndex]}'. " +
                          "If this set teaches a different beat, reorder the children (mapping is by sibling order).");
                keyIndex++;
            }
            if (keyIndex < PanelKeys.Length)
                Debug.LogWarning($"[Quest] Wirer: only {keyIndex}/{PanelKeys.Length} instruction sets found — " +
                                 $"missing keys: {string.Join(", ", PanelKeys.Skip(keyIndex))}.");

            // The skim counter appends " n / target" to the active set's own text automatically.

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);
        }

        // ── Dialogue panel ─────────────────────────────────────────────

        static void WireDialoguePanel()
        {
            var root = FindSceneObject("DialogueSetUI");
            if (root == null)
            {
                Debug.LogWarning("[Quest] Wirer: no 'DialogueSetUI' object found in the open scene — skipped.");
                return;
            }

            var view = root.GetComponent<QuestDialoguePanelView>();
            if (view == null) view = Undo.AddComponent<QuestDialoguePanelView>(root);

            var rootGroup = root.GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = Undo.AddComponent<CanvasGroup>(root);

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = rootGroup;

            // Captain portrait: child whose name contains "captain" with an Image.
            var captain = root.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.name.ToLowerInvariant().Contains("captain"));
            if (captain != null) so.FindProperty("captainImage").objectReferenceValue = captain;
            else Debug.LogWarning("[Quest] Wirer: no '*Captain*' Image child found — assign Captain Image manually.");

            // Next button: first Button under the root (your combined Next/Skip button).
            var next = root.GetComponentInChildren<Button>(true);
            if (next != null)
            {
                so.FindProperty("nextButton").objectReferenceValue = next;
                Debug.Log($"[Quest] Wirer: '{next.name}' wired as NEXT (advances lines; the last line closes the panel). " +
                          "Add a second button and assign it to skipButton if you want fast-forward too.");
            }
            else
            {
                Debug.LogWarning("[Quest] Wirer: no Button found under DialogueSetUI — assign Next Button manually.");
            }

            // Body text: first TMP that is NOT inside a Button (skips the button label).
            var body = root.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(t => t.GetComponentInParent<Button>(true) == null);
            if (body != null) so.FindProperty("bodyText").objectReferenceValue = body;
            else Debug.LogWarning("[Quest] Wirer: no body TMP text found outside the button — assign Body Text manually.");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);
            Debug.Log("[Quest] Wirer: DialogueSetUI panel ready — Dialogue nodes drive it directly (lines authored on the node; no dialogue system).");
        }

        // ── Toast notifier ─────────────────────────────────────────────

        static void WireToastNotifier()
        {
            var runner = Object.FindFirstObjectByType<QuestGraphRunner>(FindObjectsInactive.Include);
            if (runner == null) return;

            if (runner.GetComponent<QuestToastNotifier>() == null)
            {
                Undo.AddComponent<QuestToastNotifier>(runner.gameObject);
                Debug.Log("[Quest] Wirer: QuestToastNotifier added to the Quest Runner.");
            }
        }

        // ── Training-hidden UI (vessel HUD off during flight school) ───

        static void WireTrainingHiddenGroups()
        {
            var runner = Object.FindFirstObjectByType<QuestGraphRunner>(FindObjectsInactive.Include);
            if (runner == null) return;

            var so = new SerializedObject(runner);
            var prop = so.FindProperty("hideDuringFlightTraining");
            if (prop == null || prop.arraySize > 0) return; // don't clobber a manual wiring

            var gameUi = FindSceneObject("Game UI");
            if (gameUi == null)
            {
                Debug.LogWarning("[Quest] Wirer: no 'Game UI' object found — assign Hide During Flight Training on the runner manually (the vessel HUD group).");
                return;
            }

            var group = gameUi.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(gameUi);

            prop.arraySize = 1;
            prop.GetArrayElementAtIndex(0).objectReferenceValue = group;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(runner);
            Debug.Log("[Quest] Wirer: 'Game UI' (vessel HUD) will be hidden during flight training.");
        }

        // ── Helpers ────────────────────────────────────────────────────

        static GameObject FindSceneObject(string objectName)
        {
            foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (sceneRoot.name == objectName) return sceneRoot;
                var found = FindInChildren(sceneRoot.transform, objectName);
                if (found != null) return found;
            }
            return null;
        }

        static GameObject FindInChildren(Transform parent, string objectName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == objectName) return child.gameObject;
                var found = FindInChildren(child, objectName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
