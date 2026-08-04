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

        [Header("Fade")]
        [Tooltip("Duration of the fade-in (overlaps with slide-in).")]
        public float fadeInDuration = 0.25f;

        [Tooltip("Duration of the fade-out (overlaps with slide-out).")]
        public float fadeOutDuration = 0.2f;

        [Header("Lifetime")]
        [Tooltip("Seconds the toast stays visible before auto-dismissing.")]
        public float autoRemoveDelay = 5f;

        [Header("Swipe Dismiss")]
        [Tooltip("Minimum horizontal drag distance (in pixels) to trigger a swipe dismiss.")]
        public float swipeDismissThreshold = 60f;

        [Header("Layout")]
        [Tooltip("Extra pixels of padding beyond the screen edge for the off-screen start position.")]
        public float offscreenPadding = 24f;

        [Tooltip("Vertical offset from the top of the screen (in canvas units) for the toast anchor.")]
        public float topMargin = 120f;

        [Tooltip("Horizontal margin from the left edge when fully visible (in canvas units).")]
        public float leftMargin = 24f;

        [Tooltip("Spacing between stacked toasts (in canvas units).")]
        public float stackSpacing = 10f;

        [Header("Capacity")]
        [Tooltip("Maximum number of toasts visible at the same time. Oldest is dismissed when exceeded.")]
        public int maxVisible = 3;

        [Tooltip("Maximum number of queued toasts. Oldest queued toast is dropped when exceeded.")]
        public int maxQueue = 10;

        [Header("Timing")]
        [Tooltip("Use unscaled time so toasts work even when Time.timeScale is 0 (e.g. pause menus).")]
        public bool useUnscaledTime = true;
    }
}
