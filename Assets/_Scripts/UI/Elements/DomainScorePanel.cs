using System.Collections.Generic;
using CosmicShore.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// In-game HUD card representing a single domain (Jade / Ruby / Gold).
    /// Shows the team's aggregated objective progress on top and a horizontal
    /// row of small player avatars (humans + AI on the same domain) below it.
    /// Used by <see cref="MultiplayerHUD"/> for HexRace / Joust / CrystalCapture.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DomainScorePanel : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text domainSumText;
        [SerializeField] private Image domainIndicatorImage;
        [Tooltip("Alpha applied to the domain-color tint on the indicator image so the panel reads as a subtle background rather than a saturated block. 0 = invisible, 1 = fully opaque.")]
        [Range(0f, 1f)]
        [SerializeField] private float indicatorAlpha = 0.15f;

        [Header("Avatars")]
        [Tooltip("Container the small per-player avatars are parented under (HorizontalLayoutGroup expected).")]
        [SerializeField] private Transform avatarContainer;
        [Tooltip("Prefab cloned once per teammate. A PlayerScoreEntry works (name + avatar) — its score field is left empty.")]
        [SerializeField] private PlayerScoreEntry avatarEntryPrefab;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private int _displayedSum;
        private Color _baseTextColor;
        private Tween _punchTween;
        private Tween _rollTween;
        private Tween _colorFlashTween;
        private readonly List<PlayerScoreEntry> _spawnedAvatars = new();

        public Domains Domain { get; private set; } = Domains.Blue;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (domainSumText) _baseTextColor = domainSumText.color;
        }

        public void Setup(Domains domain, Color domainColor, int initialSum)
        {
            Domain = domain;
            _displayedSum = initialSum;

            if (domainSumText) domainSumText.text = initialSum.ToString();
            if (domainIndicatorImage)
            {
                domainIndicatorImage.gameObject.SetActive(true);
                var tinted = domainColor;
                tinted.a = indicatorAlpha;
                domainIndicatorImage.color = tinted;
            }
        }

        public void AddPlayerIcon(string playerName, Sprite avatar, Color domainColor, bool isLocalPlayer)
        {
            if (!avatarContainer || !avatarEntryPrefab) return;
            var entry = Instantiate(avatarEntryPrefab, avatarContainer);
            entry.Populate(isLocalPlayer ? playerName : string.Empty, string.Empty, avatar);
            _spawnedAvatars.Add(entry);
        }

        public void ClearAvatars()
        {
            foreach (var a in _spawnedAvatars)
                if (a) Destroy(a.gameObject);
            _spawnedAvatars.Clear();
        }

        public void UpdateSum(int newSum)
        {
            if (!domainSumText) return;

            if (newSum == _displayedSum)
            {
                domainSumText.text = newSum.ToString();
                return;
            }

            int from = _displayedSum;
            _displayedSum = newSum;

            PlayCounterRoll(from, newSum);
            PlayScorePunch();
            PlayColorFlash(newSum > from);
        }

        void PlayScorePunch()
        {
            if (!domainSumText) return;
            _punchTween?.Kill();

            float scale = animSettings ? animSettings.scorePunchScale : 1.15f;
            float duration = animSettings ? animSettings.scorePunchDuration : 0.2f;
            var ease = animSettings ? animSettings.scorePunchEase : Ease.OutBack;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            domainSumText.transform.localScale = Vector3.one;
            _punchTween = domainSumText.transform
                .DOScale(scale, duration * 0.4f)
                .SetEase(ease)
                .OnComplete(() =>
                {
                    _punchTween = domainSumText.transform
                        .DOScale(1f, duration * 0.6f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(unscaled);
                })
                .SetUpdate(unscaled);
        }

        void PlayCounterRoll(int from, int to)
        {
            if (!domainSumText) return;
            _rollTween?.Kill();

            float duration = animSettings ? animSettings.counterRollDuration : 0.35f;
            var ease = animSettings ? animSettings.counterRollEase : Ease.OutQuad;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            float current = from;
            _rollTween = DOTween.To(() => current, x => current = x, to, duration)
                .SetEase(ease)
                .OnUpdate(() => domainSumText.text = Mathf.RoundToInt(current).ToString())
                .SetUpdate(unscaled);
        }

        void PlayColorFlash(bool isGain)
        {
            if (!domainSumText) return;
            _colorFlashTween?.Kill();

            var flashColor = isGain
                ? (animSettings ? animSettings.scoreGainColor : new Color(0.2f, 1f, 0.4f, 1f))
                : (animSettings ? animSettings.scoreLossColor : new Color(1f, 0.3f, 0.2f, 1f));
            float duration = animSettings ? animSettings.scoreColorFlashDuration : 0.4f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            domainSumText.color = flashColor;
            _colorFlashTween = DOTween.To(
                    () => domainSumText.color,
                    c => domainSumText.color = c,
                    _baseTextColor,
                    duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(unscaled);
        }

        void OnDestroy()
        {
            _punchTween?.Kill();
            _rollTween?.Kill();
            _colorFlashTween?.Kill();
        }
    }
}
