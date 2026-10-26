#if UNITY_EDITOR
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A replay plays inside its arena only if the ghosts land where the arena stands: every pose
    /// moves by the stage's origin minus the recorded arena origin, and nothing else changes. A
    /// wrong sign or a missed track reads on screen as ghosts racing beside the rails.
    /// </summary>
    public class ReplayStagePlacementTests
    {
        static TheaterRecording Recording()
        {
            var recording = new TheaterRecording();
            for (int t = 0; t < 2; t++)
            {
                var track = new TheaterTrack { PlayerName = "P" + t };
                for (int i = 0; i < 3; i++)
                    track.Poses.Add(new TheaterPose
                    {
                        Time = i * 0.5f, Position = new Vector3(i, t, -i), Rotation = Quaternion.identity, Speed = 10f + i
                    });
                recording.Tracks.Add(track);
            }
            return recording;
        }

        [Test]
        public void Offset_IsStageMinusRecordedOrigin()
        {
            var offset = ReplayStagePlacement.Offset(new Vector3(10f, 0f, 5f), new Vector3(0f, 0f, -120000f));
            Assert.AreEqual(-10f, offset.x, 1e-3f);
            Assert.AreEqual(0f, offset.y, 1e-3f);
            Assert.AreEqual(-120005f, offset.z, 1e-3f);
        }

        [Test]
        public void Shift_MovesEveryPoseOfEveryTrack_AndNothingElse()
        {
            var recording = Recording();
            var offset = new Vector3(0f, 0f, -120000f);
            ReplayStagePlacement.Shift(recording, offset);

            for (int t = 0; t < recording.Tracks.Count; t++)
            {
                var poses = recording.Tracks[t].Poses;
                for (int i = 0; i < poses.Count; i++)
                {
                    Assert.AreEqual(i, poses[i].Position.x, 1e-3f);
                    Assert.AreEqual(t, poses[i].Position.y, 1e-3f);
                    Assert.AreEqual(-i - 120000f, poses[i].Position.z, 1e-2f);
                    Assert.AreEqual(i * 0.5f, poses[i].Time, 1e-6f, "times must not move");
                    Assert.AreEqual(10f + i, poses[i].Speed, 1e-6f, "speeds must not move");
                }
            }
        }

        [Test]
        public void Shift_NullRecording_IsANoOp()
        {
            Assert.DoesNotThrow(() => ReplayStagePlacement.Shift(null, new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void Summary_KeepsItsRecordedRings_AndAnOldSummaryHasNone()
        {
            var summary = new ReplaySummary { RecordingFile = "Headlong_2026-10-26_10-00.cstheater" };
            summary.Gates.Add(new ReplayGate { Index = 0, Position = new Vector3(10f, 0f, 0f), Axis = new Vector3(0f, 0f, 1f), Radius = 40f });
            summary.Gates.Add(new ReplayGate { Index = 1, Position = new Vector3(0f, 5f, 200f), Axis = new Vector3(1f, 0f, 0f), Radius = 32f });
            Assert.IsTrue(ReplaySummary.TryParse(summary.ToJson(), out var read, out string error), error);
            Assert.AreEqual(2, read.Gates.Count);
            Assert.AreEqual(1, read.Gates[1].Index);
            Assert.AreEqual(200f, read.Gates[1].Position.z, 1e-4f);
            Assert.AreEqual(32f, read.Gates[1].Radius, 1e-4f);

            string old = "{\"Schema\":\"" + ReplaySummary.SchemaId + "\",\"RecordingFile\":\"Regatta_2026-10-15_20-00.cstheater\"}";
            Assert.IsTrue(ReplaySummary.TryParse(old, out var legacy, out error), error);
            Assert.IsTrue(legacy.Gates == null || legacy.Gates.Count == 0);
        }

        [Test]
        public void Summary_KeepsItsArenaOrigin_AndAnOldSummaryReadsZero()
        {
            var summary = new ReplaySummary { RecordingFile = "Regatta_2026-10-24_10-00.cstheater", ArenaOrigin = new Vector3(1f, 2f, 3f) };
            Assert.IsTrue(ReplaySummary.TryParse(summary.ToJson(), out var read, out string error), error);
            Assert.AreEqual(1f, read.ArenaOrigin.x, 1e-6f);
            Assert.AreEqual(2f, read.ArenaOrigin.y, 1e-6f);
            Assert.AreEqual(3f, read.ArenaOrigin.z, 1e-6f);

            string old = "{\"Schema\":\"" + ReplaySummary.SchemaId + "\",\"RecordingFile\":\"Regatta_2026-10-15_20-00.cstheater\"}";
            Assert.IsTrue(ReplaySummary.TryParse(old, out var legacy, out error), error);
            Assert.AreEqual(0f, legacy.ArenaOrigin.x, 1e-6f);
            Assert.AreEqual(0f, legacy.ArenaOrigin.z, 1e-6f);
        }
    }
}
#endif
