using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>What a recorded actor was. Stored as a byte, so a kind added later reads as unknown, not as another kind.</summary>
    public enum TheaterActorKind : byte
    {
        /// <summary>A cell crystal a manager spawned, keyed by its crystal id (<c>CellItem.Id</c>).</summary>
        Crystal = 0,
        /// <summary>A plant (<c>Flora</c>), keyed by its species name and matched to a live plant by nearness.</summary>
        Flora = 1,
        /// <summary>A creature (<c>Fauna</c>), keyed by its species name and matched to a live creature by nearness.</summary>
        Fauna = 2,
    }

    /// <summary>
    /// One recorded pose of an actor (a crystal, plant or creature): <b>45 bytes</b> - time (4), position (12),
    /// rotation (16), local scale (12) and flags (1). <see cref="FlagCut"/> marks a pose the actor JUMPED to (a crystal
    /// respawning somewhere else), so playback snaps to it instead of gliding there.
    /// </summary>
    [Serializable]
    public struct TheaterActorPose
    {
        public const byte FlagCut = 1;
        public const int SerializedSize = 45;

        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public byte Flags;

        public bool IsCut => (Flags & FlagCut) != 0;
    }

    /// <summary>
    /// How a recorder thins an actor's poses. A pose is kept only when the actor moved, turned or resized past these,
    /// so a crystal that sits still for a minute costs two poses, not 1,800.
    /// </summary>
    public struct TheaterActorThinning
    {
        public float MinDistance;
        public float MinAngle;
        public float MinScale;
        /// <summary>A move this far between two looks is a jump (<see cref="TheaterActorPose.FlagCut"/>), not a glide.</summary>
        public float JumpDistance;

        public static TheaterActorThinning Default => new()
        {
            MinDistance = 0.01f, MinAngle = 0.25f, MinScale = 0.001f, JumpDistance = 8f
        };
    }

    /// <summary>
    /// Everything recorded about ONE crystal, plant or creature: what it was and where it was. Like a vessel's
    /// <see cref="TheaterTrack"/> it is sampled state, so it can be evaluated at any time, forward or backward, by
    /// binary search. Outside its own span the actor is ABSENT (not born yet, or gone).
    /// </summary>
    public class TheaterActorTrack
    {
        public TheaterActorKind Kind;
        /// <summary>The crystal id for <see cref="TheaterActorKind.Crystal"/>; 0 for plants and creatures.</summary>
        public int Key;
        /// <summary>The species (the prefab's name, without "(Clone)"). Live actors are only matched within one name.</summary>
        public string Name = string.Empty;
        /// <summary><c>Domains</c> as an int, as <see cref="TheaterTrack.Domain"/>.</summary>
        public int Domain;

        public readonly List<TheaterActorPose> Poses = new();

        public float StartTime => Poses.Count > 0 ? Poses[0].Time : 0f;
        public float EndTime => Poses.Count > 0 ? Poses[Poses.Count - 1].Time : 0f;

        // Recording state (never written): the last pose looked at but not kept, so a still spell ends with
        // its own last pose and the move after it does not smear back across the whole spell.
        TheaterActorPose _held;
        bool _hasHeld;
        bool _closed;

        public bool IsClosed => _closed;

        /// <summary>
        /// Look at the actor once (the recorder does this every sample) and keep the pose when it changed past
        /// <paramref name="thin"/>. Returns true when a pose was kept.
        /// </summary>
        public bool Observe(float time, Vector3 position, Quaternion rotation, Vector3 scale, in TheaterActorThinning thin)
        {
            var pose = new TheaterActorPose { Time = time, Position = position, Rotation = rotation, Scale = scale };
            _closed = false;
            if (Poses.Count == 0)
            {
                Poses.Add(pose);
                _hasHeld = false;
                return true;
            }

            // Against the last KEPT pose, so a slow drift adds up until it is worth a pose instead of never being one.
            var last = Poses[Poses.Count - 1];
            bool moved = (position - last.Position).sqrMagnitude > thin.MinDistance * thin.MinDistance;
            bool turned = Quaternion.Angle(rotation, last.Rotation) > thin.MinAngle;
            bool resized = (scale - last.Scale).sqrMagnitude > thin.MinScale * thin.MinScale;
            if (!moved && !turned && !resized)
            {
                _held = pose;
                _hasHeld = true;
                return false;
            }

            // The actor was still from `last` until `_held`: keep the end of the stillness first.
            if (_hasHeld && _held.Time > last.Time)
            {
                Poses.Add(_held);
                last = _held;
            }
            _hasHeld = false;

            if ((position - last.Position).sqrMagnitude > thin.JumpDistance * thin.JumpDistance)
                pose.Flags |= TheaterActorPose.FlagCut;
            Poses.Add(pose);
            return true;
        }

        /// <summary>The actor is gone (or the recording stopped): keep the last pose looked at, so the span ends when it did.</summary>
        public void Close()
        {
            if (_hasHeld && Poses.Count > 0 && _held.Time > Poses[Poses.Count - 1].Time) Poses.Add(_held);
            _hasHeld = false;
            _closed = true;
        }

        /// <summary>
        /// Where the actor was at <paramref name="time"/>. Between two poses it glides, unless the later one is a
        /// <see cref="TheaterActorPose.FlagCut"/> (then it holds the earlier until the jump). Outside its span it is
        /// absent and this returns false - unless <paramref name="clampStart"/>, which holds the first pose for any
        /// time before it (a crystal that already stood at Go but was first sampled a frame later).
        /// </summary>
        public bool TryEvaluate(float time, out Vector3 position, out Quaternion rotation, out Vector3 scale,
                                bool clampStart = false)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;

            int n = Poses.Count;
            if (n == 0) return false;
            if (time > Poses[n - 1].Time) return false;
            if (time < Poses[0].Time)
            {
                if (!clampStart) return false;
                time = Poses[0].Time;
            }

            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (Poses[mid].Time <= time) lo = mid; else hi = mid - 1;
            }

            var a = Poses[lo];
            if (lo >= n - 1 || Poses[lo + 1].IsCut)
            {
                position = a.Position;
                rotation = a.Rotation;
                scale = a.Scale;
                return true;
            }

            var b = Poses[lo + 1];
            float span = b.Time - a.Time;
            float t = span > 1e-6f ? Mathf.Clamp01((time - a.Time) / span) : 0f;
            position = Vector3.LerpUnclamped(a.Position, b.Position, t);
            rotation = Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, t);
            scale = Vector3.LerpUnclamped(a.Scale, b.Scale, t);
            return true;
        }

        /// <summary>
        /// The pose of an actor with these tracks (one per stay in the scene, oldest first) at <paramref name="t"/>:
        /// the stay covering it; before the first, the first pose (a crystal the manager spawned on
        /// the Go frame was first sampled a frame later); between two stays, or after the last, the latest pose seen.
        /// </summary>
        public static bool TryEvaluateStays(List<TheaterActorTrack> tracks, float t,
                                            out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;
            if (tracks == null || tracks.Count == 0) return false;

            if (t <= tracks[0].StartTime) return tracks[0].TryEvaluate(t, out position, out rotation, out scale, clampStart: true);

            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                var track = tracks[i];
                if (track.StartTime > t) continue;
                if (track.TryEvaluate(t, out position, out rotation, out scale)) return true;
                // Gone by t: hold where it was last seen until the next stay starts.
                var last = track.Poses[track.Poses.Count - 1];
                position = last.Position;
                rotation = last.Rotation;
                scale = last.Scale;
                return true;
            }
            return false;
        }
    }

    /// <summary>One shard a dying limb left in a cell's cytoplasm (<c>SnowChanger.AddMorphedShard</c>).</summary>
    [Serializable]
    public struct TheaterShardMorph
    {
        public const int SerializedSize = 40;

        /// <summary>When the limb gave it up (recording time).</summary>
        public float Time;
        public Vector3 Position;
        /// <summary>The limb's heading, which the shard turns from.</summary>
        public Quaternion Rotation;
        /// <summary>Seconds after <see cref="Time"/> the condensing starts (the limb's own fade start, which may lead or lag).</summary>
        public float StartOffset;
        /// <summary>How long it condenses.</summary>
        public float Duration;
    }

    /// <summary>
    /// One cell's cytoplasm shards (<c>SnowChanger</c>): where each one stood at Go, and the shards dying limbs added
    /// afterwards. Where a shard POINTS and how long it is follow from the crystal (the shard law), so with the
    /// crystal replayed, the positions are all the field needs.
    /// </summary>
    public class TheaterShardField
    {
        /// <summary>Which cell, as its config name; with <see cref="Index"/> it identifies the field in the scene.</summary>
        public string Cell = string.Empty;
        public int Index;
        public readonly List<Vector3> Positions = new();
        public readonly List<TheaterShardMorph> Morphs = new();
    }

    /// <summary>
    /// The crystals, plants, creatures and cytoplasm shards of a match (stream 4 of a
    /// <see cref="TheaterRecording"/>, <c>Docs/REPLAYS.md</c> §6.4). The vessels already replay from their tracks and
    /// the prisms from the world stream; these are the remaining things a viewer sees whose placement the game draws
    /// at random, recorded as outcomes so the replay can put each one where it was.
    /// </summary>
    public class TheaterActors
    {
        public readonly List<TheaterActorTrack> Tracks = new();
        public readonly List<TheaterShardField> Shards = new();

        public bool IsEmpty => Tracks.Count == 0 && Shards.Count == 0;

        public int TotalPoseCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Tracks.Count; i++) n += Tracks[i].Poses.Count;
                return n;
            }
        }

        /// <summary>Bytes the stream would occupy on disk, near enough for an overlay or a cap.</summary>
        public long EstimatedBytes
        {
            get
            {
                // Exact for names under 128 ASCII characters (one length byte), which every prefab name is.
                long bytes = 8;
                for (int i = 0; i < Tracks.Count; i++)
                    bytes += 14 + (Tracks[i].Name?.Length ?? 0) + Tracks[i].Poses.Count * (long)TheaterActorPose.SerializedSize;
                for (int i = 0; i < Shards.Count; i++)
                    bytes += 13 + (Shards[i].Cell?.Length ?? 0) + Shards[i].Positions.Count * 12L
                             + Shards[i].Morphs.Count * (long)TheaterShardMorph.SerializedSize;
                return bytes;
            }
        }

        /// <summary>Add <paramref name="offset"/> to every recorded position. Times, rotations and scales are untouched.</summary>
        public void Shift(Vector3 offset)
        {
            for (int t = 0; t < Tracks.Count; t++)
            {
                var poses = Tracks[t].Poses;
                for (int i = 0; i < poses.Count; i++)
                {
                    var p = poses[i];
                    p.Position += offset;
                    poses[i] = p;
                }
            }
            for (int f = 0; f < Shards.Count; f++)
            {
                var field = Shards[f];
                for (int i = 0; i < field.Positions.Count; i++) field.Positions[i] += offset;
                for (int i = 0; i < field.Morphs.Count; i++)
                {
                    var m = field.Morphs[i];
                    m.Position += offset;
                    field.Morphs[i] = m;
                }
            }
        }

        public void Write(Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Tracks.Count);
            for (int i = 0; i < Tracks.Count; i++)
            {
                var track = Tracks[i];
                writer.Write((byte)track.Kind);
                writer.Write(track.Key);
                writer.Write(track.Name ?? string.Empty);
                writer.Write(track.Domain);
                writer.Write(track.Poses.Count);
                for (int p = 0; p < track.Poses.Count; p++)
                {
                    var pose = track.Poses[p];
                    writer.Write(pose.Time);
                    WriteVector(writer, pose.Position);
                    WriteQuaternion(writer, pose.Rotation);
                    WriteVector(writer, pose.Scale);
                    writer.Write(pose.Flags);
                }
            }

            writer.Write(Shards.Count);
            for (int f = 0; f < Shards.Count; f++)
            {
                var field = Shards[f];
                writer.Write(field.Cell ?? string.Empty);
                writer.Write(field.Index);
                writer.Write(field.Positions.Count);
                for (int i = 0; i < field.Positions.Count; i++) WriteVector(writer, field.Positions[i]);
                writer.Write(field.Morphs.Count);
                for (int i = 0; i < field.Morphs.Count; i++)
                {
                    var m = field.Morphs[i];
                    writer.Write(m.Time);
                    WriteVector(writer, m.Position);
                    WriteQuaternion(writer, m.Rotation);
                    writer.Write(m.StartOffset);
                    writer.Write(m.Duration);
                }
            }
        }

        /// <summary>Read an actor stream. Throws on a truncated or malformed payload; <see cref="TheaterRecording.TryRead"/> reports it.</summary>
        public static TheaterActors Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var actors = new TheaterActors();
            int tracks = reader.ReadInt32();
            if (tracks < 0) throw new IOException("corrupt actor stream");
            for (int i = 0; i < tracks; i++)
            {
                var track = new TheaterActorTrack
                {
                    Kind = (TheaterActorKind)reader.ReadByte(),
                    Key = reader.ReadInt32(),
                    Name = reader.ReadString(),
                    Domain = reader.ReadInt32(),
                };
                int poses = reader.ReadInt32();
                if (poses < 0) throw new IOException("corrupt actor stream");
                if (poses > 0) track.Poses.Capacity = poses;
                for (int p = 0; p < poses; p++)
                {
                    track.Poses.Add(new TheaterActorPose
                    {
                        Time = reader.ReadSingle(),
                        Position = ReadVector(reader),
                        Rotation = ReadQuaternion(reader),
                        Scale = ReadVector(reader),
                        Flags = reader.ReadByte(),
                    });
                }
                track.Close();
                actors.Tracks.Add(track);
            }

            int fields = reader.ReadInt32();
            if (fields < 0) throw new IOException("corrupt actor stream");
            for (int f = 0; f < fields; f++)
            {
                var field = new TheaterShardField { Cell = reader.ReadString(), Index = reader.ReadInt32() };
                int count = reader.ReadInt32();
                if (count < 0) throw new IOException("corrupt actor stream");
                if (count > 0) field.Positions.Capacity = count;
                for (int i = 0; i < count; i++) field.Positions.Add(ReadVector(reader));
                int morphs = reader.ReadInt32();
                if (morphs < 0) throw new IOException("corrupt actor stream");
                for (int i = 0; i < morphs; i++)
                {
                    field.Morphs.Add(new TheaterShardMorph
                    {
                        Time = reader.ReadSingle(),
                        Position = ReadVector(reader),
                        Rotation = ReadQuaternion(reader),
                        StartOffset = reader.ReadSingle(),
                        Duration = reader.ReadSingle(),
                    });
                }
                actors.Shards.Add(field);
            }
            return actors;
        }

        static void WriteVector(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        static void WriteQuaternion(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        static Vector3 ReadVector(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        static Quaternion ReadQuaternion(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
