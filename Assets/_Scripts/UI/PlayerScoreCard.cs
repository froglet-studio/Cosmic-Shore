using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// End-game scoreboard row for a single player.
    /// Displays avatar, username, formatted score, and optional "+N" crystal reward.
    /// Background tints to the player's domain color.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PlayerScoreCard : MonoBehaviour
    {
        [Header("Main Fields")]
        [Tooltip("Player avatar / profile icon")]
        [SerializeField] private Image playerAvatarImage;
        [Tooltip("Player display name")]
        [SerializeField] private TMP_Text playerNameText;
        [Tooltip("Primary score display (time / crystals / points)")]
        [SerializeField] private TMP_Text playerScoreText;

        [Header("Domain Tint")]
        [Tooltip("Optional background image tinted with domain color (falls back to domainIndicatorImage if unset)")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Optional small indicator image (legacy)")]
        [SerializeField] private Image domainIndicatorImage;
        [Tooltip("Alpha applied to the background tint (0-1). 1 = solid, 0.2 = subtle tint")]
        [Range(0f, 1f)]
        [SerializeField] private float backgroundTintAlpha = 0.35f;

        [Header("Extra Data Panels")]
        [Tooltip("Root of DataPanels — hidden if no extra stats to show")]
        [SerializeField] private GameObject dataPanelsRoot;
        [Tooltip("Optional secondary data text (e.g. crystals collected, clean streak)")]
        [SerializeField] private TMP_Text secondaryStatText;
        [Tooltip("Optional '+N' crystal reward text shown only for winners")]
        [SerializeField] private GameObject crystalRewardRoot;
        [SerializeField] private TMP_Text crystalRewardText;

        [Header("Animation (optional — falls back to defaults)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private Tween _punchTween;
        private Tween _rollTween;
        private Sequence _entranceSeq;
        private Tween _colorFlashTween;
        private int _displayedScore;
        private Color _baseTextColor;

        private void Awake()
        {
            EnsureCanvasGroup();
        }

        void EnsureCanvasGroup()
        {
            if (_canvasGroup) return;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// Sets up the card with basic player info. Accepts a formatted score string directly
        /// so the caller controls display (time, crystals, etc).
        /// </summary>
        public void Setup(string playerName, string formattedScore, Color domainColor, int staggerIndex = 0)
        {
            if (playerNameText) playerNameText.text = playerName;
            if (playerScoreText)
            {
                playerScoreText.text = formattedScore;
                _baseTextColor = playerScoreText.color;
            }

            ApplyDomainColor(domainColor);
            HideCrystalReward();
            HideSecondaryStat();
            PlayEntrance(staggerIndex);
        }

        /// <summary>
        /// Back-compat overload for integer-score callers. Displays "{value}" as the score.
        /// </summary>
        public void Setup(string playerName, int initialScore, Color domainColor, bool isLocalPlayer, int staggerIndex = 0)
        {
            _displayedScore = initialScore;
            Setup(playerName, initialScore.ToString(), domainColor, staggerIndex);
        }

        public void SetAvatar(Sprite avatarSprite)
        {
            if (!playerAvatarImage) return;
            if (avatarSprite != null)
            {
                playerAvatarImage.sprite = avatarSprite;
                playerAvatarImage.enabled = true;
            }
            else
            {
                playerAvatarImage.enabled = false;
            }
        }

        /// <summary>
        /// Shows a "+N" crystal reward on this card (e.g. for the winning player).
        /// </summary>
        public void ShowCrystalReward(int crystalCount)
        {
            if (!crystalRewardRoot || !crystalRewardText) return;
            crystalRewardText.text = $"+{crystalCount}";
            crystalRewardRoot.SetActive(true);
        }

        public void HideCrystalReward()
        {
            if (crystalRewardRoot) crystalRewardRoot.SetActive(false);
        }

        /// <summary>
        /// Optional secondary stat line (e.g. "Jousts: 3" or "Crystals: 12").
        /// </summary>
        public void ShowSecondaryStat(string statText)
        {
            if (secondaryStatText)
            {
                secondaryStatText.text = statText;
                secondaryStatText.gameObject.SetActive(true);
            }
            if (dataPanelsRoot) dataPanelsRoot.SetActive(true);
        }

        public void HideSecondaryStat()
        {
            if (secondaryStatText) secondaryStatText.gameObject.SetActive(false);
        }

        /// <summary>
        /// In-game live score update. Animates a counter roll + punch.
        /// </summary>
        public void UpdateScore(int crystalCount)
        {
            if (!playerScoreText) return;

            if (crystalCount == _displayedScore)
            {
                playerScoreText.text = $"{crystalCount}";
                return;
            }

            int from = _displayedScore;
            _displayedScore = crystalCount;

            PlayCounterRoll(from, crystalCount);
            PlayScorePunch();
            PlayColorFlash(crystalCount > from);
        }

        private void ApplyDomainColor(Color domainColor)
        {
            if (backgroundImage)
            {
                var c = domainColor;
                c.a = backgroundTintAlpha;
                backgroundImage.color = c;
            }

            if (domainIndicatorImage)
            {
                domainIndicatorImage.gameObject.SetActive(true);
                domainIndicatorImage.color = domainColor;
            }
        }

        private void PlayEntrance(int staggerIndex)
        {
            EnsureCanvasGroup();
            _entranceSeq?.Kill();

            float duration = animSettings ? animSettings.cardEntranceDuration : 0.3f;
            float startScale = animSettings ? animSettings.cardEntranceStartScale : 0.6f;
            var ease = animSettings ? animSettings.cardEntranceEase : Ease.OutBack;
            float stagger = animSettings ? animSettings.cardEntranceStagger : 0.08f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            _canvasGroup.alpha = 0f;
            transform.localScale = Vector3.one * startScale;

            _entranceSeq = DOTween.Sequence()
                .AppendInterval(stagger * staggerIndex)
                .Append(transform.DOScale(1f, duration).SetEase(ease))
                .Join(_canvasGroup.DOFade(1f, duration))
                .SetUpdate(unscaled);
        }

        private void PlayScorePunch()
        {
            if (!playerScoreText) return;
            _punchTween?.Kill();

            float scale = animSettings ? animSettings.scorePunchScale : 1.15f;
            float duration = animSettings ? animSettings.scorePunchDuration : 0.2f;
            var ease = animSettings ? animSettings.scorePunchEase : Ease.OutBack;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            playerScoreText.transform.localScale = Vector3.one;
            _punchTween = playerScoreText.transform
                .DOScale(scale, duration * 0.4f)
                .SetEase(ease)
                .OnComplete(() =>
                {
                    _punchTween = playerScoreText.transform
                        .DOScale(1f, duration * 0.6f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(unscaled);
                })
                .SetUpdate(unscaled);
        }

        private void PlayCounterRoll(int from, int to)
        {
            if (!playerScoreText) return;
            _rollTween?.Kill();

            float duration = animSettings ? animSettings.counterRollDuration : 0.35f;
            var ease = animSettings ? animSettings.counterRollEase : Ease.OutQuad;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            float current = from;
            _rollTween = DOTween.To(() => current, x => current = x, to, duration)
                .SetEase(ease)
                .OnUpdate(() => playerScoreText.text = $"{Mathf.RoundToInt(current)}")
                .SetUpdate(unscaled);
        }

        private void PlayColorFlash(bool isGain)
        {
            if (!playerScoreText) return;
            _colorFlashTween?.Kill();

            var flashColor = isGain
                ? (animSettings ? animSettings.scoreGainColor : new Color(0.2f, 1f, 0.4f, 1f))
                : (animSettings ? animSettings.scoreLossColor : new Color(1f, 0.3f, 0.2f, 1f));
            float duration = animSettings ? animSettings.scoreColorFlashDuration : 0.4f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            playerScoreText.color = flashColor;
            _colorFlashTween = DOTween.To(
                    () => playerScoreText.color,
                    c => playerScoreText.color = c,
                    _baseTextColor,
                    duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(unscaled);
        }

        private void OnDestroy()
        {
            _punchTween?.Kill();
            _rollTween?.Kill();
            _entranceSeq?.Kill();
            _colorFlashTween?.Kill();
        }
    }
}
