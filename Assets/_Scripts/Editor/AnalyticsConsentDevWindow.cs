using CosmicShore.Core;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Developer-only view of the analytics consent gate, and a way to open it while testing.
    ///
    /// Analytics collection is opt-in: the COPPA age gate and the consent flag both default to
    /// DENIED, and until both are granted <c>AnalyticsServiceFacade</c> drops every event before
    /// it reaches any sink - so UGS and PostHog go silent together. In a shipping build the
    /// player answers <c>PrivacyConsentController</c>; that UI is not currently placed in any
    /// scene, so on a dev machine there is no way to answer it at all.
    ///
    /// This window is that answer, and nothing more. It writes the same two PlayerPrefs the real
    /// dialog writes, on this machine only. It is EDITOR-ONLY BY LOCATION (an Editor/ folder), so
    /// no build - development or release - can auto-grant consent. That is deliberate: granting
    /// consent on a player's behalf is the exact thing the gate exists to prevent.
    ///
    /// Read-only tool otherwise: it writes no assets, so it needs no ship panel (Docs/TOOLING.md).
    /// </summary>
    public class AnalyticsConsentDevWindow : EditorWindow
    {
        [MenuItem("FrogletTools/Services/Analytics Consent (Dev)")]
        [FrogletTool(FrogletToolCategory.Services, Importance = 5,
            Description = "Inspect the analytics consent gate and grant it on this machine for " +
                          "testing. Until it is granted, every event is dropped before reaching " +
                          "UGS or PostHog.")]
        public static void Open()
        {
            var window = GetWindow<AnalyticsConsentDevWindow>(false, "Analytics Consent");
            window.minSize = new Vector2(430f, 300f);
            window.Show();
        }

        static bool AgeAnswered => PlayerPrefs.HasKey(AnalyticsServiceFacade.AgeGatePrefKey);
        static bool AgeEligible => PlayerPrefs.GetInt(AnalyticsServiceFacade.AgeGatePrefKey, 0) == 1;
        static bool ConsentAnswered => PlayerPrefs.HasKey(AnalyticsServiceFacade.ConsentPrefKey);
        static bool ConsentGranted => PlayerPrefs.GetInt(AnalyticsServiceFacade.ConsentPrefKey, 0) == 1;

        static bool Collecting => AgeEligible && ConsentGranted;

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Analytics Consent", "Developer gate control",
                FrogletEditorPalette.Azure);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Collection is opt-in. Both gates must be granted or every event is dropped " +
                "before it reaches UGS or PostHog - which looks like a backend problem and is not.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(8f);
            DrawGate("Age gate (COPPA)", AgeAnswered, AgeEligible, "13+", "under 13");
            DrawGate("Analytics consent", ConsentAnswered, ConsentGranted, "granted", "declined");

            EditorGUILayout.Space(6f);
            var pillRect = GUILayoutUtility.GetRect(GUIContent.none, FrogletEditorPalette.Pill,
                GUILayout.Height(20f), GUILayout.ExpandWidth(true));
            FrogletEditorPalette.StatusPill(pillRect,
                Collecting ? "COLLECTING - events will reach both sinks" : "BLOCKED - all events are dropped",
                Collecting ? FrogletEditorPalette.Ok : FrogletEditorPalette.Error);

            EditorGUILayout.Space(12f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Grant (this machine)", FrogletEditorPalette.Ok, 170f))
                    SetGates(true);
                if (FrogletEditorPalette.ColorButton("Revoke", FrogletEditorPalette.Warn, 100f))
                    SetGates(false);
                if (FrogletEditorPalette.ColorButton("Clear (unanswered)", FrogletEditorPalette.Info, 150f))
                    ClearGates();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "This is a stand-in for PrivacyConsentController, which exists in code but is not " +
                "placed in any scene. Placing it is the real fix and a release blocker - a shipping " +
                "build must never grant consent on the player's behalf.\n\n" +
                "Granting while in Play Mode takes effect on the next sign-in, so restart Play Mode.",
                MessageType.Info);

            if (Application.isPlaying)
                Repaint();
        }

        static void DrawGate(string label, bool answered, bool granted, string yes, string no)
        {
            string state = !answered ? "not answered" : granted ? yes : no;
            var tint = !answered ? FrogletEditorPalette.Warn
                : granted ? FrogletEditorPalette.Ok
                : FrogletEditorPalette.Error;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(180f));
                var prev = GUI.color;
                GUI.color = tint;
                EditorGUILayout.LabelField(state, EditorStyles.boldLabel);
                GUI.color = prev;
            }
        }

        static void SetGates(bool granted)
        {
            PlayerPrefs.SetInt(AnalyticsServiceFacade.AgeGatePrefKey, granted ? 1 : 0);
            PlayerPrefs.SetInt(AnalyticsServiceFacade.ConsentPrefKey, granted ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[Analytics] Consent gates set to {(granted ? "GRANTED" : "DENIED")} on this machine. " +
                      "Restart Play Mode for it to take effect.");
        }

        static void ClearGates()
        {
            PlayerPrefs.DeleteKey(AnalyticsServiceFacade.AgeGatePrefKey);
            PlayerPrefs.DeleteKey(AnalyticsServiceFacade.ConsentPrefKey);
            PlayerPrefs.Save();
            Debug.Log("[Analytics] Consent gates cleared - back to unanswered, the shipping default.");
        }
    }
}
