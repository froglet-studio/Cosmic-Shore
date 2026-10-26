using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's bought elemental levels, per vessel and element (Docs/MENU_REDESIGN_B.md section 41).
    /// LOOK-ONLY by the owner's decision (2026-10-17: "make it look-only for now"): a level shapes the
    /// hull in the Hangar and nothing in a match reads it. Saved on the player's profile
    /// (<see cref="ProfileEconomy.HangarUpgrades"/>) in the same save as the crystals it costs, so a
    /// reinstall keeps both. Levels bought before that were saved on the device only (PlayerPrefs); a
    /// device level above the profile's is adopted into the profile the next time it is read.
    ///
    /// <para>Every element starts at level 1 (Rev 24, Shombith 2026-10-26: "they always start with 1 value"): a vessel
    /// nobody has upgraded reads 1, which lights one petal of the element's flower, and the first UPGRADE buys level 2.
    /// The floor is applied on read, so a saved 0 (or no record at all) is a 1 and nothing has to be migrated.</para>
    /// </summary>
    public static class HangarUpgradeStore
    {
        public const int MaxLevel = 10;
        public const int StartLevel = 1;

        /// <summary>A level was bought: the vessel, the element and the new level. The arcade cards redraw their flowers.</summary>
        public static event System.Action<VesselClassType, Element, int> LevelChanged;

        /// <summary>A stored level as the player sees it: never below <see cref="StartLevel"/>, never above <see cref="MaxLevel"/>.</summary>
        public static int Shown(int stored) => Mathf.Clamp(stored, StartLevel, MaxLevel);

        // Where levels lived before they moved onto the profile (2026-10-25). Read only, never written:
        // the key stays so a level bought then is still adopted after a cloud reload replaces the profile.
        static string LegacyDeviceKey(VesselClassType vessel, Element element) => $"hangar.upgrade.{(int)vessel}.{(int)element}";

        public static int GetLevel(VesselClassType vessel, Element element)
        {
            var profile = PlayerDataService.Instance;
            int saved = profile ? profile.GetHangarUpgradeLevel((int)vessel, (int)element) : 0;
            int device = PlayerPrefs.GetInt(LegacyDeviceKey(vessel, element), 0);
            // Levels only go up, so the higher of the two is what the player paid for. Adopted only once the
            // cloud profile has loaded: a save before then would copy a default profile over the player's record.
            if (device > saved && profile && profile.IsInitialized)
                profile.SetHangarUpgradeLevel((int)vessel, (int)element, Mathf.Clamp(device, 0, MaxLevel));
            return Shown(Mathf.Max(saved, device));
        }

        public static void SetLevel(VesselClassType vessel, Element element, int level)
        {
            var profile = PlayerDataService.Instance;
            if (profile) profile.SetHangarUpgradeLevel((int)vessel, (int)element, Mathf.Clamp(level, 0, MaxLevel));
            LevelChanged?.Invoke(vessel, element, Shown(level));
        }

        /// <summary>The price of the NEXT level (the owner: 100 crystals x the next level).</summary>
        public static int CostOfNext(int currentLevel, int perLevel) => perLevel * (currentLevel + 1);
    }
}
