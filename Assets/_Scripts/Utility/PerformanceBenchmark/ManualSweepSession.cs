using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Runtime companion for the Sweep tab's Manual session: while you play, it captures
    /// errors/exceptions/asserts and the moments you mark (F8), with near-zero overhead - just a
    /// log callback plus a smoothed fps average. Frame stats are recorded separately by
    /// <see cref="PerformanceBenchmarkRunner"/>; this only adds the error log + marks that get
    /// folded into the saved report via <see cref="FillReport"/>.
    /// </summary>
    public class ManualSweepSession : MonoBehaviour
    {
        public static ManualSweepSession Instance { get; private set; }

        const int MaxErrors = 500;
        const int MaxMarks = 500;

        readonly List<SweepError> _errors = new();
        readonly List<SweepMark> _marks = new();

        public IReadOnlyList<SweepError> Errors => _errors;
        public IReadOnlyList<SweepMark> Marks => _marks;
        public int ErrorCount { get; private set; }   // total seen (may exceed the capped list)

        [SerializeField] Key markKey = Key.F8;

        float _startTime;
        float _smoothedMs;
        int _markCounter;

        public static ManualSweepSession StartSession()
        {
            Stop();
            var go = new GameObject("[ManualSweepSession]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ManualSweepSession>();
            return Instance;
        }

        public static void Stop()
        {
            if (Instance != null) Destroy(Instance.gameObject);
        }

        void Awake()
        {
            Instance = this;
            _startTime = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (Instance == this) Instance = null;
        }

        float Elapsed => Time.realtimeSinceStartup - _startTime;
        float Fps => _smoothedMs > 0.0001f ? 1000f / _smoothedMs : 0f;

        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            _smoothedMs = _smoothedMs <= 0f ? ms : Mathf.Lerp(_smoothedMs, ms, 0.1f);

            var kb = Keyboard.current;
            if (kb != null && kb[markKey].wasPressedThisFrame)
                AddMark(null);
        }

        /// <summary>Drops a timestamped mark with the current fps. Null/empty label auto-numbers it.</summary>
        public void AddMark(string label)
        {
            if (_marks.Count >= MaxMarks) return;
            _markCounter++;
            _marks.Add(new SweepMark
            {
                timeSeconds = Elapsed,
                fps = Fps,
                label = string.IsNullOrEmpty(label) ? $"Mark {_markCounter}" : label
            });
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            ErrorCount++;
            if (_errors.Count >= MaxErrors) return;

            string firstStack = "";
            if (!string.IsNullOrEmpty(stackTrace))
            {
                int nl = stackTrace.IndexOf('\n');
                firstStack = " @ " + (nl > 0 ? stackTrace.Substring(0, nl) : stackTrace).Trim();
            }

            _errors.Add(new SweepError
            {
                timeSeconds = Elapsed,
                type = type.ToString(),
                message = condition + firstStack
            });
        }

        /// <summary>Copies the captured errors + marks into a report for saving.</summary>
        public void FillReport(BenchmarkReport report)
        {
            if (report == null) return;
            report.errors = new List<SweepError>(_errors);
            report.marks = new List<SweepMark>(_marks);
        }
    }
}
