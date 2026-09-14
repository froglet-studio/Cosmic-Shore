using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// "Weight buys traits, and the signature is bought first": every clade's signature is
    /// expressed at the MINIMUM weight, whichever partner it is paired with (A wins ties, so the
    /// clade under test is placed as A). Also that a clade whose signature COLLIDES with a heavier
    /// partner's still shows through a second signature — the Corvidae/Testudines beak case.
    /// </summary>
    public class CladeSignatureTests
    {
        [Test]
        public void EverySignatureIsPresentAtMinimumWeight()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var animals = catalog.Animals();
            foreach (var clade in animals)
            {
                var sigs = clade.Signatures().Select(t => $"{clade.Key}.{t.Id}").ToList();
                Assert.IsNotEmpty(sigs, $"{clade.Key} declares no signature");
                foreach (var partner in animals)
                {
                    if (partner == clade) continue;
                    var g = GenomeRoller.RollChimera(11, catalog, config);
                    g.CladeA = clade.Key; g.CladeB = partner.Key;
                    g.Weights = new CharacterWeights { Human = CharacterWeights.Max, CladeA = CharacterWeights.Min, CladeB = CharacterWeights.Min };
                    var bp = CharacterResolver.Resolve(g, catalog, config);
                    foreach (var s in sigs)
                        Assert.Contains(s, bp.ExpressedTraitIds, $"{clade.Key} at {CharacterWeights.Min} beside {partner.Key}: signature '{s}' missing; expressed: {string.Join(", ", bp.ExpressedTraitIds)}");
                }
            }
        }

        [Test]
        public void ALighterCladeStillShowsASignatureWhenItsFirstOneCollides()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var animals = catalog.Animals();
            foreach (var light in animals)
                foreach (var heavy in animals)
                {
                    if (light == heavy) continue;
                    var g = GenomeRoller.RollChimera(5, catalog, config);
                    g.CladeA = light.Key; g.CladeB = heavy.Key;
                    g.Weights = new CharacterWeights { Human = CharacterWeights.Min, CladeA = CharacterWeights.Min, CladeB = CharacterWeights.Max };
                    var bp = CharacterResolver.Resolve(g, catalog, config);
                    bool any = light.Signatures().Any(t => bp.ExpressedTraitIds.Contains($"{light.Key}.{t.Id}"));
                    Assert.IsTrue(any, $"{light.Key} at {CharacterWeights.Min} under {heavy.Key} at {CharacterWeights.Max} shows no signature at all: {string.Join(", ", bp.ExpressedTraitIds)}");
                }
        }

        [Test]
        public void FullWeightExpressesTheWholeLadder()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var animals = catalog.Animals();
            foreach (var clade in animals)
            {
                var partner = animals.First(a => a != clade);
                var g = GenomeRoller.RollChimera(3, catalog, config);
                g.CladeA = clade.Key; g.CladeB = partner.Key;
                g.Weights = new CharacterWeights { Human = CharacterWeights.Min, CladeA = CharacterWeights.Max, CladeB = CharacterWeights.Min };
                var bp = CharacterResolver.Resolve(g, catalog, config);
                int n = clade.Traits.Length;
                for (int i = 0; i < n; i++)
                    Assert.LessOrEqual(CharacterResolver.RungThreshold(clade.Traits[i], i, n), CharacterWeights.Max + 1e-4f,
                        $"{clade.Key}.{clade.Traits[i].Id} can never be bought");
            }
        }

        [Test]
        public void EveryTraitSiteExistsOnTheProceduralHead()
        {
            var catalog = CharacterTestKit.Catalog();
            var head = new ProceduralBaseHead(HeadDetail.Runtime);
            foreach (var clade in catalog.All)
                foreach (var t in clade.Traits)
                {
                    if (t.Feature == FeatureKind.None) continue;
                    var site = string.IsNullOrEmpty(t.Site) ? FeatureCatalog.DefaultSite(t.Feature) : t.Site;
                    Assert.IsTrue(HeadSiteResolver.TryFindSpec(head, site, out _), $"{clade.Key}.{t.Id} wants site '{site}'");
                }
        }

        [Test]
        public void ExactlyOneHumanSourceAndUniqueKeys()
        {
            var catalog = CharacterTestKit.Catalog();
            Assert.AreEqual(1, catalog.All.Count(c => c.IsHuman));
            Assert.AreEqual(catalog.All.Count, catalog.All.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count());
            Assert.GreaterOrEqual(catalog.Animals().Count, 6);
        }
    }
}
