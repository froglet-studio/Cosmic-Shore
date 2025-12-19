using UnityEngine;
using System;
using CosmicShore.Game;
using CosmicShore.Utilities;

namespace CosmicShore.Core
{
    public class PrismTeamManager : MonoBehaviour
    {
        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        [SerializeField] private ScriptableEventPrismStats onPrismStolen;

        private Prism prism;
        private MaterialPropertyAnimator materialAnimator;
        private Domains currentDomain = Domains.Unassigned;

        public Domains Domain
        {
            get => currentDomain;
            private set
            {
                if (currentDomain != value)
                {
                    var oldDomain = currentDomain;
                    currentDomain = value;
                    HandleTeamChange(oldDomain, currentDomain);
                    OnTeamChanged?.Invoke(oldDomain, value);
                }
            }
        }

        public event Action<Domains, Domains> OnTeamChanged;

        private void Awake()
        {
            prism = GetComponent<Prism>();
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
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
            
            if (!superSteal && (prism.prismProperties.IsShielded || prism.prismProperties.IsSuperShielded))
            {
                prism.DeactivateShields();
                return;
            }

            playerName ??= "No name";

            // TODO - Raise events about steal.
                
            onPrismStolen.Raise(
                new PrismStats
                {
                    OwnName = playerName,
                    Volume = prism.Volume,
                    AttackerName = prism.PlayerName
                });
            /*if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.PrismStolen(newTeam, playerName, trailBlock.PrismProperties);
                }*/

            if (CellControlManager.Instance)
            {
                CellControlManager.Instance.StealBlock(newDomain, prism.prismProperties);
            }

            ChangeTeam(newDomain);
            // trailBlock.Player = player;
        }

        private void HandleTeamChange(Domains oldDomain, Domains newDomain)
        {
            if (prism.prismProperties.IsDangerous)
            {
                materialAnimator.UpdateMaterial(
                    _themeManagerData.GetTeamTransparentDangerousBlockMaterial(newDomain),
                    _themeManagerData.GetTeamDangerousBlockMaterial(newDomain)
                );
            }
            else if (prism.prismProperties.IsShielded)
            {
                materialAnimator.UpdateMaterial(
                    _themeManagerData.GetTeamTransparentShieldedBlockMaterial(newDomain),
                    _themeManagerData.GetTeamShieldedBlockMaterial(newDomain)
                );
            }
            else if (prism.prismProperties.IsSuperShielded)
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
