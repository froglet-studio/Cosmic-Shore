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
    /// The Milestone 2 voyage classes (Frogma Rev 136 E / Rev 138 Vessels; Tools/Build/author_voyage_classes.py): the
    /// shipped asset holds the seven classes with no vessel in two, each M2 voyage kind wants three, the Crystal Run wants
    /// none, the squad fit counts wanted vessels, and each class has its own glyph.
    /// </summary>
    public class VoyageClassesTests
    {
        static VoyageClassesSO Load()
        {
            var so = VoyageClassesSO.Load();
            Assert.IsNotNull(so, "Resources/Voyages/VoyageClasses is missing: run Tools/Build/author_voyage_classes.py");
            return so;
        }

        [Test]
        public void Asset_HoldsTheSevenClasses_NoVesselInTwo()
        {
            var so = Load();
            Assert.AreEqual(7, so.Classes.Count);
            var seen = new HashSet<VesselClassType>();
            foreach (var e in so.Classes)
            {
                Assert.IsFalse(string.IsNullOrEmpty(e.Name), e.Class.ToString());
                Assert.IsNotEmpty(e.Vessels, e.Name);
                foreach (var v in e.Vessels)
                {
                    Assert.IsTrue(seen.Add(v), v + " is in two classes");
                    Assert.AreEqual(e.Class, so.ClassOf(v));
                }
            }
            Assert.AreEqual(VoyageClass.None, so.ClassOf(VesselClassType.Random));
        }

        [Test]
        public void Wants_EachMilestone2Voyage_WantsThree_CrystalRunNone()
        {
            var so = Load();
            foreach (var job in new[] { "job.salvage.drift", "escort", "job.bounty.crew", "job.defence.aid" })
                Assert.AreEqual(3, so.WantedBy(job).Count, job);
            Assert.AreEqual(0, so.WantedBy("job.raid.crystals").Count, "the Crystal Run stays as Milestone 1 built it");
            Assert.IsTrue(so.Wanted("job.salvage.drift", VoyageClass.Cultivator));
            Assert.IsFalse(so.Wanted("job.salvage.drift", VoyageClass.Guardian));
        }

        [Test]
        public void JobOf_ReadsTheTemplateFromASeatKey()
        {
            Assert.AreEqual("job.salvage.drift", VoyageClassesSO.JobOf("job.salvage.drift|pilot.x|cell.7"));
            Assert.AreEqual("escort", VoyageClassesSO.JobOf(VoyageSeatRecord.EscortKey));
            Assert.IsNull(VoyageClassesSO.JobOf(null));
        }

        [Test]
        public void Fit_CountsTheSeatsAVoyageWants()
        {
            var so = Load();
            // The default squad (Sparrow, Squirrel, Dolphin) on a Salvage: only the Squirrel (Cultivator) is wanted.
            var squad = new[] { VesselClassType.Sparrow, VesselClassType.Squirrel, VesselClassType.Dolphin };
            Assert.AreEqual(1, so.Fit("job.salvage.drift", squad));
            Assert.AreEqual(2, so.Fit("escort", squad));
            Assert.AreEqual(0, so.Fit("job.raid.crystals", squad));
        }

        [Test]
        public void Glyphs_OnePerClass()
        {
            var shapes = new HashSet<VoyageGlyph.Shape>();
            foreach (VoyageClass c in Enum.GetValues(typeof(VoyageClass)))
                if (c != VoyageClass.None) Assert.IsTrue(shapes.Add(VoyageGlyph.Of(c)), c.ToString());
            Assert.AreEqual(7, shapes.Count);
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
            Assert.IsNotNull(words, "Resources/Voyages/VoyageQuay is missing: run Tools/Build/author_voyage_classes.py");
            foreach (PilotMood m in Enum.GetValues(typeof(PilotMood)))
                if (m != PilotMood.None)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(words.MoodName(m)), m.ToString());
                    Assert.IsFalse(string.IsNullOrEmpty(words.Say(m)), m.ToString());
                }
        }
    }
}
