using DG.Tweening;
using UnityEngine;

namespace CosmicShore.UI
{
    [CreateAssetMenu(
        fileName = "ToastNotificationSettings",
        menuName = "ScriptableObjects/UI/Toast Notification Settings")]
    public class ToastNotificationSettingsSO : ScriptableObject
    {
        [Header("Slide Animation")]
        [Tooltip("Duration of the slide-in animation in seconds.")]
        public float slideInDuration = 0.35f;

        [Tooltip("Duration of the slide-out animation in seconds (swipe dismiss or auto-remove).")]
        public float slideOutDuration = 0.25f;

        [Tooltip("Easing curve for slide-in.")]
        public Ease slideInEase = Ease.OutCubic;

        [Tooltip("Easing curve for slide-out.")]
        public Ease slideOutEase = Ease.InCubic;

        [Tooltip("Extra pixels beyond the toast's own width for the off-screen slide start/end position.")]
        public float offscreenPadding = 24f;

        [Header("Fade")]
        [Tooltip("Duration of the fade-in (overlaps with slide-in).")]
        public float fadeInDuration = 0.25f;

        [Tooltip("Duration of the fade-out (overlaps with slide-out).")]
        public float fadeOutDuration = 0.2f;

        [Header("Text Animation")]
        [Tooltip("Reveal the message with a typewriter effect while the toast slides in.")]
        public bool useTypewriterText = true;

        [Tooltip("Characters revealed per second during the typewriter effect.")]
        public float typewriterCharactersPerSecond = 45f;

        [Tooltip("Upper bound on the typewriter reveal so long messages don't crawl.")]
        public float typewriterMaxDuration = 1.5f;

        [Header("Lifetime")]
        [Tooltip("Seconds the toast stays visible (after the intro finishes) before auto-dismissing.")]
        public float autoRemoveDelay = 5f;

        [Header("Swipe Dismiss")]
        [Tooltip("Minimum horizontal drag distance (in pixels) to trigger a swipe dismiss.")]
        public float swipeDismissThreshold = 60f;

        [Header("Capacity")]
        [Tooltip("Maximum number of toasts visible at the same time. Additional toasts wait in the queue.")]
        public int maxVisible = 3;

        [Tooltip("Maximum number of queued toasts. Oldest queued toast is dropped when exceeded.")]
        public int maxQueue = 10;

        [Header("Timing")]
        [Tooltip("Use unscaled time so toasts work even when Time.timeScale is 0 (e.g. pause menus).")]
        public bool useUnscaledTime = true;
    }
}
