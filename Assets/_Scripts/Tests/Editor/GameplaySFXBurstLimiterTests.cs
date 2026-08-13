using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Covers the burst gate every gameplay SFX one-shot passes through
    /// (<see cref="GameplaySFXBurstLimiter"/>). The limiter takes "now" as a parameter and
    /// touches no Unity object, so the whole decision surface is testable here — which matters,
    /// because the bug it fixes (dozens of identical one-shots started on one frame, summing
    /// coherently into a phased wall and burning FMOD voices) only reproduces under a burst that
    /// is awkward to stage by hand in play mode.
    /// </summary>
    public class GameplaySFXBurstLimiterTests
    {
        const GameplaySFXCategory Cat = GameplaySFXCategory.CrystalCollect;
        const GameplaySFXCategory Other = GameplaySFXCategory.BlockDestroy;

        static GameplaySFXCategoryPolicy Policy(
            GameplaySFXCategory category = Cat,
            float volumeScale = 1f,
            int maxVoicesPerWindow = 3,
            float windowSeconds = 0.1f,
            float minRetriggerSeconds = 0.04f,
            float burstVolumeFalloff = 1f,
            float minBurstVolume = 0f,
            int maxPendingVoices = 0) => new()
        {
            category = category,
            volumeScale = volumeScale,
            maxVoicesPerWindow = maxVoicesPerWindow,
            windowSeconds = windowSeconds,
            minRetriggerSeconds = minRetriggerSeconds,
            burstVolumeFalloff = burstVolumeFalloff,
            minBurstVolume = minBurstVolume,
            maxPendingVoices = maxPendingVoices,
        };

        // A limiter fed a policy asset carrying exactly `policy`. Built through the real SO so
        // the production lookup path (GameplaySFXPolicySO.For, including its unlisted-category
        // fallback) is what the tests exercise.
        static GameplaySFXBurstLimiter LimiterFor(GameplaySFXCategoryPolicy policy) =>
            new(GameplaySFXPolicySO.Create(policy));

        // ── The bug: a frame full of identical events ────────────────────────────────────────

        [Test]
        public void SimultaneousBurst_AdmitsExactlyOneVoice()
        {
            // 30 crystals destroyed on the same frame all report the same timestamp. Only the
            // leading edge may start a voice - everything else would sum coherently with it.
            var limiter = LimiterFor(Policy(minRetriggerSeconds: 0.04f, maxVoicesPerWindow: 3));

            int admitted = 0;
            for (int i = 0; i < 30; i++)
                if (limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _)) admitted++;

            Assert.AreEqual(1, admitted,
                "A same-frame burst must start exactly one voice; the rest are what phase into noise.");
            Assert.AreEqual(29, limiter.SuppressedCount);
        }

        [Test]
        public void LeadingEdge_IsNeverDelayed()
        {
            var limiter = LimiterFor(Policy());
            Assert.IsTrue(limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out float volume),
                "The first event of a category must always play immediately - the sound stays responsive.");
            Assert.AreEqual(1f, volume, 1e-4f);
        }

        [Test]
        public void RetriggerSpacing_BlocksUntilTheIntervalElapses()
        {
            var limiter = LimiterFor(Policy(minRetriggerSeconds: 0.05f, maxVoicesPerWindow: 99));

            Assert.IsTrue(limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _));
            Assert.IsFalse(limiter.TryAdmit(Cat, 0.049f, true, Vector3.zero, out _),
                "Inside the retrigger interval the voices would comb-filter.");
            Assert.IsTrue(limiter.TryAdmit(Cat, 0.05f, true, Vector3.zero, out _),
                "At the interval the next voice is decorrelated enough to play.");
        }

        [Test]
        public void WindowBudget_CapsVoicesEvenWhenSpacingIsMet()
        {
            var limiter = LimiterFor(Policy(
                maxVoicesPerWindow: 3, windowSeconds: 1f, minRetriggerSeconds: 0f));

            for (int i = 0; i < 3; i++)
                Assert.IsTrue(limiter.TryAdmit(Cat, i * 0.1f, true, Vector3.zero, out _), $"voice {i}");

            Assert.IsFalse(limiter.TryAdmit(Cat, 0.3f, true, Vector3.zero, out _),
                "The window budget is the FMOD-voice ceiling and must hold.");
        }

        [Test]
        public void WindowBudget_RefreshesAfterTheWindowElapses()
        {
            var limiter = LimiterFor(Policy(
                maxVoicesPerWindow: 2, windowSeconds: 0.1f, minRetriggerSeconds: 0f));

            Assert.IsTrue(limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _));
            Assert.IsTrue(limiter.TryAdmit(Cat, 0.01f, true, Vector3.zero, out _));
            Assert.IsFalse(limiter.TryAdmit(Cat, 0.02f, true, Vector3.zero, out _));
            Assert.IsTrue(limiter.TryAdmit(Cat, 0.5f, true, Vector3.zero, out _),
                "A later burst must not be punished for an earlier one.");
        }

        // ── Categories are independent ───────────────────────────────────────────────────────

        [Test]
        public void CategoriesDoNotShareBudget()
        {
            var limiter = LimiterFor(Policy(maxVoicesPerWindow: 1, minRetriggerSeconds: 1f));

            Assert.IsTrue(limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _));
            Assert.IsFalse(limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _));
            Assert.IsTrue(limiter.TryAdmit(Other, 0f, true, Vector3.zero, out _),
                "Throttling crystals must not silence prisms - each category has its own gate.");
        }

        [Test]
        public void UnlistedCategory_IsUnthrottled()
        {
            // Categories nobody has identified as bursty must behave exactly as they did before
            // the policy existed.
            var limiter = LimiterFor(Policy(category: Cat));

            for (int i = 0; i < 50; i++)
                Assert.IsTrue(limiter.TryAdmit(Other, 0f, true, Vector3.zero, out _), $"event {i}");
        }

        // ── Volume falloff ───────────────────────────────────────────────────────────────────

        [Test]
        public void VolumeScale_AppliesToTheFirstVoice()
        {
            var limiter = LimiterFor(Policy(volumeScale: 0.35f));
            Assert.IsTrue(limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out float volume));
            Assert.AreEqual(0.35f, volume, 1e-4f);
        }

        [Test]
        public void SuccessiveVoicesInAWindow_GetQuieter()
        {
            var limiter = LimiterFor(Policy(
                volumeScale: 1f, burstVolumeFalloff: 0.5f, minBurstVolume: 0f,
                minRetriggerSeconds: 0f, maxVoicesPerWindow: 4, windowSeconds: 10f));

            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out float v0);
            limiter.TryAdmit(Cat, 0.1f, true, Vector3.zero, out float v1);
            limiter.TryAdmit(Cat, 0.2f, true, Vector3.zero, out float v2);

            Assert.AreEqual(1f, v0, 1e-4f);
            Assert.AreEqual(0.5f, v1, 1e-4f);
            Assert.AreEqual(0.25f, v2, 1e-4f);
        }

        [Test]
        public void BurstVolumeFalloff_IsFloored()
        {
            var limiter = LimiterFor(Policy(
                volumeScale: 1f, burstVolumeFalloff: 0.5f, minBurstVolume: 0.4f,
                minRetriggerSeconds: 0f, maxVoicesPerWindow: 8, windowSeconds: 10f));

            float last = 1f;
            for (int i = 0; i < 8; i++)
            {
                limiter.TryAdmit(Cat, i * 0.01f, true, Vector3.zero, out last);
            }

            Assert.GreaterOrEqual(last, 0.4f - 1e-4f,
                "A long burst must not decay to inaudible - the floor keeps late hits legible.");
        }

        // ── Coalescing: the burst keeps its magnitude ────────────────────────────────────────

        [Test]
        public void BlockedEvents_AreReplayedSpacedOut_WhenCoalescingIsOn()
        {
            var limiter = LimiterFor(Policy(
                minRetriggerSeconds: 0.05f, maxVoicesPerWindow: 4, windowSeconds: 1f,
                maxPendingVoices: 2));

            for (int i = 0; i < 20; i++) limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);

            var drained = new List<GameplaySFXBurstLimiter.Emission>();

            limiter.Drain(0f, drained);
            Assert.AreEqual(0, drained.Count, "Nothing may drain before the spacing has elapsed.");

            limiter.Drain(0.05f, drained);
            Assert.AreEqual(1, drained.Count, "One pending aggregate per drain - spacing is the point.");

            limiter.Drain(0.06f, drained);
            Assert.AreEqual(1, drained.Count, "Still inside the interval; nothing more may release.");

            limiter.Drain(0.1f, drained);
            Assert.AreEqual(2, drained.Count, "The second aggregate releases once spaced.");

            limiter.Drain(0.5f, drained);
            Assert.AreEqual(2, drained.Count, "Pending is capped at maxPendingVoices - it cannot grow.");
        }

        [Test]
        public void Coalescing_TotalVoicesForABurstIsBounded()
        {
            var limiter = LimiterFor(Policy(
                minRetriggerSeconds: 0.045f, maxVoicesPerWindow: 3, windowSeconds: 0.12f,
                maxPendingVoices: 2));

            int voices = 0;
            for (int i = 0; i < 200; i++)
                if (limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _)) voices++;

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            for (float t = 0f; t < 2f; t += 1f / 60f) limiter.Drain(t, drained);
            voices += drained.Count;

            Assert.AreEqual(3, voices,
                "200 simultaneous crystal deaths must resolve to 1 immediate voice + 2 spaced " +
                "reinforcements, not 200 stacked copies.");
        }

        [Test]
        public void PendingAggregate_EmitsAtTheCentroidOfWhatItRepresents()
        {
            var limiter = LimiterFor(Policy(
                minRetriggerSeconds: 0.05f, maxVoicesPerWindow: 4, windowSeconds: 1f,
                maxPendingVoices: 1));

            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);          // plays immediately
            limiter.TryAdmit(Cat, 0f, true, new Vector3(10f, 0f, 0f), out _);
            limiter.TryAdmit(Cat, 0f, true, new Vector3(-10f, 0f, 20f), out _);
            limiter.TryAdmit(Cat, 0f, true, new Vector3(0f, 30f, 10f), out _);

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            limiter.Drain(0.05f, drained);

            Assert.AreEqual(1, drained.Count);
            Assert.IsTrue(drained[0].Spatial);
            Assert.AreEqual(3, drained[0].Represents,
                "The replayed voice stands for all three suppressed events.");
            Assert.AreEqual(new Vector3(0f, 10f, 10f), drained[0].Position,
                "The voice plays at the acoustic centre of the burst, not at an arbitrary member.");
        }

        [Test]
        public void NonSpatialEvents_StayNonSpatial()
        {
            // The elemental receive stingers are played 2D. A coalesced replay must not suddenly
            // become a 3D voice at the world origin.
            var limiter = LimiterFor(Policy(
                minRetriggerSeconds: 0.05f, maxVoicesPerWindow: 4, windowSeconds: 1f,
                maxPendingVoices: 1));

            limiter.TryAdmit(Cat, 0f, false, Vector3.zero, out _);
            limiter.TryAdmit(Cat, 0f, false, Vector3.zero, out _);
            limiter.TryAdmit(Cat, 0f, false, Vector3.zero, out _);

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            limiter.Drain(0.05f, drained);

            Assert.AreEqual(1, drained.Count);
            Assert.IsFalse(drained[0].Spatial);
            Assert.AreEqual(2, drained[0].Represents);
        }

        [Test]
        public void ZeroPending_DropsOutright()
        {
            // maxPendingVoices = 0 is the older pure-throttle behaviour.
            var limiter = LimiterFor(Policy(minRetriggerSeconds: 0.05f, maxPendingVoices: 0));

            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);
            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            limiter.Drain(1f, drained);

            Assert.AreEqual(0, drained.Count);
            Assert.AreEqual(1, limiter.DroppedCount);
        }

        [Test]
        public void Drain_OnAQuietFrame_IsANoOp()
        {
            var limiter = LimiterFor(Policy(maxPendingVoices: 2));
            var drained = new List<GameplaySFXBurstLimiter.Emission>();

            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);
            for (float t = 0f; t < 1f; t += 1f / 60f) limiter.Drain(t, drained);

            Assert.AreEqual(0, drained.Count);
        }

        [Test]
        public void Reset_ClearsWindowSpacingAndPending()
        {
            var limiter = LimiterFor(Policy(minRetriggerSeconds: 1f, maxPendingVoices: 2));

            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);
            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);
            Assert.AreEqual(1, limiter.SuppressedCount);

            limiter.Reset();

            Assert.AreEqual(0, limiter.SuppressedCount);
            Assert.IsTrue(limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _),
                "After a reset the category behaves as if it had never played.");

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            limiter.Drain(5f, drained);
            Assert.AreEqual(0, drained.Count, "Reset must drop pending aggregates too.");
        }

        // ── Policy asset ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ShippedPolicy_ThrottlesEveryCrystalCategory()
        {
            var policy = ScriptableObject.CreateInstance<GameplaySFXPolicySO>();

            foreach (var category in new[]
                     {
                         GameplaySFXCategory.CrystalCollect,
                         GameplaySFXCategory.CrystalSkim,
                         GameplaySFXCategory.ElementChargeReceived,
                         GameplaySFXCategory.ElementMassReceived,
                         GameplaySFXCategory.ElementSpaceReceived,
                         GameplaySFXCategory.ElementTimeReceived,
                     })
            {
                var entry = policy.For(category);
                Assert.Greater(entry.minRetriggerSeconds, 0f,
                    $"{category} must carry retrigger spacing - it is the decoherence lever and " +
                    $"the whole reason a crystal shower stopped phasing.");
                Assert.Less(entry.maxVoicesPerWindow, 100,
                    $"{category} must carry a voice budget.");
            }
        }

        [Test]
        public void ShippedPolicy_LeavesUnlistedCategoriesPermissive()
        {
            var policy = ScriptableObject.CreateInstance<GameplaySFXPolicySO>();
            var entry = policy.For(GameplaySFXCategory.GameEnd);

            Assert.AreEqual(0f, entry.minRetriggerSeconds, 1e-6f);
            Assert.AreEqual(1f, entry.volumeScale, 1e-6f);
        }

        [Test]
        public void ShippedPolicy_PreservesTheLegacyBlockDestroyTuning()
        {
            // The hand-rolled throttle this policy replaced ran BlockDestroy at 0.35 volume,
            // 4 voices per 0.1s. That tuning must survive the migration.
            var entry = ScriptableObject.CreateInstance<GameplaySFXPolicySO>()
                .For(GameplaySFXCategory.BlockDestroy);

            Assert.AreEqual(0.35f, entry.volumeScale, 1e-4f);
            Assert.AreEqual(4, entry.maxVoicesPerWindow);
            Assert.AreEqual(0.1f, entry.windowSeconds, 1e-4f);
        }

        [Test]
        public void ShippedPolicy_HasNoDuplicateCategories()
        {
            var policy = ScriptableObject.CreateInstance<GameplaySFXPolicySO>();
            var seen = new HashSet<GameplaySFXCategory>();

            foreach (var entry in policy.CategoryPolicies)
                Assert.IsTrue(seen.Add(entry.category),
                    $"GameplaySFXCategory.{entry.category} is listed more than once; the later " +
                    $"entry would silently never apply.");
        }
    }
}
