using CosmicShore.Gameplay;
using UnityEngine;
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

        private Prism prism;
        private MaterialPropertyAnimator materialAnimator;
        private PrismTeamManager teamManager;
        private PrismOctahedronShield octahedronShield; // auto-added in Awake so every prism gets the octahedron on shield

        public BlockState CurrentState { get; private set; } = BlockState.Normal;

        private void Awake()
        {
            prism = GetComponent<Prism>();
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            teamManager = GetComponent<PrismTeamManager>();

            // Every prism gets an octahedron shield. If the prefab already
            // carries one (e.g. BlueBlock has it wired explicitly) we reuse
            // it; otherwise we add one at runtime so existing prefabs don't
            // need to be touched individually. The component's Awake resolves
            // BoxCollider / MeshFilter / Rigidbody from the same GameObject.
            octahedronShield = GetComponent<PrismOctahedronShield>();
            if (octahedronShield == null)
                octahedronShield = gameObject.AddComponent<PrismOctahedronShield>();
        }

        public void MakeDangerous()
        {
            prism.prismProperties.IsDangerous = true;
            prism.prismProperties.speedDebuffAmount = 0.1f;
            prism.prismProperties.IsShielded = false;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentDangerousBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamDangerousBlockMaterial(teamManager.Domain)
            );
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

            prism.prismProperties.IsSuperShielded = true;
            prism.prismProperties.IsDangerous = false;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentSuperShieldedBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamSuperShieldedBlockMaterial(teamManager.Domain)
            );
            CurrentState = BlockState.SuperShielded;

            // Opt-in octahedron shield visual/collider swap. Prisms without
            // this component keep the legacy material-only supershield.
            if (octahedronShield != null) octahedronShield.Engage();

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
            prism.prismProperties.IsShielded = true;
            prism.prismProperties.IsDangerous = false;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentShieldedBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamShieldedBlockMaterial(teamManager.Domain)
            );
            CurrentState = BlockState.Shielded;

            // Engage the octahedron visual/collider swap for the regular
            // shield state too, matching super shield behavior.
            if (octahedronShield != null) octahedronShield.Engage();

            SyncAOERegistryShieldState();
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldActivate);
        }

        private void ApplyNormalState()
        {
            var wasShielded = prism.prismProperties.IsShielded || prism.prismProperties.IsSuperShielded;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamBlockMaterial(teamManager.Domain)
            );

            prism.prismProperties.IsShielded = false;
            prism.prismProperties.IsSuperShielded = false;
            CurrentState = BlockState.Normal;

            if (octahedronShield != null) octahedronShield.Disengage();

            SyncAOERegistryShieldState();

            if (wasShielded)
                AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldDeactivate);
        }

        private void SyncAOERegistryShieldState()
        {
            if (prism.AOERegistryIndex >= 0)
                PrismAOERegistry.Instance?.UpdateShieldState(
                    prism.AOERegistryIndex,
                    prism.prismProperties.IsShielded,
                    prism.prismProperties.IsSuperShielded);
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
