using CosmicShore.App.Systems.Audio;
using CosmicShore.Game.UI;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.Arcade
{
    public class CountdownTimer : MonoBehaviour
    {
        [SerializeField] Image   countdownDisplay;
        [SerializeField] Sprite  countdown3;
        [SerializeField] Sprite  countdown2;
        [SerializeField] Sprite  countdown1;
        [SerializeField] Sprite  countdown0;
        [SerializeField] AudioClip countdownBeep;
        [SerializeField] float     countdownDuration  = 1f;
        [SerializeField] float     countdownGrowScale = 1.5f;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        Sprite[] _sprites;
        Sequence _seq;

        void Awake()
        {
            _sprites = new[] { countdown3, countdown2, countdown1, countdown0 };
        }

        public void BeginCountdown(Action onComplete)
        {
            _seq?.Kill();
            _seq = DOTween.Sequence();

            bool unscaled = animSettings == null || animSettings.useUnscaledTime;
            if (unscaled) _seq.SetUpdate(true);

            countdownDisplay.gameObject.SetActive(true);

            var ease = animSettings ? animSettings.countdownScaleEase : Ease.OutQuad;
            float fadeIn = animSettings ? animSettings.countdownFadeInDuration : 0.1f;
            var urgentColor = animSettings ? animSettings.countdownUrgentColor : new Color(1f, 0.3f, 0.2f, 1f);
            int urgentStart = animSettings ? animSettings.countdownUrgentStartIndex : 2;

            for (int i = 0; i < _sprites.Length; i++)
            {
                int idx = i;
                Sprite spr = _sprites[i];

                _seq.AppendCallback(() =>
                {
                    countdownDisplay.sprite = spr;
                    countdownDisplay.transform.localScale = Vector3.one;
                    countdownDisplay.color = idx >= urgentStart
                        ? urgentColor
                        : Color.white;
                    AudioSystem.Instance.PlaySFXClip(countdownBeep);
                });

                // Fade in from transparent
                _seq.Append(countdownDisplay.DOFade(1f, fadeIn));

                // Scale grow with easing (runs for remaining beat duration)
                _seq.Join(countdownDisplay.transform
                    .DOScale(countdownGrowScale, countdownDuration - fadeIn)
                    .SetEase(ease));

                // Reset alpha before next sprite (skip on last)
                if (idx < _sprites.Length - 1)
                {
                    _seq.AppendCallback(() =>
                    {
                        var c = countdownDisplay.color;
                        countdownDisplay.color = new Color(c.r, c.g, c.b, 0f);
                    });
                }
            }

            _seq.OnComplete(() =>
            {
                countdownDisplay.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }

        private void OnDestroy()
        {
            _seq?.Kill();
        }
    }
}
