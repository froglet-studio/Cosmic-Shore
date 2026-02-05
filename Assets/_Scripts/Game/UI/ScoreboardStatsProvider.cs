using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public struct StatData
    {
        public string Label;
        public string Value;
        public Sprite Icon;
    }
    
    /// <summary>
    /// Base class for game-mode specific stats.
    /// Implement this for Blitz, HexRace, etc.
    /// </summary>
    public abstract class ScoreboardStatsProvider : MonoBehaviour
    {
        public abstract List<StatData> GetStats();
    }
}