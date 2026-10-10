using System;
using System.IO;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One recorded frame of the parity replay (<c>Port/parity/README.md</c>, replay version 1).
    /// The flight floats the vessel reads from <see cref="CosmicShore.Gameplay.IInputStatus"/>
    /// plus the <see cref="CosmicShore.Data.InputEvents"/> names the strategy raised that frame.
    /// A plain [Serializable] class so <see cref="JsonUtility"/> reads and writes it in both
    /// engines; arrays, never null on a freshly built frame.
    /// </summary>
    [Serializable]
    public sealed class ReplayStatusFrame
    {
        [Tooltip("Frame index the snapshot was taken on (informational; playback is index-based).")]
        public int f;

        [Tooltip("IInputStatus.XSum after the strategy's ProcessInput.")]
        public float XSum;

        [Tooltip("IInputStatus.YSum after the strategy's ProcessInput.")]
        public float YSum;

        [Tooltip("IInputStatus.XDiff (dual-stick speed term) after the strategy's ProcessInput.")]
        public float XDiff;

        [Tooltip("IInputStatus.YDiff (dual-stick roll term) after the strategy's ProcessInput.")]
        public float YDiff;

        [Tooltip("IInputStatus.Throttle after the strategy's ProcessInput.")]
        public float Throttle;

        [Tooltip("IInputStatus.LeftTriggerAnalog after the strategy's ProcessInput.")]
        public float LeftTriggerAnalog;

        [Tooltip("IInputStatus.RightTriggerAnalog after the strategy's ProcessInput.")]
        public float RightTriggerAnalog;

        [Tooltip("InputEvents raised through OnButtonPressed during this frame, by enum name.")]
        public string[] pressed = Array.Empty<string>();

        [Tooltip("InputEvents raised through OnButtonReleased during this frame, by enum name.")]
        public string[] released = Array.Empty<string>();

        public string[] Pressed => pressed ?? Array.Empty<string>();
        public string[] Released => released ?? Array.Empty<string>();
    }

    /// <summary>
    /// The replay file both engines play (<c>Port/parity/README.md</c> § Formats, version 1):
    /// seed, start scene, frame count, checkpoint interval, the optional frame-record spec, the
    /// engine's device-level <c>do</c> steps and the game's own per-frame <c>status</c> stream.
    ///
    /// <para>Serialized with <see cref="JsonUtility"/>, which is why this is a class with public
    /// fields, why every collection is an array (a top-level array or a nullable field would not
    /// round-trip) and why the C# keyword field is written <c>@do</c> (its JSON name is still
    /// <c>do</c>).</para>
    /// </summary>
    [Serializable]
    public sealed class ReplayFile
    {
        public const int CurrentVersion = 1;

        [Tooltip("Format version; 1 is the only one the game reads.")]
        public int version = CurrentVersion;

        [Tooltip("Scene the run starts in (Bootstrap for a parity case).")]
        public string scene = "Bootstrap";

        [Tooltip("Random.InitState seed applied before the first scene loads.")]
        public int seed;

        [Tooltip("Fixed 1/60 s ticks the run lasts.")]
        public int frames;

        [Tooltip("State checkpoint interval in frames.")]
        public int checkpointEvery = 30;

        [Tooltip("Frame-record spec FROM-TO:EVERY (frames/*.png, needs a window); empty for none.")]
        public string record = "";

        [Tooltip("Device-level steps in the engine's InputScript verbs, FRAME:verb args.")]
        public string[] @do = Array.Empty<string>();

        [Tooltip("Per-frame IInputStatus snapshots the game's ReplayPlayer plays.")]
        public ReplayStatusFrame[] status = Array.Empty<ReplayStatusFrame>();

        public string[] Do => @do ?? Array.Empty<string>();
        public ReplayStatusFrame[] Status => status ?? Array.Empty<ReplayStatusFrame>();
        public bool HasRecordSpec => !string.IsNullOrEmpty(record);

        public string ToJson(bool prettyPrint = false) => JsonUtility.ToJson(this, prettyPrint);

        /// <summary>Parses a version-1 replay; throws on a different version so a stale file is loud.</summary>
        public static ReplayFile FromJson(string json)
        {
            var file = JsonUtility.FromJson<ReplayFile>(json);
            if (file == null) throw new InvalidDataException("replay JSON did not parse");
            if (file.version != CurrentVersion)
                throw new InvalidDataException($"replay version {file.version} is not {CurrentVersion}");
            file.@do ??= Array.Empty<string>();
            file.status ??= Array.Empty<ReplayStatusFrame>();
            file.record ??= "";
            return file;
        }

        public static ReplayFile Load(string path) => FromJson(File.ReadAllText(path));

        public void Save(string path, bool prettyPrint = true)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(prettyPrint) + "\n");
        }
    }
}
