using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-device nose-follow dials <c>VesselTransformer.NoseFollowFraction</c> reads for a human
    /// pilot: the pad keeps the fleet lag (1.5/s, Garrett 2026-10-23), touch a short one (9/s), and the
    /// shipped assets carry the same values as the code defaults.
    /// </summary>
    [TestFixture]
    public class NoseFollowDialTests
    {
        [Test]
        public void ThePad_KeepsTheFleetLag()
        {
            var config = ScriptableObject.CreateInstance<GamepadFlightConfigSO>();
            Assert.That(config.NoseFollowPerSecond, Is.EqualTo(1.5f));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Touch_ShipsAShortLag()
        {
            var library = ScriptableObject.CreateInstance<TouchControlProfileLibrarySO>();
            Assert.That(library.NoseFollowPerSecond, Is.EqualTo(9f));
            Object.DestroyImmediate(library);
        }

        [Test]
        public void TheShippedAssets_MatchTheDefaults()
        {
            var pad = Resources.Load<GamepadFlightConfigSO>("GamepadFlightConfig");
            Assert.That(pad, Is.Not.Null, "Resources/GamepadFlightConfig is missing");
            Assert.That(pad.NoseFollowPerSecond, Is.EqualTo(1.5f));

            var touch = Resources.Load<TouchControlProfileLibrarySO>(TouchControlProfileLibrarySO.ResourcePath);
            Assert.That(touch, Is.Not.Null, "Resources/TouchControlProfiles is missing");
            Assert.That(touch.NoseFollowPerSecond, Is.EqualTo(9f));
        }
    }
}
