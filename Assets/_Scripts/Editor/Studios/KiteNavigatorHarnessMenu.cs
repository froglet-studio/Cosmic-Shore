using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEditor;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// Seat the Kite &amp; Tail navigator on the local hull in Play mode (Crew Vessel Lab round 3). Start any match, then
    /// run the menu: you keep the hull and an AI squad mate flies the kite; D-pad left / right or 1 / 2 moves you to the
    /// kite and the AI takes the hull. Run "Remove" to hand the hull back. A reader tool: it writes no asset.
    /// </summary>
    static class KiteNavigatorHarnessMenu
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset";
        const string DocPath = "Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md";

        [MenuItem("FrogletTools/Vessels/Kite Navigator/Seat On Local Hull (Play mode)", false, 41)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 3, DocPath = DocPath,
            Description = "Put the Kite & Tail navigator on your hull in Play mode: an AI flies the kite while you fly, " +
                          "and the swap (D-pad left/right, 1/2) moves you to the kite.")]
        static void Seat()
        {
            var config = AssetDatabase.LoadAssetAtPath<KiteNavigatorConfigSO>(ConfigPath);
            if (!config)
            {
                CSDebug.LogWarning($"[KiteNavigator] No config at {ConfigPath}.");
                return;
            }
            KiteNavigator.AttachToLocalVessel(config);
        }

        [MenuItem("FrogletTools/Vessels/Kite Navigator/Seat On Local Hull (Play mode)", true)]
        static bool SeatValidate() => EditorApplication.isPlaying;

        [MenuItem("FrogletTools/Vessels/Kite Navigator/Remove (Play mode)", false, 42)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 2, DocPath = DocPath,
            Description = "Take the Kite & Tail navigator off your hull and give the hull back to you.")]
        static void Remove() => KiteNavigator.DetachActive();

        [MenuItem("FrogletTools/Vessels/Kite Navigator/Remove (Play mode)", true)]
        static bool RemoveValidate() => EditorApplication.isPlaying && KiteNavigator.Active;
    }
}
