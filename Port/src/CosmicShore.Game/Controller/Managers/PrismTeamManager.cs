using CosmicShore.Engine;
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

        // PORT Deviation (V13, restore when Prism ports): private Prism prism;
        // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): private MaterialPropertyAnimator materialAnimator;
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
            // PORT Deviation (V13, restore when Prism ports): prism = GetComponent<Prism>();
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): materialAnimator = GetComponent<MaterialPropertyAnimator>();
        }

        public void SetInitialTeam(Domains domain)
        {
            if (currentDomain == Domains.Blue)
            {
                Domain = domain;
                // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): materialAnimator.UpdateMaterial(
                // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamTransparentBlockMaterial(domain),
                // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamBlockMaterial(domain)
                // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): );
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
            // PORT Deviation (V13, restore when Prism ports): if (prism.prismProperties.IsSuperShielded) return;

            // PORT Deviation (V13, restore when Prism ports): if (!superSteal && prism.prismProperties.IsShielded)
            // PORT Deviation (V13, restore when Prism ports): {
            // PORT Deviation (V13, restore when Prism ports):     prism.DeactivateShields();
            // PORT Deviation (V13, restore when Prism ports):     return;
            // PORT Deviation (V13, restore when Prism ports): }

            playerName ??= "No name";

            // TODO - Raise events about steal.

            onPrismStolen.Raise(
                new PrismStats
                {
                    OwnName = playerName,
                    // PORT Deviation (V13, restore when Prism ports): Volume = prism.Volume,
                    // PORT Deviation (V13, restore when Prism ports): AttackerName = prism.PlayerName
                });

            /*if (CellControlManager.Instance)
            {
                CellControlManager.Instance.StealBlock(newDomain, prism.prismProperties);
            }*/

            ChangeTeam(newDomain);
        }

        private void HandleTeamChange(Domains oldDomain, Domains newDomain)
        {
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): if (prism.prismProperties.IsDangerous)
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): {
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):     materialAnimator.UpdateMaterial(
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):         _themeManagerData.GetTeamTransparentDangerousBlockMaterial(newDomain),
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):         _themeManagerData.GetTeamDangerousBlockMaterial(newDomain)
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):     );
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): }
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): else if (prism.prismProperties.IsShielded)
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): {
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):     materialAnimator.UpdateMaterial(
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):         _themeManagerData.GetTeamTransparentShieldedBlockMaterial(newDomain),
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):         _themeManagerData.GetTeamShieldedBlockMaterial(newDomain)
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):     );
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): }
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): else if (prism.prismProperties.IsSuperShielded)
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): {
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):     materialAnimator.UpdateMaterial(
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):         _themeManagerData.GetTeamTransparentSuperShieldedBlockMaterial(newDomain),
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):         _themeManagerData.GetTeamSuperShieldedBlockMaterial(newDomain)
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):     );
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): }
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): else
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): {
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):     materialAnimator.UpdateMaterial(
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):         _themeManagerData.GetTeamTransparentBlockMaterial(newDomain),
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):         _themeManagerData.GetTeamBlockMaterial(newDomain)
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port):     );
            // PORT Deviation (V13, restore when Prism + MaterialPropertyAnimator port): }
        }
    }
}
