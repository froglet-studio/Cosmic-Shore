using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>One pilot in a replayed match, as the roster stood when the turn started.</summary>
    [Serializable]
    public class ReplayPilot
    {
        public string Name = string.Empty;

        /// <summary>
        /// <c>Domains</c> and <c>VesselClassType</c> as their underlying integers, for the reason
        /// <see cref="TheaterTrack.VesselType"/> gives: an edited enum must not quietly re-read an old
        /// replay as a different ship. -1 when the hull could not be read.
        /// </summary>
        public int Domain;
        public int VesselType = -1;

        public bool IsAI;

        /// <summary>The pilot this device's player flew - what the browser highlights.</summary>
        public bool IsLocal;
    }

    /// <summary>One race ring of the match's course, relative to <see cref="ReplaySummary.ArenaOrigin"/>.</summary>
    [Serializable]
    public class ReplayGate
    {
        public int Index;
        public Vector3 Position;
        public Vector3 Axis;
        public float Radius;
    }

    /// <summary>One row of the match's final results, copied from <c>GameDataSO.Results</c>.</summary>
    [Serializable]
    public class ReplayResult
    {
        public int Rank;
        public string Name = string.Empty;
        public int Domain;
        public float Score;

        /// <summary>The score as the scoreboard printed it (a time, a count), so the browser shows the same words.</summary>
        public string ScoreText = string.Empty;
    }

    /// <summary>
    /// The small half of a replay: everything the replay browser and the Cloud Save index need,
    /// without opening the recording. Written beside the Theater recording as
    /// <c>&lt;name&gt;.replay.json</c>.
    ///
    /// <para><b>Two files, on purpose (decided 2026-10-15).</b> The frames stay in the Theater's
    /// own compact binary format, untouched; this sidecar is readable JSON. A browser listing twenty
    /// replays reads twenty small files instead of twenty recordings, and the same object is what
    /// the Cloud Save index stores, so a match recorded on one device can be listed on another.</para>
    ///
    /// <para>Public fields rather than properties, and no get-only members: <c>JsonUtility</c> writes
    /// the sidecar and Cloud Save's serializer writes the index, and both should see exactly these
    /// fields and nothing derived.</para>
    /// </summary>
    [Serializable]
    public class ReplaySummary
    {
        /// <summary>Bumped when a field changes meaning. A reader refuses a schema it does not know.</summary>
        public const string SchemaId = "cosmic-shore.replay-summary/1";

        /// <summary>The sidecar's suffix, appended to the recording's name without its extension.</summary>
        public const string SidecarExtension = ".replay.json";

        public string Schema = SchemaId;

        /// <summary>The recording's file name (no folder): <c>&lt;Mode&gt;_&lt;date&gt;_&lt;time&gt;.cstheater</c>.</summary>
        public string RecordingFile = string.Empty;

        /// <summary><c>GameModes</c> as its integer, plus its name for people and tools that read the JSON.</summary>
        public int GameMode;
        public string GameModeName = string.Empty;

        public string SceneName = string.Empty;
        public int Intensity;

        /// <summary>
        /// The world position of the match scene's cell - the point every recorded pose is around.
        /// Watching stands the arena somewhere else and shifts the poses by the difference
        /// (<see cref="ReplayStagePlacement"/>). A summary written before this field reads zero, which
        /// is where every arcade scene's cell sits.
        ///
        /// <para>Never sent to Cloud Save (<c>JsonIgnore</c>): Newtonsoft cannot write a <c>Vector3</c> (its
        /// <c>normalized</c> refers back to a Vector3), and the index on other devices does not need it. The local
        /// sidecar is written by <c>JsonUtility</c>, which keeps it.</para>
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Vector3 ArenaOrigin;

        /// <summary>When the recording started, as UTC ticks (the Theater recording's own stamp).</summary>
        public long RecordedAtUtcTicks;

        public float DurationSeconds;

        /// <summary>How the recording ended: <c>finished</c>, <c>left the match</c>, <c>app closed</c>, ...</summary>
        public string EndedBecause = string.Empty;

        /// <summary>
        /// False when the match ended without an end screen (the player left, the results never
        /// arrived). The replay is still saved and watchable; it just has no scoreboard.
        /// </summary>
        public bool HasResults;
        public string WinnerName = string.Empty;
        public int WinnerDomain;

        public List<ReplayPilot> Pilots = new();
        public List<ReplayResult> Results = new();

        /// <summary>
        /// The race rings the match was flown through (any gate race, Skim Race included), captured at Go
        /// so the replay rebuilds exactly them. Empty for a mode with no rings, and for a summary written
        /// before 2026-10-26.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public List<ReplayGate> Gates = new();

        /// <summary>
        /// The seed the match's course was built from (Skim Race's track, a gate race's rings), so watching the
        /// replay in its real scene builds the same course (<c>CosmicShore.Gameplay.CourseSeedOverride</c>).
        /// 0 for a mode with no seeded course, and for a summary written before 2026-10-27.
        /// </summary>
        public int CourseSeed;

        /// <summary>
        /// Seconds as <c>m:ss</c> - a number format, not a sentence: the browser and the playback HUD
        /// put it inside authored templates. Negative and fractional seconds floor to whole ones.
        /// </summary>
        public static string Clock(float seconds)
        {
            int whole = seconds > 0f ? (int)Math.Floor(seconds) : 0;
            return $"{whole / 60}:{whole % 60:00}";
        }

        /// <summary>When the recording started, in UTC.</summary>
        public static DateTime RecordedAt(ReplaySummary summary) =>
            new(Math.Max(0L, summary.RecordedAtUtcTicks), DateTimeKind.Utc);

        /// <summary>
        /// True when two summaries describe the same replay - the same recording file started at the
        /// same instant. The file name alone repeats across devices (it is mode plus minute).
        /// </summary>
        public static bool SameReplay(ReplaySummary a, ReplaySummary b) =>
            a != null && b != null
            && a.RecordedAtUtcTicks == b.RecordedAtUtcTicks
            && string.Equals(a.RecordingFile, b.RecordingFile, StringComparison.Ordinal);

        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true);

        /// <summary>True for a plain file name: no folder part, no parent step, not rooted.</summary>
        public static bool IsBareFileName(string name) =>
            !string.IsNullOrWhiteSpace(name)
            && name != "." && name != ".."
            && name.IndexOf('/') < 0 && name.IndexOf('\\') < 0
            && name.IndexOf(':') < 0
            && System.IO.Path.GetFileName(name) == name;

        /// <summary>
        /// Read a sidecar. Never throws: a corrupt or foreign file is reported with a reason, so the
        /// browser can skip one bad entry instead of failing to list the rest.
        /// </summary>
        public static bool TryParse(string json, out ReplaySummary summary, out string error)
        {
            summary = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json)) { error = "empty summary"; return false; }

            ReplaySummary parsed;
            try
            {
                parsed = JsonUtility.FromJson<ReplaySummary>(json);
            }
            catch (Exception e)
            {
                // JsonUtility reports malformed text as an ArgumentException; anything else it
                // throws on a foreign file is still "not a summary", never a crash in the browser.
                error = $"not a replay summary ({e.GetType().Name}: {e.Message})";
                return false;
            }

            if (parsed == null) { error = "not a replay summary"; return false; }
            if (parsed.Schema != SchemaId)
            {
                error = $"summary is '{parsed.Schema}', this build reads '{SchemaId}'";
                return false;
            }
            if (string.IsNullOrEmpty(parsed.RecordingFile)) { error = "summary names no recording"; return false; }

            // The name is joined to the replay folder to load and to DELETE, so it must be a bare
            // file name: a "../" or a rooted path would reach outside the folder.
            if (!IsBareFileName(parsed.RecordingFile))
            {
                error = $"summary names a recording outside its folder ('{parsed.RecordingFile}')";
                return false;
            }

            parsed.Pilots ??= new List<ReplayPilot>();
            parsed.Results ??= new List<ReplayResult>();
            summary = parsed;
            return true;
        }
    }
}
