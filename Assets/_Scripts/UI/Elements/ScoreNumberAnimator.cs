using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Shared animated integer readout for a score <see cref="TMP_Text"/>:
    /// counter roll + score punch + color flash, driven by
    /// <see cref="HUDAnimationSettingsSO"/>. Owns the displayed value, the rest
    /// (base) color, and the three tweens.
    ///
    /// Plain C# helper (no MonoBehaviour / prefab wiring) composed by
    /// <see cref="PlayerScoreEntry"/>, <see cref="PlayerScoreCard"/>, and
    /// <see cref="DomainScorePanel"/> so the animation logic lives in one place.
    /// </summary>
    public class ScoreNumberAnimator
    {
        private readonly TMP_Text _text;
        private readonly HUDAnimationSettingsSO _settings;

        private int _displayed;
        private Color _baseColor;

        private Tween _punchTween;
        private Tween _rollTween;
        private Tween _colorFlashTween;

        public ScoreNumberAnimator(TMP_Text text, HUDAnimationSettingsSO settings)
        {
            _text = text;
            _settings = settings;
            if (_text) _baseColor = _text.color;
        }

        /// <summary>The value currently shown (after the last SetImmediate / AnimateTo).</summary>
        public int Displayed => _displayed;

        /// <summary>
        /// Overrides the rest color the flash returns to, and applies it now.
        /// Used by the themed DomainScorePanel to make the sum read in the team color.
        /// </summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            if (_text) _text.color = color;
        }

        /// <summary>Sets the value with no animation (also snaps the text color to base).</summary>
        public void SetImmediate(int value)
        {
            _displayed = value;
            if (!_text) return;
            _text.text = value.ToString();
            _text.color = _baseColor;
        }

        /// <summary>Animates from the current value to <paramref name="value"/>.</summary>
        public void AnimateTo(int value)
        {
            if (!_text) return;

            if (value == _displayed)
            {
                _text.text = value.ToString();
                return;
            }

            int from = _displayed;
            _displayed = value;

            PlayCounterRoll(from, value);
            PlayScorePunch();
            PlayColorFlash(value > from);
        }

        /// <summary>Kills the three tweens. Call from the owner's OnDestroy.</summary>
        public void Kill()
        {
            _punchTween?.Kill();
            _rollTween?.Kill();
            _colorFlashTween?.Kill();
        }

        private void PlayScorePunch()
        {
            if (!_text) return;
            _punchTween?.Kill();

            float scale = _settings ? _settings.scorePunchScale : 1.15f;
            float duration = _settings ? _settings.scorePunchDuration : 0.2f;
            var ease = _settings ? _settings.scorePunchEase : Ease.OutBack;
            bool unscaled = _settings == null || _settings.useUnscaledTime;

            _text.transform.localScale = Vector3.one;
            _punchTween = _text.transform
                .DOScale(scale, duration * 0.4f)
                .SetEase(ease)
                .OnComplete(() =>
                {
                    _punchTween = _text.transform
                        .DOScale(1f, duration * 0.6f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(unscaled);
                })
                .SetUpdate(unscaled);
        }

        private void PlayCounterRoll(int from, int to)
        {
            if (!_text) return;
            _rollTween?.Kill();

            float duration = _settings ? _settings.counterRollDuration : 0.35f;
            var ease = _settings ? _settings.counterRollEase : Ease.OutQuad;
            bool unscaled = _settings == null || _settings.useUnscaledTime;

            float current = from;
            _rollTween = DOTween.To(() => current, x => current = x, to, duration)
                .SetEase(ease)
                .OnUpdate(() => _text.text = Mathf.RoundToInt(current).ToString())
                .SetUpdate(unscaled);
        }

        private void PlayColorFlash(bool isGain)
        {
            if (!_text) return;
            _colorFlashTween?.Kill();

            var flashColor = isGain
                ? (_settings ? _settings.scoreGainColor : new Color(0.2f, 1f, 0.4f, 1f))
                : (_settings ? _settings.scoreLossColor : new Color(1f, 0.3f, 0.2f, 1f));
            float duration = _settings ? _settings.scoreColorFlashDuration : 0.4f;
            bool unscaled = _settings == null || _settings.useUnscaledTime;

            _text.color = flashColor;
            _colorFlashTween = DOTween.To(
                    () => _text.color,
                    c => _text.color = c,
                    _baseColor,
                    duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(unscaled);
        }
    }
}
