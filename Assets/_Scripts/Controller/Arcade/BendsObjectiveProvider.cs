using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for The Bends: the closest pilot this player is actually allowed to
    /// bend.
    ///
    /// Same shape as <see cref="DogFightObjectiveProvider"/> and for the same reason - the domain
    /// check is the whole difference from <see cref="JoustObjectiveProvider"/>. Teammates cannot
    /// be caught in your blast at all (<c>ExplosionImpactor.AcceptImpactee</c> declines own-domain
    /// vessels), so in a 2v2 an arrow that pointed at the nearest player would spend the match
    /// pointing at the one vessel in the arena worth nothing.
    ///
    /// Deliberately NOT the crystal, even though the crystal is what fires the weapon: the crystal
    /// is a cell item the platform's own seeking already surfaces, and a forest thick enough to
    /// charge in is a forest thick enough to lose somebody in. The arrow answers the question the
    /// forest takes away - "which way is the fight".
    /// </summary>
    public class BendsObjectiveProvider : MonoBehaviour, IObjectiveProvider
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

                // Same-domain pilots cannot be bent, so they are not objectives.
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
