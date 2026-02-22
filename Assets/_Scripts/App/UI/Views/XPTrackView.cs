using System.Collections;
using System.Collections.Generic;
using CosmicShore.App.Profile;
using CosmicShore.Game.XP;
using CosmicShore.Models;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    /// <summary>
    /// Displays the XP track in the Profile Screen.
    /// Shows current XP, a slider that animates with DOTween,
    /// milestone indicators, and reward unlock animations.
    /// </summary>
    public class XPTrackView : MonoBehaviour
    {
        [Header("XP Display")]
        [SerializeField] private TMP_Text xpText;
        [SerializeField] private Slider xpSlider;
        [SerializeField] private Image xpSliderFill;

        [Header("Milestone Info")]
        [SerializeField] private TMP_Text currentMilestoneText;
        [SerializeField] private TMP_Text nextMilestoneText;

        [Header("Reward Unlock Panel")]
        [SerializeField] private GameObject rewardUnlockPanel;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardNameText;
        [SerializeField] private TMP_Text rewardDescriptionText;
        [SerializeField] private Button rewardCloseButton;
        [SerializeField] private CanvasGroup rewardPanelCanvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float sliderAnimationDuration = 1.2f;
        [SerializeField] private Ease sliderEase = Ease.OutQuad;
        [SerializeField] private float rewardShowDelay = 0.3f;

        [Header("Data")]
        [SerializeField] private SO_XPTrackData xpTrackData;

        private int _displayedXP;
        private int _targetXP;
        private Tween _sliderTween;
        private Coroutine _animationRoutine;

        void OnEnable()
        {
            if (rewardCloseButton != null)
                rewardCloseButton.onClick.AddListener(HideRewardPanel);

            if (rewardUnlockPanel != null)
                rewardUnlockPanel.SetActive(false);

            RefreshXPDisplay();
        }

        void OnDisable()
        {
            if (rewardCloseButton != null)
                rewardCloseButton.onClick.RemoveListener(HideRewardPanel);

            KillTween();

            if (_animationRoutine != null)
            {
                StopCoroutine(_animationRoutine);
                _animationRoutine = null;
            }
        }

        /// <summary>
        /// Refreshes the XP display. If XP has changed since last display,
        /// plays the slider animation from old value to new value.
        /// </summary>
        public void RefreshXPDisplay()
        {
            var profileService = PlayerDataService.Instance;
            if (profileService == null || profileService.CurrentProfile == null)
            {
                SetXPImmediate(0);
                return;
            }

            _targetXP = profileService.GetXP();

            // Check if we need to animate (XP increased since last display)
            var xpRewardService = XPRewardService.Instance;
            if (xpRewardService != null && xpRewardService.LastXPEarned > 0)
            {
                int previousXP = xpRewardService.PreviousXP;

                // Only animate if we haven't already shown this animation
                if (_displayedXP <= previousXP && _targetXP > previousXP)
                {
                    _displayedXP = previousXP;
                    _animationRoutine = StartCoroutine(AnimateXPGain(previousXP, _targetXP,
                        xpRewardService.LastUnlockedMilestones));
                    return;
                }
            }

            // No animation needed, just set immediately
            SetXPImmediate(_targetXP);
        }

        /// <summary>
        /// Animates the XP slider from oldXP to newXP, pausing at each milestone
        /// to show reward unlock animations.
        /// </summary>
        IEnumerator AnimateXPGain(int fromXP, int toXP, List<XPMilestone> unlockedMilestones)
        {
            if (xpTrackData == null)
            {
                SetXPImmediate(toXP);
                yield break;
            }

            // Update text immediately to show target
            UpdateXPText(fromXP);

            int currentXP = fromXP;
            int milestoneInterval = xpTrackData.xpPerMilestone;
            int unlockedIndex = 0;

            // Find all milestone thresholds between fromXP and toXP
            List<int> milestoneThresholds = new List<int>();
            if (milestoneInterval > 0)
            {
                int nextThreshold = ((fromXP / milestoneInterval) + 1) * milestoneInterval;
                while (nextThreshold <= toXP)
                {
                    milestoneThresholds.Add(nextThreshold);
                    nextThreshold += milestoneInterval;
                }
            }

            // Animate to each milestone, then show reward
            foreach (int threshold in milestoneThresholds)
            {
                // Animate slider from current to this milestone
                yield return AnimateSliderTo(currentXP, threshold);
                currentXP = threshold;

                // Show reward if one exists for this milestone
                if (unlockedMilestones != null && unlockedIndex < unlockedMilestones.Count)
                {
                    var milestone = unlockedMilestones[unlockedIndex];
                    if (milestone.reward != null)
                    {
                        yield return new WaitForSeconds(rewardShowDelay);
                        ShowRewardPanel(milestone.reward);
                        yield return new WaitUntil(() =>
                            rewardUnlockPanel == null || !rewardUnlockPanel.activeSelf);
                    }
                    unlockedIndex++;
                }
            }

            // Animate remaining XP (from last milestone to final value)
            if (currentXP < toXP)
            {
                yield return AnimateSliderTo(currentXP, toXP);
            }

            _displayedXP = toXP;
            _animationRoutine = null;
        }

        /// <summary>
        /// Animates the slider from one XP value to another using DOTween.
        /// </summary>
        IEnumerator AnimateSliderTo(int fromXP, int toXP)
        {
            KillTween();

            float fromNorm = GetNormalizedValue(fromXP);
            float toNorm = GetNormalizedValue(toXP);

            if (xpSlider != null)
            {
                xpSlider.value = fromNorm;
                _sliderTween = DOTween.To(
                    () => xpSlider.value,
                    x =>
                    {
                        xpSlider.value = x;
                        // Update XP text during animation
                        int interpolatedXP = Mathf.RoundToInt(Mathf.Lerp(fromXP, toXP,
                            Mathf.InverseLerp(fromNorm, toNorm, x)));
                        UpdateXPText(interpolatedXP);
                    },
                    toNorm,
                    sliderAnimationDuration
                ).SetEase(sliderEase);

                yield return _sliderTween.WaitForCompletion();
            }

            UpdateXPText(toXP);
            UpdateMilestoneText(toXP);
        }

        void SetXPImmediate(int xp)
        {
            _displayedXP = xp;
            _targetXP = xp;

            if (xpSlider != null)
                xpSlider.value = GetNormalizedValue(xp);

            UpdateXPText(xp);
            UpdateMilestoneText(xp);
        }

        float GetNormalizedValue(int xp)
        {
            if (xpTrackData != null)
                return xpTrackData.GetNormalizedProgress(xp);

            return 0f;
        }

        void UpdateXPText(int xp)
        {
            if (xpText != null)
            {
                if (xpTrackData != null)
                {
                    int progress = xpTrackData.GetProgressInCurrentMilestone(xp);
                    xpText.text = $"{xp} XP ({progress}/{xpTrackData.xpPerMilestone})";
                }
                else
                {
                    xpText.text = $"{xp} XP";
                }
            }
        }

        void UpdateMilestoneText(int xp)
        {
            if (xpTrackData == null) return;

            int milestoneIndex = xpTrackData.GetMilestoneIndex(xp);

            if (currentMilestoneText != null)
                currentMilestoneText.text = $"Level {milestoneIndex + 1}";

            if (nextMilestoneText != null)
            {
                int nextMilestoneXP = (milestoneIndex + 1) * xpTrackData.xpPerMilestone;
                nextMilestoneText.text = $"Next: {nextMilestoneXP} XP";
            }
        }

        /// <summary>
        /// Shows the reward unlock panel with animation.
        /// </summary>
        void ShowRewardPanel(SO_XPTrackReward reward)
        {
            if (rewardUnlockPanel == null) return;

            if (rewardIcon != null && reward.icon != null)
            {
                rewardIcon.sprite = reward.icon;
                rewardIcon.enabled = true;
            }

            if (rewardNameText != null)
                rewardNameText.text = reward.rewardName;

            if (rewardDescriptionText != null)
                rewardDescriptionText.text = reward.unlockDescription;

            rewardUnlockPanel.SetActive(true);

            // Animate panel in
            if (rewardPanelCanvasGroup != null)
            {
                rewardPanelCanvasGroup.alpha = 0f;
                var rectTransform = rewardPanelCanvasGroup.GetComponent<RectTransform>();

                var sequence = DOTween.Sequence();
                sequence.Append(rewardPanelCanvasGroup.DOFade(1f, 0.4f).SetEase(Ease.OutQuad));

                if (rectTransform != null)
                {
                    rectTransform.localScale = Vector3.one * 0.5f;
                    sequence.Join(rectTransform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
                }

                sequence.Play();
            }

            // Animate the reward icon
            if (rewardIcon != null)
            {
                var iconRect = rewardIcon.GetComponent<RectTransform>();
                if (iconRect != null)
                {
                    iconRect.localScale = Vector3.zero;
                    iconRect.DOScale(1.2f, 0.4f)
                        .SetEase(Ease.OutBack)
                        .SetDelay(0.2f)
                        .OnComplete(() =>
                        {
                            iconRect.DOScale(1f, 0.15f).SetEase(Ease.InOutQuad);
                        });
                }
            }
        }

        void HideRewardPanel()
        {
            if (rewardUnlockPanel == null) return;

            if (rewardPanelCanvasGroup != null)
            {
                rewardPanelCanvasGroup.DOFade(0f, 0.3f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => rewardUnlockPanel.SetActive(false));
            }
            else
            {
                rewardUnlockPanel.SetActive(false);
            }
        }

        void KillTween()
        {
            if (_sliderTween != null && _sliderTween.IsActive())
            {
                _sliderTween.Kill();
                _sliderTween = null;
            }
        }
    }
}
