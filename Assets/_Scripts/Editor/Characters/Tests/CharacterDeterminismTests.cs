using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>A genome is the whole face: same genome → same mesh, across JSON, across rolls.</summary>
    public class CharacterDeterminismTests
    {
        [Test]
        public void SameSeedSameGenome()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var a = GenomeRoller.RollChimera(4242, catalog, config);
            var b = GenomeRoller.RollChimera(4242, catalog, config);
            Assert.AreEqual(a.ContentHash(), b.ContentHash());
            Assert.AreEqual(a.ToJson(false), b.ToJson(false));
            Assert.AreNotEqual(a.ContentHash(), GenomeRoller.RollChimera(4243, catalog, config).ContentHash());
        }

        [Test]
        public void JsonRoundTripReproducesTheIdenticalFace()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var g = GenomeRoller.RollChimera(77, catalog, config);
            Assert.IsTrue(CharacterGenome.TryFromJson(g.ToJson(true), out var back));
            Assert.AreEqual(g.ContentHash(), back.ContentHash());
            var m1 = CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime);
            var m2 = CharacterTestKit.Build(back, catalog, config, HeadDetail.Runtime);
            Assert.IsTrue(CharacterTestKit.VerticesEqual(m1, m2), "mesh differs after a JSON round trip");
            Assert.AreEqual(m1.Blueprint.ExpressedTraitIds, m2.Blueprint.ExpressedTraitIds);
        }

        [Test]
        public void TexturesAreDeterministic()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var g = GenomeRoller.RollHuman(9, config);
            var m1 = CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime);
            var m2 = CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime);
            var t1 = CharacterTexturePainter.Paint(m1, config, null, 128);
            var t2 = CharacterTexturePainter.Paint(m2, config, null, 128);
            for (int i = 0; i < t1.Skin.R.Length; i++)
            {
                Assert.AreEqual(t1.Skin.R[i], t2.Skin.R[i]);
                Assert.AreEqual(t1.Skin.G[i], t2.Skin.G[i]);
                Assert.AreEqual(t1.Skin.B[i], t2.Skin.B[i]);
            }
            for (int i = 0; i < t1.Eye.R.Length; i++) Assert.AreEqual(t1.Eye.R[i], t2.Eye.R[i]);
        }

        [Test]
        public void PureHumanControlUsesNoCladeContribution()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var g = GenomeRoller.RollHuman(3, config);
            Assert.IsTrue(g.IsPureHuman);
            var bp = CharacterResolver.Resolve(g, catalog, config);
            Assert.IsNull(bp.CladeA);
            Assert.IsNull(bp.CladeB);
            foreach (var id in bp.ExpressedTraitIds) Assert.IsTrue(id.StartsWith(catalog.Human.Key + "."), id);
            Assert.AreEqual(1, bp.Coverings.Count);
        }
    }
}
