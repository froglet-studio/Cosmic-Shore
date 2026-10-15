using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// Opens the Goby &amp; Pistol duo in Play mode (<see cref="GobyPistolHarness"/>): the local vessel becomes the Goby's
    /// stand-in and the Pistol rides its unrolled path, side on, acting on the scene's real prisms. Fly the Squirrel
    /// (the Goby is a two-thumb flier on its numbers) in any mode that is not an arena match.
    ///
    /// <para>Writes nothing to disk (it adds a scene object for the rest of the Play session), so it needs no change
    /// ledger. The numbers come from the clone of the Vessel Studio artifact,
    /// <c>Assets/_SO_Assets/VesselActions/GobyPistol/GobyPistolConfig.asset</c>.</para>
    /// </summary>
    public static class GobyPistolHarnessMenu
    {
        const string MenuPath = "FrogletTools/Vessels/Goby and Pistol (Play mode)";
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/GobyPistol/GobyPistolConfig.asset";

        [MenuItem(MenuPath, false, 40)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 3,
            Description = "Play the Goby & Pistol duo in the real game: your vessel lays the path, the Pistol rides it side on " +
                          "and breaks real prisms (dive, launch, ghost, the vortex-ring Snap). D-pad left/right or 1/2 swaps halves.",
            DocPath = ".claude/skills/duo-vessel/references/designs/goby-and-pistol.md")]
        public static void Open()
        {
            var config = AssetDatabase.LoadAssetAtPath<GobyPistolConfigSO>(ConfigPath);
            if (!config) Debug.LogWarning($"[GobyPistol] {ConfigPath} is missing: the harness runs on the artifact's SHIPPED defaults.");
            if (!GobyPistolHarness.Attach(config))
                EditorUtility.DisplayDialog("Goby and Pistol", "No local vessel is flying. Start a mode, launch a vessel (the Squirrel), then open this again.", "OK");
        }

        [MenuItem(MenuPath, true)]
        static bool CanOpen() => EditorApplication.isPlaying;
    }
}
