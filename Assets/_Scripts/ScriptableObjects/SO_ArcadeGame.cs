using CosmicShore.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Game", menuName = "ScriptableObjects/Game/ArcadeGame", order = 0)]
    [System.Serializable]
    public class SO_ArcadeGame : SO_Game
    {
        [FormerlySerializedAs("Captains")]
        public List<SO_Vessel> Vessels;

        public int MinPlayersAllowed = 1;
        public int MaxPlayersAllowed = 2;

        [Tooltip("Per-game bounds for the domain-count stepper in the configure modal. " +
                 "Games that require a fixed team shape (e.g. Astro League needs exactly two " +
                 "domains for its two goals) constrain the range here.")]
        [Range(1, 3)] public int MinDomainsAllowed = 1;
        [Range(1, 3)] public int MaxDomainsAllowed = 3;

        [Min(1)] public int MinIntensity = 1;
        [Range(1, 4)] public int MaxIntensity = 4;
        public CallToActionTargetType CallToActionTargetType;
        public UserActionType ViewUserAction;
        public UserActionType PlayUserAction;
    }
}
