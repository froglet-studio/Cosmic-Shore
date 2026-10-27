#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pilot and artifact compatibility (Docs/PILOTS_AND_ARTIFACTS.md §4): a vessel declares the
    /// keywords it satisfies, a pilot or artifact declares what it requires, and a squad slot is only
    /// offered what matches. The pickers and the match-start resolve read the same catalog, so these
    /// tests pin both halves: what is offered, and what a saved slot resolves to, including the stale
    /// and incompatible ids a saved squad can carry.
    /// </summary>
    [TestFixture]
    public class PilotArtifactCatalogTests
    {
        static readonly VesselKeyword[] MantaKeywords = { VesselKeyword.BasePrismProduction, VesselKeyword.CrystalBlast };
        static readonly VesselKeyword[] ButterflyKeywords = { VesselKeyword.BasePrismProduction };
        static readonly VesselKeyword[] SparrowKeywords = { VesselKeyword.BasePrismProduction, VesselKeyword.Boost, VesselKeyword.Projectile };

        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ---------------------------------------------------------------- the pure rule

        [Test]
        public void VesselKeyword_ValuesAreAStorageContract()
        {
            Assert.AreEqual(0, (int)VesselKeyword.None);
            Assert.AreEqual(1, (int)VesselKeyword.BasePrismProduction);
            Assert.AreEqual(2, (int)VesselKeyword.Boost);
            Assert.AreEqual(3, (int)VesselKeyword.Projectile);
            Assert.AreEqual(4, (int)VesselKeyword.Blade);
            Assert.AreEqual(5, (int)VesselKeyword.CrystalBlast);
            Assert.AreEqual(6, (int)VesselKeyword.Attach);
            Assert.AreEqual(7, (int)VesselKeyword.Tether);
        }

        [Test]
        public void Requirement_GeneralisedOnKeywords_IsMetByEveryHullDeclaringThem()
        {
            var shieldsTheTrail = new VesselRequirement(VesselClassType.Any, VesselKeyword.BasePrismProduction);

            Assert.IsTrue(shieldsTheTrail.IsMetBy(VesselClassType.Manta, MantaKeywords));
            Assert.IsTrue(shieldsTheTrail.IsMetBy(VesselClassType.Butterfly, ButterflyKeywords));
            Assert.IsFalse(shieldsTheTrail.IsMetBy(VesselClassType.Thresher, new VesselKeyword[0]),
                "a hull that does not declare the keyword is not offered it");
        }

        [Test]
        public void Requirement_BoundToAClass_NeedsThatClassAndItsKeywords()
        {
            var sparrowOnly = new VesselRequirement(VesselClassType.Sparrow, VesselKeyword.Boost);

            Assert.IsTrue(sparrowOnly.IsMetBy(VesselClassType.Sparrow, SparrowKeywords));
            Assert.IsFalse(sparrowOnly.IsMetBy(VesselClassType.Manta, SparrowKeywords), "wrong class, right keywords");
            Assert.IsFalse(sparrowOnly.IsMetBy(VesselClassType.Sparrow, ButterflyKeywords), "right class, keyword missing");
        }

        [Test]
        public void Requirement_IsNeverMetByAHullThatIsNotConcrete()
        {
            var anything = new VesselRequirement();
            Assert.IsTrue(anything.IsMetBy(VesselClassType.Manta, null), "no keywords needed, so a null list is fine");
            Assert.IsFalse(anything.IsMetBy(VesselClassType.Random, MantaKeywords));
            Assert.IsFalse(anything.IsMetBy(VesselClassType.Any, MantaKeywords));
        }

        [Test]
        public void Requirement_IgnoresNoneAndToleratesANullKeywordList()
        {
            var withNone = new VesselRequirement(VesselClassType.Any, VesselKeyword.None);
            Assert.IsTrue(withNone.IsMetBy(VesselClassType.Manta, null));

            var nullList = new VesselRequirement { Keywords = null };
            Assert.IsTrue(nullList.IsMetBy(VesselClassType.Manta, MantaKeywords));
        }

        [Test]
        public void ElementalBoost_IsNoneUnlessItAddsPetalsToOneOfTheFourElements()
        {
            Assert.IsTrue(default(ElementalBoost).IsNone());
            Assert.IsTrue(new ElementalBoost(Element.Mass, 0).IsNone());
            Assert.IsTrue(new ElementalBoost(Element.Omni, 2).IsNone());
            Assert.IsTrue(new ElementalBoost(Element.None, 2).IsNone());
            Assert.IsFalse(new ElementalBoost(Element.Time, 1).IsNone());
        }

        // ---------------------------------------------------------------- the catalog

        PilotSO Pilot(string id, VesselClassType vessel, bool isDefault, params VesselKeyword[] requires)
        {
            var pilot = ScriptableObject.CreateInstance<PilotSO>();
            pilot.name = id;
            _created.Add(pilot);

            var so = new SerializedObject(pilot);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("isDefault").boolValue = isDefault;
            WriteRequirement(so.FindProperty("requirement"), vessel, requires);
            so.ApplyModifiedPropertiesWithoutUndo();
            return pilot;
        }

        int _nextStat;

        ArtifactSO Artifact(string id, VesselClassType vessel, params VesselKeyword[] requires)
        {
            var artifact = ScriptableObject.CreateInstance<ArtifactSO>();
            artifact.name = id;
            _created.Add(artifact);

            var so = new SerializedObject(artifact);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            WriteRequirement(so.FindProperty("requirement"), vessel, requires);
            // 2026-10-26: an artifact names the stat it moves and reads as something at every rank.
            so.FindProperty("stat").intValue = ++_nextStat;
            var labels = so.FindProperty("rankLabels");
            labels.arraySize = ArtifactSO.RankCount;
            for (int i = 0; i < ArtifactSO.RankCount; i++) labels.GetArrayElementAtIndex(i).stringValue = "+" + (i + 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            return artifact;
        }

        static void WriteRequirement(SerializedProperty requirement, VesselClassType vessel, VesselKeyword[] requires)
        {
            // Enum properties are written by value here, not by index: VesselClassType starts at -1.
            requirement.FindPropertyRelative("Vessel").intValue = (int)vessel;
            var keywords = requirement.FindPropertyRelative("Keywords");
            keywords.arraySize = requires.Length;
            for (int i = 0; i < requires.Length; i++)
                keywords.GetArrayElementAtIndex(i).intValue = (int)requires[i];
        }

        PilotArtifactCatalogSO Catalog(PilotSO[] pilots, ArtifactSO[] artifacts)
        {
            var catalog = ScriptableObject.CreateInstance<PilotArtifactCatalogSO>();
            _created.Add(catalog);

            var so = new SerializedObject(catalog);
            var pilotList = so.FindProperty("pilots");
            pilotList.arraySize = pilots.Length;
            for (int i = 0; i < pilots.Length; i++) pilotList.GetArrayElementAtIndex(i).objectReferenceValue = pilots[i];

            var artifactList = so.FindProperty("artifacts");
            artifactList.arraySize = artifacts.Length;
            for (int i = 0; i < artifacts.Length; i++) artifactList.GetArrayElementAtIndex(i).objectReferenceValue = artifacts[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        PilotArtifactCatalogSO MilestoneSample(out PilotSO mantaDefault, out PilotSO shieldedWake, out PilotSO broadside,
                                               out PilotSO sparrowDefault, out ArtifactSO afterburner)
        {
            // Listed out of order on purpose: the default must still come first in the picker.
            shieldedWake = Pilot("pilot.manta.shielded-wake", VesselClassType.Any, false, VesselKeyword.BasePrismProduction);
            mantaDefault = Pilot("pilot.manta.default", VesselClassType.Manta, true);
            broadside = Pilot("pilot.sparrow.broadside", VesselClassType.Sparrow, false);
            sparrowDefault = Pilot("pilot.sparrow.default", VesselClassType.Sparrow, true);
            afterburner = Artifact("artifact.sparrow.afterburner-acceleration", VesselClassType.Sparrow, VesselKeyword.Boost);
            var anyBoost = Artifact("artifact.any.boost-cooldown", VesselClassType.Any, VesselKeyword.Boost);

            return Catalog(new[] { shieldedWake, mantaDefault, broadside, sparrowDefault },
                           new[] { afterburner, anyBoost });
        }

        [Test]
        public void PilotsFor_ListsTheDefaultFirst_ThenEveryCompatiblePilotInAuthoredOrder()
        {
            var catalog = MilestoneSample(out var mantaDefault, out var shieldedWake, out _, out _, out _);
            var offered = new List<PilotSO> { null };   // stale contents must be cleared

            catalog.GetPilotsFor(VesselClassType.Manta, MantaKeywords, offered);

            CollectionAssert.AreEqual(new[] { mantaDefault, shieldedWake }, offered);
        }

        [Test]
        public void PilotsFor_OffersAGeneralisedPilotOnAnotherHull_ButNotAnotherHullsOwnPilot()
        {
            var catalog = MilestoneSample(out _, out var shieldedWake, out var broadside, out var sparrowDefault, out _);
            var offered = new List<PilotSO>();

            catalog.GetPilotsFor(VesselClassType.Sparrow, SparrowKeywords, offered);
            CollectionAssert.AreEqual(new[] { sparrowDefault, shieldedWake, broadside }, offered);

            catalog.GetPilotsFor(VesselClassType.Thresher, new VesselKeyword[0], offered);
            CollectionAssert.IsEmpty(offered, "no default authored and no keywords met");

            catalog.GetPilotsFor(VesselClassType.Random, SparrowKeywords, offered);
            CollectionAssert.IsEmpty(offered, "a slot left on Random is offered nothing");
        }

        [Test]
        public void ArtifactsFor_FiltersByClassAndKeywords()
        {
            var catalog = MilestoneSample(out _, out _, out _, out _, out var afterburner);
            var offered = new List<ArtifactSO>();

            catalog.GetArtifactsFor(VesselClassType.Sparrow, SparrowKeywords, offered);
            Assert.AreEqual(2, offered.Count);
            Assert.AreSame(afterburner, offered[0]);

            catalog.GetArtifactsFor(VesselClassType.Manta, MantaKeywords, offered);
            CollectionAssert.IsEmpty(offered, "the Manta has no Boost keyword");
        }

        [Test]
        public void ResolvePilot_KeepsAValidPick_AndFallsBackToTheDefaultOtherwise()
        {
            var catalog = MilestoneSample(out var mantaDefault, out var shieldedWake, out _, out _, out _);

            var picked = new SquadSlot(VesselClassType.Manta, Element.Mass, shieldedWake.Id);
            Assert.AreSame(shieldedWake, catalog.ResolvePilot(picked, MantaKeywords));

            var none = new SquadSlot(VesselClassType.Manta, Element.Mass);
            Assert.AreSame(mantaDefault, catalog.ResolvePilot(none, MantaKeywords));

            var stale = new SquadSlot(VesselClassType.Manta, Element.Mass, "pilot.retired");
            Assert.AreSame(mantaDefault, catalog.ResolvePilot(stale, MantaKeywords), "an id that no longer exists falls back");

            var otherHulls = new SquadSlot(VesselClassType.Manta, Element.Mass, "pilot.sparrow.broadside");
            Assert.AreSame(mantaDefault, catalog.ResolvePilot(otherHulls, MantaKeywords), "a pilot this hull cannot fly falls back");

            var random = new SquadSlot(VesselClassType.Random, Element.Mass, shieldedWake.Id);
            Assert.IsNull(catalog.ResolvePilot(random, MantaKeywords), "no concrete hull, no pilot");
        }

        [Test]
        public void ResolveArtifact_KeepsAValidPick_AndDropsAnythingElse()
        {
            var catalog = MilestoneSample(out _, out _, out _, out _, out var afterburner);

            var carried = new SquadSlot(VesselClassType.Sparrow, Element.Time, null, afterburner.Id);
            Assert.AreSame(afterburner, catalog.ResolveArtifact(carried, SparrowKeywords));

            var wrongHull = new SquadSlot(VesselClassType.Manta, Element.Time, null, afterburner.Id);
            Assert.IsNull(catalog.ResolveArtifact(wrongHull, MantaKeywords));

            var stale = new SquadSlot(VesselClassType.Sparrow, Element.Time, null, "artifact.lost-to-a-pirate");
            Assert.IsNull(catalog.ResolveArtifact(stale, SparrowKeywords));

            Assert.IsNull(catalog.ResolveArtifact(new SquadSlot(VesselClassType.Sparrow, Element.Time), SparrowKeywords));
        }

        [Test]
        public void Validate_PassesAConsistentCatalog()
        {
            var catalog = MilestoneSample(out _, out _, out _, out _, out _);
            var problems = new List<string>();

            catalog.Validate(problems);

            CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Validate_NamesEachAuthoringError()
        {
            var catalog = Catalog(
                new[]
                {
                    Pilot("pilot.manta.default", VesselClassType.Manta, true),
                    Pilot("pilot.manta.default-copy", VesselClassType.Manta, true),          // second default
                    Pilot("pilot.manta.default", VesselClassType.Manta, false),              // reused id
                    Pilot("pilot.loose-default", VesselClassType.Any, true),                 // unbound default
                    Pilot("pilot.random", VesselClassType.Random, false),                    // bound to Random
                    Pilot("pilot.dolphin.default", VesselClassType.Dolphin, true, VesselKeyword.Boost), // default with keywords
                    null,
                },
                new[] { Artifact("", VesselClassType.Any, VesselKeyword.None) });           // no id, keyword None
            var problems = new List<string>();

            catalog.Validate(problems);
            string all = string.Join("\n", problems);

            StringAssert.Contains("second default pilot for Manta", all);
            StringAssert.Contains("reuses the id 'pilot.manta.default'", all);
            StringAssert.Contains("not bound to a vessel class", all);
            StringAssert.Contains("bound to Random", all);
            StringAssert.Contains("requires keywords", all);
            StringAssert.Contains("pilots[6] is empty", all);
            StringAssert.Contains("has no id", all);
            StringAssert.Contains("lists the keyword None", all);
        }

        [Test]
        public void Validate_NamesArtifactValueErrors()
        {
            var noStat = Artifact("artifact.sparrow.no-stat", VesselClassType.Sparrow);
            var first = Artifact("artifact.sparrow.first", VesselClassType.Sparrow);
            var twin = Artifact("artifact.sparrow.twin", VesselClassType.Sparrow);
            var so = new SerializedObject(noStat);
            so.FindProperty("stat").intValue = (int)ArtifactStat.None;
            so.FindProperty("rankLabels").GetArrayElementAtIndex(1).stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            so = new SerializedObject(twin);
            so.FindProperty("stat").intValue = (int)first.Stat;
            so.ApplyModifiedPropertiesWithoutUndo();

            var problems = new List<string>();
            Catalog(new PilotSO[0], new[] { noStat, first, twin }).Validate(problems);
            string all = string.Join("\n", problems);

            StringAssert.Contains("names no stat", all);
            StringAssert.Contains("which another artifact already changes", all);
            StringAssert.Contains("rank 2 label has no display name", all);
        }

        [Test]
        public void Validate_RejectsADisplayNameTheUiFontCannotDraw()
        {
            var pilot = Pilot("pilot.manta.default", VesselClassType.Manta, true);
            var so = new SerializedObject(pilot);
            so.FindProperty("displayName").stringValue = "Ace ★";
            so.ApplyModifiedPropertiesWithoutUndo();

            var problems = new List<string>();
            Catalog(new[] { pilot }, new ArtifactSO[0]).Validate(problems);

            Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("U+2605", problems[0]);
        }
    }
}
#endif
