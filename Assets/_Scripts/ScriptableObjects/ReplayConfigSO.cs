using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The tuning surface for PLAYER-FACING match replays: which modes record themselves, how many
    /// replays a device keeps, and how many summaries the Cloud Save index mirrors.
    ///
    /// <para>The recording itself (sample rate, length cap, ghosts, shots) belongs to the Theater
    /// and stays on <see cref="TheaterConfigSO"/>. This asset only decides WHEN a match is recorded
    /// without anyone pressing a key, and what happens to the file afterwards.</para>
    ///
    /// <para>Zero-wire, the same way <see cref="TheaterConfigSO"/> is: with no asset at
    /// <c>Resources/ReplayConfig</c> the replay service runs on these field defaults, so the
    /// feature works in a fresh clone with nothing authored. Create the asset (right-click ▸
    /// Create ▸ ScriptableObjects ▸ Utility ▸ Replay Config) to record another mode or keep more
    /// replays.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ReplayConfig",
        menuName = "ScriptableObjects/Utility/Replay Config", order = 42)]
    public class ReplayConfigSO : ScriptableObject
    {
        /// <summary>Where <see cref="Resolve"/> looks before falling back to code defaults.</summary>
        public const string ResourcePath = "ReplayConfig";

        [Header("Auto record")]
        [Tooltip("Modes whose matches record themselves from Go to the end of the turn, on every peer " +
                 "that plays them. Regatta was chosen first (2026-10-15); add a mode here to record it " +
                 "too. Nothing else about a mode needs to change.")]
        [SerializeField] List<GameModes> autoRecordModes = new() { GameModes.Regatta };

        [Tooltip("Seconds to wait, after the turn ends, for the end screen - the moment the final " +
                 "results exist. A replay whose end screen never comes is saved without results once " +
                 "this runs out, rather than lost. Real time: a paused game does not stretch it.")]
        [SerializeField, Range(1f, 120f)] float resultsWaitSeconds = 20f;

        [Header("Storage")]
        [Tooltip("Replays kept on this device. Saving one more deletes the oldest. Only replays are " +
                 "ever pruned: a Theater recording made by hand has no summary beside it and is " +
                 "never touched.")]
        [SerializeField, Range(1, 200)] int keepNewest = 20;

        [Tooltip("Replay SUMMARIES mirrored to Cloud Save, so the browser can list a match recorded on " +
                 "another device. Summaries only - never frames. Zero turns the mirror off.")]
        [SerializeField, Range(0, 200)] int cloudIndexSize = 50;

        [Header("Watching")]
        [Tooltip("Seconds the skip controls (shoulders, arrows, the HUD's skip buttons) jump back or forward.")]
        [SerializeField, Range(1f, 60f)] float nudgeSeconds = 5f;

        [Tooltip("Each Faster press multiplies the playback rate by this; each Slower press divides by it.")]
        [SerializeField, Range(1.1f, 4f)] float speedStep = 2f;

        [Tooltip("The slowest playback rate (1 is real time).")]
        [SerializeField, Range(0.05f, 1f)] float minSpeed = 0.25f;

        [Tooltip("The fastest playback rate (1 is real time).")]
        [SerializeField, Range(1f, 16f)] float maxSpeed = 4f;

        public float ResultsWaitSeconds => resultsWaitSeconds;
        public float NudgeSeconds => nudgeSeconds;
        public float SpeedStep => speedStep;
        public float MinSpeed => minSpeed;
        public float MaxSpeed => maxSpeed;
        public int KeepNewest => keepNewest;
        public int CloudIndexSize => cloudIndexSize;

        /// <summary>Whether a match of <paramref name="mode"/> records itself.</summary>
        public bool ShouldAutoRecord(GameModes mode) =>
            autoRecordModes != null && autoRecordModes.Contains(mode);

        /// <summary>
        /// The config in use: the authored asset if there is one, otherwise a code-default instance
        /// minted once. Never returns null, so no caller needs a null branch.
        /// </summary>
        public static ReplayConfigSO Resolve()
        {
            var asset = Resources.Load<ReplayConfigSO>(ResourcePath);
            if (asset != null) return asset;

            if (_fallback == null)
            {
                _fallback = CreateInstance<ReplayConfigSO>();
                _fallback.name = "ReplayConfig (defaults)";
            }
            return _fallback;
        }

        static ReplayConfigSO _fallback;
    }
}
