using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// Adds the Goby &amp; Pistol duo (<see cref="GobyPistolDuo"/>, what <c>Goby.prefab</c> carries) to the vessel you are
    /// flying in Play mode: it becomes the Goby's stand-in and the Pistol rides its unrolled path, side on, acting on the
    /// scene's real prisms. To fly the real Goby, pick it in the freestyle Vessel Changer instead.
    ///
    /// <para>Writes nothing to disk (it adds a component to your hull for the rest of the Play session), so it needs no change
    /// ledger. The numbers come from the clone of the Vessel Studio artifact,
    /// <c>Assets/_SO_Assets/VesselActions/GobyPistol/GobyPistolConfig.asset</c>.</para>
    /// </summary>
    public static class GobyPistolHarnessMenu
    {
        const string MenuPath = "FrogletTools/Vessels/Goby and Pistol (Play mode)";
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/GobyPistol/GobyPistolConfig.asset";

        [MenuItem(MenuPath, false, 40)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 3,
            Description = "Put the Goby & Pistol duo on whatever you are flying: your vessel lays the path, the Pistol rides it side on " +
                          "and breaks real prisms (dive, launch, ghost, the vortex-ring Snap). D-pad left/right or 1/2 swaps halves.",
            DocPath = ".claude/skills/duo-vessel/references/designs/goby-and-pistol.md")]
        public static void Open()
        {
            var config = AssetDatabase.LoadAssetAtPath<GobyPistolConfigSO>(ConfigPath);
            if (!config) Debug.LogWarning($"[GobyPistol] {ConfigPath} is missing: the harness runs on the artifact's SHIPPED defaults.");
            if (!GobyPistolDuo.AttachToLocalVessel(config))
                EditorUtility.DisplayDialog("Goby and Pistol", "No local vessel is flying. Start a mode and fly a vessel (any hull; the Goby itself is in the freestyle Vessel Changer), then open this again.", "OK");
        }

        [MenuItem(MenuPath, true)]
        static bool CanOpen() => EditorApplication.isPlaying;
    }
}
