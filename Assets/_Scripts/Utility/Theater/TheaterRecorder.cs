using CosmicShore.Data;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Samples every live vessel into a <see cref="TheaterRecording"/> while a match runs.
    ///
    /// <para><b>The roster comes from the vessel vision band, not from DI.</b>
    /// <see cref="VesselVisionShading.CollectStampedVessels"/> is maintained because a PLATFORM LAW
    /// depends on it being right, which is a far stronger guarantee than a list this recorder would
    /// keep for itself — and it is the same argument the screenshot director already makes for
    /// reading it. It also means the recorder needs no <c>[Inject]</c>, no scene wiring and no
    /// <c>GameDataSO</c> reference, so it can be stood up by a zero-wire singleton.</para>
    ///
    /// <para><b>The timebase is <c>Time.time</c>, deliberately.</b> That is exactly what
    /// <c>PrismClock.Now</c> reads, so when P1 starts recording prism births and deaths the two
    /// streams already share one clock and no conversion has to be invented later. It also means a
    /// recording follows the game's own scaled time, so a slow-motion celebration is recorded as
    /// the slow-motion the players actually saw.</para>
    ///
    /// <para>Costs one <c>List</c> walk per SAMPLE — not per frame — over at most eight vessels,
    /// and appends to lists that grow amortised. Nothing here allocates per frame.</para>
    /// </summary>
    public class TheaterRecorder : IVesselActionObserver
    {
        readonly TheaterRecording _recording = new();
        readonly Dictionary<Transform, TheaterTrack> _tracks = new();
        readonly List<Transform> _scratch = new();

        float _interval = 1f / 30f;
        float _accumulator;
        float _recordedSeconds;
        float _limitSeconds = float.MaxValue;
        TheaterWorldRecorder _world;
        TheaterActorRecorder _actors;
        bool _watchingActions;
        readonly Dictionary<TheaterTrack, int> _trackIndex = new();

        public bool IsRecording { get; private set; }

        /// <summary>Seconds of match recorded so far — what the overlay reads.</summary>
        public float RecordedSeconds => _recordedSeconds;

        /// <summary>Total poses captured so far, across every track.</summary>
        public int PoseCount => _recording.TotalPoseCount;

        /// <summary>Distinct vessels seen so far.</summary>
        public int TrackCount => _recording.Tracks.Count;

        /// <summary>Bytes the current recording would occupy on disk, near enough for an overlay.</summary>
        public long EstimatedBytes => 64L + PoseCount * (long)TheaterPose.SerializedSize;

        public TheaterRecording Recording => _recording;

        public void Begin(TheaterConfigSO config, string sceneName, int gameMode, string cellConfigName)
        {
            float hz = config != null ? Mathf.Max(1f, config.vesselSampleHz) : 30f;
            _interval = 1f / hz;
            _limitSeconds = config != null ? Mathf.Max(1f, config.maxRecordMinutes) * 60f : 1800f;

            _world?.Detach();
            _world = null;
            _recording.World = null;
            _actors?.Detach();
            _actors = null;
            _recording.Actors = null;
            StopWatchingActions();
            _recording.Tracks.Clear();
            _recording.Actions.Clear();
            _tracks.Clear();
            _trackIndex.Clear();
            _recording.SampleHz = hz;
            _recording.SceneName = sceneName ?? string.Empty;
            _recording.CellConfigName = cellConfigName ?? string.Empty;
            _recording.GameMode = gameMode;
            _recording.UtcTicks = System.DateTime.UtcNow.Ticks;

            _accumulator = 0f;
            _recordedSeconds = 0f;
            IsRecording = true;

            // Take the opening sample immediately: a recording that starts on its first INTERVAL
            // misses the launch, which is the one moment a director always wants.
            SampleNow();
        }

        /// <summary>Called once per frame by the director while recording.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsRecording) return;

            _recordedSeconds += deltaTime;
            if (_recordedSeconds >= _limitSeconds)
            {
                // The cap STOPS the recording; it never discards it. A session left running
                // overnight should cost a bounded file, not a lost afternoon.
                Stop();
                CSDebug.LogWarning($"[Theater] Recording hit its {_limitSeconds / 60f:0} minute cap and stopped. " +
                                   $"{PoseCount} poses kept.");
                return;
            }

            _accumulator += deltaTime;
            if (_accumulator < _interval) return;

            // Subtract rather than zero, so a frame-rate hitch does not slew the sample rate.
            // Clamped to one interval of debt: catching up after a two-second stall by taking
            // sixty samples at one position records a stutter that did not happen.
            _accumulator -= _interval;
            if (_accumulator > _interval) _accumulator = _interval;

            SampleNow();
        }

        void SampleNow()
        {
            VesselVisionShading.CollectStampedVessels(_scratch);
            float now = Time.time;

            for (int i = 0; i < _scratch.Count; i++)
            {
                var vessel = _scratch[i];
                if (vessel == null) continue;
                if (!vessel.TryGetComponent(out IVesselStatus status)) continue;

                var track = TrackFor(vessel, status);
                track.Poses.Add(new TheaterPose
                {
                    Time = now,
                    Position = vessel.position,
                    Rotation = vessel.rotation,
                    Speed = status.Speed
                });
            }

            _actors?.Sample(now);
        }

        /// <summary>
        /// Record the crystals, plants, creatures and cytoplasm shards beside the vessels, on the same samples
        /// (<c>Docs/REPLAYS.md</c> §6.4). Call after <see cref="Begin"/>; false when not recording.
        /// </summary>
        public bool RecordActors(TheaterActorRecorder actors)
        {
            if (!IsRecording || actors == null) return false;
            _actors?.Detach();
            _actors = actors;
            _recording.Actors = actors.Actors;
            actors.Attach();
            actors.Sample(Time.time);
            return true;
        }

        /// <summary>
        /// Record the prism world beside the vessels (Theater P1), through <paramref name="world"/>,
        /// until <see cref="Stop"/>. Call right after <see cref="Begin"/> so both streams start
        /// together. False, and no world stream, when the scene has no prism index or another
        /// recorder already watches it.
        /// </summary>
        public bool RecordWorld(TheaterWorldRecorder world)
        {
            if (!IsRecording || world == null || !world.Attach()) return false;
            _world = world;
            _recording.World = world.World;
            return true;
        }

        TheaterTrack TrackFor(Transform vessel, IVesselStatus status)
        {
            if (_tracks.TryGetValue(vessel, out var track)) return track;
            track = new TheaterTrack
            {
                PlayerName = string.IsNullOrEmpty(status.PlayerName) ? vessel.name : status.PlayerName,
                VesselType = (int)status.VesselType,
                Domain = (int)status.Domain,
                IsAI = status.Player != null && status.Player.IsInitializedAsAI
            };
            _tracks[vessel] = track;
            _trackIndex[track] = _recording.Tracks.Count;
            _recording.Tracks.Add(track);
            return track;
        }

        /// <summary>
        /// Record every vessel's actions beside its poses (<see cref="VesselActionTape"/>), until <see cref="Stop"/>,
        /// so the match replay re-runs them on the real vessels. False when another recorder already watches.
        /// </summary>
        public bool RecordActions()
        {
            if (!IsRecording) return false;
            if (_watchingActions) return true;
            if (VesselActionTape.Observer != null) return false;
            VesselActionTape.Observer = this;
            _watchingActions = true;
            return true;
        }

        void StopWatchingActions()
        {
            if (_watchingActions && VesselActionTape.Observer == this) VesselActionTape.Observer = null;
            _watchingActions = false;
        }

        void IVesselActionObserver.OnInputAction(IVesselStatus vessel, InputEvents input, byte device, bool down) =>
            AddAction(vessel, new TheaterActionEvent
            {
                Kind = TheaterActionEvent.KindInput, Down = down, Input = (int)input, Device = device
            });

        void IVesselActionObserver.OnDirectAction(IVesselStatus vessel, ShipActionSO action, bool start) =>
            AddAction(vessel, new TheaterActionEvent
            {
                Kind = TheaterActionEvent.KindDirect, Down = start, Action = R_VesselActionHandler.ReplayActionName(action)
            });

        void AddAction(IVesselStatus vessel, TheaterActionEvent e)
        {
            if (!IsRecording || vessel == null) return;
            var root = vessel.Transform;
            if (!root) return;
            e.Time = Time.time;
            e.Track = _trackIndex[TrackFor(root, vessel)];
            _recording.Actions.Add(e);
        }

        public void Stop()
        {
            IsRecording = false;
            _world?.Detach();
            _world = null;
            _actors?.Detach();
            _actors = null;
            StopWatchingActions();
        }

        /// <summary>
        /// Write the recording and return the path, or null with a reason. Never throws: a dev tool
        /// that loses a recording should say where it tried to put it.
        /// </summary>
        public string TryWrite(TheaterConfigSO config, out string error)
        {
            error = null;
            if (_recording.Tracks.Count == 0) { error = "nothing was recorded"; return null; }

            string folder = config != null ? config.ResolveOutputFolder()
                                           : Path.Combine(Application.persistentDataPath, "Recordings");
            try
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, BuildFileName());
                using (var stream = File.Create(path)) _recording.Write(stream);
                return path;
            }
            catch (IOException e) { error = $"{e.Message} (folder: {folder})"; return null; }
            catch (System.UnauthorizedAccessException e) { error = $"{e.Message} (folder: {folder})"; return null; }
        }

        /// <summary>
        /// <c>{scene}_{yyyy-MM-dd}_{HH-mm}.cstheater</c> — the same naming rule the screenshot
        /// director settled on: the subject leads (that is what you triage a folder by), the
        /// timestamp trails and still sorts, and SECONDS are left out because a filename is read by
        /// a person. Uniqueness moves to the folder rather than into the format.
        /// </summary>
        string BuildFileName()
        {
            string scene = string.IsNullOrWhiteSpace(_recording.SceneName) ? "Match" : _recording.SceneName;
            foreach (char c in Path.GetInvalidFileNameChars()) scene = scene.Replace(c, '_');

            var stamp = new System.DateTime(_recording.UtcTicks, System.DateTimeKind.Utc).ToLocalTime();
            string date = stamp.ToString("yyyy-MM-dd_HH-mm", System.Globalization.CultureInfo.InvariantCulture);
            return $"{scene}_{date}.{TheaterRecording.Extension}";
        }
    }
}
