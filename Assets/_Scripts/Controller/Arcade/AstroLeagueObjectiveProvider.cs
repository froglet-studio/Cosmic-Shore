using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Astro League: the indicator always points at the ball.
    /// The ball is a single scene object that toggles <see cref="AstroLeagueBall.IsHidden"/>
    /// (hidden during goal celebration / kickoff reset) rather than being destroyed, so we
    /// cache it on first find and hide the indicator while it is hidden.
    /// </summary>
    public class AstroLeagueObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        AstroLeagueBall _ball;

        public bool TryGetObjective(out Transform target)
        {
            target = null;

            // Cache once. Until the ball has network-spawned this returns null and we keep
            // retrying; thereafter the reference is stable for the scene's lifetime.
            if (_ball == null)
                _ball = FindAnyObjectByType<AstroLeagueBall>();

            if (_ball == null || _ball.IsHidden) return false;

            target = _ball.transform;
            return true;
        }
    }
}
