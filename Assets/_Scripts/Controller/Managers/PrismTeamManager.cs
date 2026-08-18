using UnityEngine;
using System;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class PrismTeamManager : MonoBehaviour
    {
        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        [SerializeField] private ScriptableEventPrismStats onPrismStolen;

        private Prism prism;
        private MaterialPropertyAnimator materialAnimator;
        private Domains currentDomain = Domains.Blue;

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
            if (currentDomain == Domains.Blue)
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

            // Super-shielded prisms are fully invulnerable: no team change,
            // no shield decay. Ways to break super-shields will be added
            // later as targeted opt-in mechanics.
            if (prism.prismProperties.IsSuperShielded) return;

            if (!superSteal && prism.prismProperties.IsShielded)
            {
                prism.DeactivateShields();
                return;
            }

            playerName ??= "No name";

            // Capture the payload BEFORE the flip (AttackerName is the PREVIOUS owner), but
            // RAISE it after: the raise runs its listeners inline, so a throwing listener - or
            // an unwired event slot on a prism variant - used to abort the steal itself, and
            // the caller's whole effect chain with it (the Urchin's chain volley runs AFTER the
            // steal in the same effect list). The steal is gameplay; the event is reporting.
            // Reporting must never be able to veto gameplay.
            var stolenStats = new PrismStats
            {
                OwnName = playerName,
                Volume = prism.Volume,
                AttackerName = prism.PlayerName
            };

            ChangeTeam(newDomain);

            onPrismStolen.Raise(stolenStats);
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
