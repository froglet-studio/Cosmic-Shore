using UnityEngine;
using System.Collections;
using System;

namespace CosmicShore.Core
{
    public class BlockStateManager : MonoBehaviour
    {
        private TrailBlock trailBlock;
        private MaterialPropertyAnimator materialAnimator;
        private BlockTeamManager teamManager;
        private Coroutine activeStateCoroutine;

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
                ThemeManager.Instance.GetTeamTransparentDangerousBlockMaterial(teamManager.Team),
                ThemeManager.Instance.GetTeamDangerousBlockMaterial(teamManager.Team)
            );
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
                ThemeManager.Instance.GetTeamTransparentSuperShieldedBlockMaterial(teamManager.Team),
                ThemeManager.Instance.GetTeamSuperShieldedBlockMaterial(teamManager.Team)
            );
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
                ThemeManager.Instance.GetTeamTransparentShieldedBlockMaterial(teamManager.Team),
                ThemeManager.Instance.GetTeamShieldedBlockMaterial(teamManager.Team)
            );
        }

        private void ApplyNormalState()
        {
            materialAnimator.UpdateMaterial(
                ThemeManager.Instance.GetTeamTransparentBlockMaterial(teamManager.Team),
                ThemeManager.Instance.GetTeamBlockMaterial(teamManager.Team)
            );

            trailBlock.TrailBlockProperties.IsShielded = false;
            trailBlock.TrailBlockProperties.IsSuperShielded = false;
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
