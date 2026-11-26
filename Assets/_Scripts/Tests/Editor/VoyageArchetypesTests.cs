using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Vessel roles and archetypes on voyages (Docs/Voyages/ARCHETYPES.md; Tools/Build/author_voyage_archetypes.py from
    /// Shombith's board, Docs/Vessels/archetypes.json): every rostered vessel on the board has a primary archetype, each
    /// voyage type asks for three seats and the Crystal Run for none, fit scores 1 for a primary and 0.5 for a lesser
    /// archetype with one vessel per seat, a named voyage's crew is countered on the board, and each role has a glyph.
    /// </summary>
    public class VoyageArchetypesTests
    {
        static readonly VesselClassType[] Starters =
        {
            VesselClassType.Squirrel, VesselClassType.Sparrow, VesselClassType.Dolphin, VesselClassType.Thresher,
            VesselClassType.TigerMoth, VesselClassType.Swordfish,
        };

        static VoyageArchetypesSO Load()
        {
            var so = VoyageArchetypesSO.Load();
            Assert.IsNotNull(so, "Resources/Voyages/VoyageArchetypes is missing: run Tools/Build/author_voyage_archetypes.py");
            return so;
        }

        [Test]
        public void Roles_OwnTheirThreeArchetypes()
        {
            Assert.AreEqual(VesselRole.Cultivator, VesselArchetypes.RoleOf(VesselArchetype.Burster));
            Assert.AreEqual(VesselRole.Destroyer, VesselArchetypes.RoleOf(VesselArchetype.Specialist));
            Assert.AreEqual(VesselRole.Guardian, VesselArchetypes.RoleOf(VesselArchetype.Harraser));
            Assert.AreEqual(VesselRole.None, VesselArchetypes.RoleOf(VesselArchetype.None));
        }

        [Test]
        public void Fit_PrimaryFull_LesserHalf_OneVesselPerSeat()
        {
            var seats = new[] { VesselArchetype.Pumper, VesselArchetype.Sapper, VesselArchetype.Nuke };
            var pumper = new[] { VesselArchetype.Pumper, VesselArchetype.Harraser };
            var lesserSapper = new[] { VesselArchetype.Nuke, VesselArchetype.Sapper };
            Assert.AreEqual(1f, VesselArchetypes.Meets(pumper, VesselArchetype.Pumper));
            Assert.AreEqual(0.5f, VesselArchetypes.Meets(pumper, VesselArchetype.Harraser));
            Assert.AreEqual(0f, VesselArchetypes.Meets(pumper, VesselArchetype.Nuke));
            // The second vessel is a Nuke primary and a Sapper secondary: it takes the Nuke seat (1), not the Sapper (0.5).
            Assert.AreEqual(2f, VesselArchetypes.Fit(seats, new IReadOnlyList<VesselArchetype>[] { pumper, lesserSapper }));
            Assert.AreEqual(1f, VesselArchetypes.Fit(seats, new IReadOnlyList<VesselArchetype>[] { pumper, pumper }),
                "two Pumpers still fill one Pumper seat");
            Assert.AreEqual(0f, VesselArchetypes.Fit(seats, Array.Empty<IReadOnlyList<VesselArchetype>>()));
        }

        [Test]
        public void Asset_EveryStarterHasAPrimary_AndIsAFullSeatSomewhere()
        {
            var so = Load();
            foreach (var v in Starters)
            {
                var primary = so.PrimaryOf(v);
                Assert.AreNotEqual(VesselArchetype.None, primary, v.ToString());
                bool full = false;
                foreach (var job in new[] { "job.salvage.drift", "escort", "job.bounty.crew", "job.defence.aid" })
                    foreach (var seat in so.AskOf(job))
                        if (seat == primary) full = true;
                Assert.IsTrue(full, v + "'s primary is a full seat on no voyage type");
            }
            Assert.AreEqual(VesselArchetype.Sapper, so.PrimaryOf(VesselClassType.Sparrow), "the board: Sparrow is a Sapper");
            Assert.AreEqual(VesselRole.Cultivator, so.RoleOf(VesselClassType.Squirrel), "the board: Squirrel is a Pumper");
            Assert.AreEqual(VesselArchetype.None, so.PrimaryOf(VesselClassType.Random));
        }

        [Test]
        public void Ask_EachMilestone2Voyage_AsksForThree_CrystalRunNone()
        {
            var so = Load();
            foreach (var job in new[] { "job.salvage.drift", "escort", "job.bounty.crew", "job.defence.aid" })
                Assert.AreEqual(3, so.AskOf(job).Count, job);
            Assert.AreEqual(0, so.AskOf("job.raid.crystals").Count, "the Crystal Run stays as Milestone 1 built it");
            Assert.IsTrue(so.Wanted("job.salvage.drift", null, VesselRole.Destroyer));
            Assert.IsFalse(so.Wanted("job.raid.crystals", null, VesselRole.Destroyer));
        }

        [Test]
        public void NamedVoyage_CountersItsCrew()
        {
            var so = Load();
            // The Quiet Spear lead with a Sparrow (Sapper); Builders counter Sappers on the board.
            var spear = so.VoyageAskOf("voyage.bounty.spear");
            Assert.IsNotNull(spear);
            Assert.AreEqual(VesselArchetype.Sapper, spear.Threat);
            Assert.AreEqual(VesselArchetype.Builder, spear.Countered);
            Assert.Contains(VesselArchetype.Builder, new List<VesselArchetype>(so.AskOf("job.bounty.crew", "voyage.bounty.spear")));
            Assert.Contains(VesselArchetype.Builder, so.CounteredBy(VesselArchetype.Sapper));
            Assert.IsFalse(string.IsNullOrEmpty(so.CounterReason("voyage.bounty.spear")));
            // A hold names its retake crew; the first hold meets none and keeps its type's ask.
            Assert.IsNotEmpty(so.VoyageAskOf("voyage.hold.ironmouth").CrewHulls);
            Assert.IsNull(so.VoyageAskOf("voyage.hold.verdigris"));
        }

        [Test]
        public void Fit_DefaultSquad_AndPay()
        {
            var so = Load();
            // The default squad (Sparrow, Squirrel, Dolphin) on a Salvage (Nuke, Sapper, Harraser): Dolphin Nuke 1,
            // Sparrow Sapper 1, Squirrel Harraser 0.5.
            var squad = new[] { VesselClassType.Sparrow, VesselClassType.Squirrel, VesselClassType.Dolphin };
            Assert.AreEqual(2.5f, so.Fit("job.salvage.drift", null, squad));
            Assert.AreEqual(0f, so.Fit("job.raid.crystals", null, squad));
            Assert.AreEqual(100, so.FitPay(100, 0f));
            Assert.Greater(so.FitPay(100, 3f), so.FitPay(100, 1f));
            Assert.AreEqual("2.5", VoyageArchetypesSO.FitText(2.5f));
            Assert.AreEqual("2", VoyageArchetypesSO.FitText(2f));
        }

        [Test]
        public void JobOf_ReadsTheTemplateFromASeatKey()
        {
            Assert.AreEqual("job.salvage.drift", VoyageArchetypesSO.JobOf("job.salvage.drift|pilot.x|cell.7"));
            Assert.AreEqual("escort", VoyageArchetypesSO.JobOf(VoyageSeatRecord.EscortKey));
            Assert.IsNull(VoyageArchetypesSO.JobOf(null));
        }

        [Test]
        public void Glyphs_OnePerRole()
        {
            var shapes = new HashSet<VoyageGlyph.Shape>();
            foreach (VesselRole r in Enum.GetValues(typeof(VesselRole)))
                if (r != VesselRole.None) Assert.IsTrue(shapes.Add(VoyageGlyph.Of(r)), r.ToString());
            Assert.AreEqual(3, shapes.Count);
            Assert.AreEqual(VoyageGlyph.Of(VesselRole.Destroyer), VoyageGlyph.Of(VesselArchetype.Nuke));
        }

        [Test]
        public void HasRole_MatchesTheRolesTheAiFlies()
        {
            foreach (VesselClassType v in Enum.GetValues(typeof(VesselClassType)))
                Assert.AreEqual(ArkEscortRoles.Create(v) is not HeartChaserRole, ArkEscortRoles.HasRole(v), v.ToString());
        }

        [Test]
        public void Mood_ReadsTheFactionAgainstTheTypesLists()
        {
            var info = new VoyageTypeInfo
            {
                Type = "job.salvage.drift",
                Keen = new List<FactionType> { (FactionType)4 },
                Reluctant = new List<FactionType> { (FactionType)3 },
                Refuse = new List<FactionType> { (FactionType)7 },
            };
            Assert.AreEqual(PilotMood.Keen, VoyageMood.Of(info, (FactionType)4));
            Assert.AreEqual(PilotMood.Reluctant, VoyageMood.Of(info, (FactionType)3));
            Assert.AreEqual(PilotMood.Refuses, VoyageMood.Of(info, (FactionType)7));
            Assert.AreEqual(PilotMood.Steady, VoyageMood.Of(info, (FactionType)1));
            Assert.AreEqual(PilotMood.None, VoyageMood.Of(info, FactionType.None), "no faction, no mood");
            Assert.AreEqual(PilotMood.None, VoyageMood.Of(null, (FactionType)4), "no giver (the Crystal Run), no mood");
            Assert.AreEqual(3, VoyageMoodMark.Bars(PilotMood.Keen));
            Assert.AreEqual(0, VoyageMoodMark.Bars(PilotMood.Refuses));
        }

        [Test]
        public void QuayWords_LoadWithEveryMood()
        {
            var words = VoyageQuaySO.Load();
            Assert.IsNotNull(words, "Resources/Voyages/VoyageQuay is missing: run Tools/Build/author_voyage_archetypes.py");
            foreach (PilotMood m in Enum.GetValues(typeof(PilotMood)))
                if (m != PilotMood.None)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(words.MoodName(m)), m.ToString());
                    Assert.IsFalse(string.IsNullOrEmpty(words.Say(m)), m.ToString());
                }
        }
    }
}
