using UnityEngine;
using System;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Data;
using CosmicShore.Utility;
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

        public void ResetToNeutralForReuse()
        {
            if (!prism) prism = GetComponent<Prism>();
            if (!materialAnimator) materialAnimator = GetComponent<MaterialPropertyAnimator>();

            currentDomain = Domains.Blue;

            if (!TryResolveMaterials(Domains.Blue, out var trans, out var opaque))
            {
                CSDebug.LogError("No Blue materials for environment-pool reuse", this);
                return;
            }
            materialAnimator.BindMaterialsImmediate(trans, opaque);
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
            if (!TryResolveMaterials(newDomain, out var trans, out var opaque))
            {
                CSDebug.LogError($"No materials found for team {newDomain}", this);
                return;
            }
            materialAnimator.UpdateMaterial(trans, opaque);
        }

        bool TryResolveMaterials(Domains domain, out Material transparent, out Material opaque)
        {
            transparent = null;
            opaque = null;
            if (!_themeManagerData) return false;
            if (!prism) prism = GetComponent<Prism>();
            if (!prism || prism.prismProperties == null) return false;

            var props = prism.prismProperties;
            if (props.IsDangerous)
            {
                transparent = _themeManagerData.GetTeamTransparentDangerousBlockMaterial(domain);
                opaque = _themeManagerData.GetTeamDangerousBlockMaterial(domain);
            }
            else if (props.IsShielded)
            {
                transparent = _themeManagerData.GetTeamTransparentShieldedBlockMaterial(domain);
                opaque = _themeManagerData.GetTeamShieldedBlockMaterial(domain);
            }
            else if (props.IsSuperShielded)
            {
                transparent = _themeManagerData.GetTeamTransparentSuperShieldedBlockMaterial(domain);
                opaque = _themeManagerData.GetTeamSuperShieldedBlockMaterial(domain);
            }
            else
            {
                transparent = _themeManagerData.GetTeamTransparentBlockMaterial(domain);
                opaque = _themeManagerData.GetTeamBlockMaterial(domain);
            }
            return transparent != null && opaque != null;
        }
    }
}
