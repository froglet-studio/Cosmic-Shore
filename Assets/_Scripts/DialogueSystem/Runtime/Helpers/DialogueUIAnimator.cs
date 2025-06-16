using DG.Tweening;
using System;
using UnityEngine;

namespace CosmicShore.DialogueSystem.View
{
    public static class DialogueUIAnimator
    {
        public static void AnimateSpeakerIn(
            RectTransform speaker,
            RectTransform moveFrom,
            RectTransform moveTo,
            float duration,
            Ease ease,
            Action onArrive = null)
        {
            speaker.anchoredPosition = moveFrom.anchoredPosition; // CHANGED
            speaker.gameObject.SetActive(true);

            speaker.DOAnchorPos(moveTo.anchoredPosition, duration)
                   .SetEase(ease)
                   .OnComplete(() => onArrive?.Invoke());
        }

        public static void AnimateSpeakerOut(
            RectTransform speaker,
            RectTransform moveTo,
            float duration,
            Ease ease,
            Action onComplete = null)
        {
            speaker.DOAnchorPos(moveTo.anchoredPosition, duration)
                   .SetEase(ease)
                   .OnComplete(() =>
                   {
                       speaker.gameObject.SetActive(false);
                       onComplete?.Invoke();
                   });
        }

        // You can keep this utility for hiding things if you want
        public static void Hide(RectTransform t)
        {
            t.gameObject.SetActive(false);
        }
    }
}
