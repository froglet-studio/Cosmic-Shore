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
            int maxPendingVoices = 0,
            float burstMagnitudeGain = 0.3f,
            float maxBurstMagnitude = 2f) => new()
        {
            category = category,
            volumeScale = volumeScale,
            maxVoicesPerWindow = maxVoicesPerWindow,
            windowSeconds = windowSeconds,
            minRetriggerSeconds = minRetriggerSeconds,
            burstVolumeFalloff = burstVolumeFalloff,
            minBurstVolume = minBurstVolume,
            maxPendingVoices = maxPendingVoices,
            burstMagnitudeGain = burstMagnitudeGain,
            maxBurstMagnitude = maxBurstMagnitude,
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

        // ── The Dolphin case: a SUSTAINED burst ──────────────────────────────────────────────
        // An AOE blast damages at PrismSpatialIndex.MAX_NEW_HITS_PER_FRAME (48) per frame and
        // backlogs the rest, so events keep arriving every frame for the whole blast. Both tests
        // below cover ordering flaws that a simultaneous-burst test cannot see.

        [Test]
        public void SustainedBurst_DrainsTheBacklogInsteadOfStarvingIt()
        {
            // Fresh arrivals must NOT keep taking the voice slot ahead of the queue. If they do,
            // every voice speaks for exactly one prism, no voice ever carries the crowd, and the
            // aggregates just grow until the blast ends and release as a late thump.
            var limiter = LimiterFor(Policy(
                minRetriggerSeconds: 0.02f, maxVoicesPerWindow: 4, windowSeconds: 0.1f,
                maxPendingVoices: 2, burstVolumeFalloff: 1f));

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            int immediate = 0;
            const float dt = 1f / 60f;

            for (int frame = 0; frame < 20; frame++)
            {
                float t = frame * dt;
                for (int i = 0; i < 48; i++)
                    if (limiter.TryAdmit(Cat, t, true, Vector3.zero, out _)) immediate++;
                limiter.Drain(t, drained);
            }

            Assert.AreEqual(1, immediate,
                "Only the very first event may play immediately; after that a backlog exists and " +
                "must outrank fresh arrivals.");
            Assert.Greater(drained.Count, 0,
                "The backlog must actually drain DURING the burst, not only after it ends.");
            foreach (var emission in drained)
                Assert.Greater(emission.Represents, 1,
                    "Every voice released during a sustained burst should stand for many events - " +
                    "that is what carries the blast's magnitude.");
        }

        [Test]
        public void OverflowFolding_BalancesAcrossAggregates()
        {
            // Drain pops FIFO, so appending overflow to the LAST aggregate parks the whole crowd
            // behind a single-event aggregate and the first replay carries no magnitude.
            var limiter = LimiterFor(Policy(
                minRetriggerSeconds: 0.05f, maxVoicesPerWindow: 4, windowSeconds: 10f,
                maxPendingVoices: 2));

            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);           // plays immediately
            for (int i = 0; i < 100; i++)                                    // 100 suppressed
                limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            limiter.Drain(0.05f, drained);
            limiter.Drain(0.10f, drained);

            Assert.AreEqual(2, drained.Count);
            foreach (var emission in drained)
                Assert.Greater(emission.Represents, 10,
                    "Both aggregates must carry a fair share of the 100 suppressed events; a " +
                    "near-empty one means overflow piled onto a single aggregate.");
            Assert.AreEqual(100, drained[0].Represents + drained[1].Represents,
                "No suppressed event may be lost while the aggregates have room.");
        }

        // ── Magnitude ────────────────────────────────────────────────────────────────────────

        [Test]
        public void MagnitudeGain_MakesACrowdVoiceLouderThanASingleEvent()
        {
            var limiter = LimiterFor(Policy(
                volumeScale: 0.35f, minRetriggerSeconds: 0.05f, maxVoicesPerWindow: 4,
                windowSeconds: 10f, burstVolumeFalloff: 1f, maxPendingVoices: 1));

            limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out float singleVolume);
            for (int i = 0; i < 64; i++) limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            limiter.Drain(0.05f, drained);

            Assert.AreEqual(1, drained.Count);
            Assert.AreEqual(64, drained[0].Represents);
            Assert.Greater(drained[0].VolumeMultiplier, singleVolume,
                "A voice standing for 64 prisms must be louder than one standing for 1 - this is " +
                "what replaces the loudness a big burst used to get from stacking.");
        }

        [Test]
        public void MagnitudeGain_IsClamped()
        {
            var limiter = LimiterFor(Policy(
                volumeScale: 0.35f, minRetriggerSeconds: 0.05f, maxVoicesPerWindow: 4,
                windowSeconds: 10f, burstVolumeFalloff: 1f, maxPendingVoices: 1));

            for (int i = 0; i < 5000; i++) limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out _);

            var drained = new List<GameplaySFXBurstLimiter.Emission>();
            limiter.Drain(0.05f, drained);

            Assert.AreEqual(1, drained.Count);
            Assert.LessOrEqual(drained[0].VolumeMultiplier, 0.35f * 2f + 1e-4f,
                "A huge burst must not run away into clipping - maxBurstMagnitude bounds it.");
        }

        [Test]
        public void ImmediateVoice_GetsNoMagnitudeBonus()
        {
            var limiter = LimiterFor(Policy(volumeScale: 0.5f, maxPendingVoices: 2));
            Assert.IsTrue(limiter.TryAdmit(Cat, 0f, true, Vector3.zero, out float volume));
            Assert.AreEqual(0.5f, volume, 1e-4f,
                "An immediate voice represents exactly one event, so log2(1)=0 and it plays at " +
                "the plain category volume.");
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

            var centroid = drained[0].Position;
            const string why =
                "The voice plays at the acoustic centre of the burst, not at an arbitrary member.";
            Assert.AreEqual(0f, centroid.x, 1e-4f, why);
            Assert.AreEqual(10f, centroid.y, 1e-4f, why);
            Assert.AreEqual(10f, centroid.z, 1e-4f, why);
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

        // Every category a burst source is known to drive. Each entry is a real, traced burst
        // path - see Docs/AUDIO.md §1 for the sources.
        static readonly GameplaySFXCategory[] KnownBurstCategories =
        {
            GameplaySFXCategory.CrystalCollect,
            GameplaySFXCategory.CrystalSkim,
            GameplaySFXCategory.ElementChargeReceived,
            GameplaySFXCategory.ElementMassReceived,
            GameplaySFXCategory.ElementSpaceReceived,
            GameplaySFXCategory.ElementTimeReceived,
            GameplaySFXCategory.BlockDestroy,
            GameplaySFXCategory.FloraCollision,
            GameplaySFXCategory.CreatureBlockHit,
            GameplaySFXCategory.CreatureDeath,
            GameplaySFXCategory.Explosion,
            GameplaySFXCategory.MineExplode,
            GameplaySFXCategory.ShieldActivate,
            GameplaySFXCategory.ShieldDeactivate,
            GameplaySFXCategory.VesselImpact,
            GameplaySFXCategory.TrackImpact,
        };

        [Test]
        public void ShippedPolicy_GovernsEveryKnownBurstCategory()
        {
            var policy = ScriptableObject.CreateInstance<GameplaySFXPolicySO>();

            foreach (var category in KnownBurstCategories)
            {
                var entry = policy.For(category);
                Assert.Greater(entry.minRetriggerSeconds, 0f,
                    $"{category} must carry retrigger spacing - it is the decoherence lever and " +
                    $"the only one that actually stops identical one-shots comb-filtering.");
                Assert.Less(entry.maxVoicesPerWindow, 100,
                    $"{category} must carry a voice budget.");
            }
        }

        [Test]
        public void ShippedPolicy_HoldsTheUnspatializedCategoriesTightest()
        {
            // These are played 2D (no world position), so they get ZERO spatial decorrelation -
            // every copy lands dead centre on the listener and sums perfectly. They must be held
            // at least as tightly as the spatialized crystal collect.
            //
            // ShieldDeactivate is the worst of the set and is synchronized BY CONSTRUCTION:
            // PrismTimerManager drains every expired shield timer in one Update, so prisms
            // shielded together expire together.
            var policy = ScriptableObject.CreateInstance<GameplaySFXPolicySO>();
            float spatialReference = policy.For(GameplaySFXCategory.CrystalCollect).minRetriggerSeconds;

            foreach (var category in new[]
                     {
                         GameplaySFXCategory.ElementChargeReceived,
                         GameplaySFXCategory.ElementMassReceived,
                         GameplaySFXCategory.ElementSpaceReceived,
                         GameplaySFXCategory.ElementTimeReceived,
                         GameplaySFXCategory.ShieldActivate,
                         GameplaySFXCategory.ShieldDeactivate,
                     })
            {
                Assert.GreaterOrEqual(policy.For(category).minRetriggerSeconds, spatialReference,
                    $"{category} is a 2D one-shot with no spatial decorrelation, so it must be " +
                    $"spaced at least as far apart as the spatialized CrystalCollect.");
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
