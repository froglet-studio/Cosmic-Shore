using System;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Toy Box's <b>daily activity</b> tile - the left of the two big buttons at the top of the
    /// window, and the Toy Box's answer to the arcade's weekly-challenge card.
    ///
    /// <para>Names today's activity, what it pays, and how long is left to do it. Pressing it opens
    /// that toy's detail window with the activity already selected, so the commit is one more press
    /// and the player can see what they are being asked to fly first.</para>
    ///
    /// <para><b>A VIEW.</b> It holds no activity state and makes no decision: <see cref="DailyToyActivity"/>
    /// is the authority on what today is and whether it has been paid, and
    /// <see cref="ToyboxModal"/> owns what a press does - the same split as
    /// <see cref="ToyboxCard"/>. Every field is optional, for the reason
    /// <see cref="WeeklyChallengeCard"/> gives: an un-wired label must never be the reason a
    /// feature looks broken.</para>
    ///
    /// <para>The countdown ticks at 1 Hz, not per frame - it displays whole minutes at worst, so
    /// anything faster is work nobody can see. The tick also re-reads the activity, because the
    /// toybox is built after this card is enabled and a card that resolved once would sit on
    /// "UNAVAILABLE" for the life of the scene.</para>
    /// </summary>
    public class DailyActivityCard : MonoBehaviour
    {
        [Header("Labels (all optional)")]
        [SerializeField, Tooltip("Heading - 'TODAY'S ACTIVITY'. Taken from the config asset.")]
        TMP_Text titleText;

        [SerializeField, Tooltip("Today's activity, e.g. 'Rainbow' - the leaf's own name.")]
        TMP_Text activityText;

        [SerializeField, Tooltip("Which toy offers it, e.g. 'Connect the Dots'.")]
        TMP_Text toyText;

        [SerializeField, Tooltip("What it pays, or that today is already claimed, plus the " +
                                 "countdown to tomorrow's activity.")]
        TMP_Text statusText;

        [Header("Art (all optional)")]
        [SerializeField, Tooltip("Flat fill behind the card, tinted with the offering toy's accent.")]
        Image accentFill;

        [SerializeField, Tooltip("Shown only once today's activity has been started and paid for.")]
        GameObject claimedBadge;

        ToyboxModal _modal;
        Button _button;
        float _tickAccumulator;

        void Awake() => _button = GetComponent<Button>();

        void OnEnable() => Redraw();

        void Update()
        {
            _tickAccumulator += Time.unscaledDeltaTime;
            if (_tickAccumulator < 1f) return;
            _tickAccumulator = 0f;
            Redraw();
        }

        /// <summary>
        /// Called by <see cref="ToyboxModal"/> as it refreshes, so a press can route without the
        /// card hunting for the window - and so the card redraws when the toybox changes under it.
        /// </summary>
        public void Bind(ToyboxModal modal)
        {
            _modal = modal;

            if (!_button) _button = GetComponent<Button>();
            if (_button)
            {
                _button.onClick.RemoveListener(HandleClicked);
                _button.onClick.AddListener(HandleClicked);
            }

            Redraw();
        }

        void HandleClicked()
        {
            if (_modal) _modal.SelectDailyActivity();
        }

        void Redraw()
        {
            var config = ToyboxDailyActivityConfigSO.Instance;
            SetText(titleText, config != null ? config.ActivityTitle : "TODAY'S ACTIVITY");

            var pick = DailyToyActivity.Today;

            if (!pick.IsValid)
            {
                // No toy is offering an activity - the toybox has not been built, or a cell swap
                // has just torn it down. Say so rather than showing a live-looking card that does
                // nothing when pressed.
                SetText(activityText, "UNAVAILABLE");
                SetText(toyText, "");
                SetText(statusText, "NO ACTIVITY RIGHT NOW");
                if (claimedBadge) claimedBadge.SetActive(false);
                if (_button) _button.interactable = false;
                return;
            }

            bool claimed = DailyToyActivity.ClaimedToday;

            SetText(activityText, pick.ActivityName);
            SetText(toyText, pick.ToyName);

            int reward = DailyToyActivity.RewardCrystals;
            string countdown = FormatCountdown(DailyToyActivity.TimeUntilTomorrow);

            // Three readings, and the difference between the first two is the point: a claimed day
            // is still PLAYABLE - it just pays nothing more - so the card never goes dead on the
            // player who did what it asked.
            SetText(statusText, claimed ? $"DONE - NEW ACTIVITY IN {countdown}"
                    : reward > 0 ? $"TRY IT FOR {reward} CRYSTALS - {countdown} LEFT"
                                 : $"{countdown} LEFT");

            if (claimedBadge) claimedBadge.SetActive(claimed);

            if (accentFill)
            {
                // The toy's own accent, at whatever opacity the strip was authored with - and only
                // when it changed, for the same reason SetText compares first.
                var accent = pick.Option.Accent;
                accent.a = accentFill.color.a;
                if (accent != accentFill.color) accentFill.color = accent;
            }

            if (_button) _button.interactable = true;
        }

        /// <summary>
        /// The countdown, at the resolution the remaining time deserves: hours while there are any
        /// (<c>7h 12m</c>), minutes inside the last hour, and seconds only in the last minute - the
        /// one stretch where a second matters to somebody deciding whether to start.
        /// </summary>
        public static string FormatCountdown(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;

            if (span.TotalHours >= 1d) return $"{(int)span.TotalHours}h {span.Minutes}m";
            if (span.TotalMinutes >= 1d) return $"{(int)span.TotalMinutes}m";
            return $"{span.Seconds}s";
        }

        /// <summary>
        /// Write a label only when the visible string actually CHANGED. A TMP_Text assignment
        /// dirties the mesh and queues a rebuild, and this card redraws once a second for the life
        /// of the scene - the window hides by CanvasGroup alpha rather than deactivating, so the
        /// tick never stops. Comparing against the label's own text needs no per-label state.
        /// </summary>
        static void SetText(TMP_Text label, string value)
        {
            if (!label || label.text == value) return;
            label.text = value;
        }
    }
}
