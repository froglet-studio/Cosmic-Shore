#if UNITY_EDITOR
using System.IO;
using System.Text;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The recorded crystals, plants, creatures and cytoplasm shards (stream 4, <c>Docs/REPLAYS.md</c> §6.4): they
    /// survive the file, a recording without them still reads, a reader that predates them still reads the rest,
    /// the thinning keeps what moved and the stillness around it, and a respawn jump snaps instead of gliding.
    /// </summary>
    public class TheaterActorsTests
    {
        static readonly TheaterActorThinning Thin = TheaterActorThinning.Default;

        static TheaterRecording Recording(TheaterActors actors)
        {
            var recording = new TheaterRecording { SceneName = "MinigameRegatta", GameMode = 7 };
            var track = new TheaterTrack { PlayerName = "P" };
            track.Poses.Add(new TheaterPose { Time = 0f, Rotation = Quaternion.identity });
            track.Poses.Add(new TheaterPose { Time = 10f, Rotation = Quaternion.identity });
            recording.Tracks.Add(track);
            recording.Actors = actors;
            return recording;
        }

        static TheaterActors Actors()
        {
            var actors = new TheaterActors();
            var crystal = new TheaterActorTrack { Kind = TheaterActorKind.Crystal, Key = 1, Name = "Crystal", Domain = 2 };
            crystal.Observe(0f, new Vector3(1f, 2f, 3f), Quaternion.identity, Vector3.one, Thin);
            crystal.Observe(5f, new Vector3(100f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 2f, Thin);
            crystal.Close();
            actors.Tracks.Add(crystal);

            var plant = new TheaterActorTrack { Kind = TheaterActorKind.Flora, Name = "BranchingFlora", Domain = 3 };
            plant.Observe(1f, new Vector3(-5f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            plant.Observe(9f, new Vector3(-5f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            plant.Close();
            actors.Tracks.Add(plant);

            var field = new TheaterShardField { Cell = "CellConfig_Regatta", Index = 0 };
            field.Positions.Add(new Vector3(1f, 1f, 1f));
            field.Positions.Add(new Vector3(-2f, 3f, 4f));
            field.Morphs.Add(new TheaterShardMorph
            {
                Time = 4f, Position = new Vector3(7f, 8f, 9f), Rotation = Quaternion.Euler(10f, 0f, 0f),
                StartOffset = -0.5f, Duration = 1.5f
            });
            actors.Shards.Add(field);
            return actors;
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
        public void Actors_SurviveTheFile_PoseForPose()
        {
            var read = RoundTrip(Recording(Actors()));
            Assert.IsTrue(read.HasActors);
            Assert.AreEqual(2, read.Actors.Tracks.Count);

            var crystal = read.Actors.Tracks[0];
            Assert.AreEqual(TheaterActorKind.Crystal, crystal.Kind);
            Assert.AreEqual(1, crystal.Key);
            Assert.AreEqual("Crystal", crystal.Name);
            Assert.AreEqual(2, crystal.Domain);
            Assert.AreEqual(2, crystal.Poses.Count);
            Assert.AreEqual(100f, crystal.Poses[1].Position.x, 1e-5f);
            Assert.AreEqual(Quaternion.Euler(0f, 90f, 0f).y, crystal.Poses[1].Rotation.y, 1e-6f);
            Assert.AreEqual(2f, crystal.Poses[1].Scale.z, 1e-6f);
            Assert.IsTrue(crystal.Poses[1].IsCut, "a 100-unit move in one look is a respawn jump");

            var plant = read.Actors.Tracks[1];
            Assert.AreEqual(TheaterActorKind.Flora, plant.Kind);
            Assert.AreEqual("BranchingFlora", plant.Name);
            Assert.AreEqual(9f, plant.EndTime, 1e-6f, "a still plant's span still ends when it was last seen");

            var field = read.Actors.Shards[0];
            Assert.AreEqual("CellConfig_Regatta", field.Cell);
            Assert.AreEqual(2, field.Positions.Count);
            Assert.AreEqual(4f, field.Positions[1].z, 1e-6f);
            Assert.AreEqual(1, field.Morphs.Count);
            Assert.AreEqual(-0.5f, field.Morphs[0].StartOffset, 1e-6f);
            Assert.AreEqual(1.5f, field.Morphs[0].Duration, 1e-6f);
            Assert.AreEqual(1, read.Tracks.Count, "the vessel stream is untouched");
        }

        [Test]
        public void Recording_WithoutActors_ReadsWithNone()
        {
            var read = RoundTrip(Recording(null));
            Assert.IsFalse(read.HasActors);
            Assert.IsNull(read.Actors);

            var empty = RoundTrip(Recording(new TheaterActors()));
            Assert.IsFalse(empty.HasActors, "empty actors write no stream");
        }

        [Test]
        public void AReaderThatPredatesActors_StillReadsTheRest()
        {
            // An older build knows streams 1-3 only: stream 4 must be skipped by its length. Simulated by renumbering
            // the actor chunk to an id no build knows, which is exactly what stream 4 is to an older reader.
            var recording = Recording(Actors());
            using var stream = new MemoryStream();
            recording.Write(stream);
            byte[] bytes = stream.ToArray();

            int at = FindChunk(bytes, TheaterRecording.StreamActors);
            Assert.Greater(at, 0, "the actor chunk was written");
            bytes[at] = 0x7F;
            bytes[at + 1] = 0x00;

            using var older = new MemoryStream(bytes);
            Assert.IsTrue(TheaterRecording.TryRead(older, out var read, out string error), error);
            Assert.IsNull(read.Actors);
            Assert.AreEqual(2, read.Tracks[0].Poses.Count);
        }

        /// <summary>Byte offset of the chunk header carrying <paramref name="id"/> (walks the chunk table, not a byte search).</summary>
        static int FindChunk(byte[] bytes, ushort id)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
            reader.ReadUInt32(); reader.ReadUInt16(); reader.ReadString(); reader.ReadString();
            reader.ReadInt32(); reader.ReadSingle(); reader.ReadInt64();
            int chunks = reader.ReadUInt16();
            for (int c = 0; c < chunks; c++)
            {
                int at = (int)reader.BaseStream.Position;
                ushort chunk = reader.ReadUInt16();
                int length = reader.ReadInt32();
                if (chunk == id) return at;
                reader.BaseStream.Position += length;
            }
            return -1;
        }

        [Test]
        public void EstimatedBytes_MatchesTheWrittenStream()
        {
            var actors = Actors();
            using var stream = new MemoryStream();
            actors.Write(stream);
            Assert.AreEqual(stream.Length, actors.EstimatedBytes);
        }

        [Test]
        public void Thinning_KeepsMoves_AndTheEndOfEachStillSpell()
        {
            var track = new TheaterActorTrack();
            var p = new Vector3(10f, 0f, 0f);
            for (int i = 0; i <= 30; i++) track.Observe(i / 30f, p, Quaternion.identity, Vector3.one, Thin);
            Assert.AreEqual(1, track.Poses.Count, "a still second is one pose while it lasts");

            track.Observe(31f / 30f, p + Vector3.up, Quaternion.identity, Vector3.one, Thin);
            Assert.AreEqual(3, track.Poses.Count, "the move keeps the stillness's last pose, then itself");
            Assert.AreEqual(1f, track.Poses[1].Time, 1e-6f);
            Assert.AreEqual(10f, track.Poses[1].Position.x, 1e-6f);

            // Playback holds through the still spell rather than gliding across it.
            Assert.IsTrue(track.TryEvaluate(0.5f, out var mid, out _, out _));
            Assert.AreEqual(0f, mid.y, 1e-6f);

            track.Close();
            Assert.AreEqual(31f / 30f, track.EndTime, 1e-6f);
        }

        [Test]
        public void Thinning_KeepsASlowDriftOnceItAddsUp()
        {
            var track = new TheaterActorTrack();
            for (int i = 0; i < 10; i++)
                track.Observe(i, new Vector3(i * 0.004f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            Assert.Greater(track.Poses.Count, 1, "drift under the step but over the threshold in total is kept");
        }

        [Test]
        public void Thinning_KeepsTurnsAndResizes()
        {
            var track = new TheaterActorTrack();
            track.Observe(0f, Vector3.zero, Quaternion.identity, Vector3.one, Thin);
            track.Observe(1f, Vector3.zero, Quaternion.Euler(0f, 5f, 0f), Vector3.one, Thin);
            track.Observe(2f, Vector3.zero, Quaternion.Euler(0f, 5f, 0f), Vector3.one * 1.5f, Thin);
            Assert.AreEqual(3, track.Poses.Count);
        }

        [Test]
        public void Evaluate_GlidesBetweenPoses_SnapsAcrossACut_AndIsAbsentOutside()
        {
            var track = new TheaterActorTrack();
            track.Observe(0f, Vector3.zero, Quaternion.identity, Vector3.one, Thin);
            track.Observe(1f, new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            track.Observe(2f, new Vector3(500f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            track.Close();
            Assert.IsFalse(track.Poses[1].IsCut);
            Assert.IsTrue(track.Poses[2].IsCut);

            Assert.IsTrue(track.TryEvaluate(0.5f, out var glide, out _, out _));
            Assert.AreEqual(1f, glide.x, 1e-5f);

            Assert.IsTrue(track.TryEvaluate(1.9f, out var held, out _, out _));
            Assert.AreEqual(2f, held.x, 1e-5f, "a respawn is a jump at its moment, not a 500-unit glide");

            Assert.IsTrue(track.TryEvaluate(2f, out var landed, out _, out _));
            Assert.AreEqual(500f, landed.x, 1e-3f);

            Assert.IsFalse(track.TryEvaluate(-0.1f, out _, out _, out _), "not born yet");
            Assert.IsFalse(track.TryEvaluate(2.1f, out _, out _, out _), "gone");
            Assert.IsTrue(track.TryEvaluate(-0.1f, out var early, out _, out _, clampStart: true));
            Assert.AreEqual(0f, early.x, 1e-6f, "clamped to the first pose");
        }

        [Test]
        public void Shift_MovesActorsAndShards_ButNotTimesOrScales()
        {
            var recording = Recording(Actors());
            ReplayStagePlacement.Shift(recording, new Vector3(0f, 0f, -120000f));

            var crystal = recording.Actors.Tracks[0];
            Assert.AreEqual(3f - 120000f, crystal.Poses[0].Position.z, 1e-2f);
            Assert.AreEqual(5f, crystal.Poses[1].Time, 1e-6f);
            Assert.AreEqual(2f, crystal.Poses[1].Scale.z, 1e-6f);
            var field = recording.Actors.Shards[0];
            Assert.AreEqual(1f - 120000f, field.Positions[0].z, 1e-2f);
            Assert.AreEqual(9f - 120000f, field.Morphs[0].Position.z, 1e-2f);
        }

        [Test]
        public void Stays_ClampBeforeTheFirst_HoldBetween_AndFollowTheLatest()
        {
            // One crystal id, two stays in the scene (it was taken, then respawned elsewhere a second later).
            var first = new TheaterActorTrack { Kind = TheaterActorKind.Crystal, Key = 3 };
            first.Observe(1f, new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            first.Observe(2f, new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            first.Close();
            var second = new TheaterActorTrack { Kind = TheaterActorKind.Crystal, Key = 3 };
            second.Observe(3f, new Vector3(50f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            second.Observe(4f, new Vector3(50f, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
            second.Close();
            var stays = new System.Collections.Generic.List<TheaterActorTrack> { first, second };

            Assert.IsTrue(TheaterActorTrack.TryEvaluateStays(stays, 0.9f, out var before, out _, out _));
            Assert.AreEqual(1f, before.x, 1e-6f, "the Go frame shows where it first stood");
            Assert.IsTrue(TheaterActorTrack.TryEvaluateStays(stays, 1.5f, out var during, out _, out _));
            Assert.AreEqual(1.5f, during.x, 1e-5f);
            Assert.IsTrue(TheaterActorTrack.TryEvaluateStays(stays, 2.5f, out var between, out _, out _));
            Assert.AreEqual(2f, between.x, 1e-6f, "between stays it holds where it was last seen");
            Assert.IsTrue(TheaterActorTrack.TryEvaluateStays(stays, 3.5f, out var later, out _, out _));
            Assert.AreEqual(50f, later.x, 1e-6f);
            Assert.IsTrue(TheaterActorTrack.TryEvaluateStays(stays, 9f, out var after, out _, out _));
            Assert.AreEqual(50f, after.x, 1e-6f);
            Assert.IsFalse(TheaterActorTrack.TryEvaluateStays(new System.Collections.Generic.List<TheaterActorTrack>(), 1f, out _, out _, out _));
        }

        [Test]
        public void PickNearest_TakesTheClosestFreeOneThatIsPresent()
        {
            TheaterActorTrack Creature(float x, float from, float to)
            {
                var track = new TheaterActorTrack { Kind = TheaterActorKind.Fauna, Name = "Tadpole" };
                track.Observe(from, new Vector3(x, 0f, 0f), Quaternion.identity, Vector3.one, Thin);
                track.Observe(to, new Vector3(x, 0f, 0.5f), Quaternion.identity, Vector3.one, Thin);
                track.Close();
                return track;
            }

            var near = Creature(1f, 0f, 10f);
            var far = Creature(30f, 0f, 10f);
            var unborn = Creature(0f, 5f, 10f);
            var species = new System.Collections.Generic.List<TheaterActorTrack> { far, near, unborn };
            var taken = new System.Collections.Generic.HashSet<TheaterActorTrack>();

            Assert.AreSame(near, TheaterActorTrack.PickNearest(species, taken, Vector3.zero, 1f),
                           "the creature not born yet is not a partner, however close");
            taken.Add(near);
            Assert.AreSame(far, TheaterActorTrack.PickNearest(species, taken, Vector3.zero, 1f));
            taken.Add(far);
            Assert.IsNull(TheaterActorTrack.PickNearest(species, taken, Vector3.zero, 1f), "no counterpart: hide it");
            Assert.AreSame(unborn, TheaterActorTrack.PickNearest(species, taken, Vector3.zero, 6f));
        }
        [Test]
        public void ShardMorphs_SortByTime_AndComeDueOnce()
        {
            var field = new TheaterShardField { Cell = "C", Index = 0 };
            foreach (float time in new[] { 4f, 1f, 2.5f, 2.5f })
                field.Morphs.Add(new TheaterShardMorph { Time = time, Duration = time });
            field.SortMorphs();
            Assert.AreEqual(1f, field.Morphs[0].Time);
            Assert.AreEqual(4f, field.Morphs[3].Time);

            Assert.AreEqual(0, field.MorphsDue(0, 0.5f), "none due before the first");
            int next = field.MorphsDue(0, 2.5f);
            Assert.AreEqual(3, next, "a morph exactly at t is due");
            Assert.AreEqual(3, field.MorphsDue(next, 3f), "nothing new until the next one");
            Assert.AreEqual(4, field.MorphsDue(next, 100f));
        }

        [Test]
        public void SnowChanger_ReplayHooks_AreAdditive()
        {
            const string path = "Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs";
            if (!File.Exists(path)) Assert.Ignore("run from the Unity project root");
            string src = File.ReadAllText(path);
            foreach (string member in new[]
                     {
                         "public static event System.Action<SnowChanger, Vector3, Quaternion, float, float> MorphedShardAdded",
                         "public static bool ReplayHoldsMorphs",
                         "public void CopyShardPositions(",
                         "public bool ApplyShardPositions(",
                         "public bool AddRecordedMorphedShard(",
                     })
                StringAssert.Contains(member, src, "the replay's hook into the cytoplasm is missing");
            StringAssert.Contains("if (ReplayHoldsMorphs) return false;", src,
                                  "live dying limbs must not add shards over a replay's recorded ones");
        }
    }
}
#endif
