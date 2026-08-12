using System;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Measures time the player actually spends at the stick: from the moment control is
    /// granted (countdown end) to game end, with pause and app-background excluded.
    /// This is the <c>flight_time_seconds</c> the instrumentation team asked for, and it also
    /// feeds the per-vessel, per-mode and lifetime totals in Cloud Save.
    /// See Docs/Analytics/DATA_ARCHITECTURE.md §5.
    ///
    /// It measures TWO kinds of flying, because there are two. A game has a countdown, turns
    /// and an end; Menu_Main's freestyle (the lava lamp - one system, two names) has none of
    /// them, and never raises <c>GameDataSO.StartTurn</c>. Both are the player holding the
    /// stick, so both count toward lifetime and per-vessel totals - the second gate below is
    /// what stops menu flight from being invisible. They are accumulated separately so a
    /// freestyle segment can never contaminate a game's <see cref="LastGameSeconds"/>.
    ///
    /// Both obvious implementations are wrong, which is why this class exists:
    ///  - <c>Time.realtimeSinceStartup</c> (what the old <c>duration_seconds</c> uses) counts
    ///    pause and background time.
    ///  - Scaled <c>Time.time</c> excludes pause correctly, but Astro League drives
    ///    <c>Time.timeScale</c> for hitstop and goal slow-mo, so seconds spent in slow-mo
    ///    would be under-counted.
    ///
    /// <c>Time.unscaledTime</c> is immune to slow-mo, and it does not advance while the app is
    /// suspended, so it is the correct base clock.
    ///
    /// There is no Update: the clock integrates on state transitions only, so it costs nothing
    /// per frame. The game segment is open only while the turn is running, the game is not
    /// paused, and the app is in the foreground; the freestyle segment is the same minus the
    /// turn gate.
    /// </summary>
    public static class FlightClock
    {
        static bool _gameActive;
        static bool _turnRunning;
        static bool _paused;
        static bool _backgrounded;

        static bool _segmentOpen;
        static float _segmentStartUnscaled;
        static float _accumulated;

        static bool _freestyleActive;
        static bool _freestyleSegmentOpen;
        static float _freestyleSegmentStartUnscaled;

        /// <summary>
        /// Raised with the seconds of each completed freestyle segment. A segment closes on
        /// leaving freestyle, on pause, and on backgrounding - the last one deliberately, so a
        /// mobile app killed while suspended has already banked the time it earned.
        /// Subscribers must be additive: several segments make up one visit to freestyle.
        /// </summary>
        public static event Action<float> OnFreestyleSegmentCompleted;

        /// <summary>Flight seconds accumulated so far in the current game.</summary>
        public static float CurrentGameSeconds => _accumulated + OpenSegmentSeconds();

        /// <summary>Seconds banked so far in the freestyle segment currently open (0 when none is).</summary>
        public static float CurrentFreestyleSeconds => OpenFreestyleSegmentSeconds();

        /// <summary>Total freestyle seconds measured this run. Diagnostics only - not persisted from here.</summary>
        public static float SessionFreestyleSeconds { get; private set; }

        public static bool IsFreestyleActive => _freestyleActive;

        /// <summary>
        /// Flight seconds for the most recently completed game. Valid from the moment
        /// <see cref="EndGame"/> runs until the next game starts - which is why
        /// <c>GameDataSO.InvokeMiniGameEnd</c> calls EndGame BEFORE raising the SOAP event,
        /// so every subscriber reads a settled value regardless of subscription order.
        /// </summary>
        public static float LastGameSeconds { get; private set; }

        public static bool IsGameActive => _gameActive;

        /// <summary>
        /// Increments once per genuinely completed game. Consumers that must act exactly once
        /// per game latch this: some modes raise OnMiniGameEnd more than once (HexRace sets
        /// HasEndGame=false specifically to avoid a duplicate), and a repeated raise must not
        /// double-count lifetime totals.
        /// </summary>
        public static int CompletedGameSequence { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            // Domain reload may be disabled in the editor, so clear any carried-over state.
            _gameActive = _turnRunning = _paused = _backgrounded = _segmentOpen = false;
            _freestyleActive = _freestyleSegmentOpen = false;
            _accumulated = 0f;
            LastGameSeconds = 0f;
            SessionFreestyleSeconds = 0f;
            CompletedGameSequence = 0;
            OnFreestyleSegmentCompleted = null;

            PauseSystem.OnGamePaused -= HandleGamePaused;
            PauseSystem.OnGameResumed -= HandleGameResumed;
            ApplicationLifecycleManager.OnAppPaused -= HandleAppPaused;
            ApplicationLifecycleManager.OnAppFocusChanged -= HandleAppFocusChanged;

            PauseSystem.OnGamePaused += HandleGamePaused;
            PauseSystem.OnGameResumed += HandleGameResumed;
            ApplicationLifecycleManager.OnAppPaused += HandleAppPaused;
            ApplicationLifecycleManager.OnAppFocusChanged += HandleAppFocusChanged;

            // PauseSystem is static and survives scene loads; sync to its current state.
            _paused = PauseSystem.Paused;
        }

        /// <summary>
        /// Called from <c>GameDataSO.StartTurn</c> - the instant after the countdown when
        /// players are activated, i.e. exactly "when the player is given control".
        /// The first turn of a game starts a fresh accumulator; later turns and rounds add to
        /// it, so a multi-round game reports total time at the stick and the Ready-button gap
        /// between rounds is excluded for free.
        /// </summary>
        public static void OnTurnStarted()
        {
            if (!_gameActive)
            {
                _gameActive = true;
                _accumulated = 0f;
            }

            _turnRunning = true;
            UpdateSegment();
        }

        /// <summary>Called from <c>GameDataSO.InvokeGameTurnConditionsMet</c>.</summary>
        public static void OnTurnEnded()
        {
            _turnRunning = false;
            UpdateSegment();
        }

        /// <summary>
        /// Called from <c>GameDataSO.InvokeMiniGameEnd</c>, before the SOAP raise. Closes the
        /// open segment and publishes <see cref="LastGameSeconds"/>.
        /// </summary>
        public static void EndGame()
        {
            _turnRunning = false;
            UpdateSegment();

            if (!_gameActive)
                return;

            LastGameSeconds = _accumulated;
            CompletedGameSequence++;
            _gameActive = false;
            _accumulated = 0f;
        }

        /// <summary>Abandons the current game without publishing a time (scene bail-out, replay reset).</summary>
        public static void AbortGame()
        {
            _turnRunning = false;
            UpdateSegment();
            _gameActive = false;
            _accumulated = 0f;
        }

        /// <summary>
        /// Called from <c>MenuCrystalClickHandler</c> the moment the player takes the stick in
        /// Menu_Main. There is no countdown and no turn here, so this is the freestyle
        /// equivalent of <see cref="OnTurnStarted"/>.
        /// </summary>
        public static void OnFreestyleEntered()
        {
            _freestyleActive = true;
            UpdateFreestyleSegment();
        }

        /// <summary>
        /// Called when the player hands the vessel back to autopilot, and from the handler's
        /// OnDestroy so leaving Menu_Main banks the time instead of dropping it.
        /// Idempotent - a second call with no segment open does nothing.
        /// </summary>
        public static void OnFreestyleExited()
        {
            _freestyleActive = false;
            UpdateFreestyleSegment();
        }

        static void HandleGamePaused()
        {
            _paused = true;
            UpdateAllSegments();
        }

        static void HandleGameResumed()
        {
            _paused = false;
            UpdateAllSegments();
        }

        static void HandleAppPaused(bool paused)
        {
            _backgrounded = paused;
            UpdateAllSegments();
        }

        static void HandleAppFocusChanged(bool hasFocus)
        {
            // Desktop does not raise OnApplicationPause when the window loses focus, so focus
            // is the only signal there that the player has stopped flying.
            _backgrounded = !hasFocus;
            UpdateAllSegments();
        }

        static void UpdateAllSegments()
        {
            UpdateSegment();
            UpdateFreestyleSegment();
        }

        /// <summary>Opens or closes the accumulating segment to match the current gate state.</summary>
        static void UpdateSegment()
        {
            bool shouldRun = _gameActive && _turnRunning && !_paused && !_backgrounded;

            if (shouldRun == _segmentOpen)
                return;

            if (shouldRun)
            {
                _segmentStartUnscaled = Time.unscaledTime;
                _segmentOpen = true;
            }
            else
            {
                _accumulated += Mathf.Max(0f, Time.unscaledTime - _segmentStartUnscaled);
                _segmentOpen = false;
            }
        }

        /// <summary>
        /// Same integrator for freestyle, minus the turn gate. Publishes on close rather than
        /// accumulating to a "last visit" value: freestyle has no end event to read one at.
        /// </summary>
        static void UpdateFreestyleSegment()
        {
            bool shouldRun = _freestyleActive && !_paused && !_backgrounded;

            if (shouldRun == _freestyleSegmentOpen)
                return;

            if (shouldRun)
            {
                _freestyleSegmentStartUnscaled = Time.unscaledTime;
                _freestyleSegmentOpen = true;
                return;
            }

            float seconds = Mathf.Max(0f, Time.unscaledTime - _freestyleSegmentStartUnscaled);
            _freestyleSegmentOpen = false;

            if (seconds <= 0f)
                return;

            SessionFreestyleSeconds += seconds;
            OnFreestyleSegmentCompleted?.Invoke(seconds);
        }

        static float OpenSegmentSeconds() =>
            _segmentOpen ? Mathf.Max(0f, Time.unscaledTime - _segmentStartUnscaled) : 0f;

        static float OpenFreestyleSegmentSeconds() =>
            _freestyleSegmentOpen ? Mathf.Max(0f, Time.unscaledTime - _freestyleSegmentStartUnscaled) : 0f;
    }
}
