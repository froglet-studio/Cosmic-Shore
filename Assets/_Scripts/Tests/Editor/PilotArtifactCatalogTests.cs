#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pilot and artifact compatibility (Shombith's Docs/PILOTS_AND_ARTIFACTS.md §4 requirement rule, kept): a vessel
    /// declares the keywords it satisfies, a pilot or artifact declares what it requires. Since 2026-10-27 (Garrett,
    /// Docs/Pilots/CALLSIGNS.md and ARTIFACTS.md) every pilot is one callsign on any vessel and no artifact names a
    /// class, so the catalog tests pin that shape: what is offered, what a saved slot resolves to (including the retired
    /// ids an old save carries), and every authoring error Validate names.
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

        // ---------------------------------------------------------------- the catalog

        PilotSO Pilot(string id, VesselClassType vessel, PilotVision vision, params VesselKeyword[] requires)
        {
            var pilot = ScriptableObject.CreateInstance<PilotSO>();
            pilot.name = id;
            _created.Add(pilot);

            var so = new SerializedObject(pilot);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("vision").intValue = (int)vision;
            WriteRequirement(so.FindProperty("requirement"), vessel, requires);
            so.ApplyModifiedPropertiesWithoutUndo();
            return pilot;
        }

        static readonly Element[] TrackElements = { Element.Mass, Element.Space, Element.Time, Element.Charge };

        /// <summary>An artifact as ARTIFACTS.md §3 shapes one: a payload transformation and one track per element.</summary>
        ArtifactSO Artifact(string id, VesselClassType vessel = VesselClassType.Any, bool withTransform = true,
                            params Element[] tracks)
        {
            var artifact = ScriptableObject.CreateInstance<ArtifactSO>();
            artifact.name = id;
            _created.Add(artifact);

            var so = new SerializedObject(artifact);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            WriteRequirement(so.FindProperty("requirement"), vessel, new VesselKeyword[0]);
            if (withTransform)
            {
                var transform = ScriptableObject.CreateInstance<JavelinTransformSO>();
                _created.Add(transform);
                so.FindProperty("payloadTransform").objectReferenceValue = transform;
            }
            if (tracks.Length == 0) tracks = TrackElements;
            var list = so.FindProperty("tracks");
            list.arraySize = tracks.Length;
            for (int i = 0; i < tracks.Length; i++)
            {
                var t = list.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("element").intValue = (int)tracks[i];
                t.FindPropertyRelative("label").stringValue = "reach";
                t.FindPropertyRelative("twistLine").stringValue = "It reaches further.";
                t.FindPropertyRelative("baseCost").intValue = 1;
            }
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

        // CALLSIGNS.md (Garrett, 2026-10-27): one pilot per callsign, and any vessel seats any of them.
        PilotArtifactCatalogSO CallsignSample(out PilotSO ensign, out PilotSO volt, out ArtifactSO javelin)
        {
            ensign = Pilot("pilot.callsign.ensign", VesselClassType.Any, PilotVision.Ensign);
            volt = Pilot("pilot.callsign.volt", VesselClassType.Any, PilotVision.Volt);
            javelin = Artifact("artifact.javelin");
            return Catalog(new[] { ensign, volt }, new[] { javelin, Artifact("artifact.aegis") });
        }

        [Test]
        public void PilotsFor_EveryHullIsOfferedEveryPilot_InAuthoredOrder()
        {
            var catalog = CallsignSample(out var ensign, out var volt, out _);
            var offered = new List<PilotSO> { null };   // stale contents must be cleared

            catalog.GetPilotsFor(VesselClassType.Manta, MantaKeywords, offered);
            CollectionAssert.AreEqual(new[] { ensign, volt }, offered);

            catalog.GetPilotsFor(VesselClassType.Thresher, new VesselKeyword[0], offered);
            CollectionAssert.AreEqual(new[] { ensign, volt }, offered, "a hull declaring no keywords still seats every pilot");

            catalog.GetPilotsFor(VesselClassType.Random, SparrowKeywords, offered);
            CollectionAssert.IsEmpty(offered, "a slot left on Random is offered nothing");
        }

        [Test]
        public void ArtifactsFor_EveryHullIsOfferedEveryArtifact()
        {
            var catalog = CallsignSample(out _, out _, out var javelin);
            var offered = new List<ArtifactSO>();

            catalog.GetArtifactsFor(VesselClassType.Sparrow, SparrowKeywords, offered);
            Assert.AreEqual(2, offered.Count);
            Assert.AreSame(javelin, offered[0]);

            catalog.GetArtifactsFor(VesselClassType.Butterfly, ButterflyKeywords, offered);
            Assert.AreEqual(2, offered.Count, "no vessel class appears in an artifact");
        }

        [Test]
        public void ResolvePilot_KeepsAValidPick_AndOtherwiseLeavesTheSeatEmpty()
        {
            var catalog = CallsignSample(out _, out var volt, out _);

            Assert.AreSame(volt, catalog.ResolvePilot(new SquadSlot(VesselClassType.Manta, Element.Mass, volt.Id), MantaKeywords));
            Assert.IsNull(catalog.ResolvePilot(new SquadSlot(VesselClassType.Manta, Element.Mass), MantaKeywords),
                "an empty seat flies no pilot (no sight)");
            Assert.IsNull(catalog.ResolvePilot(new SquadSlot(VesselClassType.Manta, Element.Mass, "pilot.sparrow.shrapnel"), MantaKeywords),
                "a retired named pilot's id resolves to an empty seat");
            Assert.IsNull(catalog.ResolvePilot(new SquadSlot(VesselClassType.Random, Element.Mass, volt.Id), MantaKeywords),
                "no concrete hull, no pilot");
        }

        [Test]
        public void ResolveArtifact_KeepsAValidPick_AndDropsAnythingElse()
        {
            var catalog = CallsignSample(out _, out _, out var javelin);

            Assert.AreSame(javelin, catalog.ResolveArtifact(new SquadSlot(VesselClassType.Manta, Element.Time, null, javelin.Id), MantaKeywords));
            Assert.IsNull(catalog.ResolveArtifact(new SquadSlot(VesselClassType.Sparrow, Element.Time, null, "artifact.sparrow.extra-bay"), SparrowKeywords),
                "a retired numeric artifact's id is dropped");
            Assert.IsNull(catalog.ResolveArtifact(new SquadSlot(VesselClassType.Sparrow, Element.Time), SparrowKeywords));
        }

        [Test]
        public void Validate_PassesAConsistentCatalog()
        {
            var problems = new List<string>();
            CallsignSample(out _, out _, out _).Validate(problems);
            CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Validate_NamesEachAuthoringError()
        {
            var catalog = Catalog(
                new[]
                {
                    Pilot("pilot.callsign.ensign", VesselClassType.Any, PilotVision.Ensign),
                    Pilot("pilot.callsign.ensign", VesselClassType.Any, PilotVision.Volt),       // reused id
                    Pilot("pilot.blind", VesselClassType.Any, PilotVision.None),                 // no sight
                    Pilot("pilot.random", VesselClassType.Random, PilotVision.Bandit),           // bound to Random
                    null,
                },
                new[]
                {
                    Artifact("artifact.rhino-only", VesselClassType.Rhino),                      // bound to a class
                    Artifact("artifact.bare", withTransform: false),                             // no transformation
                    Artifact("artifact.short", tracks: new[] { Element.Mass, Element.Space, Element.Time }),
                    Artifact("artifact.twin", tracks: new[] { Element.Mass, Element.Mass, Element.Time, Element.Charge }),
                    Artifact("artifact.omni", tracks: new[] { Element.Omni, Element.Space, Element.Time, Element.Charge }),
                });
            var problems = new List<string>();

            catalog.Validate(problems);
            string all = string.Join("\n", problems);

            StringAssert.Contains("reuses the id 'pilot.callsign.ensign'", all);
            StringAssert.Contains("carries no sight", all);
            StringAssert.Contains("bound to Random", all);
            StringAssert.Contains("pilots[4] is empty", all);
            StringAssert.Contains("is bound to Rhino; no vessel class appears in an artifact", all);
            StringAssert.Contains("has no payload transformation", all);
            StringAssert.Contains("has 3 element tracks, not 4", all);
            StringAssert.Contains("has two Mass tracks", all);
            StringAssert.Contains("has a Omni track", all);
        }

        [Test]
        public void Validate_RejectsATwistTheUiFontCannotDraw()
        {
            var artifact = Artifact("artifact.javelin");
            var so = new SerializedObject(artifact);
            so.FindProperty("tracks").GetArrayElementAtIndex(2).FindPropertyRelative("twistLine").stringValue = "Lingers → longer";
            so.ApplyModifiedPropertiesWithoutUndo();

            var problems = new List<string>();
            Catalog(new PilotSO[0], new[] { artifact }).Validate(problems);

            Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("U+2192", problems[0]);
        }
    }
}
#endif
