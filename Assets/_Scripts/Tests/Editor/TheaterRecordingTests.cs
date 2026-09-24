#if UNITY_EDITOR
using System.IO;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The theater format's own gates. The recorder and the playback both need Unity objects, but
    /// the FORMAT and the track evaluation are pure and are exactly where a silent defect would
    /// cost a whole recording — so they are what is tested.
    /// </summary>
    public class TheaterRecordingTests
    {
        static TheaterRecording BuildSample()
        {
            var recording = new TheaterRecording
            {
                SceneName = "MinigameRampage",
                CellConfigName = "Rampage Cell Config 4",
                GameMode = 2,
                SampleHz = 30f,
                UtcTicks = 638000000000000000L
            };

            var a = new TheaterTrack { PlayerName = "Will", VesselType = 2, Domain = 1, IsAI = false };
            for (int i = 0; i < 64; i++)
                a.Poses.Add(new TheaterPose
                {
                    Time = 10f + i * (1f / 30f),
                    Position = new Vector3(i, i * 2f, -i),
                    Rotation = Quaternion.Euler(i, i * 3f, i * 0.5f),
                    Speed = 60f + i
                });

            // Deliberately a LATER start and an EARLIER end: a vessel that joined late and died
            // early is the case the per-sample timestamp exists to make ordinary.
            var b = new TheaterTrack { PlayerName = "Bot 1", VesselType = 11, Domain = 2, IsAI = true };
            for (int i = 0; i < 16; i++)
                b.Poses.Add(new TheaterPose
                {
                    Time = 12f + i * (1f / 30f),
                    Position = new Vector3(-i, 5f, i),
                    Rotation = Quaternion.identity,
                    Speed = 100f
                });

            recording.Tracks.Add(a);
            recording.Tracks.Add(b);
            return recording;
        }

        [Test]
        public void RoundTrip_PreservesEveryField()
        {
            var original = BuildSample();

            using var stream = new MemoryStream();
            original.Write(stream);
            stream.Position = 0;

            Assert.IsTrue(TheaterRecording.TryRead(stream, out var read, out string error),
                $"round trip failed: {error}");

            Assert.AreEqual(original.SceneName, read.SceneName);
            Assert.AreEqual(original.CellConfigName, read.CellConfigName);
            Assert.AreEqual(original.GameMode, read.GameMode);
            Assert.AreEqual(original.SampleHz, read.SampleHz);
            Assert.AreEqual(original.UtcTicks, read.UtcTicks);
            Assert.AreEqual(original.Tracks.Count, read.Tracks.Count);

            for (int t = 0; t < original.Tracks.Count; t++)
            {
                var o = original.Tracks[t];
                var r = read.Tracks[t];
                Assert.AreEqual(o.PlayerName, r.PlayerName);
                Assert.AreEqual(o.VesselType, r.VesselType);
                Assert.AreEqual(o.Domain, r.Domain);
                Assert.AreEqual(o.IsAI, r.IsAI);
                Assert.AreEqual(o.Poses.Count, r.Poses.Count);

                for (int p = 0; p < o.Poses.Count; p++)
                {
                    Assert.AreEqual(o.Poses[p].Time, r.Poses[p].Time, 1e-6f);
                    Assert.AreEqual(o.Poses[p].Position, r.Poses[p].Position);
                    Assert.AreEqual(o.Poses[p].Speed, r.Poses[p].Speed, 1e-6f);
                    Assert.AreEqual(o.Poses[p].Rotation.x, r.Poses[p].Rotation.x, 1e-6f);
                    Assert.AreEqual(o.Poses[p].Rotation.w, r.Poses[p].Rotation.w, 1e-6f);
                }
            }
        }

        [Test]
        public void Read_RefusesForeignBytes_WithAReason()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Assert.IsFalse(TheaterRecording.TryRead(stream, out _, out string error));
            Assert.IsNotNull(error);
            StringAssert.Contains("magic", error);
        }

        [Test]
        public void Read_RefusesTruncatedFile_WithoutThrowing()
        {
            using var full = new MemoryStream();
            BuildSample().Write(full);

            byte[] half = full.ToArray();
            using var truncated = new MemoryStream(half, 0, half.Length / 2);

            Assert.IsFalse(TheaterRecording.TryRead(truncated, out _, out string error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void Read_SkipsAnUnknownStream_RatherThanAborting()
        {
            // Forward compatibility is the whole reason the payload is length-prefixed: a P0 build
            // must still open a recording a P1 build made, minus the streams it cannot use.
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(TheaterRecording.Magic);
                writer.Write(TheaterRecording.Version);
                writer.Write("SomeScene");
                writer.Write("SomeCell");
                writer.Write(7);
                writer.Write(30f);
                writer.Write(123L);
                writer.Write((ushort)1);
                writer.Write((ushort)9999);            // a stream id this build has never heard of
                writer.Write(4);
                writer.Write(new byte[] { 9, 9, 9, 9 });
            }
            stream.Position = 0;

            Assert.IsTrue(TheaterRecording.TryRead(stream, out var read, out string error), error);
            Assert.AreEqual("SomeScene", read.SceneName);
            Assert.AreEqual(0, read.Tracks.Count);
        }

        [Test]
        public void Evaluate_InterpolatesBetweenSamples()
        {
            var track = new TheaterTrack();
            track.Poses.Add(new TheaterPose
            { Time = 0f, Position = Vector3.zero, Rotation = Quaternion.identity, Speed = 0f });
            track.Poses.Add(new TheaterPose
            { Time = 1f, Position = new Vector3(10f, 0f, 0f), Rotation = Quaternion.identity, Speed = 100f });

            Assert.IsTrue(track.TryEvaluate(0.5f, out Vector3 position, out _, out float speed));
            Assert.AreEqual(5f, position.x, 1e-4f);
            Assert.AreEqual(50f, speed, 1e-4f);
        }

        [Test]
        public void Evaluate_ReportsAbsenceOutsideItsOwnSpan()
        {
            // A vessel that had not spawned yet, or was already gone, is ABSENT — not frozen at an
            // endpoint. That is what lets a puppet be hidden instead of parked at a stale pose.
            var recording = BuildSample();
            var late = recording.Tracks[1];

            Assert.IsFalse(late.TryEvaluate(10.5f, out _, out _, out _), "before its first sample");
            Assert.IsFalse(late.TryEvaluate(30f, out _, out _, out _), "after its last sample");
            Assert.IsTrue(late.TryEvaluate(12.1f, out _, out _, out _), "inside its span");
        }

        [Test]
        public void Evaluate_IsOrderIndependent_SoSeekingBackwardCostsNothing()
        {
            // The claim P0 rests on: a vessel track is sampled state, so any timestamp resolves by
            // binary search with nothing to rebuild. Walking backward must give the same answers as
            // walking forward, or "snap to any point" is not true.
            var track = BuildSample().Tracks[0];
            float start = track.StartTime, end = track.EndTime;

            var forward = new Vector3[32];
            for (int i = 0; i < 32; i++)
            {
                float t = Mathf.Lerp(start, end, i / 31f);
                Assert.IsTrue(track.TryEvaluate(t, out forward[i], out _, out _));
            }

            for (int i = 31; i >= 0; i--)
            {
                float t = Mathf.Lerp(start, end, i / 31f);
                Assert.IsTrue(track.TryEvaluate(t, out Vector3 backward, out _, out _));
                Assert.AreEqual(forward[i].x, backward.x, 1e-5f, $"sample {i} differed by direction");
                Assert.AreEqual(forward[i].y, backward.y, 1e-5f, $"sample {i} differed by direction");
                Assert.AreEqual(forward[i].z, backward.z, 1e-5f, $"sample {i} differed by direction");
            }
        }

        [Test]
        public void Duration_SpansEveryTrack()
        {
            var recording = BuildSample();
            Assert.AreEqual(10f, recording.StartTime, 1e-4f);
            Assert.AreEqual(63f / 30f, recording.Duration, 1e-3f);
            Assert.AreEqual(80, recording.TotalPoseCount);
        }

        [Test]
        public void SerializedSize_MatchesWhatTheFormatActuallyWrites()
        {
            // The size report and every estimate in Docs/THEATER.md are quoted off this constant,
            // so it has to be MEASURED against the writer rather than believed.
            var one = new TheaterRecording();
            var track = new TheaterTrack { PlayerName = string.Empty };
            track.Poses.Add(new TheaterPose());
            one.Tracks.Add(track);

            var two = new TheaterRecording();
            var track2 = new TheaterTrack { PlayerName = string.Empty };
            track2.Poses.Add(new TheaterPose());
            track2.Poses.Add(new TheaterPose());
            two.Tracks.Add(track2);

            using var a = new MemoryStream();
            using var b = new MemoryStream();
            one.Write(a);
            two.Write(b);

            Assert.AreEqual(TheaterPose.SerializedSize, b.Length - a.Length,
                "one extra pose must cost exactly TheaterPose.SerializedSize bytes");
        }
    }
}
#endif
