#if !LINUX_BUILD
using System.Linq;
using CosmicShore.Core;
using CosmicShore.UI;
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
            WireProgressionService();
            WireInstructionSet();
            WireDialoguePanel();
            QuestRunnerSetup.SetupRunner(); // resolves runner refs incl. the new instruction view
            WireToastNotifier();
            WireTrainingHiddenGroups();
            WireNavButtons();
            WireQuestButtons();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Quest] Phase 0 UI wiring complete — review the mapping logs above, then SAVE THE SCENE.");
        }

        // ── Progression service (was in NO scene — Instance null every run) ──
        //
        // GameModeProgressionService is the lock authority for arcade cards, intensity
        // tiers, and play-count tracking, but the only prefab carrying it lives in
        // "MIgration_Prefabs (DELETE LATER)" and is placed nowhere. With Instance null,
        // cards were never progression-locked and the quest's played-game gates could
        // never resume after a scene round-trip.

        static void WireProgressionService()
        {
            if (Object.FindFirstObjectByType<GameModeProgressionService>(FindObjectsInactive.Include) != null)
                return;

            var go = new GameObject("GameModeProgressionService");
            Undo.RegisterCreatedObjectUndo(go, "Create GameModeProgressionService");
            var svc = go.AddComponent<GameModeProgressionService>();

            var so = new SerializedObject(svc);
            var questList = QuestRunnerSetup.FindAsset<CosmicShore.ScriptableObjects.SO_UnlockList>();
            so.FindProperty("questList").objectReferenceValue = questList;
            so.FindProperty("progressionConfig").objectReferenceValue =
                QuestRunnerSetup.FindAsset<CosmicShore.ScriptableObjects.SO_ProgressionConfig>();
            so.FindProperty("gameData").objectReferenceValue =
                QuestRunnerSetup.FindAsset<CosmicShore.Utility.GameDataSO>();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(svc);

            if (questList == null)
                Debug.LogWarning("[Quest] Wirer: no SO_UnlockList asset found — assign Quest List on GameModeProgressionService manually.");
            Debug.Log($"[Quest] Wirer: GameModeProgressionService was in NO scene (Instance was always null → no card locks, " +
                      $"no play tracking, played-game gates couldn't resume). Created + wired it (questList: {(questList != null ? questList.name : "MISSING")}).");
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

        // ── Training-hidden UI ─────────────────────────────────────────
        //
        // The vessel HUD is hidden at runtime through its own controller
        // (VesselHUDController.HideHUD) — no group wiring needed. This step only
        // REMOVES the legacy 'Game UI' entry an older wirer added: hiding all of
        // Game UI also hid the volume/pause button, which is now the taught exit.

        static void WireTrainingHiddenGroups()
        {
            var runner = Object.FindFirstObjectByType<QuestGraphRunner>(FindObjectsInactive.Include);
            if (runner == null) return;

            var so = new SerializedObject(runner);
            var prop = so.FindProperty("hideDuringFlightTraining");
            if (prop == null) return;

            for (int i = prop.arraySize - 1; i >= 0; i--)
            {
                var element = prop.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue is CanvasGroup group && group.gameObject.name == "Game UI")
                {
                    prop.DeleteArrayElementAtIndex(i);
                    Debug.Log("[Quest] Wirer: removed 'Game UI' from Hide During Flight Training — it would hide the " +
                              "volume button (the taught exit). The vessel HUD hides via its own controller now.");
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(runner);
        }

        // ── Footer nav buttons (LockNavigation nodes) ──────────────────

        static void WireNavButtons()
        {
            var runner = Object.FindFirstObjectByType<QuestGraphRunner>(FindObjectsInactive.Include);
            var switcher = Object.FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (runner == null || switcher == null)
            {
                Debug.LogWarning("[Quest] Wirer: runner or ScreenSwitcher missing — nav buttons not wired.");
                return;
            }

            Button arcade = null;
            var lockable = new System.Collections.Generic.List<Button>();

            // Nav-lock design (per the NavBarButtons layout): the quest locks EXACTLY the
            // Hangar and Profile links. ArkLink and PortLink are permanently locked on the
            // scene side — they must NOT be in the lockable list, or the quest's later
            // Unlock step would re-enable them. HomeLink stays available throughout.
            // The "arcade button" is the one wired to OnClickArcadeNav (opens the arcade
            // modal) — NOT ArkLink (the always-locked ARK screen).
            foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                for (int i = 0; i < btn.onClick.GetPersistentEventCount(); i++)
                {
                    string method = btn.onClick.GetPersistentMethodName(i);
                    if (string.IsNullOrEmpty(method)) continue;

                    if (method == "OnClickHangarNav" || method == "OnClickProfileNav")
                    {
                        lockable.Add(btn);
                        break;
                    }

                    if (method == "OnClickArcadeNav")
                    {
                        arcade = btn;
                        break;
                    }
                }
            }

            var so = new SerializedObject(runner);
            so.FindProperty("arcadeNavButton").objectReferenceValue = arcade;
            var listProp = so.FindProperty("lockableNavButtons");
            listProp.arraySize = lockable.Count;
            for (int i = 0; i < lockable.Count; i++)
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = lockable[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(runner);

            if (arcade == null)
                Debug.LogWarning("[Quest] Wirer: no Arcade button found (OnClickArcadeNav) — " +
                                 "assign Arcade Nav Button on the runner manually.");
            Debug.Log($"[Quest] Wirer: nav buttons — arcade: {(arcade != null ? arcade.name : "NOT FOUND")}, " +
                      $"locked during FTUE: {lockable.Count} ({string.Join(", ", lockable.ConvertAll(b => b.name))}) " +
                      "— Hangar + Profile only; Ark/Port stay scene-locked, Home stays available.");
        }

        // ── Quest buttons (SetButtonInteractable nodes) ────────────────
        //
        // The Episodes button is authored non-interactable in the scene; phase 5's
        // 'Enable Episodes Button' node flips it on right before the Episodes CTA.
        // Register it on the runner under the key the node references.

        static void WireQuestButtons()
        {
            var runner = Object.FindFirstObjectByType<QuestGraphRunner>(FindObjectsInactive.Include);
            if (runner == null) return;

            Button episodes = null;
            foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (btn.gameObject.name.ToLowerInvariant().Contains("episode"))
                {
                    episodes = btn;
                    break;
                }
            }

            if (episodes == null)
            {
                Debug.LogWarning("[Quest] Wirer: no Button with 'episode' in its name found — add your Episodes " +
                                 "button to the runner's Quest Buttons list manually (key 'episodes').");
                return;
            }

            var so = new SerializedObject(runner);
            var listProp = so.FindProperty("questButtons");

            // Update an existing 'episodes' entry instead of duplicating it.
            int slot = -1;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == "episodes")
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                slot = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(slot);
            }

            var entry = listProp.GetArrayElementAtIndex(slot);
            entry.FindPropertyRelative("key").stringValue = "episodes";
            entry.FindPropertyRelative("button").objectReferenceValue = episodes;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(runner);

            Debug.Log($"[Quest] Wirer: quest button 'episodes' → '{episodes.name}' " +
                      "(phase 5's Enable Episodes Button node sets it interactable).");
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
