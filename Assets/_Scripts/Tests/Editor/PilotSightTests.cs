using System;
using System.IO;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pilot vision on LB (Docs/Pilots/CALLSIGNS.md §6): the seventeen authored callsigns, the four
    /// sights built first, and the rule that a sight only writes rendering state. The look itself
    /// (fade, suppression, marks) is a hand test (Docs/UNITY_VERIFICATION_CHECKLIST.md).
    /// </summary>
    public class PilotSightTests
    {
        static readonly PilotVision[] BuiltFirst =
            { PilotVision.Ensign, PilotVision.Citadel, PilotVision.Volt, PilotVision.Bandit };

        static CallsignSetSO LoadSet()
        {
            var set = Resources.Load<CallsignSetSO>("Pilots/CallsignSet");
            Assert.IsNotNull(set, "Resources/Pilots/CallsignSet is written by Tools/Build/author_callsign_assets.py");
            return set;
        }

        [Test]
        public void TheVisionValuesAreFixedOneToSeventeen()
        {
            var values = Enum.GetValues(typeof(PilotVision)).Cast<int>().OrderBy(v => v).ToArray();
            Assert.AreEqual(Enumerable.Range(0, 18).ToArray(), values, "None = 0, then the seventeen sights; Unity serializes the number");
            Assert.AreEqual(1, (int)PilotVision.Ensign);
            Assert.AreEqual(9, (int)PilotVision.Citadel, "the prism shader's CITADEL define");
            Assert.AreEqual(17, (int)PilotVision.Galleon);
        }

        [Test]
        public void EverySightHasOneAuthoredRow()
        {
            var set = LoadSet();
            Assert.AreEqual(17, set.Count);
            foreach (PilotVision sight in Enum.GetValues(typeof(PilotVision)))
            {
                if (sight == PilotVision.None) continue;
                var row = set.For(sight);
                Assert.IsNotNull(row, $"{sight} has no row");
                Assert.IsNotEmpty(row.word, $"{sight} has no word");
                Assert.IsNotEmpty(row.meaning, $"{sight} has no meaning");
                Assert.IsTrue(row.openSound.IsNull, $"{sight}'s sting ships empty (the FMOD rule)");
            }
        }

        [Test]
        public void CallsignsStartWithDistinctLetters()
        {
            var set = LoadSet();
            var firsts = Enum.GetValues(typeof(PilotVision)).Cast<PilotVision>()
                .Where(s => s != PilotVision.None).Select(s => set.For(s).word[0]).ToArray();
            Assert.AreEqual(firsts.Length, firsts.Distinct().Count(), "a callsign is read by its first letter on the plate");
        }

        [Test]
        public void NoCallsignCollidesWithAnElementVesselOrModeName()
        {
            var set = LoadSet();
            var taken = Enum.GetNames(typeof(Element))
                .Concat(Enum.GetNames(typeof(VesselClassType)))
                .Concat(Enum.GetNames(typeof(GameModes)))
                .Select(n => n.ToUpperInvariant()).ToHashSet();
            foreach (PilotVision sight in Enum.GetValues(typeof(PilotVision)))
            {
                if (sight == PilotVision.None) continue;
                Assert.IsFalse(taken.Contains(set.For(sight).word.ToUpperInvariant()),
                    $"{set.For(sight).word} is already a game name");
            }
        }

        [Test]
        public void TheFourFirstSightsHaveProviders()
        {
            foreach (var sight in BuiltFirst)
                Assert.IsNotNull(PilotSightProviders.For(sight), $"{sight} is one of the first four sights");
            Assert.IsNull(PilotSightProviders.For(PilotVision.None), "None lights nothing");
        }

        [Test]
        public void PrismSightsPublishTheirValueAndTheViewersDomain()
        {
            var state = new PilotSightPrismState();
            var ctx = new PilotSightContext(null, Domains.Ruby, 1f, null, null);
            PilotSightProviders.For(PilotVision.Ensign).Gather(ctx, ref state, new PilotSightMarks());
            Assert.AreEqual((float)PilotVision.Ensign, state.Sight);
            Assert.AreEqual((float)Domains.Ruby, state.Domain);
        }

        [Test]
        public void ASightOnlyWritesRenderingState()
        {
            // Photons only (CALLSIGNS.md §6.1): nothing reads a vision light to decide an outcome, and
            // a sight never changes the world. Scan the sight's sources for the calls that would.
            string[] forbidden = { "Damage(", "TryAdmit", "TryGrant", "TryCrack", "Silence", "Destroy(", "NetworkVariable", "Rpc" };
            foreach (var path in Directory.GetFiles("Assets/_Scripts/Controller/PilotSight", "*.cs"))
            {
                var source = File.ReadAllText(path);
                foreach (var call in forbidden)
                    Assert.IsFalse(source.Contains(call), $"{Path.GetFileName(path)} calls {call}");
            }
        }

        [Test]
        public void TheMarkMaterialAndGeneratorExist()
        {
            Assert.IsNotNull(Resources.Load<Material>("Pilots/PilotSightMark"));
            Assert.IsTrue(File.Exists("Tools/Build/author_callsign_assets.py"));
        }
    }
}
