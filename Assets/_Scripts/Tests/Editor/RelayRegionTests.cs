#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Settings > REGION: the Relay region a host's parties are served from.
    ///
    /// WHY THIS MATTERS:
    /// A party plays through its HOST's Relay allocation. With Auto, UGS picks the region closest to the host, so a US
    /// host put an India guest on a US relay at ~1200 ms (2026-11-12). The region tab lets the host choose a fair middle.
    /// What is pinned here: the choice persists and normalises; a region Relay refuses falls back to Auto exactly once
    /// (a player must never be left without a session); the region list joins UGS's regions with the measured pings,
    /// best first; and the status line tells the truth about when a change takes effect (a solo host in the menu moves on
    /// close, a guest's choice never touches the friend's party, a host with guests is never dropped).
    /// </summary>
    [TestFixture]
    public class RelayRegionTests
    {
        string _savedPref;
        bool _hadPref;

        [SetUp]
        public void SetUp()
        {
            _hadPref = PlayerPrefs.HasKey(RelayRegionPreference.PrefsKey);
            _savedPref = PlayerPrefs.GetString(RelayRegionPreference.PrefsKey, string.Empty);
            PlayerPrefs.DeleteKey(RelayRegionPreference.PrefsKey);
            RelayRegionPreference.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadPref) PlayerPrefs.SetString(RelayRegionPreference.PrefsKey, _savedPref);
            else PlayerPrefs.DeleteKey(RelayRegionPreference.PrefsKey);
            RelayRegionPreference.ResetForTests();
        }

        // ── The preference ───────────────────────────────────────────────────

        [Test]
        public void Default_IsAuto_AndAsksRelayForNoRegion()
        {
            Assert.IsTrue(RelayRegionPreference.IsAuto);
            Assert.AreEqual(string.Empty, RelayRegionPreference.RegionId);
            Assert.IsNull(RelayRegionPreference.ForSession);
        }

        [Test]
        public void Set_Normalises_Persists_AndRaisesChangedOnce()
        {
            var raised = new List<string>();
            RelayRegionPreference.Changed += raised.Add;

            RelayRegionPreference.Set("  Europe-West4 ");
            RelayRegionPreference.Set("europe-west4");   // the same choice again: no event

            Assert.AreEqual("europe-west4", RelayRegionPreference.ForSession);
            CollectionAssert.AreEqual(new[] { "europe-west4" }, raised);
            Assert.AreEqual("europe-west4", PlayerPrefs.GetString(RelayRegionPreference.PrefsKey));

            RelayRegionPreference.ResetForTests();   // a new run reads it back from PlayerPrefs
            Assert.AreEqual("europe-west4", RelayRegionPreference.RegionId);
        }

        [Test]
        public void Set_EmptyOrWhitespace_IsAuto()
        {
            RelayRegionPreference.Set("asia-south1");
            RelayRegionPreference.Set("   ");
            Assert.IsTrue(RelayRegionPreference.IsAuto);
            Assert.IsNull(RelayRegionPreference.ForSession);
        }

        [Test]
        public void HostedSession_IsStale_OnlyWhenCreatedInAnotherRegion()
        {
            Assert.IsFalse(RelayRegionPreference.HostedSessionIsStale, "no hosted session yet: nothing to move");

            RelayRegionPreference.MarkHostedSession(null);          // created on Auto
            Assert.IsFalse(RelayRegionPreference.HostedSessionIsStale);

            RelayRegionPreference.Set("europe-west4");
            Assert.IsTrue(RelayRegionPreference.HostedSessionIsStale, "Auto session, Europe chosen: it has to move");

            RelayRegionPreference.MarkHostedSession("Europe-West4");
            Assert.IsFalse(RelayRegionPreference.HostedSessionIsStale);

            RelayRegionPreference.Set(string.Empty);
            Assert.IsTrue(RelayRegionPreference.HostedSessionIsStale, "Europe session, Auto chosen: it has to move");
        }

        // ── Creating a session in the region ─────────────────────────────────

        [Test]
        public void CreateWithFallback_Auto_AsksOnceWithNoRegion()
        {
            var asked = new List<string>();
            var (session, region) = RelayRegionPreference.CreateWithFallbackAsync(r =>
            {
                asked.Add(r);
                return UniTask.FromResult("session");
            }, "test").GetAwaiter().GetResult();

            Assert.AreEqual("session", session);
            Assert.IsNull(region);
            CollectionAssert.AreEqual(new string[] { null }, asked);
        }

        [Test]
        public void CreateWithFallback_ChosenRegion_IsAskedFor()
        {
            RelayRegionPreference.Set("asia-south1");
            var asked = new List<string>();
            var (_, region) = RelayRegionPreference.CreateWithFallbackAsync(r =>
            {
                asked.Add(r);
                return UniTask.FromResult("session");
            }, "test").GetAwaiter().GetResult();

            Assert.AreEqual("asia-south1", region);
            CollectionAssert.AreEqual(new[] { "asia-south1" }, asked);
        }

        [Test]
        public void CreateWithFallback_RefusedRegion_FallsBackToAutoOnce()
        {
            RelayRegionPreference.Set("mars-north1");
            var asked = new List<string>();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("mars-north1"));
            var (session, region) = RelayRegionPreference.CreateWithFallbackAsync(r =>
            {
                asked.Add(r);
                if (r != null) throw new InvalidOperationException("region not found");
                return UniTask.FromResult("auto-session");
            }, "test").GetAwaiter().GetResult();

            Assert.AreEqual("auto-session", session);
            Assert.IsNull(region, "the session was created on Auto, and says so");
            CollectionAssert.AreEqual(new[] { "mars-north1", null }, asked);
        }

        [Test]
        public void CreateWithFallback_AutoFailure_Propagates()
        {
            Assert.Throws<InvalidOperationException>(() =>
                RelayRegionPreference.CreateWithFallbackAsync<string>(_ => throw new InvalidOperationException("down"), "test")
                    .GetAwaiter().GetResult());
        }

        [Test]
        public void CreateWithFallback_Cancellation_IsNotRetriedOnAuto()
        {
            RelayRegionPreference.Set("europe-west4");
            int calls = 0;
            Assert.Throws<OperationCanceledException>(() =>
                RelayRegionPreference.CreateWithFallbackAsync<string>(_ => { calls++; throw new OperationCanceledException(); }, "test")
                    .GetAwaiter().GetResult());
            Assert.AreEqual(1, calls);
        }

        // ── The region list ──────────────────────────────────────────────────

        [Test]
        public void Merge_MeasuredFirstByPing_ThenUnmeasuredById()
        {
            var merged = RelayRegionProbe.Merge(
                new[] { ("us-east1", "US East"), ("europe-west4", "Netherlands"), ("asia-south1", "Mumbai"), ("australia-southeast1", "Sydney") },
                new[] { ("asia-south1", 40), ("europe-west4", 140), ("us-east1", 260) });

            CollectionAssert.AreEqual(new[] { "asia-south1", "europe-west4", "us-east1", "australia-southeast1" }, merged.Select(r => r.Id));
            Assert.AreEqual(40, merged[0].PingMs);
            Assert.IsFalse(merged[3].HasPing);
            Assert.AreEqual("Sydney", merged[3].Description);
        }

        [Test]
        public void Merge_DropsDuplicatesBlanksAndUnlistedPings_KeepsBestPing()
        {
            var merged = RelayRegionProbe.Merge(
                new[] { ("Europe-West4", "first"), ("europe-west4", "second"), ("", "blank"), (null, "null") },
                new[] { ("europe-west4", 150), ("EUROPE-WEST4", 120), ("us-east1", 10), ("europe-west4", -1) });

            Assert.AreEqual(1, merged.Count, "one entry per region; an unlisted region's ping cannot be allocated in");
            Assert.AreEqual("europe-west4", merged[0].Id);
            Assert.AreEqual("first", merged[0].Description);
            Assert.AreEqual(120, merged[0].PingMs);
        }

        [Test]
        public void Merge_NullInputs_AreEmpty()
        {
            Assert.AreEqual(0, RelayRegionProbe.Merge(null, null).Count);
        }

        // ── What a change does from here ─────────────────────────────────────

        [Test]
        public void Classify_SoloHostInMenu_MovesOnClose()
        {
            Assert.AreEqual(RegionSettingsView.Status.MovesOnClose,
                RegionSettingsView.Classify(online: true, hasSession: true, isHost: true, otherMembers: 0, inMainMenu: true, hostedSessionIsStale: true));
        }

        [Test]
        public void Classify_HostWithGuests_NeverDropsThem()
        {
            Assert.AreEqual(RegionSettingsView.Status.NextHostedParty,
                RegionSettingsView.Classify(true, true, true, otherMembers: 2, inMainMenu: true, hostedSessionIsStale: true));
        }

        [Test]
        public void Classify_HostInAMatch_WaitsForTheNextParty()
        {
            Assert.AreEqual(RegionSettingsView.Status.NextHostedParty,
                RegionSettingsView.Classify(true, true, true, 0, inMainMenu: false, hostedSessionIsStale: true));
        }

        [Test]
        public void Classify_Guest_UsesTheHostsRegion()
        {
            Assert.AreEqual(RegionSettingsView.Status.FriendsParty,
                RegionSettingsView.Classify(true, true, isHost: false, otherMembers: 1, inMainMenu: true, hostedSessionIsStale: true));
        }

        [Test]
        public void Classify_AlreadyInTheChosenRegion_IsCurrent()
        {
            Assert.AreEqual(RegionSettingsView.Status.Current,
                RegionSettingsView.Classify(true, true, true, 0, true, hostedSessionIsStale: false));
        }

        [Test]
        public void Classify_OfflineOrNoSession_WaitsForTheNextParty()
        {
            Assert.AreEqual(RegionSettingsView.Status.NextHostedParty, RegionSettingsView.Classify(false, true, true, 0, true, true));
            Assert.AreEqual(RegionSettingsView.Status.NextHostedParty, RegionSettingsView.Classify(true, false, true, 0, true, true));
        }
    }
}
#endif
