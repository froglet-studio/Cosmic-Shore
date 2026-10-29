using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One sampled vessel pose. P0 writes these uncompressed at <b>36 bytes</b> — time (4),
    /// position (12), rotation (16), speed (4).
    ///
    /// <para>The time is stored PER SAMPLE rather than derived from a fixed rate, and that is
    /// deliberate: a vessel that spawns late, despawns early, or is missed for a frame during a
    /// hitch then needs no hole marker and no special case — its track is simply shorter, and
    /// <see cref="TheaterTrack.TryEvaluate"/> reports it as absent outside its own span. Four
    /// bytes buys the whole late-join/early-death class of correctness.</para>
    ///
    /// <para>Speed is recorded although nothing in P0 reads it. It is the drive signal for the
    /// speed-tunnel law (<c>Docs/SPEED_TUNNEL.md</c>), so a later phase can reproduce a pilot's
    /// own field of view from the recording instead of guessing it from finite differences.</para>
    /// </summary>
    [Serializable]
    public struct TheaterPose
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Speed;

        /// <summary>Bytes one pose occupies on disk. Used by the size report and by the tests.</summary>
        public const int SerializedSize = 36;
    }

    /// <summary>
    /// Everything recorded about ONE vessel over a match: who flew it, what it was, and where it
    /// was. A track is pure sampled state with no events in it, which is what makes seeking
    /// anywhere in it — forward or backward — a plain lookup rather than a rebuild.
    /// </summary>
    public class TheaterTrack
    {
        public string PlayerName = string.Empty;

        /// <summary>
        /// <c>VesselClassType</c> as an int, and <c>Domains</c> likewise below.
        ///
        /// <para>Stored as the underlying integer rather than the enum so the FORMAT does not
        /// silently re-interpret an old recording when the enum is edited. A vessel class removed
        /// or renumbered makes an old recording resolve to no prefab and say so, which is the loud
        /// failure a dev artifact should have — where a serialized enum would quietly hand back a
        /// different ship.</para>
        /// </summary>
        public int VesselType;
        public int Domain;
        public bool IsAI;

        public readonly List<TheaterPose> Poses = new();

        public float StartTime => Poses.Count > 0 ? Poses[0].Time : 0f;
        public float EndTime => Poses.Count > 0 ? Poses[Poses.Count - 1].Time : 0f;

        /// <summary>
        /// Where this vessel was at <paramref name="time"/>, interpolated between the two
        /// surrounding samples. Returns false outside the track's own span — a vessel that had not
        /// spawned yet, or was already gone, is ABSENT rather than frozen at an endpoint, so a
        /// puppet can be hidden instead of parked.
        ///
        /// <para>Resolved by binary search and NOT by a cursor, so evaluating backward costs
        /// exactly what evaluating forward costs. That is why P0 can snap to any timestamp with no
        /// rebuild at all: a vessel track is sampled state, and sampled state has no history to
        /// replay. (Prisms, arriving in P1, are events and do need the snapshot machinery.)</para>
        /// </summary>
        public bool TryEvaluate(float time, out Vector3 position, out Quaternion rotation, out float speed)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            speed = 0f;

            int n = Poses.Count;
            if (n == 0) return false;
            if (time < Poses[0].Time || time > Poses[n - 1].Time) return false;

            if (n == 1)
            {
                position = Poses[0].Position;
                rotation = Poses[0].Rotation;
                speed = Poses[0].Speed;
                return true;
            }

            // Largest index whose time is <= the query.
            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (Poses[mid].Time <= time) lo = mid; else hi = mid - 1;
            }

            TheaterPose a = Poses[lo];
            if (lo >= n - 1)
            {
                position = a.Position;
                rotation = a.Rotation;
                speed = a.Speed;
                return true;
            }

            TheaterPose b = Poses[lo + 1];
            float span = b.Time - a.Time;
            float t = span > 1e-6f ? Mathf.Clamp01((time - a.Time) / span) : 0f;

            position = Vector3.LerpUnclamped(a.Position, b.Position, t);
            rotation = Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, t);
            speed = Mathf.LerpUnclamped(a.Speed, b.Speed, t);
            return true;
        }
    }

    /// <summary>
    /// One action a recorded vessel ran (<c>VesselActionTape</c>): an input pressed or released, resolved against a
    /// device, or an action an autopilot started or stopped by name. The match replay re-runs it on the real
    /// vessel at <see cref="Time"/>.
    /// </summary>
    [Serializable]
    public struct TheaterActionEvent
    {
        public const byte KindInput = 0;
        public const byte KindDirect = 1;

        public float Time;
        /// <summary>Index into <see cref="TheaterRecording.Tracks"/>.</summary>
        public int Track;
        public byte Kind;
        /// <summary>Pressed / started (true) or released / stopped (false).</summary>
        public bool Down;
        /// <summary><c>InputEvents</c> as an int, for <see cref="KindInput"/>.</summary>
        public int Input;
        /// <summary>The device the input was resolved against (<c>R_VesselActionHandler</c>'s carried byte).</summary>
        public byte Device;
        /// <summary>The action's asset name, for <see cref="KindDirect"/>.</summary>
        public string Action;
    }

    /// <summary>
    /// A theater recording: the outcome log of one match, replayable without any determinism.
    ///
    /// <para><b>P0 records vessels; P1 adds the prism world</b> (<see cref="World"/>, stream 2).
    /// The format is chunked so adding a stream does not invalidate a recording made earlier: an
    /// unknown stream id is SKIPPED by its own byte length rather than aborting the read, and a
    /// recording without a world stream reads as <see cref="World"/> null.</para>
    ///
    /// <para><b>Deliberately uncompressed.</b> P0 writes raw little-endian floats — 36 B per pose,
    /// so four pilots at 30 Hz for five minutes is about 1.3 MB. Delta-coding the positions and
    /// packing the rotations smallest-three takes that to roughly 10 B and an LZ4 pass takes it
    /// down again, but neither belongs in the phase whose job is to prove the loop works: a
    /// recording you can read in a hex editor is worth more right now than one that is four times
    /// smaller.</para>
    ///
    /// <para><b>Versioning is loud.</b> A recording whose magic or version does not match is
    /// refused with a reason rather than partially read. These are development artifacts; a hard
    /// break is acceptable as long as nobody has to wonder whether they got half a match.</para>
    /// </summary>
    public class TheaterRecording
    {
        /// <summary>"CSTH" — Cosmic Shore THeater.</summary>
        public const uint Magic = 0x48545343;

        /// <summary>Bumped whenever the layout below changes in a way an older reader cannot survive.</summary>
        public const ushort Version = 1;

        /// <summary>File extension, without the dot.</summary>
        public const string Extension = "cstheater";

        /// <summary>Stream ids. A reader skips any id it does not know, by the length that precedes it.</summary>
        public const ushort StreamVessels = 1;

        /// <summary>The prism world (<see cref="TheaterWorld"/>, Theater P1). Written only when it holds events.</summary>
        public const ushort StreamWorld = 2;

        /// <summary>Every vessel's actions (<see cref="Actions"/>). Written only when there are any.</summary>
        public const ushort StreamActions = 3;

        public string SceneName = string.Empty;
        public string CellConfigName = string.Empty;
        public int GameMode;
        public float SampleHz = 30f;
        public long UtcTicks;
        public readonly List<TheaterTrack> Tracks = new();

        /// <summary>The prisms the match was flown among, or null when the world was not recorded.</summary>
        public TheaterWorld World;

        /// <summary>The actions the vessels ran, in time order. Empty for a recording made before 2026-10-27.</summary>
        public readonly List<TheaterActionEvent> Actions = new();

        /// <summary>True when there is a recorded world to play.</summary>
        public bool HasWorld => World != null && World.Events.Count > 0;

        /// <summary>Seconds from the first sample of any track to the last. Zero for an empty recording.</summary>
        public float Duration
        {
            get
            {
                float start = float.MaxValue, end = float.MinValue;
                for (int i = 0; i < Tracks.Count; i++)
                {
                    if (Tracks[i].Poses.Count == 0) continue;
                    start = Mathf.Min(start, Tracks[i].StartTime);
                    end = Mathf.Max(end, Tracks[i].EndTime);
                }
                return end > start ? end - start : 0f;
            }
        }

        /// <summary>The timestamp playback starts from — the earliest sample in the recording.</summary>
        public float StartTime
        {
            get
            {
                float start = float.MaxValue;
                for (int i = 0; i < Tracks.Count; i++)
                    if (Tracks[i].Poses.Count > 0) start = Mathf.Min(start, Tracks[i].StartTime);
                return start == float.MaxValue ? 0f : start;
            }
        }

        public int TotalPoseCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Tracks.Count; i++) n += Tracks[i].Poses.Count;
                return n;
            }
        }

        public void Write(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(SceneName ?? string.Empty);
            writer.Write(CellConfigName ?? string.Empty);
            writer.Write(GameMode);
            writer.Write(SampleHz);
            writer.Write(UtcTicks);

            // One chunk per stream. Each is (id, byteLength, payload) so a reader from a future
            // phase — or a reader OLDER than the recording — can step over what it does not know.
            writer.Write((ushort)(1 + (HasWorld ? 1 : 0) + (Actions.Count > 0 ? 1 : 0))); // chunk count

            writer.Write(StreamVessels);
            using (var payload = new MemoryStream())
            {
                WriteVesselStream(payload);
                var bytes = payload.ToArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }

            if (HasWorld)
            {
                writer.Write(StreamWorld);
                using var payload = new MemoryStream();
                World.Write(payload);
                var bytes = payload.ToArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }

            if (Actions.Count > 0)
            {
                writer.Write(StreamActions);
                using var payload = new MemoryStream();
                WriteActionStream(payload);
                var bytes = payload.ToArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }

        void WriteActionStream(Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Actions.Count);
            for (int i = 0; i < Actions.Count; i++)
            {
                var a = Actions[i];
                writer.Write(a.Time);
                writer.Write(a.Track);
                writer.Write(a.Kind);
                writer.Write(a.Down);
                if (a.Kind == TheaterActionEvent.KindInput)
                {
                    writer.Write(a.Input);
                    writer.Write(a.Device);
                }
                else writer.Write(a.Action ?? string.Empty);
            }
        }

        static void ReadActionStream(Stream stream, TheaterRecording into)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            int count = reader.ReadInt32();
            if (count < 0) throw new IOException("corrupt action stream");
            if (count > 0) into.Actions.Capacity = count;
            for (int i = 0; i < count; i++)
            {
                var a = new TheaterActionEvent
                {
                    Time = reader.ReadSingle(),
                    Track = reader.ReadInt32(),
                    Kind = reader.ReadByte(),
                    Down = reader.ReadBoolean(),
                };
                if (a.Kind == TheaterActionEvent.KindInput)
                {
                    a.Input = reader.ReadInt32();
                    a.Device = reader.ReadByte();
                }
                else a.Action = reader.ReadString();
                into.Actions.Add(a);
            }
        }

        void WriteVesselStream(Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Tracks.Count);
            for (int i = 0; i < Tracks.Count; i++)
            {
                var track = Tracks[i];
                writer.Write(track.PlayerName ?? string.Empty);
                writer.Write(track.VesselType);
                writer.Write(track.Domain);
                writer.Write(track.IsAI);
                writer.Write(track.Poses.Count);
                for (int p = 0; p < track.Poses.Count; p++)
                {
                    TheaterPose pose = track.Poses[p];
                    writer.Write(pose.Time);
                    writer.Write(pose.Position.x);
                    writer.Write(pose.Position.y);
                    writer.Write(pose.Position.z);
                    writer.Write(pose.Rotation.x);
                    writer.Write(pose.Rotation.y);
                    writer.Write(pose.Rotation.z);
                    writer.Write(pose.Rotation.w);
                    writer.Write(pose.Speed);
                }
            }
        }

        /// <summary>
        /// Read a recording. Never throws on a malformed file — it reports why, because the caller
        /// is a dev tool that should say "this recording predates the format change" rather than
        /// spill a stack trace.
        /// </summary>
        public static bool TryRead(Stream stream, out TheaterRecording recording, out string error)
        {
            recording = null;
            error = null;

            if (stream == null) { error = "no stream"; return false; }

            try
            {
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

                uint magic = reader.ReadUInt32();
                if (magic != Magic) { error = "not a theater recording (bad magic)"; return false; }

                ushort version = reader.ReadUInt16();
                if (version != Version)
                {
                    error = $"recording is format v{version}, this build reads v{Version}";
                    return false;
                }

                var result = new TheaterRecording
                {
                    SceneName = reader.ReadString(),
                    CellConfigName = reader.ReadString(),
                    GameMode = reader.ReadInt32(),
                    SampleHz = reader.ReadSingle(),
                    UtcTicks = reader.ReadInt64()
                };

                ushort chunks = reader.ReadUInt16();
                for (int c = 0; c < chunks; c++)
                {
                    ushort id = reader.ReadUInt16();
                    int length = reader.ReadInt32();
                    if (length < 0) { error = "corrupt chunk length"; return false; }

                    byte[] payload = reader.ReadBytes(length);
                    if (payload.Length != length) { error = "truncated recording"; return false; }

                    using var payloadStream = new MemoryStream(payload, writable: false);
                    if (id == StreamVessels) ReadVesselStream(payloadStream, result);
                    else if (id == StreamWorld) result.World = TheaterWorld.Read(payloadStream);
                    else if (id == StreamActions) ReadActionStream(payloadStream, result);
                    // Any other id is a stream this build does not know: already stepped over.
                }

                recording = result;
                return true;
            }
            catch (EndOfStreamException) { error = "truncated recording"; return false; }
            catch (IOException e) { error = e.Message; return false; }
            catch (ArgumentException e) { error = e.Message; return false; }
        }

        static void ReadVesselStream(Stream stream, TheaterRecording into)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            int trackCount = reader.ReadInt32();
            for (int i = 0; i < trackCount; i++)
            {
                var track = new TheaterTrack
                {
                    PlayerName = reader.ReadString(),
                    VesselType = reader.ReadInt32(),
                    Domain = reader.ReadInt32(),
                    IsAI = reader.ReadBoolean()
                };

                int poseCount = reader.ReadInt32();
                if (poseCount > 0) track.Poses.Capacity = poseCount;
                for (int p = 0; p < poseCount; p++)
                {
                    var pose = new TheaterPose { Time = reader.ReadSingle() };
                    float px = reader.ReadSingle(), py = reader.ReadSingle(), pz = reader.ReadSingle();
                    pose.Position = new Vector3(px, py, pz);
                    float rx = reader.ReadSingle(), ry = reader.ReadSingle(),
                          rz = reader.ReadSingle(), rw = reader.ReadSingle();
                    pose.Rotation = new Quaternion(rx, ry, rz, rw);
                    pose.Speed = reader.ReadSingle();
                    track.Poses.Add(pose);
                }

                into.Tracks.Add(track);
            }
        }
    }
}
