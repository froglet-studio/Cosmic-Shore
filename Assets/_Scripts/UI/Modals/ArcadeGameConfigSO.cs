using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.UI
{
    [CreateAssetMenu(
            fileName = "ArcadeGameConfig",
            menuName = "ScriptableObjects/Arcade/ArcadeGameConfig")]
        public class ArcadeGameConfigSO : ScriptableObject
        {
            [Header("Runtime State")]
            public SO_ArcadeGame SelectedGame;
            public int           Intensity;
            public int           PlayerCount;
            [FormerlySerializedAs("TeamCount")]
            public int           DomainCount;
            public SO_Vessel     SelectedShip;
            public Domains       SelectedDomain;

            [Tooltip("The AIs the host PLACED, one entry per bot, in placement order - the Add AI " +
                     "button arms placement and a domain tile tap appends here. PlayerCount " +
                     "follows humans + this list; a launch below the card's minimum tops the " +
                     "difference up with domain-balanced AI, so an empty list is always legal.")]
            public List<Domains> AIDomains = new();

            public void ResetState()
            {
                SelectedGame   = null;
                Intensity      = 0;
                PlayerCount    = 0;
                DomainCount    = 1;
                SelectedShip   = null;
                SelectedDomain = Domains.Jade;
                AIDomains.Clear();
            }
        }
}
