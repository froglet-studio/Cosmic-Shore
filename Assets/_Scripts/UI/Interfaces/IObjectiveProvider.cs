using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Supplies the world-space target that an <see cref="ObjectiveIndicator"/>
    /// should point at. Each game mode implements this differently - SkimRace
    /// returns the next uncollected crystal for the local player, Joust returns
    /// the closest other player, etc.
    /// </summary>
    public interface IObjectiveProvider
    {
        /// <summary>
        /// True when an objective is available. <paramref name="target"/> is the
        /// world-space transform to point at; null/false means no objective and
        /// the indicator should hide.
        /// </summary>
        bool TryGetObjective(out Transform target);
    }
}
