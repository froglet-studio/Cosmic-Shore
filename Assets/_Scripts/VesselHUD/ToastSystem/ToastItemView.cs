using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI.Toast
{
    public sealed class ToastItemView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] RectTransform root;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image background;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text prefixText;   // main line
        [SerializeField] TMP_Text postfixText;  // aux/timer line

        [Header("Anim")]
        [SerializeField] Vector2 chatSlideOffset = new(0f, -24f); // spawn slightly below, slide up
        [SerializeField] float inTime  = 0.22f;
        [SerializeField] float outTime = 0.18f;

        Tween _inTween, _outTween;
        Coroutine _runCR;

        public void Play(ChatToastRequest req, System.Action reclaimed, System.Action onDoneExternal)
        {
            gameObject.SetActive(true);

            if (background && req.Accent.HasValue) background.color = req.Accent.Value;
            if (iconImage)  iconImage.enabled = req.Icon != null;
            if (iconImage && req.Icon) iconImage.sprite = req.Icon;

            prefixText.text  = req.Prefix ?? "";
            postfixText.text = req.Postfix ?? "";
            postfixText.gameObject.SetActive(!string.IsNullOrEmpty(postfixText.text) || req.PostfixCountdownFrom > 0);

            _inTween?.Kill(); _outTween?.Kill();
            _inTween = PlayIn(req.Animation);

            if (_runCR != null) StopCoroutine(_runCR);
            _runCR = StartCoroutine(Run(req, reclaimed, onDoneExternal));
        }

        IEnumerator Run(ChatToastRequest req, System.Action reclaimed, System.Action onDoneExternal)
        {
            yield return _inTween.WaitForCompletion();

            if (req.PostfixCountdownFrom > 0)
            {
                int n = req.PostfixCountdownFrom;
                while (n > 0)
                {
                    postfixText.text = string.Format(req.PostfixCountdownFormat ?? "{0}", n);
                    yield return new WaitForSeconds(1f);
                    n--;
                }
                postfixText.text = string.Empty; // clear after countdown
                onDoneExternal?.Invoke();        // e.g., ConfirmOvercharge
                yield return new WaitForSeconds(0.3f); // small linger
            }
            else
            {
                // prefix-only or prefix+postfix static → stick around longer (req.Duration)
                float stay = Mathf.Max(0.5f, req.Duration <= 0f ? 4.5f : req.Duration);
                yield return new WaitForSeconds(stay);
            }

            _outTween = PlayOut();
            yield return _outTween.WaitForCompletion();

            ForceHide();
            reclaimed?.Invoke();
        }

        public void ForceHide()
        {
            _inTween?.Kill(); _outTween?.Kill();
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        Tween PlayIn(ToastAnimation anim)
        {
            canvasGroup.alpha = 0f;
            root.localScale = Vector3.one;
            switch (anim)
            {
                case ToastAnimation.ChatSubtleSlide:
                    var start = root.anchoredPosition + chatSlideOffset;
                    root.anchoredPosition = start;
                    return DOTween.Sequence()
                        .AppendCallback(() => canvasGroup.alpha = 1f)
                        .Append(root.DOAnchorPos(start - chatSlideOffset, inTime).SetEase(Ease.OutCubic));

                case ToastAnimation.Pop:
                    root.localScale = Vector3.one * 0.9f;
                    return DOTween.Sequence()
                        .AppendCallback(() => canvasGroup.alpha = 1f)
                        .Append(root.DOScale(1.08f, 0.12f))
                        .Append(root.DOScale(1f,    0.10f));

                default: // Fade
                    return canvasGroup.DOFade(1f, inTime);
            }
        }

        Tween PlayOut()
        {
            return DOTween.Sequence()
                .Append(root.DOScale(0.98f, 0.1f))
                .Join(canvasGroup.DOFade(0f, outTime));
        }
    }
}
