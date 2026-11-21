using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    public struct StatData
    {
        public string Label;
        public string Value;
        public Sprite Icon;
    }
    
    /// <summary>
    /// Base class for game-mode specific stats.
    /// Implement this for Blitz, SkimRace, etc.
    /// </summary>
    public abstract class ScoreboardStatsProvider : MonoBehaviour
    {
        public abstract List<StatData> GetStats();

        /// <summary>
        /// A value arrived after <see cref="GetStats"/> was read (another pilot's last numbers reach this machine just
        /// after the turn ends): the scoreboard reads the stats again.
        /// </summary>
        public event System.Action Changed;

        protected void RaiseChanged() => Changed?.Invoke();
    }
}