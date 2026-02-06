using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Models.Enums;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class MultiplayerHexRaceHUDView : MiniGameHUDView
    {
        [Header("Multiplayer Elements")]
        [SerializeField] private Transform playerScoreContainer;
        [SerializeField] private PlayerScoreCard playerScoreCardPrefab;

        [Header("Domain Styling")]
        [SerializeField] private List<DomainColorDef> domainColors;

        public Transform PlayerScoreContainer => playerScoreContainer;
        public PlayerScoreCard PlayerScoreCardPrefab => playerScoreCardPrefab;

        public void ClearPlayerList()
        {
            foreach (Transform child in playerScoreContainer)
            {
                Destroy(child.gameObject);
            }
        }

        public Color GetColorForDomain(Domains domain)
        {
            var def = domainColors.FirstOrDefault(d => d.Domain == domain);
            return def.Equals(default(DomainColorDef)) ? Color.white : def.Color;
        }

        [Serializable]
        public struct DomainColorDef
        {
            public Domains Domain;
            public Color Color;
        }
    }
}