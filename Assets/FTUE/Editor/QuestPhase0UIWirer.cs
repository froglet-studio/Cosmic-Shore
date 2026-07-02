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
                if (child.name == "ProgressText") continue;

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

            // Starter skim counter if none assigned (restyle/move freely).
            if (so.FindProperty("progressText").objectReferenceValue == null)
            {
                var existing = root.transform.Find("ProgressText");
                TMP_Text progress;
                if (existing != null && existing.TryGetComponent(out TMP_Text found))
                {
                    progress = found;
                }
                else
                {
                    var go = new GameObject("ProgressText", typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(go, "Create ProgressText");
                    go.transform.SetParent(root.transform, false);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(0f, 140f);
                    rt.sizeDelta = new Vector2(360f, 80f);

                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    tmp.text = "0 / 10";
                    tmp.fontSize = 48f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                    progress = tmp;
                    Debug.Log("[Quest] Wirer: created a starter 'ProgressText' (skim counter) under InGameInstructionSet — restyle/reposition it.");
                }
                so.FindProperty("progressText").objectReferenceValue = progress;
            }

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

            // Plug into the resolver's MainMenu override so Dialogue nodes use this panel.
            var resolver = Object.FindFirstObjectByType<DialogueViewResolver>(FindObjectsInactive.Include);
            if (resolver != null)
            {
                var rso = new SerializedObject(resolver);
                rso.FindProperty("mainMenuOverride").objectReferenceValue = view;
                rso.ApplyModifiedProperties();
                EditorUtility.SetDirty(resolver);
                Debug.Log("[Quest] Wirer: DialogueViewResolver.mainMenuOverride → DialogueSetUI panel.");
            }
            else
            {
                Debug.LogWarning("[Quest] Wirer: no DialogueViewResolver in the scene — is the DialogueManager set up in Menu_Main? " +
                                 "Dialogue nodes need DialogueManager + DialogueViewResolver + DialogueSetLibrary.");
            }
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
