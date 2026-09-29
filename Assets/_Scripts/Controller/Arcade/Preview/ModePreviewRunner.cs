using System;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How a preview flight ended.</summary>
    public enum ModePreviewOutcome
    {
        /// <summary>The player hit the objective target.</summary>
        Completed = 0,
        /// <summary>The preview's own time limit expired.</summary>
        TimedOut = 1,
        /// <summary>The player left (button, gamepad Start, screen change, launch, …).</summary>
        Abandoned = 2,
    }

    /// <summary>
    /// The objective half of a Test Flight: watch ONE stat and a clock, and say when the taste
    /// is over.
    ///
    /// <para>Deliberately a <b>plain MonoBehaviour</b> — not a <c>MiniGameControllerBase</c>, not
    /// a <c>NetworkBehaviour</c>. A preview has no rounds, no turns, no countdown, no scoreboard,
    /// no end-game sequence and no replay, and it must never write <see cref="GameDataSO"/>
    /// (the real launch config, which replicates to the party). Everything it needs is a
    /// baseline, a delta and a timer.</para>
    ///
    /// <para>It reads the same <see cref="ScoringMetric"/> the mode actually scores on, through
    /// the same <see cref="ScoringMetrics.Read"/> reader, so the number a player watches in the
    /// preview is the number they will watch in the real game. It is read <b>relative to a
    /// baseline</b> taken at Begin, because <c>RoundStats</c> live on the persistent Player
    /// object and have been accumulating for the whole menu session by the time a preview
    /// starts.</para>
    /// </summary>
    public sealed class ModePreviewRunner : MonoBehaviour
    {
        IRoundStats _stats;
        ScoringMetric _metric;
        int _baseline;
        int _target;
        float _duration;
        float _elapsed;
        Action<ModePreviewOutcome> _onFinished;

        /// <summary>Raised whenever <see cref="Progress"/> changes, so the HUD needs no Update.</summary>
        public event Action OnProgressChanged;

        /// <summary>True between <see cref="Begin"/> and the flight ending.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>The one line of instruction shown for the whole flight.</summary>
        public string ObjectiveText { get; private set; } = string.Empty;

        /// <summary>Metric gained since the flight started, never above <see cref="Target"/>.</summary>
        public int Progress { get; private set; }

        /// <summary>Target to reach, or 0 when the preview is open-ended.</summary>
        public int Target => _target;

        /// <summary>True when this preview counts toward something.</summary>
        public bool HasTarget => _target > 0;

        /// <summary>Seconds left, or -1 when the preview has no time limit.</summary>
        public float SecondsRemaining => _duration > 0f ? Mathf.Max(0f, _duration - _elapsed) : -1f;

        /// <summary>
        /// Start watching. <paramref name="stats"/> is the local player's own round stats; a null
        /// one is legal and simply produces a flight with no counter (a mode whose stat channel
        /// does not fire outside a real match still gets a flyable arena, which is most of the
        /// value).
        /// </summary>
        public void Begin(IRoundStats stats, ModePreviewDefinitionSO definition,
                          Action<ModePreviewOutcome> onFinished)
        {
            if (!definition)
            {
                CSDebug.LogWarning("[ModePreview] Runner started with no definition - ignored.");
                return;
            }

            _stats = stats;
            _metric = definition.ObjectiveMetric;
            _target = Mathf.Max(0, definition.ObjectiveTarget);
            _duration = Mathf.Max(0f, definition.DurationSeconds);
            _onFinished = onFinished;
            _elapsed = 0f;
            Progress = 0;
            ObjectiveText = definition.ObjectiveText;

            // The baseline, not zero: these stats have been accumulating since the player
            // entered the menu, so an absolute read would start a "collect 3 crystals" preview
            // already finished.
            _baseline = ReadMetric();

            IsRunning = true;
            OnProgressChanged?.Invoke();
        }

        /// <summary>
        /// Stop without reporting an outcome. Used by the session when the player leaves by any
        /// route - the session already knows why, and a callback here would re-enter its own
        /// teardown.
        /// </summary>
        public void Stop()
        {
            IsRunning = false;
            _onFinished = null;
            _stats = null;
        }

        void Update()
        {
            if (!IsRunning) return;

            int gained = Mathf.Max(0, ReadMetric() - _baseline);
            int clamped = HasTarget ? Mathf.Min(gained, _target) : gained;
            if (clamped != Progress)
            {
                Progress = clamped;
                OnProgressChanged?.Invoke();
            }

            if (HasTarget && Progress >= _target)
            {
                Finish(ModePreviewOutcome.Completed);
                return;
            }

            // Unscaled: a preview runs in the menu, where PauseSystem and modal flows are free
            // to touch timeScale. A taste that silently stops counting down is worse than one
            // that ends a beat early.
            _elapsed += Time.unscaledDeltaTime;
            if (_duration > 0f && _elapsed >= _duration)
                Finish(ModePreviewOutcome.TimedOut);
        }

        void Finish(ModePreviewOutcome outcome)
        {
            IsRunning = false;
            var callback = _onFinished;
            _onFinished = null;
            _stats = null;
            callback?.Invoke(outcome);
        }

        int ReadMetric() => _stats != null ? ScoringMetrics.Read(_stats, _metric) : 0;
    }
}
