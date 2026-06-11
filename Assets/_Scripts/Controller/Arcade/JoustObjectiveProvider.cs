using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Joust: the closest other player's vessel.
    /// </summary>
    public class JoustObjectiveProvider : MonoBehaviour, IObjectiveProvider
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
