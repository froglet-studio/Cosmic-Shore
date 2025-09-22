using UnityEngine;
using System;
using CosmicShore.Game;
using CosmicShore.Utilities;

namespace CosmicShore.Core
{
    public class BlockTeamManager : MonoBehaviour
    {
        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        [SerializeField] private ScriptableEventPrismStats onPrismStolen;

        private TrailBlock trailBlock;
        private MaterialPropertyAnimator materialAnimator;
        private Domains currentDomain = Domains.Unassigned;

        public Domains Domain
        {
            get => currentDomain;
            private set
            {
                if (currentDomain != value)
                {
                    var oldTeam = currentDomain;
                    currentDomain = value;
                    OnTeamChanged?.Invoke(oldTeam, value);
                }
            }
        }

        public event Action<Domains, Domains> OnTeamChanged;

        private void Awake()
        {
            trailBlock = GetComponent<TrailBlock>();
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
        }

        private void OnEnable()
        {
            OnTeamChanged += HandleTeamChange;
        }

        private void OnDisable()
        {
            OnTeamChanged -= HandleTeamChange;
        }

        public void SetInitialTeam(Domains domain)
        {
            if (currentDomain == Domains.Unassigned)
            {
                Domain = domain;
                materialAnimator.UpdateMaterial(
                    _themeManagerData.GetTeamTransparentBlockMaterial(domain),
                    _themeManagerData.GetTeamBlockMaterial(domain)
                );
            }
        }

        public void ChangeTeam(Domains newDomain)
        {
            if (Domain != newDomain)
            {
                Domain = newDomain;
            }
        }

        public void Steal(string playerName, Domains newDomain, bool superSteal)
        {
            if (Domain == newDomain) 
                return;
            
            if (!superSteal && (trailBlock.TrailBlockProperties.IsShielded || trailBlock.TrailBlockProperties.IsSuperShielded))
            {
                trailBlock.DeactivateShields();
                return;
            }

            playerName ??= "No name";

            // TODO - Raise events about steal.
                
            onPrismStolen.Raise(
                new PrismStats
                {
                    PlayerName = playerName,
                    Volume = trailBlock.Volume,
                    OtherPlayerName = trailBlock.PlayerName
                });
            /*if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.PrismStolen(newTeam, playerName, trailBlock.TrailBlockProperties);
                }*/

            if (CellControlManager.Instance)
            {
                CellControlManager.Instance.StealBlock(newDomain, trailBlock.TrailBlockProperties);
            }

            ChangeTeam(newDomain);
            // trailBlock.Player = player;
        }

        private void HandleTeamChange(Domains oldDomain, Domains newDomain)
        {
            if (trailBlock.TrailBlockProperties.IsDangerous)
            {
                materialAnimator.UpdateMaterial(
                    _themeManagerData.GetTeamTransparentDangerousBlockMaterial(newDomain),
                    _themeManagerData.GetTeamDangerousBlockMaterial(newDomain)
                );
            }
            else if (trailBlock.TrailBlockProperties.IsShielded)
            {
                materialAnimator.UpdateMaterial(
                    _themeManagerData.GetTeamTransparentShieldedBlockMaterial(newDomain),
                    _themeManagerData.GetTeamShieldedBlockMaterial(newDomain)
                );
            }
            else if (trailBlock.TrailBlockProperties.IsSuperShielded)
            {
                materialAnimator.UpdateMaterial(
                    _themeManagerData.GetTeamTransparentSuperShieldedBlockMaterial(newDomain),
                    _themeManagerData.GetTeamSuperShieldedBlockMaterial(newDomain)
                );  
            }
            else
            {
                materialAnimator.UpdateMaterial(
                    _themeManagerData.GetTeamTransparentBlockMaterial(newDomain),
                    _themeManagerData.GetTeamBlockMaterial(newDomain)
                );
            }
        }
    }
}
