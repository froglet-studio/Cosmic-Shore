using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Lightweight in-game player score entry for the MiniGameHUD during gameplay.
    /// Shows avatar, name, live-updating score with counter roll and punch animations.
    /// Used by MiniGameHUD and MultiplayerHUD for real-time score tracking.
    /// For end-game scoreboard cards, see <see cref="PlayerScoreCard"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PlayerScoreEntry : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private Image domainIndicatorImage;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private Tween _punchTween;
        private Tween _rollTween;
        private Sequence _entranceSeq;
        private Tween _colorFlashTween;
        private int _displayedScore;
        private Color _baseTextColor;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Setup(string playerName, int initialScore, Color domainColor, bool isLocalPlayer, int staggerIndex = 0)
        {
            if (playerNameText) playerNameText.text = playerName;
            _displayedScore = initialScore;
            if (scoreText)
            {
                scoreText.text = $"{initialScore}";
                _baseTextColor = scoreText.color;
            }

            if (domainIndicatorImage)
            {
                domainIndicatorImage.gameObject.SetActive(true);
                domainIndicatorImage.color = domainColor;
            }

            PlayEntrance(staggerIndex);
        }

        public void SetAvatar(Sprite avatarSprite)
        {
            if (!avatarImage) return;
            if (avatarSprite != null)
            {
                avatarImage.sprite = avatarSprite;
                avatarImage.enabled = true;
            }
            else
            {
                avatarImage.enabled = false;
            }
        }

        public void UpdateScore(int newScore)
        {
            if (!scoreText) return;

            if (newScore == _displayedScore)
            {
                scoreText.text = $"{newScore}";
                return;
            }

            int from = _displayedScore;
            _displayedScore = newScore;

            PlayCounterRoll(from, newScore);
            PlayScorePunch();
            PlayColorFlash(newScore > from);
        }

        public void Populate(string playerName, string score, Sprite avatar = null)
        {
            if (playerNameText) playerNameText.text = playerName;
            if (scoreText) scoreText.text = score;
            SetAvatar(avatar);
        }

        public void Show(bool visible) => gameObject.SetActive(visible);

        void PlayEntrance(int staggerIndex)
        {
            if (!_canvasGroup)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

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

        void PlayScorePunch()
        {
            if (!scoreText) return;
            _punchTween?.Kill();

            float scale = animSettings ? animSettings.scorePunchScale : 1.15f;
            float duration = animSettings ? animSettings.scorePunchDuration : 0.2f;
            var ease = animSettings ? animSettings.scorePunchEase : Ease.OutBack;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            scoreText.transform.localScale = Vector3.one;
            _punchTween = scoreText.transform
                .DOScale(scale, duration * 0.4f)
                .SetEase(ease)
                .OnComplete(() =>
                {
                    _punchTween = scoreText.transform
                        .DOScale(1f, duration * 0.6f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(unscaled);
                })
                .SetUpdate(unscaled);
        }

        void PlayCounterRoll(int from, int to)
        {
            if (!scoreText) return;
            _rollTween?.Kill();

            float duration = animSettings ? animSettings.counterRollDuration : 0.35f;
            var ease = animSettings ? animSettings.counterRollEase : Ease.OutQuad;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            float current = from;
            _rollTween = DOTween.To(() => current, x => current = x, to, duration)
                .SetEase(ease)
                .OnUpdate(() => scoreText.text = $"{Mathf.RoundToInt(current)}")
                .SetUpdate(unscaled);
        }

        void PlayColorFlash(bool isGain)
        {
            if (!scoreText) return;
            _colorFlashTween?.Kill();

            var flashColor = isGain
                ? (animSettings ? animSettings.scoreGainColor : new Color(0.2f, 1f, 0.4f, 1f))
                : (animSettings ? animSettings.scoreLossColor : new Color(1f, 0.3f, 0.2f, 1f));
            float duration = animSettings ? animSettings.scoreColorFlashDuration : 0.4f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            scoreText.color = flashColor;
            _colorFlashTween = DOTween.To(
                    () => scoreText.color,
                    c => scoreText.color = c,
                    _baseTextColor,
                    duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(unscaled);
        }

        void OnDestroy()
        {
            _punchTween?.Kill();
            _rollTween?.Kill();
            _entranceSeq?.Kill();
            _colorFlashTween?.Kill();
        }
    }
}
