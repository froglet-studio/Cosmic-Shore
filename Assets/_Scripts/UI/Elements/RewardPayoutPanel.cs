using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The end-game payout moment: what you just earned, and what it took your balance to.
    ///
    /// Replaces the bare "+N" text badge on the winning <see cref="PlayerScoreCard"/>, which
    /// stated the amount and nothing else - not that it was yours, not what it bought you.
    /// Reads <see cref="RewardGranted"/> and nothing else, so it cannot disagree with the
    /// wallet write that produced it.
    ///
    /// Shows and hides on a CanvasGroup rather than SetActive: the panel must stay ACTIVE to
    /// stay subscribed, and a reward that pops into existence would break the platform's
    /// continuity law the same way a prism doing it would.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class RewardPayoutPanel : MonoBehaviour
    {
        [Header("Channel")]
        [Tooltip("Resources/Channels/RewardGrantedChannel. Raised by RewardService whenever a " +
                 "reward actually lands.")]
        [SerializeField] ScriptableEventRewardGranted rewardChannel;

        [Header("Display")]
        [Tooltip("The payout itself, rendered as '+200'.")]
        [SerializeField] TMP_Text amountText;
        [Tooltip("Crystal balance after the payout. Counts up from the balance before it, so " +
                 "the reward reads as arriving rather than as a number that was always there.")]
        [SerializeField] TMP_Text balanceText;
        [Tooltip("Optional root for the crystal icon / label - hidden along with the panel.")]
        [SerializeField] GameObject crystalIconRoot;

        [Header("Animation")]
        [Tooltip("Shared HUD timings. Falls back to the same defaults ScoreNumberAnimator uses.")]
        [SerializeField] HUDAnimationSettingsSO animSettings;
        [Tooltip("Seconds the panel takes to bloom in. Nothing pops into existence.")]
        [SerializeField] float bloomDuration = 0.35f;
        [Tooltip("Seconds to wait after the panel is shown before the balance starts counting, " +
                 "so the payout is read before the total moves.")]
        [SerializeField] float countUpDelay = 0.4f;

        CanvasGroup _canvasGroup;
        ScoreNumberAnimator _balanceAnimator;
        Sequence _bloomSeq;
        Tween _countUpTween;

        // The grant sequence this panel has already displayed. Compared against
        // RewardService.GrantSequence so a payout raised before this object was enabled is
        // still shown - see RewardService.LatestGrant for why that case exists.
        int _lastShownSequence;

        ScoreNumberAnimator BalanceAnimator =>
            _balanceAnimator ??= new ScoreNumberAnimator(balanceText, animSettings);

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            HideImmediate();
        }

        void OnEnable()
        {
            rewardChannel.OnRaised += HandleRewardGranted;

            // Catch up on a payout that was announced while this object was inactive. The
            // Scoreboard awards before it activates its panel, so on the end-game screen this
            // is the NORMAL path, not a recovery one.
            if (RewardService.GrantSequence > _lastShownSequence)
                HandleRewardGranted(RewardService.LatestGrant);
        }

        void OnDisable()
        {
            rewardChannel.OnRaised -= HandleRewardGranted;
            _bloomSeq?.Kill();
            _countUpTween?.Kill();
        }

        void OnDestroy()
        {
            _bloomSeq?.Kill();
            _countUpTween?.Kill();
            _balanceAnimator?.Kill();
        }

        void HandleRewardGranted(RewardGranted granted)
        {
            _lastShownSequence = RewardService.GrantSequence;

            // An entitlement has no number to count and no balance to move. It is a real
            // reward and deserves its own presentation; until that exists, showing it through
            // a crystal payout panel would be worse than staying quiet.
            if (granted.Grant.Kind != RewardKind.Crystals || granted.CrystalDelta <= 0)
                return;

            if (amountText) amountText.text = $"+{granted.CrystalDelta}";

            // Seed at the PRE-grant balance so the count-up spans exactly the payout. Reading
            // the wallet here instead would already show the new total and count from it to
            // itself.
            BalanceAnimator.SetImmediate(granted.PreviousCrystalBalance);

            Show();

            _countUpTween?.Kill();
            _countUpTween = DOVirtual
                .DelayedCall(countUpDelay, () => BalanceAnimator.AnimateTo(granted.NewCrystalBalance))
                .SetUpdate(UseUnscaledTime);
        }

        bool UseUnscaledTime => animSettings == null || animSettings.useUnscaledTime;

        void Show()
        {
            if (crystalIconRoot) crystalIconRoot.SetActive(true);

            _bloomSeq?.Kill();
            _bloomSeq = DOTween.Sequence()
                .Append(_canvasGroup.DOFade(1f, bloomDuration).SetEase(Ease.OutQuad))
                .Join(transform.DOScale(1f, bloomDuration).SetEase(Ease.OutBack))
                .SetUpdate(UseUnscaledTime);
        }

        /// <summary>
        /// Resets to hidden with no animation. Public so the end-game screen can clear the
        /// panel between rounds without destroying it - destroying it would unsubscribe it
        /// from the very channel it needs for the next payout.
        /// </summary>
        public void HideImmediate()
        {
            _bloomSeq?.Kill();
            _countUpTween?.Kill();

            if (_canvasGroup)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            transform.localScale = Vector3.one * 0.85f;
            if (crystalIconRoot) crystalIconRoot.SetActive(false);
        }
    }
}
