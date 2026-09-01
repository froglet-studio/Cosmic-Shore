using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor.Rewards
{
    /// <summary>
    /// Puts the two reward displays on screen: the end-game payout panel in every gameplay
    /// scene, and the reward toast driver in Menu_Main.
    ///
    /// This is a TOOL rather than hand-authored prefab YAML for one reason that is worth
    /// stating: the end-game scoreboard's wiring is per-SCENE and the shared prefab is stale
    /// against it. GameCanvas.prefab's Scoreboard leaves playerCardContainer unset and still
    /// serializes ten keys the script no longer declares (SingleplayerView, MultiplayerView,
    /// the four rematch fields...), so reading the prefab tells you almost nothing about what
    /// a given scene actually shows. Only the merged, loaded hierarchy does - which is what
    /// this runs against.
    ///
    /// Idempotent, and ADOPTS before it creates. The scoreboard already carries an authored
    /// "Goodies" cluster with a CrystalIcon and a CrystalsEarned label, written since 2021 by
    /// the deprecated MiniGame path with a hardcoded 0. Where that art exists this binds to it
    /// rather than building a parallel display, so the payout lands in a slot a designer
    /// already placed. It creates its own objects only where there is nothing to adopt.
    /// </summary>
    public class RewardDisplayWirer : EditorWindow
    {
        const string ToolName = "Reward Display Wirer";
        const string PanelObjectName = "RewardPayout";
        const string ToastObjectName = "RewardToastDriver";
        const string MenuScene = "Menu_Main";

        // Names the tool adopts rather than re-creates, in preference order.
        static readonly string[] AmountLabelNames = { "CrystalsEarned", "RewardAmount" };
        static readonly string[] BalanceLabelNames = { "CrystalBalance", "RewardBalance" };
        static readonly string[] IconNames = { "CrystalIcon", "RewardIcon" };

        readonly List<string> _log = new();
        Vector2 _scroll;

        static readonly FrogletToolShipContext Ship = new(ToolName)
        {
            ToolScriptPaths = new[] { "Assets/_Scripts/Editor/Rewards/RewardDisplayWirer.cs" },
        };

        [MenuItem("FrogletTools/Interface/Wire Reward Displays")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 4,
            Description = "Bind the end-game reward payout panel and the menu reward toast " +
                          "into every scene that needs them. Idempotent; adopts existing art.")]
        static void Open() => GetWindow<RewardDisplayWirer>("Reward Displays");

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Reward Displays",
                "Wires RewardPayoutPanel into each gameplay scene's scoreboard and " +
                "RewardToastDriver into Menu_Main. Safe to re-run.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Interface));

            EditorGUILayout.HelpBox(
                "Open the scene(s) you want wired, then Wire Open Scenes. " +
                "Wire All Build Scenes opens each scene in Build Settings in turn and saves it.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Wire Open Scenes", FrogletEditorPalette.Ok, 160f))
                    Run(openOnly: true);
                if (FrogletEditorPalette.ColorButton("Wire All Build Scenes", FrogletEditorPalette.Info, 190f))
                    Run(openOnly: false);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var line in _log)
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();

            FrogletToolShipPanel.Draw(Ship, this);
        }

        void Run(bool openOnly)
        {
            _log.Clear();

            var channel = LoadChannel();
            if (channel == null)
            {
                _log.Add("ABORT: Resources/Channels/RewardGrantedChannel.asset not found. " +
                         "Run Tools/Build/author_reward_assets.py first.");
                return;
            }

            if (openOnly)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    WireScene(SceneManager.GetSceneAt(i), channel, save: false);
                _log.Add("Done. Save the scene(s) yourself, then use Validate & Push below.");
                return;
            }

            var current = EditorSceneManager.GetActiveScene().path;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                _log.Add("Cancelled - unsaved changes.");
                return;
            }

            foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                WireScene(scene, channel, save: true);
            }

            if (!string.IsNullOrEmpty(current))
                EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
        }

        static ScriptableEventRewardGranted LoadChannel() =>
            Resources.Load<ScriptableEventRewardGranted>("Channels/RewardGrantedChannel");

        void WireScene(Scene scene, ScriptableEventRewardGranted channel, bool save)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            bool changed = false;
            changed |= WirePayoutPanel(scene, channel);
            changed |= WireToastDriver(scene, channel);

            if (!changed)
            {
                _log.Add($"- {scene.name}: nothing to do.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            FrogletToolChangeLedger.Record(ToolName, scene.path);
            if (save) EditorSceneManager.SaveScene(scene);
        }

        // ---------------- End-game payout panel ----------------

        bool WirePayoutPanel(Scene scene, ScriptableEventRewardGranted channel)
        {
            // FindObjectsOfType misses inactive objects, and the whole scoreboard is inactive
            // until the game ends - so walk the roots instead.
            var scoreboard = FindInScene<Scoreboard>(scene);
            if (scoreboard == null) return false;

            var host = ResolvePanelHost(scoreboard);
            if (host == null)
            {
                _log.Add($"! {scene.name}: Scoreboard found but no panel root to hang the " +
                         "payout on (scoreboardPanel unset). Skipped.");
                return false;
            }

            var existing = host.GetComponentsInChildren<RewardPayoutPanel>(true).FirstOrDefault();
            var panel = existing != null ? existing : CreatePanelObject(host);

            var so = new SerializedObject(panel);
            bool changed = false;
            changed |= SetRef(so, "rewardChannel", channel);
            changed |= SetRef(so, "amountText", FindByName<TMP_Text>(host, AmountLabelNames));
            changed |= SetRef(so, "balanceText", FindByName<TMP_Text>(host, BalanceLabelNames));

            var icon = FindByName<Component>(host, IconNames);
            changed |= SetRef(so, "crystalIconRoot", icon != null ? icon.gameObject : null);

            if (so.FindProperty("animSettings").objectReferenceValue == null)
            {
                var anim = FindAnimSettings(scoreboard);
                if (anim != null) changed |= SetRef(so, "animSettings", anim);
            }

            if (changed) so.ApplyModifiedPropertiesWithoutUndo();

            _log.Add($"{(existing == null ? "+" : "=")} {scene.name}: payout panel on " +
                     $"'{panel.gameObject.name}' " +
                     $"(amount={Describe(so, "amountText")}, balance={Describe(so, "balanceText")}, " +
                     $"icon={Describe(so, "crystalIconRoot")})");

            return changed || existing == null;
        }

        /// <summary>
        /// Where the payout goes. The panel hides itself with a CanvasGroup rather than
        /// SetActive, so it is safe to parent under the scoreboard root even though that root
        /// is inactive until the game ends - RewardService replays the last grant to a display
        /// that was not listening when it landed.
        /// </summary>
        static Transform ResolvePanelHost(Scoreboard scoreboard)
        {
            var so = new SerializedObject(scoreboard);
            var panelProp = so.FindProperty("scoreboardPanel");
            if (panelProp?.objectReferenceValue is Transform t) return t;
            return null;
        }

        static RewardPayoutPanel CreatePanelObject(Transform host)
        {
            // Prefer the authored "Goodies" cluster if it is there - it already carries the
            // crystal icon and the earned label, laid out by a designer.
            var goodies = FindTransformByName(host, "Goodies");
            if (goodies != null && goodies.GetComponent<RewardPayoutPanel>() == null)
            {
                EnsureCanvasGroup(goodies.gameObject);
                return Undo.AddComponent<RewardPayoutPanel>(goodies.gameObject);
            }

            var go = new GameObject(PanelObjectName, typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(go, "Create Reward Payout Panel");
            go.transform.SetParent(host, false);
            go.layer = host.gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(360f, 64f);

            return go.AddComponent<RewardPayoutPanel>();
        }

        // ---------------- Menu toast ----------------

        bool WireToastDriver(Scene scene, ScriptableEventRewardGranted channel)
        {
            if (scene.name != MenuScene) return false;

            var existing = FindInScene<RewardToastDriver>(scene);
            var toastService = FindInScene<ToastService>(scene);
            if (toastService == null)
            {
                _log.Add($"! {scene.name}: no ToastService in the scene - the reward toast has " +
                         "nowhere to draw. Skipped.");
                return false;
            }

            var driver = existing;
            if (driver == null)
            {
                var go = new GameObject(ToastObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Create Reward Toast Driver");
                go.transform.SetParent(toastService.transform, false);
                driver = go.AddComponent<RewardToastDriver>();
            }

            var so = new SerializedObject(driver);
            bool changed = SetRef(so, "rewardChannel", channel);
            changed |= SetRef(so, "toastChannel", ReadToastChannel(toastService));
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();

            _log.Add($"{(existing == null ? "+" : "=")} {scene.name}: reward toast driver " +
                     $"(toastChannel={Describe(so, "toastChannel")})");
            return changed || existing == null;
        }

        static ToastChannel ReadToastChannel(ToastService service) =>
            new SerializedObject(service).FindProperty("channel")?.objectReferenceValue as ToastChannel;

        // ---------------- helpers ----------------

        static bool SetRef(SerializedObject so, string field, UnityEngine.Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null || value == null) return false;
            if (prop.objectReferenceValue == value) return false;
            prop.objectReferenceValue = value;
            return true;
        }

        static string Describe(SerializedObject so, string field)
        {
            var v = so.FindProperty(field)?.objectReferenceValue;
            return v != null ? v.name : "<none>";
        }

        static void EnsureCanvasGroup(GameObject go)
        {
            if (go.GetComponent<CanvasGroup>() == null)
                Undo.AddComponent<CanvasGroup>(go);
        }

        static HUDAnimationSettingsSO FindAnimSettings(Scoreboard scoreboard)
        {
            var so = new SerializedObject(scoreboard);
            return so.FindProperty("animSettings")?.objectReferenceValue as HUDAnimationSettingsSO;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var hit = root.GetComponentsInChildren<T>(true).FirstOrDefault();
                if (hit != null) return hit;
            }
            return null;
        }

        static T FindByName<T>(Transform host, IReadOnlyList<string> names) where T : Component
        {
            foreach (var name in names)
            {
                foreach (var candidate in host.GetComponentsInChildren<T>(true))
                    if (string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
                        return candidate;
            }
            return null;
        }

        static Transform FindTransformByName(Transform host, string name)
        {
            foreach (var t in host.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }
    }
}
