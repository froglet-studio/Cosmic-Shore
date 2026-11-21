#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Core;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The replay layer's own gates: the summary sidecar, the store's naming / listing / pruning
    /// rules, and the Cloud Save index's upsert. All of it is pure (file I/O against a temporary
    /// folder, no scene), which is exactly where a silent defect would lose a player's replays -
    /// a prune that deletes the wrong files, a save that overwrites the last match.
    /// </summary>
    public class ReplayStoreTests
    {
        string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ReplayStoreTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }

        static TheaterRecording BuildRecording(long utcTicks, int poses = 30)
        {
            var recording = new TheaterRecording
            {
                SceneName = "MinigameRegatta",
                GameMode = 56,
                SampleHz = 30f,
                UtcTicks = utcTicks
            };
            var track = new TheaterTrack { PlayerName = "Yash", VesselType = 4, Domain = 1, IsAI = false };
            for (int i = 0; i < poses; i++)
                track.Poses.Add(new TheaterPose
                {
                    Time = 5f + i / 30f,
                    Position = new Vector3(i, 0f, i * 2f),
                    Rotation = Quaternion.Euler(0f, i, 0f),
                    Speed = 100f + i
                });
            recording.Tracks.Add(track);
            return recording;
        }

        static ReplaySummary BuildSummary(long utcTicks)
        {
            var summary = new ReplaySummary
            {
                GameMode = 56,
                GameModeName = "Regatta",
                SceneName = "MinigameRegatta",
                Intensity = 2,
                RecordedAtUtcTicks = utcTicks,
                DurationSeconds = 1f,
                EndedBecause = "finished",
                HasResults = true,
                WinnerName = "Yash",
                WinnerDomain = 1
            };
            summary.Pilots.Add(new ReplayPilot { Name = "Yash", Domain = 1, VesselType = 4, IsAI = false, IsLocal = true });
            summary.Pilots.Add(new ReplayPilot { Name = "Bot 1", Domain = 2, VesselType = 11, IsAI = true });
            summary.Results.Add(new ReplayResult { Rank = 1, Name = "Yash", Domain = 1, Score = 61.5f, ScoreText = "1:01.5" });
            return summary;
        }

        static readonly long T0 = new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc).Ticks;

        ReplaySummary Save(ReplayStore store, long ticks)
        {
            var summary = BuildSummary(ticks);
            Assert.IsTrue(store.TrySave(BuildRecording(ticks), summary, out _, out string error), error);
            return summary;
        }

        // ── Summary ─────────────────────────────────────────────────────────

        [Test]
        public void Summary_RoundTripsThroughJson()
        {
            var original = BuildSummary(T0);
            original.RecordingFile = "Regatta_2026-10-15_20-00.cstheater";

            Assert.IsTrue(ReplaySummary.TryParse(original.ToJson(), out var read, out string error), error);
            Assert.AreEqual(ReplaySummary.SchemaId, read.Schema);
            Assert.AreEqual(original.RecordingFile, read.RecordingFile);
            Assert.AreEqual(56, read.GameMode);
            Assert.AreEqual("Regatta", read.GameModeName);
            Assert.AreEqual(T0, read.RecordedAtUtcTicks);
            Assert.AreEqual(2, read.Pilots.Count);
            Assert.IsTrue(read.Pilots[0].IsLocal);
            Assert.IsTrue(read.Pilots[1].IsAI);
            Assert.AreEqual(11, read.Pilots[1].VesselType);
            Assert.AreEqual("1:01.5", read.Results[0].ScoreText);
            Assert.IsTrue(read.HasResults);
            Assert.IsTrue(ReplaySummary.SameReplay(original, read));
        }

        [Test]
        public void Summary_RefusesAForeignSchema()
        {
            var summary = BuildSummary(T0);
            summary.RecordingFile = "a.cstheater";
            summary.Schema = "cosmic-shore.replay-summary/99";

            Assert.IsFalse(ReplaySummary.TryParse(summary.ToJson(), out _, out string error));
            StringAssert.Contains("replay-summary/99", error);
        }

        [Test]
        public void Summary_RefusesGarbageWithoutThrowing()
        {
            Assert.IsFalse(ReplaySummary.TryParse("", out _, out _));
            Assert.IsFalse(ReplaySummary.TryParse("{ this is not json", out _, out _));
            Assert.IsFalse(ReplaySummary.TryParse("{}", out _, out _));
        }

        [TestCase("../outside.cstheater")]
        [TestCase("sub/inner.cstheater")]
        [TestCase("sub\\inner.cstheater")]
        [TestCase("C:\\Windows\\x.cstheater")]
        [TestCase("/etc/x.cstheater")]
        [TestCase("..")]
        public void Summary_RefusesARecordingOutsideItsFolder(string name)
        {
            // The name is joined to the folder to DELETE, so anything but a bare name is refused.
            var summary = BuildSummary(T0);
            summary.RecordingFile = name;
            Assert.IsFalse(ReplaySummary.TryParse(summary.ToJson(), out _, out _), name);
            Assert.IsFalse(ReplaySummary.IsBareFileName(name), name);
        }

        [Test]
        public void Summary_AcceptsAPlainFileName()
        {
            Assert.IsTrue(ReplaySummary.IsBareFileName("Regatta_2026-10-15_20-00_2.cstheater"));
        }

        // ── Store ───────────────────────────────────────────────────────────

        [Test]
        public void Save_WritesTheRecordingAndItsSummary_NamedByMode()
        {
            var store = new ReplayStore(_folder);
            var summary = BuildSummary(T0);

            Assert.IsTrue(store.TrySave(BuildRecording(T0), summary, out string path, out string error), error);

            Assert.IsTrue(File.Exists(path));
            StringAssert.StartsWith("Regatta_", summary.RecordingFile);
            StringAssert.EndsWith("." + TheaterRecording.Extension, summary.RecordingFile);
            Assert.AreEqual(Path.GetFileName(path), summary.RecordingFile);

            string sidecar = Path.Combine(_folder,
                Path.GetFileNameWithoutExtension(summary.RecordingFile) + ReplaySummary.SidecarExtension);
            Assert.IsTrue(File.Exists(sidecar), "the summary is written beside the recording");
        }

        [Test]
        public void Save_TwiceInOneMinute_KeepsBoth()
        {
            // The file name has minute precision, so two quick races share a stem; the second must
            // take _2 rather than overwrite the first match.
            var store = new ReplayStore(_folder);
            var first = Save(store, T0);
            var second = Save(store, T0 + TimeSpan.TicksPerSecond * 20);

            Assert.AreNotEqual(first.RecordingFile, second.RecordingFile);
            StringAssert.Contains("_2.", second.RecordingFile);
            Assert.AreEqual(2, store.List().Count);
        }

        [Test]
        public void Save_RefusesAnEmptyRecording()
        {
            var store = new ReplayStore(_folder);
            var empty = new TheaterRecording { UtcTicks = T0 };

            Assert.IsFalse(store.TrySave(empty, BuildSummary(T0), out _, out string error));
            Assert.AreEqual("nothing was recorded", error);
            Assert.AreEqual(0, store.List().Count);
        }

        [Test]
        public void List_IsNewestFirst_AndSkipsAReplayWhoseRecordingIsGone()
        {
            var store = new ReplayStore(_folder);
            var oldest = Save(store, T0);
            var middle = Save(store, T0 + TimeSpan.TicksPerHour);
            var newest = Save(store, T0 + TimeSpan.TicksPerHour * 2);

            File.Delete(store.RecordingPath(middle));

            var skipped = new List<string>();
            var list = store.List(skipped);

            Assert.AreEqual(2, list.Count);
            Assert.IsTrue(ReplaySummary.SameReplay(newest, list[0]));
            Assert.IsTrue(ReplaySummary.SameReplay(oldest, list[1]));
            Assert.AreEqual(1, skipped.Count, "the orphaned summary is named, not silently dropped");
        }

        [Test]
        public void Prune_KeepsTheNewest_AndNeverTouchesAHandMadeRecording()
        {
            var store = new ReplayStore(_folder);
            Directory.CreateDirectory(_folder);

            // A Theater recording made with the 9 key: a .cstheater with no summary beside it.
            string handMade = Path.Combine(_folder, "MinigameRegatta_2026-10-01_10-00." + TheaterRecording.Extension);
            using (var stream = File.Create(handMade)) BuildRecording(T0 - TimeSpan.TicksPerDay).Write(stream);

            var oldest = Save(store, T0);
            Save(store, T0 + TimeSpan.TicksPerHour);
            var newest = Save(store, T0 + TimeSpan.TicksPerHour * 2);

            int removed = store.Prune(keepNewest: 2);

            Assert.AreEqual(1, removed);
            var list = store.List();
            Assert.AreEqual(2, list.Count);
            Assert.IsTrue(ReplaySummary.SameReplay(newest, list[0]));
            Assert.IsFalse(File.Exists(store.RecordingPath(oldest)), "the oldest replay's frames went with it");
            Assert.IsTrue(File.Exists(handMade), "a recording with no summary is not a replay and is never pruned");
        }

        [Test]
        public void Delete_RemovesTheRecordingAndTheSummary()
        {
            var store = new ReplayStore(_folder);
            var keep = Save(store, T0);
            var drop = Save(store, T0 + TimeSpan.TicksPerHour);

            Assert.IsTrue(store.Delete(drop));

            Assert.IsFalse(File.Exists(store.RecordingPath(drop)));
            var list = store.List();
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(ReplaySummary.SameReplay(keep, list[0]));
        }

        [Test]
        public void LoadRecording_ReturnsTheSameFrames()
        {
            var store = new ReplayStore(_folder);
            var summary = Save(store, T0);

            Assert.IsTrue(store.TryLoadRecording(summary, out var recording, out string error), error);
            Assert.AreEqual(1, recording.Tracks.Count);
            Assert.AreEqual(30, recording.Tracks[0].Poses.Count);
            Assert.AreEqual(T0, recording.UtcTicks);
            Assert.AreEqual("Yash", recording.Tracks[0].PlayerName);
        }

        [Test]
        public void BuildStem_LeadsWithTheMode_AndCleansIt()
        {
            StringAssert.StartsWith("Regatta_", ReplayStore.BuildStem("Regatta", T0));
            StringAssert.StartsWith("Match_", ReplayStore.BuildStem("", T0));
            StringAssert.DoesNotContain("/", ReplayStore.BuildStem("Bad/Name", T0));
        }

        // ── Cloud index ─────────────────────────────────────────────────────

        [Test]
        public void Index_Upsert_ReplacesTheSameReplay_SortsNewestFirst_AndTrims()
        {
            var entries = new List<ReplaySummary>();
            ReplaySummary At(long ticks, string file)
            {
                var s = BuildSummary(ticks);
                s.RecordingFile = file;
                return s;
            }

            ReplayIndexCloudData.Upsert(entries, At(T0, "a.cstheater"), cap: 3);
            ReplayIndexCloudData.Upsert(entries, At(T0 + 2, "c.cstheater"), cap: 3);
            ReplayIndexCloudData.Upsert(entries, At(T0 + 1, "b.cstheater"), cap: 3);

            var again = At(T0 + 1, "b.cstheater");
            again.WinnerName = "updated";
            ReplayIndexCloudData.Upsert(entries, again, cap: 3);

            Assert.AreEqual(3, entries.Count, "the same replay is replaced, not added twice");
            Assert.AreEqual("c.cstheater", entries[0].RecordingFile);
            Assert.AreEqual("updated", entries[1].WinnerName);

            ReplayIndexCloudData.Upsert(entries, At(T0 + 3, "d.cstheater"), cap: 3);
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual("d.cstheater", entries[0].RecordingFile);
            Assert.IsFalse(entries.Exists(e => e.RecordingFile == "a.cstheater"), "the oldest falls off the cap");
        }

        [Test]
        public void Index_SameFileNameOnTwoDevices_IsTwoReplays()
        {
            // Names are mode + minute, so two devices can produce the same one; the start instant
            // tells them apart.
            var entries = new List<ReplaySummary>();
            var here = BuildSummary(T0);
            here.RecordingFile = "Regatta_2026-10-15_20-00.cstheater";
            var there = BuildSummary(T0 + 7);
            there.RecordingFile = here.RecordingFile;

            ReplayIndexCloudData.Upsert(entries, here, cap: 10);
            ReplayIndexCloudData.Upsert(entries, there, cap: 10);

            Assert.AreEqual(2, entries.Count);
            Assert.IsTrue(ReplayIndexCloudData.Remove(entries, here));
            Assert.AreEqual(1, entries.Count);
            Assert.IsTrue(ReplaySummary.SameReplay(there, entries[0]));
        }
    }
}
#endif
