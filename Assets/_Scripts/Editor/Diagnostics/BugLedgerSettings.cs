using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Machine-local settings for the shared bug ledger (<see cref="BugLedger"/>).
    ///
    /// Lives under <c>UserSettings/</c> (gitignored) per the Docs/TOOLING.md authoring contract —
    /// these tune how THIS machine captures and validates; the issues themselves are the shared,
    /// committed data under <c>BugLedger/</c> at the project root. Per-issue policy that must be
    /// deterministic for the whole team (e.g. how many clean sessions close an issue) is stamped
    /// INTO the issue file at creation, so a later local change never rewrites history.
    ///
    /// The window is the only writer; it applies values through the properties and then calls
    /// <see cref="SaveNow"/> once per change.
    /// </summary>
    [FilePath("UserSettings/BugLedgerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class BugLedgerSettings : ScriptableSingleton<BugLedgerSettings>
    {
        [SerializeField, Tooltip("File an issue automatically for every distinct error/exception/assert " +
                                 "signature. Also gates auto-validation — a clean session only counts as " +
                                 "evidence while something was listening for the error.")]
        bool autoCaptureEnabled = true;

        [SerializeField, Range(1, 10), Tooltip("Clean sessions a newly-filed issue will require before a " +
                                               "'fixed' mark auto-resolves it. Stamped into each issue at " +
                                               "creation so the team sees one deterministic policy per issue.")]
        int defaultCleanSessionsRequired = 2;

        [SerializeField, Range(0, 300), Tooltip("A play run shorter than this (seconds) is not evidence — " +
                                                "an accidental play press must not credit a clean session.")]
        int minValidationPlaySeconds = 15;

        [SerializeField, Range(0, 120), Tooltip("An editor session shorter than this (minutes) does not " +
                                                "credit edit-mode-scoped issues at quit.")]
        int minEditorSessionMinutes = 10;

        [SerializeField, Range(10, 1000), Tooltip("Hard cap on auto-filed issues. A runaway error generator " +
                                                  "must not mint files; past the cap new signatures are " +
                                                  "dropped with one console warning.")]
        int maxAutoIssues = 150;

        [SerializeField, Tooltip("On resolution, stamp the issue into BugLedger/resolved/ (pruned to a cap) " +
                                 "instead of discarding it outright. Git history keeps everything either way; " +
                                 "the archive makes 'what got fixed lately' browsable without git.")]
        bool keepResolvedArchive = true;

        public bool AutoCaptureEnabled { get => autoCaptureEnabled; set => autoCaptureEnabled = value; }
        public bool KeepResolvedArchive { get => keepResolvedArchive; set => keepResolvedArchive = value; }
        public int DefaultCleanSessionsRequired { get => defaultCleanSessionsRequired; set => defaultCleanSessionsRequired = Mathf.Clamp(value, 1, 10); }
        public int MinValidationPlaySeconds { get => minValidationPlaySeconds; set => minValidationPlaySeconds = Mathf.Clamp(value, 0, 300); }
        public int MinEditorSessionMinutes { get => minEditorSessionMinutes; set => minEditorSessionMinutes = Mathf.Clamp(value, 0, 120); }
        public int MaxAutoIssues { get => maxAutoIssues; set => maxAutoIssues = Mathf.Clamp(value, 10, 1000); }

        public void SaveNow() => Save(true);
    }
}
