using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for the destruction races whose score IS the arena (Cleave, Sirocco):
    /// the densest stand of mass that is hostile to the local pilot's domain.
    ///
    /// <para><b>Why not a crystal.</b> Rampage's arrow names the omni crystal because in Rampage
    /// the crystal is the weapon's trigger. In these two modes the crystal is an elemental pickup
    /// and nothing more - Sirocco's AI overrides crystal seeking outright for that reason - so
    /// pointing at it would teach the wrong objective. The thing a pilot is racing to destroy is
    /// the mass, and the question the arena takes away is not "where is mass" (it surrounds you)
    /// but "where is there still a LOT of it". Cleave's two open rungs are 2,160 units across, and
    /// Sirocco's forest thins unevenly as stands erode, so after the first minute that question
    /// has a real answer and it is far from obvious from the cockpit.</para>
    ///
    /// <para><b>The query is the platform's, not a new one.</b> <see cref="Cell.GetExplosionTarget"/>
    /// is the density query aggression-1 fauna hunt with and the one both modes' AI already steer
    /// by (Cleave's raid beat, Sirocco's erosion runs), so the arrow and the bots agree on where
    /// the fight is. It is cached inside <c>BlockDensityGrid</c> and costs a dictionary read plus a
    /// cached-result return on the steady state; this provider samples it on a slow clock anyway,
    /// because a centroid that moves every frame would make the arrow twitch.</para>
    ///
    /// <para><b>Multiplayer.</b> Every peer builds its own density grids from the prisms it
    /// simulates (environment mass is per-peer, <c>Docs/ECOSYSTEM.md §27</c>; trails replicate),
    /// so the answer is computed locally from what this machine shows the pilot - no server-only
    /// state is read. The domain is <see cref="IPlayer.Domain"/>, which every peer knows.</para>
    ///
    /// <para>The grid for a domain omits mass WEARING that domain's colour and shielded mass
    /// (<c>Cell.AddBlock</c>). In Sirocco that is exactly the scoring set - your own colour never
    /// scores and Charge plants need their shield shed first. Cleave's arena is painted across the
    /// whole triad and scores whatever it wears, so the arrow leans toward the two-thirds of it
    /// in other colours; the triad is evenly mixed, so the densest region is still the densest
    /// region.</para>
    ///
    /// The target is a proxy transform owned by this component (the <see cref="HijackObjectiveProvider"/>
    /// shape), because the answer is a position, not an object.
    /// </summary>
    public class HostileMassObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        /// <summary>
        /// Re-read period. Matches the AI's own re-target clock order of magnitude (Cleave 2 s,
        /// Sirocco 4 s) so the arrow settles on a stand long enough to be flown to.
        /// </summary>
        const float ResampleSeconds = 1.5f;

        Transform _proxy;
        Cell _cell;
        float _nextSample;
        bool _hasTarget;

        static readonly ProfilerMarker s_TryGetObjectiveMarker =
            new("HostileMassObjectiveProvider.TryGetObjective");

        public bool TryGetObjective(out Transform target)
        {
            using (s_TryGetObjectiveMarker.Auto())
            {
                target = null;

                if (gameData == null) return false;
                var localPlayer = gameData.LocalPlayer;
                var vesselTf = localPlayer?.Vessel?.Transform;
                if (vesselTf == null) return false;

                if (Time.time >= _nextSample || !_hasTarget)
                {
                    _nextSample = Time.time + ResampleSeconds;
                    _hasTarget = false;

                    // Re-resolve when the cached cell is gone (scene reload for replay).
                    if (!_cell) _cell = Cell.FindNearestActiveCell(vesselTf.position, sceneCellsOnly: true);
                    if (!_cell) return false;

                    EnsureProxy().position = _cell.GetExplosionTarget(localPlayer.Domain);
                    _hasTarget = true;
                }

                target = _proxy;
                return target != null;
            }
        }

        Transform EnsureProxy()
        {
            if (_proxy) return _proxy;
            var go = new GameObject("HostileMassObjectiveTarget");
            go.transform.SetParent(transform, false);
            _proxy = go.transform;
            return _proxy;
        }
    }
}
