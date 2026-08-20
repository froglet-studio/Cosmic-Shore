using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Machine-local settings for the editor crash detector (<see cref="CrashDetectorMonitor"/>).
    ///
    /// Lives under <c>UserSettings/</c> (gitignored) per the Docs/TOOLING.md authoring contract:
    /// tool config is a real ScriptableObject with tooltips, never constants in the window — and
    /// per-machine state must not ride the branch.
    ///
    /// The window is the only writer; it applies values through the properties and then calls
    /// <see cref="SaveNow"/> once per change.
    /// </summary>
    [FilePath("UserSettings/CrashDetectorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class CrashDetectorSettings : ScriptableSingleton<CrashDetectorSettings>
    {
        [SerializeField, Tooltip("Master switch. While on, a background thread journals every error/exception " +
                                 "and heartbeats a session sentinel so an abnormal editor exit is reported on the next launch.")]
        bool detectorEnabled = true;

        [SerializeField, Range(1, 30), Tooltip("Seconds between sentinel heartbeats. Lower = a crash report can date " +
                                               "the death more precisely; higher = fewer tiny file writes. 5 is plenty.")]
        int heartbeatSeconds = 5;

        [SerializeField, Tooltip("Also journal warnings. Default off — errors, exceptions and asserts only, " +
                                 "which keeps the journal readable and the report minimal.")]
        bool captureWarnings;

        [SerializeField, Range(0, 30), Tooltip("Stack-trace lines kept per captured entry. 0 = message only.")]
        int stackTraceLines = 8;

        [SerializeField, Range(1, 50), Tooltip("Crash reports kept in Logs/CrashDetector/ before the oldest is pruned.")]
        int maxReportsKept = 10;

        [SerializeField, Range(1, 32), Tooltip("Session journal size cap in MB. Past it, further entries are dropped " +
                                               "with one marker — an error storm must not fill the disk.")]
        int maxJournalMB = 4;

        public bool Enabled { get => detectorEnabled; set => detectorEnabled = value; }
        public int HeartbeatSeconds { get => heartbeatSeconds; set => heartbeatSeconds = Mathf.Clamp(value, 1, 30); }
        public bool CaptureWarnings { get => captureWarnings; set => captureWarnings = value; }
        public int StackTraceLines { get => stackTraceLines; set => stackTraceLines = Mathf.Clamp(value, 0, 30); }
        public int MaxReportsKept { get => maxReportsKept; set => maxReportsKept = Mathf.Clamp(value, 1, 50); }
        public int MaxJournalMB { get => maxJournalMB; set => maxJournalMB = Mathf.Clamp(value, 1, 32); }

        public void SaveNow() => Save(true);
    }
}
