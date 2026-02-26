using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using CosmicShore.Data;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Enum Integrity Tests — Guard against Unity serialization drift.
    ///
    /// WHY THIS MATTERS:
    /// Unity serializes enums by their integer value, not their name. If someone
    /// accidentally reorders enum members or changes their numeric values, every
    /// ScriptableObject, prefab, and save file that references that enum will
    /// silently point to the wrong value. These tests lock down the exact
    /// integer ↔ name mapping so that any drift is caught immediately.
    /// </summary>
    [TestFixture]
    public class EnumIntegrityTests
    {
        #region VesselClassType

        [Test]
        public void VesselClassType_HasExpectedMemberCount()
        {
            // If someone adds or removes a vessel, this test forces them to
            // update the test suite — ensuring new vessels get tested too.
            var values = Enum.GetValues(typeof(VesselClassType));
            Assert.AreEqual(13, values.Length,
                "VesselClassType member count changed. Update tests if a vessel was added/removed.");
        }

        [Test]
        [TestCase(VesselClassType.Any, -1)]
        [TestCase(VesselClassType.Random, 0)]
        [TestCase(VesselClassType.Manta, 1)]
        [TestCase(VesselClassType.Dolphin, 2)]
        [TestCase(VesselClassType.Rhino, 3)]
        [TestCase(VesselClassType.Urchin, 4)]
        [TestCase(VesselClassType.Grizzly, 5)]
        [TestCase(VesselClassType.Squirrel, 6)]
        [TestCase(VesselClassType.Serpent, 7)]
        [TestCase(VesselClassType.Termite, 8)]
        [TestCase(VesselClassType.Falcon, 9)]
        [TestCase(VesselClassType.Shrike, 10)]
        [TestCase(VesselClassType.Sparrow, 11)]
        public void VesselClassType_HasCorrectIntegerValue(VesselClassType vessel, int expectedValue)
        {
            // Locks the serialized integer value so Unity assets don't drift.
            Assert.AreEqual(expectedValue, (int)vessel,
                $"VesselClassType.{vessel} integer value changed from {expectedValue} to {(int)vessel}. " +
                "This will break all serialized references to this vessel.");
        }

        [Test]
        public void VesselClassType_AllValuesAreUnique()
        {
            // Two enum members sharing the same int would cause ambiguous deserialization.
            var values = Enum.GetValues(typeof(VesselClassType)).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in VesselClassType.");
        }

        [Test]
        public void VesselClassType_MetaValues_AreNonPositive()
        {
            // Any and Random are meta-selectors, not real vessels.
            // They must stay at ≤ 0 so game logic can filter them out easily.
            Assert.LessOrEqual((int)VesselClassType.Any, 0);
            Assert.LessOrEqual((int)VesselClassType.Random, 0);
        }

        [Test]
        public void VesselClassType_PlayableVessels_ArePositive()
        {
            // All real vessel types must have positive IDs.
            var playable = Enum.GetValues(typeof(VesselClassType))
                .Cast<VesselClassType>()
                .Where(v => v != VesselClassType.Any && v != VesselClassType.Random);

            foreach (var vessel in playable)
            {
                Assert.Greater((int)vessel, 0,
                    $"Playable vessel {vessel} must have a positive integer value.");
            }
        }

        #endregion

        #region Domains

        [Test]
        public void Domains_HasExpectedMemberCount()
        {
            var values = Enum.GetValues(typeof(Domains));
            Assert.AreEqual(6, values.Length,
                "Domains member count changed. Update tests if a domain was added/removed.");
        }

        [Test]
        [TestCase(Domains.None, -1)]
        [TestCase(Domains.Unassigned, 0)]
        [TestCase(Domains.Jade, 1)]
        [TestCase(Domains.Ruby, 2)]
        [TestCase(Domains.Blue, 3)]
        [TestCase(Domains.Gold, 4)]
        public void Domains_HasCorrectIntegerValue(Domains domain, int expectedValue)
        {
            Assert.AreEqual(expectedValue, (int)domain,
                $"Domains.{domain} integer value changed. This will break team assignments in saved data.");
        }

        [Test]
        public void Domains_AllValuesAreUnique()
        {
            var values = Enum.GetValues(typeof(Domains)).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in Domains.");
        }

        [Test]
        public void Domains_PlayableTeams_ArePositive()
        {
            // Jade, Ruby, Blue, Gold are real teams — they must be > 0.
            Assert.Greater((int)Domains.Jade, 0);
            Assert.Greater((int)Domains.Ruby, 0);
            Assert.Greater((int)Domains.Blue, 0);
            Assert.Greater((int)Domains.Gold, 0);
        }

        #endregion

        #region GameModes

        [Test]
        public void GameModes_HasExpectedMemberCount()
        {
            var values = Enum.GetValues(typeof(GameModes));
            Assert.AreEqual(35, values.Length,
                "GameModes member count changed. Update tests if a game mode was added/removed.");
        }

        [Test]
        public void GameModes_AllValuesAreUnique()
        {
            var values = Enum.GetValues(typeof(GameModes)).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in GameModes.");
        }

        [Test]
        [TestCase(GameModes.Random, 0)]
        [TestCase(GameModes.Freestyle, 7)]
        [TestCase(GameModes.MultiplayerFreestyle, 28)]
        [TestCase(GameModes.MultiplayerCellularDuel, 29)]
        [TestCase(GameModes.Multiplayer2v2CoOpVsAI, 30)]
        [TestCase(GameModes.MultiplayerWildlifeBlitzGame, 32)]
        [TestCase(GameModes.HexRace, 33)]
        [TestCase(GameModes.MultiplayerJoust, 34)]
        [TestCase(GameModes.MultiplayerCrystalCapture, 35)]
        public void GameModes_KeyValues_AreCorrect(GameModes mode, int expectedValue)
        {
            Assert.AreEqual(expectedValue, (int)mode,
                $"GameModes.{mode} value changed. This will break saved game mode selections.");
        }

        [Test]
        public void GameModes_AllValuesAreNonNegative()
        {
            foreach (GameModes mode in Enum.GetValues(typeof(GameModes)))
            {
                Assert.GreaterOrEqual((int)mode, 0,
                    $"GameModes.{mode} has negative value {(int)mode}. Game modes should be non-negative.");
            }
        }

        [Test]
        public void GameModes_MultiplayerModes_AllContainMultiplayerInName()
        {
            // Convention check: multiplayer modes should be identifiable by name.
            var multiplayerModes = new[]
            {
                GameModes.MultiplayerFreestyle,
                GameModes.MultiplayerCellularDuel,
                GameModes.Multiplayer2v2CoOpVsAI,
                GameModes.MultiplayerWildlifeBlitzGame,
                GameModes.MultiplayerJoust,
                GameModes.MultiplayerCrystalCapture
            };

            foreach (var mode in multiplayerModes)
            {
                Assert.IsTrue(mode.ToString().Contains("Multiplayer"),
                    $"Multiplayer mode {mode} should contain 'Multiplayer' in its name.");
            }
        }

        #endregion

        #region Element

        [Test]
        public void Element_HasExpectedMemberCount()
        {
            var values = Enum.GetValues(typeof(Element));
            Assert.AreEqual(6, values.Length,
                "Element member count changed.");
        }

        [Test]
        [TestCase(Element.None, 0)]
        [TestCase(Element.Charge, 1)]
        [TestCase(Element.Mass, 2)]
        [TestCase(Element.Space, 3)]
        [TestCase(Element.Time, 4)]
        [TestCase(Element.Omni, 5)]
        public void Element_HasCorrectIntegerValue(Element element, int expectedValue)
        {
            Assert.AreEqual(expectedValue, (int)element,
                $"Element.{element} value changed from {expectedValue}. This breaks crystal and XP data.");
        }

        [Test]
        public void Element_AllValuesAreUnique()
        {
            var values = Enum.GetValues(typeof(Element)).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in Element.");
        }

        [Test]
        public void Element_CoreElements_ArePositive()
        {
            // The four core gameplay elements must be positive.
            Assert.Greater((int)Element.Charge, 0);
            Assert.Greater((int)Element.Mass, 0);
            Assert.Greater((int)Element.Space, 0);
            Assert.Greater((int)Element.Time, 0);
        }

        #endregion

        #region ShipActions

        [Test]
        [TestCase(ShipActions.Boost, 1)]
        [TestCase(ShipActions.Invulnerability, 2)]
        [TestCase(ShipActions.Drift, 16)]
        [TestCase(ShipActions.ExplosiveAcorn, 20)]
        public void ShipActions_HasCorrectIntegerValue(ShipActions action, int expectedValue)
        {
            Assert.AreEqual(expectedValue, (int)action,
                $"ShipActions.{action} value changed. This breaks action bindings in saved data.");
        }

        [Test]
        public void ShipActions_AllValuesAreUnique()
        {
            var values = Enum.GetValues(typeof(ShipActions)).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in ShipActions.");
        }

        [Test]
        public void ShipActions_AllValuesArePositive()
        {
            foreach (ShipActions action in Enum.GetValues(typeof(ShipActions)))
            {
                Assert.Greater((int)action, 0,
                    $"ShipActions.{action} should have a positive value.");
            }
        }

        #endregion

        #region ResourceType

        [Test]
        [TestCase(ResourceType.Gauge, 0)]
        [TestCase(ResourceType.Item, 1)]
        public void ResourceType_HasCorrectIntegerValue(ResourceType type, int expectedValue)
        {
            Assert.AreEqual(expectedValue, (int)type);
        }

        [Test]
        public void ResourceType_HasExpectedMemberCount()
        {
            Assert.AreEqual(2, Enum.GetValues(typeof(ResourceType)).Length);
        }

        #endregion
    }
}
