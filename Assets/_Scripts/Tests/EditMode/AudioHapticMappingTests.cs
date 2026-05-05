using System;
using System.Linq;
using NUnit.Framework;
using CosmicShore.Core;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Verifies that every sound effect category has a corresponding haptic mapping.
    /// If someone adds a new GameplaySFXCategory or MenuAudioCategory value, these
    /// tests force them to wire up a haptic in AudioSystem.GetHapticFor* — otherwise
    /// the new sound silently plays without a buzz.
    /// </summary>
    [TestFixture]
    public class AudioHapticMappingTests
    {
        [Test]
        public void EveryGameplaySFXCategoryHasNonNoneHaptic()
        {
            var unmapped = Enum.GetValues(typeof(GameplaySFXCategory))
                .Cast<GameplaySFXCategory>()
                .Where(c => AudioSystem.GetHapticForGameplaySFX(c) == HapticType.None)
                .ToList();

            Assert.IsEmpty(unmapped,
                $"GameplaySFXCategory values without a haptic mapping: {string.Join(", ", unmapped)}. " +
                "Add a case in AudioSystem.GetHapticForGameplaySFX.");
        }

        [Test]
        public void EveryMenuAudioCategoryHasNonNoneHaptic()
        {
            var unmapped = Enum.GetValues(typeof(MenuAudioCategory))
                .Cast<MenuAudioCategory>()
                .Where(c => AudioSystem.GetHapticForMenuAudio(c) == HapticType.None)
                .ToList();

            Assert.IsEmpty(unmapped,
                $"MenuAudioCategory values without a haptic mapping: {string.Join(", ", unmapped)}. " +
                "Add a case in AudioSystem.GetHapticForMenuAudio.");
        }

        [Test]
        public void GameplaySFXImpactCategoriesMapToImpactHaptics()
        {
            Assert.AreEqual(HapticType.ShipCollision,   AudioSystem.GetHapticForGameplaySFX(GameplaySFXCategory.VesselImpact));
            Assert.AreEqual(HapticType.MineCollision,   AudioSystem.GetHapticForGameplaySFX(GameplaySFXCategory.MineExplode));
            Assert.AreEqual(HapticType.MineCollision,   AudioSystem.GetHapticForGameplaySFX(GameplaySFXCategory.Explosion));
            Assert.AreEqual(HapticType.CrystalCollision, AudioSystem.GetHapticForGameplaySFX(GameplaySFXCategory.CrystalCollect));
        }
    }
}
