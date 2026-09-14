using System;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The seam contract fails loudly, by name, on every way a generator can break it.</summary>
    public class AttachmentContractTests
    {
        static AttachmentSite SiteWithRing()
        {
            var ring = new Vector3[AttachmentContract.RingCount];
            for (int k = 0; k < ring.Length; k++) ring[k] = AttachmentContract.UnitRingPoint(k) * 0.1f + Vector3.forward * 0.4f;
            return new AttachmentSite { Name = "Test", Position = Vector3.forward * 0.4f, Normal = Vector3.forward, Right = Vector3.right, Up = Vector3.up, Radius = 0.1f, Ring = ring };
        }

        static MeshPart SeamPart(int ringCount, float jitter = 0f)
        {
            var part = new MeshPart("Synthetic", CharacterMaterialSlot.Keratin) { SeamRingCount = ringCount };
            for (int k = 0; k < ringCount; k++)
            {
                var p = AttachmentContract.UnitRingPoint(k);
                if (k == 3) p += new Vector3(jitter, 0f, 0f);
                part.AddVertex(p, Vector2.zero);
            }
            part.AddVertex(Vector3.forward, Vector2.zero);
            return part;
        }

        [Test]
        public void ACorrectRingPasses() => Assert.DoesNotThrow(() => AttachmentContract.AssertSeamFeature(SeamPart(AttachmentContract.RingCount), SiteWithRing()));

        [Test]
        public void WrongRingCountThrowsByName()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => AttachmentContract.AssertSeamFeature(SeamPart(AttachmentContract.RingCount - 1), SiteWithRing()));
            StringAssert.Contains("Synthetic", ex.Message);
        }

        [Test]
        public void RingOffTheUnitCircleThrowsWithTheVertexIndex()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => AttachmentContract.AssertSeamFeature(SeamPart(AttachmentContract.RingCount, 0.1f), SiteWithRing()));
            StringAssert.Contains("ring vertex 3", ex.Message);
        }

        [Test]
        public void EmbeddedPartsAreExemptAndSitesWithoutRingsRejectSeams()
        {
            var embedded = new MeshPart("Embedded", CharacterMaterialSlot.Skin);
            embedded.AddVertex(Vector3.one, Vector2.zero);
            Assert.DoesNotThrow(() => AttachmentContract.AssertSeamFeature(embedded, new AttachmentSite { Name = "NoRing" }));
            Assert.Throws<InvalidOperationException>(() => AttachmentContract.AssertSeamFeature(SeamPart(AttachmentContract.RingCount), new AttachmentSite { Name = "NoRing" }));
        }

        [Test]
        public void TheShippedBeakHonoursTheContractOnBothBeakedClades()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            foreach (var key in new[] { "Corvidae", "Testudines" })
            {
                if (!catalog.TryGet(key, out var clade)) continue;
                var partner = catalog.Animals().Find(c => c != clade);
                var g = GenomeRoller.RollChimera(8, catalog, config);
                g.CladeA = key; g.CladeB = partner.Key;
                g.Weights = new CharacterWeights { Human = 0.2f, CladeA = 0.6f, CladeB = 0.2f };
                var model = CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime);
                bool anyBeak = false;
                foreach (var part in model.Parts)
                    if (part.SeamRingCount > 0) { anyBeak = true; Assert.AreEqual(AttachmentContract.RingCount, part.SeamRingCount); }
                Assert.IsTrue(anyBeak, $"{key} at 0.6 placed no seam feature");
            }
        }
    }
}
