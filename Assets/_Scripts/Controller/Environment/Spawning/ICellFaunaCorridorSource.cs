using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An authored environment that asks its cell for a fauna path corridor (<see cref="FaunaCorridor"/>), the
    /// way a garden environment offers planting sites: the environment DESCRIBES the corridor (a cell-local
    /// path and a radius) and the <see cref="Cell"/> adopts it when it builds that environment, through the
    /// same <see cref="Cell.SetFaunaPathCorridor"/> a race controller calls (Switchback). Nothing is culled
    /// or walled: the corridor is the cell's spatial diet + steering rule.
    ///
    /// <para>Implemented by <see cref="SpawnableRegattaRails"/>, whose corridor radius is a course setting
    /// (<see cref="RegattaLifeformSiteSettings.faunaCorridorRadius"/>, 0 = none, which is every shipped
    /// course).</para>
    /// </summary>
    public interface ICellFaunaCorridorSource
    {
        /// <summary>
        /// Fill <paramref name="pathLocal"/> (cleared first) with the corridor's path in the environment's
        /// local space. False, or a non-positive radius, means no corridor.
        /// </summary>
        bool TryGetFaunaCorridor(List<Vector3> pathLocal, out float radius, out bool closed, out float goalStandoff);
    }
}
