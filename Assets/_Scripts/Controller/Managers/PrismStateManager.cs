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

        [Header("Shield Transition Tuning")]
        [Tooltip("Duration of the material color lerp when shielding/unshielding. Short values make the transition read as an event instead of a slow fade and drain the material animation job queue faster.")]
        [SerializeField] private float shieldMaterialTransitionDuration = 0.15f;

        private Prism prism;
        private MaterialPropertyAnimator materialAnimator;
        private PrismTeamManager teamManager;

        // Optional per-activation override set by callers that know the spatial origin
        // of the shield wave (e.g. the crystal that triggered it). Consumed once by the
        // next ApplyShieldState/ApplySuperShieldState call, then cleared.
        private Vector3? _pendingShieldOriginWS;

        public BlockState CurrentState { get; private set; } = BlockState.Normal;

        private void Awake()
        {
            prism = GetComponent<Prism>();
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            teamManager = GetComponent<PrismTeamManager>();
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
            ActivateShield(duration, null);
        }

        /// <summary>
        /// Activate a shield with an explicit spatial origin for the shield wave.
        /// The origin is used by <see cref="PrismShieldBroadcaster"/> to coalesce
        /// nearby simultaneous activations into a single shockwave event instead
        /// of one visual per prism.
        /// </summary>
        public void ActivateShield(float? duration, Vector3? originWS)
        {
            // Cancel any pending timer before applying new state
            PrismTimerManager.EnsureInstance().CancelTimers(this);

            _pendingShieldOriginWS = originWS;
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
                _themeManagerData.GetTeamSuperShieldedBlockMaterial(teamManager.Domain),
                shieldMaterialTransitionDuration
            );
            CurrentState = BlockState.SuperShielded;

            SyncAOERegistryShieldState();

            Vector3 origin = _pendingShieldOriginWS ?? transform.position;
            _pendingShieldOriginWS = null;
            PrismShieldBroadcaster.EnsureInstance()
                .ReportShieldActivation(origin, teamManager.Domain, isSuper: true);
            PlayShieldSfxIfNotBroadcasted(GameplaySFXCategory.ShieldActivate);
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
                _themeManagerData.GetTeamShieldedBlockMaterial(teamManager.Domain),
                shieldMaterialTransitionDuration
            );
            CurrentState = BlockState.Shielded;

            SyncAOERegistryShieldState();

            Vector3 origin = _pendingShieldOriginWS ?? transform.position;
            _pendingShieldOriginWS = null;
            PrismShieldBroadcaster.EnsureInstance()
                .ReportShieldActivation(origin, teamManager.Domain, isSuper: false);
            PlayShieldSfxIfNotBroadcasted(GameplaySFXCategory.ShieldActivate);
        }

        private void ApplyNormalState()
        {
            var wasShielded = prism.prismProperties.IsShielded || prism.prismProperties.IsSuperShielded;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamBlockMaterial(teamManager.Domain),
                shieldMaterialTransitionDuration
            );

            prism.prismProperties.IsShielded = false;
            prism.prismProperties.IsSuperShielded = false;
            CurrentState = BlockState.Normal;

            SyncAOERegistryShieldState();

            if (wasShielded)
            {
                PrismShieldBroadcaster.EnsureInstance()
                    .ReportShieldDeactivation(transform.position, teamManager.Domain);
                PlayShieldSfxIfNotBroadcasted(GameplaySFXCategory.ShieldDeactivate);
            }
        }

        private void PlayShieldSfxIfNotBroadcasted(GameplaySFXCategory category)
        {
            // If the broadcaster owns shield SFX, it will play exactly one per coalesced
            // event — suppress the per-prism play to prevent N-stacked audio spikes.
            var broadcaster = PrismShieldBroadcaster.Instance;
            if (broadcaster != null && broadcaster.OwnsShieldSfx) return;
            if (AudioSystem.Instance != null)
                AudioSystem.Instance.PlayGameplaySFX(category);
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
