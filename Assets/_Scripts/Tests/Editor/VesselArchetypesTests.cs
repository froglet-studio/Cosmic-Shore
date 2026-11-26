#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The fleet's one roles-and-archetypes table (<see cref="VesselArchetypesSO"/>, Docs/Vessels/ARCHETYPES.md) and the
    /// Hangar's kicker built from it (<see cref="HangarArchetypeWords"/>). Tools/Build/author_vessel_archetypes.py
    /// --check holds the table against Shombith's board offline.
    /// </summary>
    public class VesselArchetypesTests
    {
        const string AssetPath = "Assets/Resources/Vessels/VesselArchetypes.asset";

        static VesselArchetypesSO Table()
        {
            var table = AssetDatabase.LoadAssetAtPath<VesselArchetypesSO>(AssetPath);
            Assert.IsNotNull(table, AssetPath + " is missing");
            return table;
        }

        [Test]
        public void TheBoardsSecondSecondaryIsTheTertiary()
        {
            var sparrow = Table().ArchetypesOf(VesselClassType.Sparrow);
            CollectionAssert.AreEqual(new[] { VesselArchetype.Sapper, VesselArchetype.Harraser, VesselArchetype.Builder },
                sparrow);
            Assert.AreEqual(VesselRole.Destroyer, Table().RoleOf(VesselClassType.Sparrow));
            Assert.AreEqual(VesselRole.Cultivator, Table().RoleOf(VesselClassType.Squirrel));
        }

        [Test]
        public void EveryVesselHasOneToThreeDistinctArchetypes()
        {
            var table = Table();
            int count = 0;
            foreach (var vessel in table.Vessels)
            {
                var list = table.ArchetypesOf(vessel);
                Assert.That(list.Count, Is.InRange(1, 3), vessel.ToString());
                for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    Assert.AreNotEqual(list[i], list[j], vessel.ToString());
                count++;
            }
            Assert.GreaterOrEqual(count, 24);
        }

        [Test]
        public void EveryRoleAndArchetypeHasAName()
        {
            var table = Table();
            foreach (VesselRole role in System.Enum.GetValues(typeof(VesselRole)))
                if (role != VesselRole.None) Assert.IsNotEmpty(table.RoleName(role), role.ToString());
            foreach (VesselArchetype a in System.Enum.GetValues(typeof(VesselArchetype)))
                if (a != VesselArchetype.None) Assert.IsNotEmpty(table.ArchetypeName(a), a.ToString());
        }

        [Test]
        public void CountersReadBothWays()
        {
            var table = Table();
            CollectionAssert.Contains(table.Counters(VesselArchetype.Sapper), VesselArchetype.Pumper);
            CollectionAssert.Contains(table.CounteredBy(VesselArchetype.Pumper), VesselArchetype.Sapper);
            foreach (VesselArchetype a in System.Enum.GetValues(typeof(VesselArchetype)))
            foreach (var b in table.Counters(a))
                CollectionAssert.Contains(table.CounteredBy(b), a);
        }

        [Test]
        public void VesselsOfGivesTheRank()
        {
            bool found = false;
            foreach (var h in Table().VesselsOf(VesselArchetype.Builder))
                if (h.Vessel == VesselClassType.Sparrow) { Assert.AreEqual(VesselArchetypesSO.Tertiary, h.Rank); found = true; }
            Assert.IsTrue(found);
        }

        [Test]
        public void TheHangarKickerNamesTheRoleAndArchetypesPrimaryFirst()
        {
            string kicker = HangarArchetypeWords.Kicker(Table(), VesselClassType.Sparrow, "{role} · {archetypes} · {state}",
                "VESSEL · {state}", " / ", "Owned");
            Assert.AreEqual("DESTROYER · SAPPER / HARRASER / BUILDER · OWNED", kicker);
        }

        [Test]
        public void AVesselWithNoRoleKeepsTheOldKicker()
        {
            Assert.AreEqual("VESSEL · LOCKED", HangarArchetypeWords.Kicker(Table(), VesselClassType.Random,
                "{role} · {archetypes} · {state}", "VESSEL · {state}", " / ", "Locked"));
            Assert.AreEqual("VESSEL · LOCKED", HangarArchetypeWords.Kicker(null, VesselClassType.Sparrow,
                "{role} · {archetypes} · {state}", "VESSEL · {state}", " / ", "Locked"));
        }

        [Test]
        public void ABlankTemplateShowsNothing()
        {
            Assert.AreEqual(string.Empty, HangarArchetypeWords.Kicker(Table(), VesselClassType.Sparrow, "", "VESSEL · {state}",
                " / ", "Owned"));
        }
    }
}
#endif
