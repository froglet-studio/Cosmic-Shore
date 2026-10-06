using CosmicShore.Core;
using CosmicShore.Data;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Which <see cref="DeviceTier"/> this machine resolves to and why, and a way to simulate another
    /// one. The Editor always classifies as Desktop (or, under the Device Simulator, as the simulated
    /// phone), so this is how a mobile profile gets played on a desktop before it reaches a device.
    ///
    /// The override is the same PlayerPrefs key the runtime reads (<see cref="PlatformProfile.TierOverride"/>);
    /// the tier is read once per session, so a change applies from the next Play. Reader plus a
    /// PlayerPrefs setter: it writes no assets, so it needs no ship panel (Docs/TOOLING.md).
    /// <c>Docs/PLATFORM_UNIFICATION.md</c> §3.
    /// </summary>
    public class DeviceTierWindow : EditorWindow
    {
        [MenuItem("FrogletTools/Performance/Device Tier")]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 4,
            Description = "See which device tier (Desktop / MobileHigh / MobileLow) and platform profile " +
                          "this machine resolves to and why, and simulate another tier in Play mode.")]
        public static void Open()
        {
            var window = GetWindow<DeviceTierWindow>(false, "Device Tier");
            window.minSize = new Vector2(460f, 360f);
            window.Show();
        }

        Vector2 _scroll;

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Device Tier", "Which platform profile this machine runs, and why",
                FrogletEditorPalette.Indigo);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var set = PlatformProfile.LoadSet();
            if (!set)
                EditorGUILayout.HelpBox(
                    "Assets/Resources/PlatformProfiles.asset is missing: every device runs its pre-tier " +
                    "behaviour. Restore it.", MessageType.Error);

            // Computed fresh rather than read from PlatformProfile, which latches once per session.
            var facts = PlatformProfile.ReadDeviceFacts();
            var detected = DeviceTierClassifier.Classify(facts, set ? set.Rules : DeviceTierRules.Default,
                out string why);

            Section("This machine");
            Row("Classifies as", $"{detected} - {why}");
            Row("Device facts", facts.ToString());

            Section("Simulate a tier");
            var forced = PlatformProfile.TierOverride;
            var pill = GUILayoutUtility.GetRect(GUIContent.none, FrogletEditorPalette.Pill,
                GUILayout.Height(20f), GUILayout.ExpandWidth(true));
            FrogletEditorPalette.StatusPill(pill,
                forced.HasValue ? $"FORCED: {forced.Value}" : "AUTO-DETECT",
                forced.HasValue ? FrogletEditorPalette.Warn : FrogletEditorPalette.Ok);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Auto", FrogletEditorPalette.Ok, 80f,
                        outline: forced.HasValue))
                    PlatformProfile.TierOverride = null;
                TierButton(DeviceTier.Desktop, "Desktop", forced);
                TierButton(DeviceTier.MobileHigh, "Mobile High", forced);
                TierButton(DeviceTier.MobileLow, "Mobile Low", forced);
            }
            EditorGUILayout.LabelField(
                "Applies from the next Play: the tier is read once per session. Saved in this machine's " +
                "PlayerPrefs (key " + PlatformProfile.TierOverridePrefKey + "); set Auto before shipping " +
                "a build from this machine's settings.", EditorStyles.wordWrappedMiniLabel);

            if (EditorApplication.isPlaying)
            {
                Section("This Play session");
                Row("Tier", $"{PlatformProfile.Tier} - {PlatformProfile.Reason}");
                var current = PlatformProfile.Current;
                Row("Profile", current ? current.name : "(none)");

                EditorGUILayout.Space(4f);
                bool canApply = DisplayGraphicsSettings.Instance;
                if (FrogletEditorPalette.ColorButton("Re-run graphics auto-detect", FrogletEditorPalette.Info,
                        220f, tooltip: "Same as the settings panel's Auto-Detect: replaces this machine's " +
                                       "saved graphics settings with the tier's recommendation.",
                        enabled: canApply))
                    DisplayGraphicsSettings.Instance.ApplyAutoDetect();
            }

            if (set)
            {
                Section("Assets");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (FrogletEditorPalette.ColorButton("Ping profile set", FrogletEditorPalette.Slate, 130f,
                            outline: true))
                        EditorGUIUtility.PingObject(set);
                    var profile = set.For(forced ?? detected);
                    if (FrogletEditorPalette.ColorButton("Ping effective profile", FrogletEditorPalette.Slate,
                            170f, outline: true, enabled: profile))
                        EditorGUIUtility.PingObject(profile);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        static void TierButton(DeviceTier tier, string label, DeviceTier? forced)
        {
            bool selected = forced.HasValue && forced.Value == tier;
            if (FrogletEditorPalette.ColorButton(label, FrogletEditorPalette.Warn, 100f, outline: !selected))
                PlatformProfile.TierOverride = tier;
        }

        static void Section(string title)
        {
            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField(title, FrogletEditorPalette.SectionLabel);
        }

        static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(110f));
                EditorGUILayout.LabelField(value, EditorStyles.wordWrappedLabel);
            }
        }
    }
}
