using System.Collections.Generic;

namespace CosmicShore.Game.Arcade
{
    public interface IStatExposable
    {
        /// <summary>
        /// Returns a dictionary of stat display names to their current values.
        /// Called by UniversalStatsProvider to retrieve stat values.
        /// </summary>
        Dictionary<string, object> GetExposedStats();
    }
}