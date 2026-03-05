using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Core
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
        // [ScenePerf] Teardown counters — static so they accumulate across all instances
        private static int _disableCount;
        private static float _firstDisableTime;

        [Header("Data Containers")] [SerializeField]
        ThemeManagerDataContainerSO _themeManagerData;

        private Prism prism;
        private MaterialPropertyAnimator materialAnimator;
        private PrismTeamManager teamManager;

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

            SyncAOERegistryShieldState();
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

            SyncAOERegistryShieldState();
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
            _disableCount++;
            if (_disableCount == 1)
                _firstDisableTime = Time.realtimeSinceStartup;

            PrismTimerManager.Instance?.CancelTimers(this);

            // Log summary every 500 prisms so we can see throughput
            if (_disableCount % 500 == 0)
                Debug.Log($"[ScenePerf] PrismStateManager.OnDisable #{_disableCount} elapsed={((Time.realtimeSinceStartup - _firstDisableTime)*1000f):F0}ms t={Time.realtimeSinceStartup:F3}");
        }

        private void OnDestroy()
        {
            PrismTimerManager.Instance?.CancelTimers(this);
        }

        /// <summary>
        /// Call from GameManager before scene load to emit a final summary.
        /// </summary>
        internal static void LogTeardownSummary()
        {
            if (_disableCount > 0)
            {
                Debug.Log($"[ScenePerf] PrismStateManager TEARDOWN TOTAL: {_disableCount} prisms in {((Time.realtimeSinceStartup - _firstDisableTime)*1000f):F0}ms");
                _disableCount = 0;
            }
        }
    }
}
