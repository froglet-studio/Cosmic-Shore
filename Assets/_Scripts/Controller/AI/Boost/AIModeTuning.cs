using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A mode's own copy of any AI tuning asset: <c>Resources/AIBoostPolicies/&lt;asset name&gt;_&lt;mode&gt;</c> of the same type,
    /// else the asset itself - the <see cref="AIBoostPolicySO.ForMode"/> rule for configs that are not boost policies (the
    /// Stoat's three: <c>StoatModeAction</c>, <c>StoatDipoleConfig</c>, <c>StoatSlingConfig</c>). Written by
    /// <c>Tools/Build/learn_drives_from_flights.py</c> from people's editor flights; a hull reads ONLY its autopilot numbers
    /// through it, so a person flying the same hull in the same mode is untouched, and so is every other mode.
    /// </summary>
    public static class AIModeTuning
    {
        public static T For<T>(T asset, GameModes mode) where T : ScriptableObject
        {
            if (!asset) return asset;
            var variant = Resources.Load<T>($"{AIBoostPolicySO.ModeVariantFolder}/{asset.name}_{mode}");
            return variant ? variant : asset;
        }

        /// <summary>The variant for the mode <paramref name="status"/>'s pilot races in, once that is known; null until then
        /// (ask again next frame: the game data is wired after the vessel's executors initialise).</summary>
        public static T ForPilot<T>(T asset, IVesselStatus status) where T : ScriptableObject
        {
            var ai = status?.AIPilot;
            return ai && ai.TryGetGameMode(out var mode) ? For(asset, mode) : null;
        }
    }
}
