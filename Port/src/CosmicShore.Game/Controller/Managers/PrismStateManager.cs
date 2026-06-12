using CosmicShore.Gameplay;
using CosmicShore.Engine;
using System.Collections;
using System;
using CosmicShore.Core;

namespace CosmicShore.Gameplay
{
    public enum BlockState
    {
        Normal,
        Shielded,
        SuperShielded,
        Dangerous
    }

    public class PrismStateManager : MonoBehaviour
    {
        [Header("Data Containers")] [SerializeField]
        ThemeManagerDataContainerSO _themeManagerData;

        // PORT Deviation (V13, restore when Prism ports): private Prism prism;
        // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): private MaterialPropertyAnimator materialAnimator;
        private PrismTeamManager teamManager;
        // PORT Deviation (V13, restore when PrismOctahedronShield ports): private PrismOctahedronShield octahedronShield; // auto-added in Awake so every prism gets the octahedron on shield

        public BlockState CurrentState { get; private set; } = BlockState.Normal;

        private void Awake()
        {
            // PORT Deviation (V13, restore when Prism ports): prism = GetComponent<Prism>();
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): materialAnimator = GetComponent<MaterialPropertyAnimator>();
            teamManager = GetComponent<PrismTeamManager>();

            // Every prism gets an octahedron shield. If the prefab already
            // carries one (e.g. BlueBlock has it wired explicitly) we reuse
            // it; otherwise we add one at runtime so existing prefabs don't
            // need to be touched individually. The component's Awake resolves
            // BoxCollider / MeshFilter / Rigidbody from the same GameObject.
            // PORT Deviation (V13, restore when PrismOctahedronShield ports): octahedronShield = GetComponent<PrismOctahedronShield>();
            // PORT Deviation (V13, restore when PrismOctahedronShield ports): if (octahedronShield == null)
            // PORT Deviation (V13, restore when PrismOctahedronShield ports):     octahedronShield = gameObject.AddComponent<PrismOctahedronShield>();
        }

        public void MakeDangerous()
        {
            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.IsDangerous = true;
            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.speedDebuffAmount = 0.1f;
            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.IsShielded = false;

            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): materialAnimator.UpdateMaterial(
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamTransparentDangerousBlockMaterial(teamManager.Domain),
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamDangerousBlockMaterial(teamManager.Domain)
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): );
            CurrentState = BlockState.Dangerous;
        }

        public void ActivateShield(float? duration = null)
        {
            // Cancel any pending timer before applying new state
            PrismTimerManager.EnsureInstance().CancelTimers(this);

            ApplyShieldState();

            if (duration.HasValue)
            {
                PrismTimerManager.EnsureInstance().ScheduleShieldDeactivation(this, duration.Value);
            }
        }

        public void ActivateSuperShield()
        {
            PrismTimerManager.EnsureInstance().CancelTimers(this);

            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.IsSuperShielded = true;
            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.IsDangerous = false;

            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): materialAnimator.UpdateMaterial(
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamTransparentSuperShieldedBlockMaterial(teamManager.Domain),
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamSuperShieldedBlockMaterial(teamManager.Domain)
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): );
            CurrentState = BlockState.SuperShielded;

            // Opt-in octahedron shield visual/collider swap. Prisms without
            // this component keep the legacy material-only supershield.
            // PORT Deviation (V13, restore when PrismOctahedronShield ports): if (octahedronShield != null) octahedronShield.Engage();

            SyncAOERegistryShieldState();
        }

        public void DeactivateShields(float? delay = null)
        {
            PrismTimerManager.EnsureInstance().CancelTimers(this);

            if (delay.HasValue)
            {
                PrismTimerManager.EnsureInstance().ScheduleShieldDeactivation(this, delay.Value);
            }
            else
            {
                ApplyNormalState();
            }
        }

        /// <summary>
        /// Called by PrismTimerManager when a scheduled deactivation timer expires.
        /// </summary>
        internal void ExecuteTimerDeactivation()
        {
            ApplyNormalState();
        }

        private void ApplyShieldState()
        {
            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.IsShielded = true;
            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.IsDangerous = false;

            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): materialAnimator.UpdateMaterial(
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamTransparentShieldedBlockMaterial(teamManager.Domain),
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamShieldedBlockMaterial(teamManager.Domain)
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): );
            CurrentState = BlockState.Shielded;

            // Engage the octahedron visual/collider swap for the regular
            // shield state too, matching super shield behavior.
            // PORT Deviation (V13, restore when PrismOctahedronShield ports): if (octahedronShield != null) octahedronShield.Engage();

            SyncAOERegistryShieldState();
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldActivate);
        }

        private void ApplyNormalState()
        {
            // PORT Deviation (V13, restore when Prism ports): var wasShielded = prism.prismProperties.IsShielded || prism.prismProperties.IsSuperShielded;
            // CurrentState mirrors the prismProperties shield booleans (this class is their
            // single writer), so the state enum stands in until Prism lands (V15).
            var wasShielded = CurrentState == BlockState.Shielded || CurrentState == BlockState.SuperShielded;

            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): materialAnimator.UpdateMaterial(
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamTransparentBlockMaterial(teamManager.Domain),
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports):     _themeManagerData.GetTeamBlockMaterial(teamManager.Domain)
            // PORT Deviation (V13, restore when MaterialPropertyAnimator ports): );

            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.IsShielded = false;
            // PORT Deviation (V13, restore when Prism ports): prism.prismProperties.IsSuperShielded = false;
            CurrentState = BlockState.Normal;

            // PORT Deviation (V13, restore when PrismOctahedronShield ports): if (octahedronShield != null) octahedronShield.Disengage();

            SyncAOERegistryShieldState();

            if (wasShielded)
                AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldDeactivate);
        }

        private void SyncAOERegistryShieldState()
        {
            // PORT Deviation (V13, restore when Prism / PrismAOERegistry port (V15)): if (prism.AOERegistryIndex >= 0)
            // PORT Deviation (V13, restore when Prism / PrismAOERegistry port (V15)):     PrismAOERegistry.Instance?.UpdateShieldState(
            // PORT Deviation (V13, restore when Prism / PrismAOERegistry port (V15)):         prism.AOERegistryIndex,
            // PORT Deviation (V13, restore when Prism / PrismAOERegistry port (V15)):         prism.prismProperties.IsShielded,
            // PORT Deviation (V13, restore when Prism / PrismAOERegistry port (V15)):         prism.prismProperties.IsSuperShielded);
        }

        private void OnDisable()
        {
            PrismTimerManager.Instance?.CancelTimers(this);
        }

        private void OnDestroy()
        {
            PrismTimerManager.Instance?.CancelTimers(this);
        }
    }
}
