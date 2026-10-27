#if UNITY_EDITOR
using System.IO;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The recorded prism world (Theater P1): it survives the file, a recording without it still
    /// reads, it moves with the stage, and playback shows exactly the prisms standing at the time
    /// asked for - forward, backward and after a loop.
    /// </summary>
    public class TheaterWorldTests
    {
        static TheaterWorldEvent Born(float t, int id, Vector3 p, byte domain = 1, byte flags = 0) => new()
        {
            Kind = TheaterWorldEventKind.Born, Time = t, Id = id, Position = p,
            Rotation = Quaternion.Euler(0f, 30f, 0f), Scale = new Vector3(1f, 2f, 3f), Domain = domain, Flags = flags
        };

        static TheaterWorldEvent Gone(float t, int id) => new() { Kind = TheaterWorldEventKind.Gone, Time = t, Id = id };

        static TheaterWorldEvent Moved(float t, int id, Vector3 p) => new()
        {
            Kind = TheaterWorldEventKind.Moved, Time = t, Id = id, Position = p, Rotation = Quaternion.identity
        };

        static TheaterRecording Recording(TheaterWorld world)
        {
            var recording = new TheaterRecording { SceneName = "MinigameRegatta", GameMode = 7 };
            var track = new TheaterTrack { PlayerName = "P" };
            track.Poses.Add(new TheaterPose { Time = 0f, Rotation = Quaternion.identity });
            track.Poses.Add(new TheaterPose { Time = 10f, Rotation = Quaternion.identity });
            recording.Tracks.Add(track);
            recording.World = world;
            return recording;
        }

        static TheaterWorld World()
        {
            var world = new TheaterWorld();
            world.Events.Add(Born(0f, 0, new Vector3(1f, 2f, 3f), domain: 2, flags: TheaterWorld.FlagShielded));
            world.Events.Add(Born(1f, 1, new Vector3(4f, 5f, 6f)));
            world.Events.Add(Moved(2f, 1, new Vector3(7f, 8f, 9f)));
            world.Events.Add(Gone(3f, 0));
            world.Events.Add(new TheaterWorldEvent
            {
                Kind = TheaterWorldEventKind.Restored, Time = 4f, Id = 0, Position = new Vector3(1f, 2f, 3f),
                Rotation = Quaternion.identity, Scale = Vector3.one, Domain = 2
            });
            return world;
        }

        static TheaterRecording RoundTrip(TheaterRecording recording)
        {
            using var stream = new MemoryStream();
            recording.Write(stream);
            stream.Position = 0;
            Assert.IsTrue(TheaterRecording.TryRead(stream, out var read, out string error), error);
            return read;
        }

        [Test]
        public void World_SurvivesTheFile_EventForEvent()
        {
            var read = RoundTrip(Recording(World()));
            Assert.IsTrue(read.HasWorld);
            Assert.AreEqual(5, read.World.Events.Count);

            var born = read.World.Events[0];
            Assert.AreEqual(TheaterWorldEventKind.Born, born.Kind);
            Assert.AreEqual(3f, born.Position.z, 1e-6f);
            Assert.AreEqual(3f, born.Scale.z, 1e-6f);
            Assert.AreEqual(2, born.Domain);
            Assert.AreEqual(TheaterWorld.FlagShielded, born.Flags);
            Assert.AreEqual(Quaternion.Euler(0f, 30f, 0f).y, born.Rotation.y, 1e-6f);

            var moved = read.World.Events[2];
            Assert.AreEqual(TheaterWorldEventKind.Moved, moved.Kind);
            Assert.AreEqual(1, moved.Id);
            Assert.AreEqual(9f, moved.Position.z, 1e-6f);

            Assert.AreEqual(TheaterWorldEventKind.Gone, read.World.Events[3].Kind);
            Assert.AreEqual(3f, read.World.Events[3].Time, 1e-6f);
            Assert.AreEqual(TheaterWorldEventKind.Restored, read.World.Events[4].Kind);
            Assert.AreEqual(1, read.Tracks.Count, "the vessel stream is untouched");
        }

        [Test]
        public void Recording_WithoutAWorld_ReadsWithNone()
        {
            var read = RoundTrip(Recording(null));
            Assert.IsFalse(read.HasWorld);
            Assert.IsNull(read.World);
            Assert.AreEqual(2, read.Tracks[0].Poses.Count);

            var empty = RoundTrip(Recording(new TheaterWorld()));
            Assert.IsFalse(empty.HasWorld, "an empty world writes no stream");
        }

        [Test]
        public void EstimatedBytes_MatchesTheWrittenStream()
        {
            var world = World();
            using var stream = new MemoryStream();
            world.Write(stream);
            Assert.AreEqual(stream.Length, world.EstimatedBytes);
        }

        [Test]
        public void Shift_MovesWorldAndVessels_ButNotTimesOrScales()
        {
            var recording = Recording(World());
            ReplayStagePlacement.Shift(recording, new Vector3(0f, 0f, -120000f));

            var events = recording.World.Events;
            Assert.AreEqual(3f - 120000f, events[0].Position.z, 1e-2f);
            Assert.AreEqual(9f - 120000f, events[2].Position.z, 1e-2f);
            Assert.AreEqual(3f, events[0].Scale.z, 1e-6f);
            Assert.AreEqual(2f, events[2].Time, 1e-6f);
            Assert.AreEqual(-120000f, recording.Tracks[0].Poses[0].Position.z, 1e-2f);
        }

        [Test]
        public void Playback_ShowsWhatStandsAtTheTimeAsked()
        {
            var playback = new TheaterWorldPlayback();
            Assert.IsTrue(playback.Begin(World(), null));

            playback.Advance(0.5f);
            Assert.AreEqual(1, playback.StandingCount);
            Assert.AreEqual(1, playback.StandingIn(2));

            playback.Advance(2.5f);
            Assert.AreEqual(2, playback.StandingCount);
            Assert.IsTrue(playback.TryGetStanding(1, out var moved));
            Assert.AreEqual(9f, moved.z, 1e-5f, "the move landed");

            playback.Advance(3.5f);
            Assert.AreEqual(1, playback.StandingCount, "prism 0 is gone");
            Assert.IsFalse(playback.TryGetStanding(0, out _));
            Assert.IsTrue(playback.TryGetStanding(1, out moved), "the swap-remove kept prism 1 findable");
            Assert.AreEqual(9f, moved.z, 1e-5f);

            playback.Advance(5f);
            Assert.AreEqual(2, playback.StandingCount, "prism 0 is restored");

            // Backward: the log replays from the start.
            playback.Advance(1.5f);
            Assert.AreEqual(2, playback.StandingCount);
            Assert.IsTrue(playback.TryGetStanding(1, out var before));
            Assert.AreEqual(6f, before.z, 1e-5f, "before its move, prism 1 is where it was born");

            playback.Advance(0f);
            Assert.AreEqual(1, playback.StandingCount);
            playback.Stop();
            Assert.IsFalse(playback.IsPlaying);
        }

        [Test]
        public void Playback_RemovingFromTheMiddle_KeepsTheOthersWhereTheyAre()
        {
            var world = new TheaterWorld();
            for (int i = 0; i < 4; i++) world.Events.Add(Born(0f, i, new Vector3(i * 10f, 0f, 0f)));
            world.Events.Add(Gone(1f, 1));
            world.Events.Add(Moved(2f, 3, new Vector3(99f, 0f, 0f)));
            world.Events.Add(Gone(3f, 0));

            var playback = new TheaterWorldPlayback();
            Assert.IsTrue(playback.Begin(world, null));
            playback.Advance(5f);

            Assert.AreEqual(2, playback.StandingCount);
            Assert.AreEqual(2, playback.StandingIn(1));
            Assert.IsTrue(playback.TryGetStanding(2, out var two));
            Assert.AreEqual(20f, two.x, 1e-5f);
            Assert.IsTrue(playback.TryGetStanding(3, out var three));
            Assert.AreEqual(99f, three.x, 1e-5f, "the move reached the prism that was swapped into the hole");
            Assert.IsFalse(playback.TryGetStanding(0, out _));
            Assert.IsFalse(playback.TryGetStanding(1, out _));
        }

        [Test]
        public void Playback_EmptyWorld_DoesNotStart()
        {
            var playback = new TheaterWorldPlayback();
            Assert.IsFalse(playback.Begin(null, null));
            Assert.IsFalse(playback.Begin(new TheaterWorld(), null));
            Assert.DoesNotThrow(() => playback.Advance(1f));
        }
    }
}
#endif
