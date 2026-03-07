using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(
        fileName = "SO_GameTipsList",
        menuName = "ScriptableObjects/UI/GameTipsList")]
    public class SO_GameTipsList : ScriptableObject
    {
        [Serializable]
        public struct GameModeTips
        {
            [Tooltip("The game mode these tips apply to.")]
            public GameModes GameMode;

            [Tooltip("Tips shown when this game mode is loading.")]
            [TextArea(1, 3)]
            public List<string> Tips;
        }

        [Header("Per-Game-Mode Tips")]
        [SerializeField] private List<GameModeTips> gameModeTips = new();

        [Header("Fallback Tips")]
        [Tooltip("Shown when no tips exist for the current game mode.")]
        [TextArea(1, 3)]
        [SerializeField] private List<string> generalTips = new();

        /// <summary>
        /// Returns the tip list for the given game mode.
        /// Falls back to generalTips if none are configured for that mode.
        /// </summary>
        public List<string> GetTips(GameModes mode)
        {
            for (int i = 0; i < gameModeTips.Count; i++)
            {
                if (gameModeTips[i].GameMode == mode && gameModeTips[i].Tips is { Count: > 0 })
                    return gameModeTips[i].Tips;
            }

            return generalTips is { Count: > 0 } ? generalTips : null;
        }
    }
}
