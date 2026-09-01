using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Registry of every mode's <see cref="GameToastConfigSO"/> plus the shared config that
    /// covers mode-agnostic situations (player joined / ready / disconnected). Resolution
    /// order: the current mode's config first, then the shared config, then nothing - an
    /// unresolved situation simply shows no toast.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameToastLibrary",
        menuName = "ScriptableObjects/UI/Game Toast Library")]
    public class GameToastLibrarySO : ScriptableObject
    {
        [Tooltip("Mode-agnostic toasts every game shows (player joined / ready / disconnected).")]
        [SerializeField] private GameToastConfigSO sharedConfig;

        [Tooltip("One config per game mode. Modes without an entry fall back to the shared config only.")]
        [SerializeField] private List<GameToastConfigSO> modeConfigs = new();

        public GameToastConfigSO GetModeConfig(GameModes mode)
        {
            for (int i = 0, count = modeConfigs.Count; i < count; i++)
            {
                var config = modeConfigs[i];
                if (config != null && config.gameMode == mode)
                    return config;
            }

            return null;
        }

        /// <summary>Mode config first, shared config second.</summary>
        public bool TryResolve(GameModes mode, GameToastSituation situation, out GameToastDefinition definition)
        {
            var modeConfig = GetModeConfig(mode);
            if (modeConfig != null && modeConfig.TryGetDefinition(situation, out definition))
                return true;

            if (sharedConfig != null && sharedConfig.TryGetDefinition(situation, out definition))
                return true;

            definition = null;
            return false;
        }

        /// <summary>
        /// All idle-hint definitions active for <paramref name="mode"/> (mode config + shared).
        /// </summary>
        public void CollectIdleHints(GameModes mode, List<GameToastDefinition> results)
        {
            results.Clear();
            GetModeConfig(mode)?.CollectIdleHints(results);
            if (sharedConfig != null)
                sharedConfig.CollectIdleHints(results);
        }
    }
}
