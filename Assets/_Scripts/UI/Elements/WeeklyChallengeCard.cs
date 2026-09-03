using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The arcade grid's weekly-challenge tile: this week's mode, this week's objective, and a live
    /// countdown to the next challenge.
    ///
    /// <para>Every field is optional - a prefab that only wires <see cref="GameTitle"/> and
    /// <see cref="TimeRemaining"/> (which is all the shipped one has) still shows the mode and the
    /// countdown. That is deliberate: the card's job is to be readable with whatever art it has,
    /// and an un-wired label must never be the reason the feature looks broken.</para>
    ///
    /// <para>The countdown ticks at 1 Hz, not per frame - it displays whole seconds, so anything
    /// faster is work nobody can see.</para>
    /// </summary>
    public class WeeklyChallengeCard : MonoBehaviour
    {
        [Header("Placeholder Locations")]
        [Tooltip("ThisWeek's mode name, e.g. 'Scurry'.")]
        [SerializeField] TMP_Text GameTitle;

        [Tooltip("Countdown line: 'ENDS IN 6d 3h' while the challenge is live, " +
                 "'NEXT IN 6d 3h' once the player has completed it. Days lead until the last " +
                 "day, then hours, then minutes.")]
        [SerializeField] TMP_Text TimeRemaining;

        [Tooltip("Optional card art. Filled from the mode's arcade card when one is authored.")]
        [SerializeField] Image BackgroundImage;

        [Header("Challenge Detail (all optional)")]
        [Tooltip("The objective line, e.g. 'Collect 30 crystals in 1:00'.")]
        [SerializeField] TMP_Text ObjectiveText;

        [Tooltip("Progress line, e.g. 'BEST 24 / 30' or 'COMPLETE'.")]
        [SerializeField] TMP_Text StatusText;

        [Tooltip("Shown only once this week's challenge has been completed (a tick, a ribbon).")]
        [SerializeField] GameObject CompletedBadge;

        ArcadeExploreView _exploreView;
        Button _button;
        float _tickAccumulator;
        string _lastCountdown = "";
        WeeklyChallengeService _subscribedService;

        void Awake()
        {
            _button = GetComponent<Button>();
        }

        void OnEnable()
        {
            EnsureSubscribed();
            Redraw();
        }

        void OnDisable()
        {
            if (_subscribedService != null)
            {
                _subscribedService.OnChallengeChanged -= Redraw;
                _subscribedService = null;
            }
        }

        void Update()
        {
            _tickAccumulator += Time.unscaledDeltaTime;
            if (_tickAccumulator < 1f) return;
            _tickAccumulator = 0f;

            // Re-checked on the tick, not only at OnEnable: the service creates itself at
            // AfterSceneLoad, so a card enabled in the very first scene can come up before it
            // exists. Subscribing once and never looking again would leave that card frozen on
            // whatever it drew first.
            EnsureSubscribed();
            RedrawCountdown();
        }

        void EnsureSubscribed()
        {
            var service = WeeklyChallengeService.Instance;
            if (service == null || service == _subscribedService) return;

            if (_subscribedService != null)
                _subscribedService.OnChallengeChanged -= Redraw;

            _subscribedService = service;
            _subscribedService.OnChallengeChanged += Redraw;
            Redraw();
        }

        /// <summary>
        /// Called by <see cref="ArcadeExploreView"/> as it builds the grid, so the card can route
        /// a press without hunting for the view. Also re-arms the button, which the view clears
        /// alongside every other card's.
        /// </summary>
        public void Bind(ArcadeExploreView exploreView)
        {
            _exploreView = exploreView;

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
            if (_exploreView == null) return;
            _exploreView.SelectWeeklyChallenge();
        }

        void Redraw()
        {
            var service = WeeklyChallengeService.Instance;
            var challenge = service != null ? service.ThisWeek : default(WeeklyChallenge);

            if (!challenge.IsValid)
            {
                // No catalog, or every entry filtered out. Say so rather than showing a live-looking
                // card that does nothing when pressed.
                SetText(GameTitle, "WEEKLY CHALLENGE");
                SetText(TimeRemaining, "UNAVAILABLE");
                SetText(ObjectiveText, "");
                SetText(StatusText, "");
                if (CompletedBadge) CompletedBadge.SetActive(false);
                if (_button) _button.interactable = false;
                return;
            }

            bool completed = service.CompletedThisWeek;
            bool spent = !service.CanAttempt;

            SetText(GameTitle, ResolveModeName(challenge.GameMode));
            SetText(ObjectiveText, challenge.ObjectiveText);

            // Three states, and the difference between the last two matters: a player who ran out
            // of attempts without meeting the objective has NOT completed it, and a card that said
            // COMPLETE either way would be lying about their day.
            // A mode-target challenge has no denominator to show before the match exists - the
            // number is the live race's - so the card shows the best alone.
            string best = challenge.UsesModeTarget
                ? $"{service.BestValueThisWeek}"
                : $"{service.BestValueThisWeek} / {challenge.TargetValue}";

            SetText(StatusText,
                completed ? "COMPLETE"
                : spent   ? $"PLAYED - BEST {best}"
                          : $"BEST {best}");

            if (CompletedBadge) CompletedBadge.SetActive(completed);

            if (BackgroundImage)
            {
                var art = ResolveModeArt(challenge.GameMode);
                if (art) BackgroundImage.sprite = art;
            }

            // The card stays OPENABLE once the attempt is spent, and the launch panel greys its
            // own Start button instead. It used to go dead here, which also made the week's
            // LEADERBOARD unreachable - the board lives behind this card, so a card that closes on
            // the run that earns you a place on it hides the result for the other six days.
            // CanAttempt is still the single authority for whether it can be PLAYED; it is simply
            // no longer the authority for whether it can be LOOKED at.
            if (_button)
                _button.interactable = true;

            _lastCountdown = "";
            RedrawCountdown();
        }

        void RedrawCountdown()
        {
            if (!TimeRemaining) return;

            var service = WeeklyChallengeService.Instance;
            if (service == null || !service.ThisWeek.IsValid) return;

            var remaining = service.TimeUntilNextChallenge;
            string label = service.CanAttempt ? "ENDS IN" : "NEXT IN";
            string text = $"{label} {FormatCountdown(remaining)}";

            // Only touch the label when the visible string actually changed - a TMP_Text assignment
            // dirties the mesh, and this runs on a card that may be one of two dozen on screen.
            if (text == _lastCountdown) return;
            _lastCountdown = text;
            TimeRemaining.text = text;
        }

        /// <summary>
        /// The countdown, at the resolution the remaining time deserves.
        ///
        /// <para>A week is up to <b>168 hours</b>, and "163:04:11" is a number nobody reads as
        /// time. So days lead while there are any (<c>6d 3h</c>), hours take over inside the last
        /// day (<c>7:12:33</c>), and only the final hour counts seconds — the one stretch where a
        /// second matters to somebody deciding whether to start a run.</para>
        /// </summary>
        public static string FormatCountdown(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;

            if (span.TotalDays >= 1d) return $"{span.Days}d {span.Hours}h";
            if (span.TotalHours >= 1d) return $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}";
            return $"{span.Minutes}:{span.Seconds:D2}";
        }

        string ResolveModeName(GameModes mode)
        {
            var card = _exploreView != null ? _exploreView.FindGameByMode(mode) : null;
            return card != null && !string.IsNullOrWhiteSpace(card.DisplayName)
                ? card.DisplayName
                : mode.ToString();
        }

        Sprite ResolveModeArt(GameModes mode)
        {
            var card = _exploreView != null ? _exploreView.FindGameByMode(mode) : null;
            return card != null ? card.CardBackground : null;
        }

        static void SetText(TMP_Text label, string value)
        {
            if (label) label.text = value;
        }
    }
}
