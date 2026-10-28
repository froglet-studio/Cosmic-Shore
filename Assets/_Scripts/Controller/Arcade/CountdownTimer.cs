using CosmicShore.Core;
using CosmicShore.UI;
using DG.Tweening;
using TMPro;
using FMODUnity;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    public class CountdownTimer : MonoBehaviour
    {
        [SerializeField] Image   countdownDisplay;
        [SerializeField] Sprite  countdown3;
        [SerializeField] Sprite  countdown2;
        [SerializeField] Sprite  countdown1;
        [SerializeField] Sprite  countdown0;
        [SerializeField] float     countdownDuration  = 1f;
        [SerializeField] float     countdownGrowScale = 1.5f;

        [Header("Audio")]
        [SerializeField, Tooltip("FMOD event played on each countdown beat. Leave empty for silence.")]
        EventReference countdownBeepEvent;

        [Header("Style B (Docs/MENU_REDESIGN_B.md section 22.2)")]
        [Tooltip("When assigned, the beats are drawn as TEXT - 3, 2, 1, GO in Aldrich - instead of " +
                 "the neon sprites, which stay as the fallback. The sprite Image is kept (its " +
                 "GameObject is what shows and hides the countdown) but draws nothing.")]
        [SerializeField] TMP_Text countdownText;
        [Tooltip("The last beat's word.")]
        [SerializeField] string goWord = "GO";
        [Tooltip("The numbers' colour.")]
        [SerializeField] UIColorToken beatToken = UIColorToken.TextLight;
        [Tooltip("GO's colour - the CTA green, the one beat that is an instruction.")]
        [SerializeField] UIColorToken goToken = UIColorToken.Cta;
        [Tooltip("Optional. Hidden when the countdown starts - the 'waiting for pilots' state ends here.")]
        [SerializeField] ReadyWaitingPanel waitingPanel;
        [Tooltip("The numbers' size; 0 keeps the text's own.")]
        [SerializeField, Min(0f)] float numberSize = 168f;
        [Tooltip("GO's size; 0 keeps the text's own.")]
        [SerializeField, Min(0f)] float goSize = 132f;

        [Header("Hex frame (Rev 33, Docs/MENU_REDESIGN_B.md section 99)")]
        [Tooltip("Optional. The hex the count sits in: it fills from the bottom through each number - the cell-change " +
                 "screen's crystal, counting - and bursts outward at GO so GO stands free.")]
        [SerializeField] LoadCrystalGraphic beatHex;
        [Tooltip("The hex's group: it opens in with the count and fades as it bursts.")]
        [SerializeField] CanvasGroup beatHexGroup;
        [Tooltip("Optional. The ring that leaves the hex at GO (its outline is GO's colour; it is shown only at GO).")]
        [SerializeField] LoadCrystalGraphic goRing;
        [Tooltip("The hex's fill.")]
        [SerializeField] UIColorToken hexFillToken = UIColorToken.TextLight;
        [SerializeField, Range(0f, 1f)] float hexFillAlpha = 0.26f;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        Sprite[] _sprites;
        Sequence _seq;

        void Awake()
        {
            EnsureSpritesInitialized();
        }

        void EnsureSpritesInitialized()
        {
            _sprites ??= new[] { countdown3, countdown2, countdown1, countdown0 };
        }

        public void BeginCountdown(Action onComplete)
        {
            if (waitingPanel) waitingPanel.Hide();
            if (countdownText)
            {
                BeginTextCountdown(onComplete);
                return;
            }
            EnsureSpritesInitialized();
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
                    if (!countdownBeepEvent.IsNull)
                        AudioSystem.Instance.PlaySFXEvent(countdownBeepEvent);
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

        /// <summary>
        /// The style-B beats: each number rises from 0.7 to a 1.08 overshoot, settles, holds, and
        /// leaves at 1.3 as it fades - the board's motion. Same beat length and the same
        /// beep slot as the sprite path, so a mode's timing does not move.
        /// </summary>
        void BeginTextCountdown(Action onComplete)
        {
            _seq?.Kill();
            _seq = DOTween.Sequence();
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;
            if (unscaled) _seq.SetUpdate(true);

            countdownDisplay.sprite = null;
            countdownDisplay.color = new Color(0f, 0f, 0f, 0f);
            countdownDisplay.gameObject.SetActive(true);

            var t = countdownText.transform;
            string[] beats = { "3", "2", "1", goWord };
            float rise = countdownDuration * 0.25f, settle = countdownDuration * 0.2f, leave = countdownDuration * 0.15f;
            float hold = Mathf.Max(0f, countdownDuration - rise - settle - leave);
            RestHex();
            for (int i = 0; i < beats.Length; i++)
            {
                string word = beats[i];
                bool go = i == beats.Length - 1;
                _seq.AppendCallback(() =>
                {
                    countdownText.text = word;
                    float size = go ? goSize : numberSize;
                    if (size > 0f) countdownText.fontSize = size;
                    var c = UITheme.Resolve(null, go ? goToken : beatToken);
                    c.a = 0f;
                    countdownText.color = c;
                    t.localScale = Vector3.one * 0.7f;
                    if (beatHex) beatHex.Level = 0f;
                    if (go) BurstHex();
                    if (!countdownBeepEvent.IsNull)
                        AudioSystem.Instance.PlaySFXEvent(countdownBeepEvent);
                });
                _seq.Append(countdownText.DOFade(1f, rise));
                _seq.Join(t.DOScale(1.08f, rise).SetEase(Ease.OutCubic));
                _seq.Append(t.DOScale(1f, settle).SetEase(Ease.OutQuad));
                _seq.AppendInterval(hold);
                _seq.Append(countdownText.DOFade(0f, leave));
                _seq.Join(t.DOScale(1.3f, leave).SetEase(Ease.InQuad));
            }

            // After the beats: an Insert before them would lengthen the sequence, and every Append after it would start late.
            InsertHex(beats.Length - 1);
            _seq.OnComplete(() =>
            {
                RestHex();
                countdownDisplay.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }

        // The hex opens in with the count and fills through each numbered beat, on the sequence's own clock (inserted at
        // absolute times into the beats already appended).
        void InsertHex(int numbers)
        {
            if (!beatHex) return;
            var fill = UITheme.Resolve(null, hexFillToken);
            fill.a *= hexFillAlpha;
            beatHex.SetFill(fill);
            var hexT = beatHex.rectTransform;
            hexT.localScale = Vector3.one * 0.7f;
            if (beatHexGroup)
            {
                beatHexGroup.alpha = 0f;
                _seq.Insert(0f, beatHexGroup.DOFade(1f, countdownDuration * 0.4f));
            }
            _seq.Insert(0f, hexT.DOScale(1f, countdownDuration * 0.4f).SetEase(Ease.OutCubic));
            for (int i = 0; i < numbers; i++)
                _seq.Insert(i * countdownDuration, DOVirtual.Float(0f, 1f, countdownDuration * 0.97f, v => beatHex.Level = v)
                    .SetEase(Ease.Linear));
        }

        // GO: the hex bursts outward and the ring leaves it, so GO stands free.
        void BurstHex()
        {
            float burst = countdownDuration * 0.42f;
            if (beatHex)
            {
                beatHex.rectTransform.DOScale(1.45f, burst).SetEase(Ease.InCubic).SetUpdate(true);
                if (beatHexGroup) beatHexGroup.DOFade(0f, burst).SetUpdate(true);
            }
            if (!goRing) return;
            var c = goRing.color;
            c.a = 1f;
            goRing.color = c;
            goRing.rectTransform.localScale = Vector3.one;
            goRing.rectTransform.DOScale(3.4f, countdownDuration * 0.9f).SetEase(Ease.OutCubic).SetUpdate(true);
            goRing.DOFade(0f, countdownDuration * 0.9f).SetUpdate(true);
        }

        void RestHex()
        {
            if (beatHex)
            {
                beatHex.rectTransform.DOKill();
                beatHex.Level = 0f;
                beatHex.rectTransform.localScale = Vector3.one;
            }
            if (beatHexGroup)
            {
                beatHexGroup.DOKill();
                beatHexGroup.alpha = 0f;
            }
            if (goRing)
            {
                goRing.DOKill();
                goRing.rectTransform.DOKill();
                var c = goRing.color;
                c.a = 0f;
                goRing.color = c;
            }
        }

        private void OnDestroy()
        {
            _seq?.Kill();
        }
    }
}
