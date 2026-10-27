using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>What happened to one recorded prism at one moment.</summary>
    public enum TheaterWorldEventKind : byte
    {
        /// <summary>The prism appeared (or was already standing when recording began): full pose, domain and flags.</summary>
        Born = 0,

        /// <summary>The prism was destroyed or left the world. Its id is never used again unless it is <see cref="Restored"/>.</summary>
        Gone = 1,

        /// <summary>A destroyed prism came back, with the pose it came back at.</summary>
        Restored = 2,

        /// <summary>A standing prism moved: position and rotation only.</summary>
        Moved = 3,
    }

    /// <summary>
    /// One prism event. Which fields mean anything depends on <see cref="Kind"/>: a Born or Restored
    /// event carries the whole pose, a Moved event position and rotation, a Gone event only the id.
    /// </summary>
    [Serializable]
    public struct TheaterWorldEvent
    {
        public float Time;
        public int Id;
        public TheaterWorldEventKind Kind;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        /// <summary><c>Domains</c> as a byte, for the same reason a track stores its domain as an int.</summary>
        public byte Domain;

        /// <summary><see cref="TheaterWorld.FlagShielded"/> and its siblings.</summary>
        public byte Flags;
    }

    /// <summary>
    /// The prism world of one recording (Theater P1): every prism's birth, death, restoration and
    /// movement, in time order, on the same <c>Time.time</c> clock the vessel poses use.
    ///
    /// <para><b>Events, not samples.</b> Prisms are thousands of mostly-still objects, so sampling
    /// them like vessels would cost thousands of poses a frame to record nothing. An event log costs
    /// only what changes; the price is that seeking backward replays the log from the start, which
    /// <c>TheaterWorldPlayback</c> does in one pass over plain structs.</para>
    ///
    /// <para><b>On disk</b> it is the recording's second stream (<see cref="TheaterRecording.StreamWorld"/>):
    /// an event count, then per event a kind byte, time and id, then the kind's own fields - 51 bytes
    /// for a birth, 37 for a move, 9 for a death. A reader from before P1 skips the whole stream by
    /// its byte length.</para>
    /// </summary>
    public class TheaterWorld
    {
        public const byte FlagShielded = 1;
        public const byte FlagSuperShielded = 2;
        public const byte FlagDangerous = 4;

        public readonly List<TheaterWorldEvent> Events = new();

        /// <summary>Bytes the events would occupy on disk, near enough for an overlay or a cap.</summary>
        public long EstimatedBytes
        {
            get
            {
                long bytes = 4;
                for (int i = 0; i < Events.Count; i++) bytes += SizeOf(Events[i].Kind);
                return bytes;
            }
        }

        public static int SizeOf(TheaterWorldEventKind kind) => kind switch
        {
            TheaterWorldEventKind.Born => 51,
            TheaterWorldEventKind.Restored => 51,
            TheaterWorldEventKind.Moved => 37,
            _ => 9,
        };

        /// <summary>Add <paramref name="offset"/> to every recorded position. Times, rotations and scales are untouched.</summary>
        public void Shift(Vector3 offset)
        {
            for (int i = 0; i < Events.Count; i++)
            {
                var e = Events[i];
                if (e.Kind == TheaterWorldEventKind.Gone) continue;
                e.Position += offset;
                Events[i] = e;
            }
        }

        public void Write(Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Events.Count);
            for (int i = 0; i < Events.Count; i++)
            {
                var e = Events[i];
                writer.Write((byte)e.Kind);
                writer.Write(e.Time);
                writer.Write(e.Id);
                if (e.Kind == TheaterWorldEventKind.Gone) continue;

                writer.Write(e.Position.x);
                writer.Write(e.Position.y);
                writer.Write(e.Position.z);
                writer.Write(e.Rotation.x);
                writer.Write(e.Rotation.y);
                writer.Write(e.Rotation.z);
                writer.Write(e.Rotation.w);
                if (e.Kind == TheaterWorldEventKind.Moved) continue;

                writer.Write(e.Scale.x);
                writer.Write(e.Scale.y);
                writer.Write(e.Scale.z);
                writer.Write(e.Domain);
                writer.Write(e.Flags);
            }
        }

        /// <summary>Read a world stream. Throws on a truncated or malformed payload; <see cref="TheaterRecording.TryRead"/> reports it.</summary>
        public static TheaterWorld Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            int count = reader.ReadInt32();
            if (count < 0) throw new IOException("corrupt world stream");

            var world = new TheaterWorld();
            world.Events.Capacity = count;
            for (int i = 0; i < count; i++)
            {
                byte kind = reader.ReadByte();
                if (kind > (byte)TheaterWorldEventKind.Moved) throw new IOException($"unknown world event kind {kind}");

                var e = new TheaterWorldEvent
                {
                    Kind = (TheaterWorldEventKind)kind,
                    Time = reader.ReadSingle(),
                    Id = reader.ReadInt32(),
                    Rotation = Quaternion.identity,
                };
                if (e.Kind != TheaterWorldEventKind.Gone)
                {
                    e.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    e.Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    if (e.Kind != TheaterWorldEventKind.Moved)
                    {
                        e.Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        e.Domain = reader.ReadByte();
                        e.Flags = reader.ReadByte();
                    }
                }
                world.Events.Add(e);
            }
            return world;
        }
    }
}
