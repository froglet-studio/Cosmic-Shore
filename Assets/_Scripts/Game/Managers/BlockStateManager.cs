using UnityEngine;
using System.Collections;
using System;

namespace CosmicShore.Core
{
    public enum BlockState
    {
        Normal,
        Shielded,
        SuperShielded,
        Dangerous
    }

    public class BlockStateManager : MonoBehaviour
    {
        [Header("Data Containers")] [SerializeField]
        ThemeManagerDataContainerSO _themeManagerData;

        private TrailBlock trailBlock;
        private MaterialPropertyAnimator materialAnimator;
        private BlockTeamManager teamManager;
        private Coroutine activeStateCoroutine;

        public BlockState CurrentState { get; private set; } = BlockState.Normal;

        private void Awake()
        {
            trailBlock = GetComponent<TrailBlock>();
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            teamManager = GetComponent<BlockTeamManager>();
        }

        public void MakeDangerous()
        {
            trailBlock.TrailBlockProperties.IsDangerous = true;
            trailBlock.TrailBlockProperties.speedDebuffAmount = 0.1f;
            trailBlock.TrailBlockProperties.IsShielded = false;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentDangerousBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamDangerousBlockMaterial(teamManager.Domain)
            );
            CurrentState = BlockState.Dangerous;
        }

        public void ActivateShield(float? duration = null)
        {
            if (duration.HasValue)
            {
                if (activeStateCoroutine != null)
                {
                    StopCoroutine(activeStateCoroutine);
                }

                activeStateCoroutine = StartCoroutine(TimedShieldCoroutine(duration.Value));
            }
            else
            {
                ApplyShieldState();
            }
        }

        public void ActivateSuperShield()
        {
            trailBlock.TrailBlockProperties.IsSuperShielded = true;
            trailBlock.TrailBlockProperties.IsDangerous = false;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentSuperShieldedBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamSuperShieldedBlockMaterial(teamManager.Domain)
            );
            CurrentState = BlockState.SuperShielded;
        }

        public void DeactivateShields(float? delay = null)
        {
            if (delay.HasValue)
            {
                if (activeStateCoroutine != null)
                {
                    StopCoroutine(activeStateCoroutine);
                }

                activeStateCoroutine = StartCoroutine(DelayedShieldDeactivationCoroutine(delay.Value));
            }
            else
            {
                ApplyNormalState();
            }
        }

        private void ApplyShieldState()
        {
            trailBlock.TrailBlockProperties.IsShielded = true;
            trailBlock.TrailBlockProperties.IsDangerous = false;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentShieldedBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamShieldedBlockMaterial(teamManager.Domain)
            );
            CurrentState = BlockState.Shielded;
        }

        private void ApplyNormalState()
        {
            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamBlockMaterial(teamManager.Domain)
            );

            trailBlock.TrailBlockProperties.IsShielded = false;
            trailBlock.TrailBlockProperties.IsSuperShielded = false;
            CurrentState = BlockState.Normal;
        }

        private IEnumerator TimedShieldCoroutine(float duration)
        {
            ApplyShieldState();
            yield return new WaitForSeconds(duration);
            ApplyNormalState();
            activeStateCoroutine = null;
        }

        private IEnumerator DelayedShieldDeactivationCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            ApplyNormalState();
            activeStateCoroutine = null;
        }
    }
}