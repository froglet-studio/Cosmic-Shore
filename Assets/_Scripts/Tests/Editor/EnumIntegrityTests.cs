#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using CosmicShore.Data;
// ScoreDifferenceSource is nested in ElementalComebackSystem, which lives in Assembly-CSharp.
// This suite is under a folder literally named Editor, so it compiles into
// Assembly-CSharp-Editor, which implicitly references the monolith - no asmdef, no wiring.
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Enum Integrity Tests - Guard against Unity serialization drift.
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
            // update the test suite - ensuring new vessels get tested too.
            var values = Enum.GetValues(typeof(VesselClassType));
            Assert.AreEqual(14, values.Length,
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
        [TestCase(VesselClassType.Scarab, 12)]
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
            Assert.AreEqual(4, values.Length,
                "Domains member count changed. Update tests if a domain was added/removed.");
        }

        [Test]
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
            // Jade, Ruby, Blue, Gold are real teams - they must be > 0.
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
            // 48 = IDs 0..50 with 7, 31 and 47 deliberately skipped (retired Freestyle /
            // never assigned / retired Drumfire — see GameModes.cs). Deliberately a hard-coded
            // number rather than one derived from the enum: the whole point is that ADDING a
            // mode fails here, so a human confirms the addition was intended and that its ID
            // reuses none of 7, 31 or 47.
            // It has drifted nine times now (33 -> 42 -> 43 -> 44 -> 45 -> 46 -> 47 -> 46 ->
            // 47 -> 48),
            // so GameModes.cs carries a pointer back to this test and the next mode can update
            // it at the source.
            var values = Enum.GetValues(typeof(GameModes));
            Assert.AreEqual(48, values.Length,
                "GameModes member count changed. Update tests if a game mode was added/removed.");
        }

        [Test]
        public void GameModes_AllValuesAreUnique()
        {
            // Routed through the shared helper rather than the inline GroupBy the other enums
            // use, because this is the enum that actually collided: it names the colliding
            // MEMBERS, which is the difference between a failure you can read and one you have
            // to go hunting for. See the "Parallel-branch value collisions" region.
            AssertNoDuplicateValues(typeof(GameModes));
        }

        [Test]
        [TestCase(GameModes.Random, 0)]
        [TestCase(GameModes.MultiplayerFreestyle, 28)]
        [TestCase(GameModes.OnlineDuelForTheCell, 29)]
        [TestCase(GameModes.Multiplayer2v2CoOpVsAI, 30)]
        [TestCase(GameModes.CoOpWildlifeBlitz, 32)]
        [TestCase(GameModes.SkimRace, 33)]
        [TestCase(GameModes.Joust, 34)]
        [TestCase(GameModes.Scurry, 35)]
        // The newest modes are pinned deliberately, not for completeness: Switchback and
        // Drumfire were authored on PARALLEL branches, both claimed 45, and git merged the two
        // additions cleanly into a file that then carried 45 twice. The member that lost its
        // explicit value in that merge took the next IMPLICIT one - a shift no reflection test
        // can see, because the compiler bakes the value in and keeps no record that it was
        // ever written down. Pinning the tail of the enum is the only thing that catches it.
        // It happened a SECOND time while Breakwater was in flight: Tollway took 48 and Headlong
        // 49 on bleeding-edge, so Breakwater moved to 50 at merge. 47 is Drumfire's grave.
        [TestCase(GameModes.Switchback, 45)]
        [TestCase(GameModes.Hijack, 46)]
        [TestCase(GameModes.Tollway, 48)]
        [TestCase(GameModes.Headlong, 49)]
        [TestCase(GameModes.Breakwater, 50)]
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
                GameModes.OnlineDuelForTheCell,
                GameModes.Multiplayer2v2CoOpVsAI,
                GameModes.CoOpWildlifeBlitz,
                GameModes.Joust,
                GameModes.Scurry
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

        #region PrismscapeDimension

        // The values ARE the dimension (0D singleton .. 3D volume) - consumers may do
        // arithmetic/ordering on them, so drift here is worse than a wrong label.
        [Test]
        [TestCase(PrismscapeDimension.Singleton, 0)]
        [TestCase(PrismscapeDimension.Trail, 1)]
        [TestCase(PrismscapeDimension.Surface, 2)]
        [TestCase(PrismscapeDimension.Volume, 3)]
        public void PrismscapeDimension_ValueIsTheDimension(PrismscapeDimension d, int expectedValue)
        {
            Assert.AreEqual(expectedValue, (int)d);
        }

        [Test]
        public void PrismscapeDimension_HasExpectedMemberCount()
        {
            Assert.AreEqual(4, Enum.GetValues(typeof(PrismscapeDimension)).Length);
        }

        #endregion

        #region Parallel-branch value collisions

        // WHY THIS REGION EXISTS:
        // Switchback and Drumfire were built on parallel branches and both claimed mode 45,
        // metric 9 and comeback source 8. git merged the two additions cleanly - a merge sees
        // two files each adding a line at a different place and has no idea the lines say the
        // same number - and the result compiled, ran, and shipped an enum carrying each value
        // twice. ScoreDifferenceSource came out worse than the other two: the member that lost
        // its explicit value in the merge silently took the next IMPLICIT one and landed on
        // Jousts = 7, so a mode's comeback layer read another mode's stat.
        //
        // The "always assign explicit values" comment at the top of each of those enums cannot
        // prevent this - both branches DID assign explicit values, and both were right in
        // isolation. Only a check over the whole enum sees it. The duplicate check below names
        // the colliding MEMBERS rather than just the value, because "duplicate value 9" sends
        // you looking through an enum by hand while "9 = SwitchesThreaded, VolumeDestroyed"
        // is the whole diagnosis.

        [Test]
        public void ScoringMetric_AllValuesAreUnique()
        {
            AssertNoDuplicateValues(typeof(ScoringMetric));
        }

        [Test]
        public void ScoreDifferenceSource_AllValuesAreUnique()
        {
            AssertNoDuplicateValues(typeof(ElementalComebackSystem.ScoreDifferenceSource));
        }

        // GameModes' own uniqueness test lives up in the GameModes region with every other
        // enum's, so it stays findable where a reader expects it; it routes through the same
        // helper for the same named diagnosis.

        /// <summary>
        /// Fails when two members of <paramref name="enumType"/> share an integer value, naming
        /// every colliding member. Reads the NAMES rather than the values, because
        /// <see cref="Enum.GetValues"/> yields one entry per member and a duplicate is only
        /// visible once you can say which two members produced it.
        /// </summary>
        static void AssertNoDuplicateValues(Type enumType)
        {
            var collisions = Enum.GetNames(enumType)
                .Select(name => new
                {
                    Name = name,
                    Value = Convert.ToInt64(Enum.Parse(enumType, name)),
                })
                .GroupBy(member => member.Value)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} = " + string.Join(", ", group.Select(m => m.Name)))
                .ToList();

            Assert.IsEmpty(collisions,
                $"Duplicate integer values in {enumType.Name} - two members serialize as the " +
                "same number, so every asset referencing either one is ambiguous:\n" +
                string.Join("\n", collisions));
        }

        #endregion
    }
}
#endif
