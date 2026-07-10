// Ported verbatim from Assets/_Scripts/UI/IStatExposable.cs (stats-reporter
// family 2026-07-10). No substitutions.
using System.Collections.Generic;

namespace CosmicShore.UI
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
