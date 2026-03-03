using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
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
        private RectTransform _rect;
        private Tween _punchTween;
        private Tween _rollTween;
        private Tween _entranceTween;
        private int _displayedScore;

        // Fallback defaults when no SO is assigned
        private const float DefaultEntranceDuration = 0.3f;
        private const float DefaultEntranceSlideOffset = 80f;
        private const float DefaultPunchScale = 1.15f;
        private const float DefaultPunchDuration = 0.2f;
        private const float DefaultRollDuration = 0.35f;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _rect = GetComponent<RectTransform>();
        }

        public void Setup(string playerName, int initialCrystals, Color domainColor, bool isLocalPlayer)
        {
            playerNameText.text = playerName;
            _displayedScore = initialCrystals;
            playerScoreText.text = $"{initialCrystals}";

            if (domainIndicatorImage)
            {
                domainIndicatorImage.gameObject.SetActive(true);
                domainIndicatorImage.color = domainColor;
            }

            PlayEntrance();
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
        }

        private void PlayEntrance()
        {
            _entranceTween?.Kill();

            float duration = animSettings ? animSettings.cardEntranceDuration : DefaultEntranceDuration;
            float offset = animSettings ? animSettings.cardEntranceSlideOffset : DefaultEntranceSlideOffset;
            var ease = animSettings ? animSettings.cardEntranceEase : Ease.OutCubic;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            _canvasGroup.alpha = 0f;
            var startPos = _rect.anchoredPosition;
            _rect.anchoredPosition = new Vector2(startPos.x + offset, startPos.y);

            _entranceTween = DOTween.Sequence()
                .Join(_rect.DOAnchorPos(startPos, duration).SetEase(ease))
                .Join(_canvasGroup.DOFade(1f, duration))
                .SetUpdate(unscaled);
        }

        private void PlayScorePunch()
        {
            _punchTween?.Kill();

            float scale = animSettings ? animSettings.scorePunchScale : DefaultPunchScale;
            float duration = animSettings ? animSettings.scorePunchDuration : DefaultPunchDuration;
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

            float duration = animSettings ? animSettings.counterRollDuration : DefaultRollDuration;
            var ease = animSettings ? animSettings.counterRollEase : Ease.OutQuad;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            float current = from;
            _rollTween = DOTween.To(() => current, x => current = x, to, duration)
                .SetEase(ease)
                .OnUpdate(() => playerScoreText.text = $"{Mathf.RoundToInt(current)}")
                .SetUpdate(unscaled);
        }

        private void OnDestroy()
        {
            _punchTween?.Kill();
            _rollTween?.Kill();
            _entranceTween?.Kill();
        }
    }
}
