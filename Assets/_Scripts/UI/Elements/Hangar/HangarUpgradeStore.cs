using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's bought elemental levels, per vessel and element (Docs/MENU_REDESIGN_B.md section 41).
    /// LOOK-ONLY by the owner's decision (2026-10-17: "make it look-only for now"): a level shapes the
    /// hull in the Hangar and nothing in a match reads it. Saved on the device; the crystals it costs
    /// come out of the player's real wallet.
    /// </summary>
    public static class HangarUpgradeStore
    {
        public const int MaxLevel = 10;

        static string Key(VesselClassType vessel, Element element) => $"hangar.upgrade.{(int)vessel}.{(int)element}";

        public static int GetLevel(VesselClassType vessel, Element element) =>
            Mathf.Clamp(PlayerPrefs.GetInt(Key(vessel, element), 0), 0, MaxLevel);

        public static void SetLevel(VesselClassType vessel, Element element, int level)
        {
            PlayerPrefs.SetInt(Key(vessel, element), Mathf.Clamp(level, 0, MaxLevel));
            PlayerPrefs.Save();
        }

        /// <summary>The price of the NEXT level (the owner: 100 crystals x the next level).</summary>
        public static int CostOfNext(int currentLevel, int perLevel) => perLevel * (currentLevel + 1);
    }
}
