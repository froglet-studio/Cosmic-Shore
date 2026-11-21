using DG.Tweening;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Shared look/feel for the in-game toast feed (per CLAUDE.md config separation - one
    /// asset, no per-prefab drift). Entries do NOT expire: they stay in the scroll view and
    /// get pushed up as new lines arrive, dimming with age instead of disappearing.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameToastSettings",
        menuName = "ScriptableObjects/UI/Game Toast Settings")]
    public class GameToastSettingsSO : ScriptableObject
    {
        [Header("Slide In")]
        [Tooltip("Seconds for a new line to slide/fade in at the bottom of the feed.")]
        [SerializeField] float slideInDuration = 0.25f;
        public float SlideInDuration => slideInDuration;

        [Tooltip("Horizontal offset (from the right) a new line spawns at before sliding to " +
                 "rest. Only X animates - the vertical layout group owns Y so older lines " +
                 "can be pushed up by newer ones mid-tween.")]
        [SerializeField] float slideInOffset = 120f;
        public float SlideInOffset => slideInOffset;

        [SerializeField] Ease slideInEase = Ease.OutCubic;
        public Ease SlideInEase => slideInEase;

        [Header("Aging (entries never disappear - they dim and scroll up)")]
        [Tooltip("Seconds a line stays at full opacity before dimming.")]
        [SerializeField] float ageAfterSeconds = 6f;
        public float AgeAfterSeconds => ageAfterSeconds;

        [Range(0f, 1f)]
        [Tooltip("Alpha an aged line settles at. 1 = no dimming.")]
        [SerializeField] float agedAlpha = 0.45f;
        public float AgedAlpha => agedAlpha;

        [Tooltip("Seconds the dim fade takes.")]
        [SerializeField] float ageFadeDuration = 1f;
        public float AgeFadeDuration => ageFadeDuration;

        [Header("Capacity")]
        [Min(1)]
        [Tooltip("Oldest entries beyond this count are removed from the top of the scroll " +
                 "content (memory bound - far above what fits on screen).")]
        [SerializeField] int maxRetainedEntries = 40;
        public int MaxRetainedEntries => maxRetainedEntries;

        [Header("Scrolling")]
        [Tooltip("Keep the view pinned to the newest line when it was already at the bottom.")]
        [SerializeField] bool autoScrollToBottom = true;
        public bool AutoScrollToBottom => autoScrollToBottom;

        [Range(0f, 0.5f)]
        [Tooltip("How close to the bottom (normalized) the view must be for auto-scroll to " +
                 "re-pin after the player scrolled up to read history.")]
        [SerializeField] float stickToBottomThreshold = 0.05f;
        public float StickToBottomThreshold => stickToBottomThreshold;

        [Header("Time")]
        [SerializeField] bool useUnscaledTime = true;
        public bool UseUnscaledTime => useUnscaledTime;
    }
}
