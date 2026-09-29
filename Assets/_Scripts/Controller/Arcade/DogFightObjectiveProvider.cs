using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Dog Fight: the closest vessel this pilot is actually allowed to
    /// shoot.
    ///
    /// Deliberately NOT <see cref="JoustObjectiveProvider"/>, which points at the nearest other
    /// player regardless of colour. That is right for Joust - overtaking a teammate buffs them,
    /// so a teammate is a legitimate thing to fly at - and wrong here: teammates cannot be
    /// damaged at all (<c>Projectile.DisallowImpactOnVessel</c> refuses own-domain contact), so
    /// in a 2v2 the arrow would spend the match pointing at the one vessel in the arena that is
    /// worth nothing. The one added line is the domain check, and it is the whole difference.
    ///
    /// NEAREST rather than "the pilot who is winning" or "the one hunting me" on purpose: in an
    /// arena built to break sightlines, the question the arrow has to answer is "which way is
    /// the fight" when a wreck has just swallowed your target. Anything cleverer would fight the
    /// player's own read of the situation instead of restoring the one thing the Boneyard takes
    /// away.
    /// </summary>
    public class DogFightObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        public bool TryGetObjective(out Transform target)
        {
            target = null;

            if (gameData == null) return false;

            var localPlayer = gameData.LocalPlayer;
            var localVessel = localPlayer?.Vessel;
            if (localVessel == null) return false;

            List<IPlayer> players = gameData.Players;
            if (players == null || players.Count == 0) return false;

            var origin = localVessel.Transform.position;
            float bestSqr = float.MaxValue;
            Transform bestTransform = null;

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player == localPlayer) continue;

                // Same-domain pilots are un-shootable, so they are not objectives.
                if (player.Domain == localPlayer.Domain) continue;

                var vessel = player.Vessel;
                if (vessel == null) continue;

                var t = vessel.Transform;
                if (t == null) continue;

                float sqr = (t.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestTransform = t;
                }
            }

            target = bestTransform;
            return target != null;
        }
    }
}
