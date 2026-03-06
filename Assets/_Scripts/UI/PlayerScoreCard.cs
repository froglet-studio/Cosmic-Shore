using CosmicShore.Game.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PlayerScoreCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text playerScoreText;
        [SerializeField] private Image domainIndicatorImage;
        [SerializeField] private Image playerAvatarImage;

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
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Setup(string playerName, int initialCrystals, Color domainColor, bool isLocalPlayer, int staggerIndex = 0)
        {
            playerNameText.text = playerName;
            _displayedScore = initialCrystals;
            playerScoreText.text = $"{initialCrystals}";
            _baseTextColor = playerScoreText.color;

            if (domainIndicatorImage)
            {
                domainIndicatorImage.gameObject.SetActive(true);
                domainIndicatorImage.color = domainColor;
            }

            PlayEntrance(staggerIndex);
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

        public void UpdateScore(int crystalCount)
        {
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

        private void PlayEntrance(int staggerIndex)
        {
            _entranceSeq?.Kill();

            float duration = animSettings ? animSettings.cardEntranceDuration : 0.3f;
            float startScale = animSettings ? animSettings.cardEntranceStartScale : 0.6f;
            var ease = animSettings ? animSettings.cardEntranceEase : Ease.OutBack;
            float stagger = animSettings ? animSettings.cardEntranceStagger : 0.08f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            // Scale+fade entrance — does NOT touch anchoredPosition, so LayoutGroups work
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
