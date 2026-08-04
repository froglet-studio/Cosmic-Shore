#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Obvious.Soap;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// TournamentStandingsFormatter tests - covers the "(You)" owner tag added beside the local
    /// player's DOMAIN row, so each peer can read which team line is theirs on the Maelstrom summary
    /// (FormatFinal) and the between-game splash (FormatRunning). Scoring is per-domain, so exactly one
    /// row is ever tagged, and Domains.Blue (the no-team sentinel, never a standings row) tags nothing.
    /// The formatter is a pure function, so these need no scene.
    /// </summary>
    [TestFixture]
    public class TournamentStandingsFormatterTests
    {
        const string YouTag = "<b>(You)</b>";

        TournamentDataSO _data;
        readonly List<ScriptableEventNoParam> _events = new();

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<TournamentDataSO>();
            _data.OnTournamentStarted   = MakeEvent();
            _data.OnGameResultRecorded  = MakeEvent();
            _data.OnStandingsChanged    = MakeEvent();
            _data.OnTournamentCompleted = MakeEvent();
            _data.PointsByPlace = new List<int> { 2, 1, 0 };

            // One finished game → standings exist for Jade / Ruby / Gold.
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var e in _events) if (e) Object.DestroyImmediate(e);
            _events.Clear();
            Object.DestroyImmediate(_data);
        }

        ScriptableEventNoParam MakeEvent()
        {
            var e = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            _events.Add(e);
            return e;
        }

        static ScoreResult Row(int rank, Domains domain) =>
            new ScoreResult(rank, $"P{rank}", domain, 0f, string.Empty, null);

        static int CountTags(string text) =>
            (text.Length - text.Replace(YouTag, string.Empty).Length) / YouTag.Length;

        [Test]
        public void FormatFinal_TagsOnlyTheLocalDomainRow()
        {
            var final = TournamentStandingsFormatter.FormatFinal(_data, Domains.Ruby);

            StringAssert.Contains($"Ruby {YouTag}", final, "The local player's domain row is tagged.");
            Assert.AreEqual(1, CountTags(final), "Exactly one row is ever tagged (per-domain scoring).");
            StringAssert.DoesNotContain($"Jade {YouTag}", final);
            StringAssert.DoesNotContain($"Gold {YouTag}", final);
        }

        [Test]
        public void FormatRunning_TagsOnlyTheLocalDomainRow()
        {
            var running = TournamentStandingsFormatter.FormatRunning(_data, Domains.Jade);

            StringAssert.Contains($"Jade {YouTag}", running, "The local player's domain row is tagged.");
            Assert.AreEqual(1, CountTags(running), "Exactly one row is ever tagged.");
            StringAssert.DoesNotContain($"Ruby {YouTag}", running);
        }

        [Test]
        public void FormatConnecting_ShowsLeadingDomain()
        {
            var s = TournamentStandingsFormatter.FormatConnecting(_data, Domains.Blue);

            StringAssert.Contains("JADE", s, "Leading domain (Jade, 2 pts) is named.");
            StringAssert.Contains("leading", s);
            Assert.AreEqual(0, CountTags(s), "Blue sentinel tags nothing.");
        }

        [Test]
        public void FormatConnecting_TagsLocalLead_AndShowsUpNext()
        {
            _data.NextGameName = "Skim Race";
            _data.NextGameIntensity = 3;

            var s = TournamentStandingsFormatter.FormatConnecting(_data, Domains.Jade);

            StringAssert.Contains(YouTag, s, "Local domain (Jade) tagged when it leads.");
            StringAssert.Contains("Skim Race", s, "Up-next mode is shown.");
            StringAssert.Contains("Intensity 3", s, "Up-next intensity is shown.");
        }

        [Test]
        public void NoLocalDomain_TagsNothing()
        {
            // Default arg (Domains.Blue, the no-team sentinel) and an absent domain both tag nothing.
            Assert.AreEqual(0, CountTags(TournamentStandingsFormatter.FormatFinal(_data)),
                "Blue sentinel never matches a standings row.");
            Assert.AreEqual(0, CountTags(TournamentStandingsFormatter.FormatRunning(_data, Domains.Blue)));
        }
    }
}
#endif
