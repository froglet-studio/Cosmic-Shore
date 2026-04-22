using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Maps GameModes to pre-game cinematic setups.
    /// Assign in the Inspector to configure per-mode camera behavior.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PreGameCinematicLibrary",
        menuName = "ScriptableObjects/Cinematics/Pre-Game Cinematic Library")]
    public class PreGameCinematicLibrarySO : ScriptableObject
    {
        [System.Serializable]
        public struct GameModeCinematicEntry
        {
            [Tooltip("The game mode this setup applies to.")]
            public GameModes gameMode;

            [Tooltip("The pre-game cinematic configuration for this mode.")]
            public PreGameCinematicSetupSO setup;
        }

        [Header("Mode Mappings")]
        [Tooltip("Map each game mode to its pre-game cinematic setup.")]
        [SerializeField] private List<GameModeCinematicEntry> entries = new();

        [Header("Fallback")]
        [Tooltip("Default setup used when no mode-specific entry is found.")]
        [SerializeField] private PreGameCinematicSetupSO defaultSetup;

        private Dictionary<GameModes, PreGameCinematicSetupSO> _lookup;

        /// <summary>
        /// Returns the cinematic setup for the given game mode, or the default if none is mapped.
        /// </summary>
        public PreGameCinematicSetupSO GetSetup(GameModes mode)
        {
            BuildLookup();
            return _lookup.TryGetValue(mode, out var setup) ? setup : defaultSetup;
        }

        private void BuildLookup()
        {
            if (_lookup != null) return;

            _lookup = new Dictionary<GameModes, PreGameCinematicSetupSO>();
            foreach (var entry in entries)
            {
                if (entry.setup != null)
                    _lookup[entry.gameMode] = entry.setup;
            }
        }

        private void OnValidate()
        {
            _lookup = null; // Force rebuild on Inspector changes
        }
    }
}
