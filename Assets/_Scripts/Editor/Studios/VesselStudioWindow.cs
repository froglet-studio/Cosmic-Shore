using System.Collections.Generic;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// The Vessel Studio in Unity (<c>Docs/Studios/VESSEL_STUDIO_PLAN.md</c>, <c>/vessel-studio</c> D32 and D33): the
    /// studio HOME, the web hub's front page drawn in Unity (<c>VesselStudioWindow.Home.cs</c>). A card opens that
    /// studio's page from this checkout in its own app window (<see cref="LaunchPrisma.OpenStudioWindow"/>): the
    /// artifact's own files, so every tab, parameter and graphic is the artifact's.
    ///
    /// <para><b>No tuning UI of its own.</b> The artifact is the one source of truth for a studio's parameters (the
    /// user, 2026-10-10). Unity never draws a second set of tabs over the assets; the artifact's numbers reach the
    /// game through <c>/artifact-to-unity</c> (<c>Tools/Build/studio_to_unity.py</c>), which writes them into the
    /// vessel's config assets. READER: this window writes nothing.</para>
    /// </summary>
    public sealed partial class VesselStudioWindow : EditorWindow
    {
        const string StudioUrl = "https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa";

        [MenuItem("FrogletTools/Vessels/Vessel Studio", false, 0)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 5,
            Description = "The Vessel Studio home: every studio as a card with its live preview, as on the web hub. A card " +
                          "opens that studio (the artifact's own pages, every tab and parameter, from this checkout) in its own window.",
            DocPath = "Docs/Studios/VESSEL_STUDIO_PLAN.md")]
        public static void Open()
        {
            var w = GetWindow<VesselStudioWindow>();
            w.titleContent = new GUIContent("Vessel Studio");
            w.minSize = new Vector2(420f, 360f);
            w.Show();
            w.Focus();
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            LoadHome();
            EditorApplication.update += Animate;
        }

        void OnDisable()
        {
            EditorApplication.update -= Animate;
            foreach (var tex in _previewTex.Values)
                if (tex) DestroyImmediate(tex);
            _previewTex.Clear();
        }

        void OnGUI() => DrawHome();
    }
}
