#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Minors are never penalised automatically (fair play, Threat Board R13). The device keeps the birth year the age
    /// screen accepted, and its minor rule must match the server's (treatAsMinor in Tools/UGS/CloudCode/match-report.js).
    /// </summary>
    public class FairPlayAgeTests
    {
        int _saved;
        bool _had;

        [SetUp]
        public void SaveYear()
        {
            _had = PlayerPrefs.HasKey(FairPlayAge.BirthYearPrefKey);
            _saved = PlayerPrefs.GetInt(FairPlayAge.BirthYearPrefKey, 0);
            PlayerPrefs.DeleteKey(FairPlayAge.BirthYearPrefKey);
        }

        [TearDown]
        public void RestoreYear()
        {
            if (_had) PlayerPrefs.SetInt(FairPlayAge.BirthYearPrefKey, _saved);
            else PlayerPrefs.DeleteKey(FairPlayAge.BirthYearPrefKey);
        }

        [Test]
        public void SameRuleAsTheServer()
        {
            Assert.IsTrue(FairPlayAge.TreatAsMinor(0, 2026), "unknown age is a minor");
            Assert.IsTrue(FairPlayAge.TreatAsMinor(2009, 2026), "17");
            Assert.IsTrue(FairPlayAge.TreatAsMinor(2008, 2026), "18 or still 17: the birthday is unknown");
            Assert.IsFalse(FairPlayAge.TreatAsMinor(2007, 2026));
        }

        [Test]
        public void NoYearRecorded_ReadsUnknown()
        {
            Assert.AreEqual(0, FairPlayAge.BirthYear);
            Assert.IsTrue(FairPlayAge.TreatAsMinorNow);
        }

        [Test]
        public void TheYoungestYearWins()
        {
            FairPlayAge.Record(2012);
            FairPlayAge.Record(1980);
            Assert.AreEqual(2012, FairPlayAge.BirthYear, "re-entering an older year never makes a child an adult");
            FairPlayAge.Record(2014);
            Assert.AreEqual(2014, FairPlayAge.BirthYear);
        }
    }
}
#endif
