#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="MicroscenePatterns.Plan"/> - the pure recipe generators + theming behind
    /// the freestyle conveyor toy (Docs/ToySystem/ARCHITECTURE.md ▸ Wanderway). Load-bearing
    /// guarantees locked here:
    ///
    ///   1. BUDGET EXACTNESS - every recipe emits exactly prismBudget points (geometry AND themed),
    ///      so a recycled scene can re-pose its fixed stock of prisms into ANY recipe (mass is
    ///      conserved: the belt never needs to create or destroy prisms to change arrangement).
    ///   2. DETERMINISM - same seed → identical plan, geometry and theming (instance-local
    ///      System.Random only; the generators must never touch the global UnityEngine.Random).
    ///
    /// Plus sanity bounds: crystal clamp, lifeform counts confined to the living recipes, prism
    /// points staying within the scene's advertised extent, and the theming's collider-budget caps
    /// on shielded/supershielded prisms.
    /// </summary>
    [TestFixture]
    public class MicroscenePatternsTests
    {
        // 1500 = the shipped Toy_Conveyor budget (20 scenes × 1500 = the belt's 30,000-prism
        // conserved stock); the small values keep the low-budget paths honest.
        static readonly int[] Budgets = { 12, 42, 60, 100, 1500 };
        static readonly int[] Seeds = { 1, 7, 12345 };
        const float Radius = 55f;
        const int MaxCrystals = 3;

        static MicroscenePlan Plan(int recipe, int seed, int budget = 42, int maxCrystals = MaxCrystals) =>
            MicroscenePatterns.Plan(recipe, new System.Random(seed), budget, Radius, maxCrystals);

        [Test]
        public void EveryRecipe_EmitsExactlyThePrismBudget()
        {
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int budget in Budgets)
                    foreach (int seed in Seeds)
                    {
                        var plan = Plan(recipe, seed, budget);
                        Assert.AreEqual(budget, plan.PrismPoints.Count,
                            $"geometry: recipe {recipe} ({MicroscenePatterns.RecipeName(recipe)}), budget {budget}, seed {seed}");
                        Assert.AreEqual(budget, plan.Prisms.Count,
                            $"themed: recipe {recipe} ({MicroscenePatterns.RecipeName(recipe)}), budget {budget}, seed {seed}");
                    }
        }

        [Test]
        public void EveryRecipe_RespectsTheCrystalClamp()
        {
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int seed in Seeds)
                {
                    Assert.LessOrEqual(Plan(recipe, seed).Crystals.Count, MaxCrystals, $"recipe {recipe}, seed {seed}");
                    Assert.AreEqual(0, Plan(recipe, seed, maxCrystals: 0).Crystals.Count,
                        $"recipe {recipe}, seed {seed}: maxCrystals 0 must yield no pickups");
                }
        }

        [Test]
        public void SameSeed_ProducesIdenticalPlans()
        {
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
            {
                var a = Plan(recipe, 42);
                var b = Plan(recipe, 42);

                Assert.AreEqual(a.Prisms.Count, b.Prisms.Count, $"recipe {recipe}");
                for (int i = 0; i < a.Prisms.Count; i++)
                {
                    Assert.AreEqual(a.Prisms[i].Point.Position, b.Prisms[i].Point.Position, $"recipe {recipe} point {i}");
                    Assert.AreEqual(a.Prisms[i].Point.Rotation, b.Prisms[i].Point.Rotation, $"recipe {recipe} rot {i}");
                    Assert.AreEqual(a.Prisms[i].Point.Scale, b.Prisms[i].Point.Scale, $"recipe {recipe} scale {i}");
                    Assert.AreEqual(a.Prisms[i].Domain, b.Prisms[i].Domain, $"recipe {recipe} domain {i}");
                    Assert.AreEqual(a.Prisms[i].Kind, b.Prisms[i].Kind, $"recipe {recipe} kind {i}");
                }
                Assert.AreEqual(a.Crystals.Count, b.Crystals.Count, $"recipe {recipe} crystals");
                for (int i = 0; i < a.Crystals.Count; i++)
                    Assert.AreEqual(a.Crystals[i].Kind, b.Crystals[i].Kind, $"recipe {recipe} crystal kind {i}");
                Assert.AreEqual(a.FloraCount, b.FloraCount, $"recipe {recipe} flora");
                Assert.AreEqual(a.FaunaCount, b.FaunaCount, $"recipe {recipe} fauna");
            }
        }

        [Test]
        public void OnlyTheLivingRecipes_RequestLifeforms()
        {
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int seed in Seeds)
                {
                    var plan = Plan(recipe, seed);
                    string name = MicroscenePatterns.RecipeName(recipe);

                    if (MicroscenePatterns.IsLifeformRecipe(recipe))
                        Assert.Greater(plan.FloraCount + plan.FaunaCount, 0,
                            $"living recipe {name} (seed {seed}) must release at least one lifeform");
                    else
                    {
                        Assert.AreEqual(0, plan.FloraCount, $"{name} seed {seed} flora");
                        Assert.AreEqual(0, plan.FaunaCount, $"{name} seed {seed} fauna");
                    }
                }
        }

        [Test]
        public void PrismPoints_StayInsideTheAdvertisedSceneExtent()
        {
            // The conveyor clamps scene anchors to (bounds − sceneRadius×1.1 − margin), so a
            // generator escaping this envelope would push registered mass outside the cell's sense
            // radius - invisible to the ecosystem.
            float lateral = Radius * 1.1f;
            float along = Radius * 1.1f * 1.2f; // 2.2×radius length / 2, small jitter allowance

            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int budget in Budgets)
                    foreach (int seed in Seeds)
                        foreach (var p in Plan(recipe, seed, budget).Prisms)
                        {
                            Assert.LessOrEqual(Mathf.Abs(p.Point.Position.x), lateral, $"recipe {recipe} budget {budget} seed {seed} x");
                            Assert.LessOrEqual(Mathf.Abs(p.Point.Position.y), lateral, $"recipe {recipe} budget {budget} seed {seed} y");
                            Assert.LessOrEqual(Mathf.Abs(p.Point.Position.z), along, $"recipe {recipe} budget {budget} seed {seed} z");
                        }
        }

        [Test]
        public void PrismScales_AreStrictlyPositive()
        {
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (var p in Plan(recipe, 3).Prisms)
                {
                    Assert.Greater(p.Point.Scale.x, 0f);
                    Assert.Greater(p.Point.Scale.y, 0f);
                    Assert.Greater(p.Point.Scale.z, 0f);
                }
        }

        [Test]
        public void Theming_RespectsCollider_BudgetCapsOnShieldedPrisms()
        {
            // Shielded / supershielded prisms carry an always-on convex MeshCollider the collider-LOD
            // cannot reclaim, so the palette caps them per scene (danger is capped for readability).
            // The painter's EnforceKindCaps backstop must hold for EVERY scheme it can roll.
            var pal = MicroscenePalette.Default;
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int seed in Seeds)
                {
                    var plan = MicroscenePatterns.Plan(recipe, new System.Random(seed), 60, Radius, MaxCrystals, pal);
                    int danger = 0, shielded = 0, superShielded = 0;
                    foreach (var p in plan.Prisms)
                    {
                        if (p.Kind == PrismKind.Danger) danger++;
                        else if (p.Kind == PrismKind.Shielded) shielded++;
                        else if (p.Kind == PrismKind.SuperShielded) superShielded++;
                    }
                    Assert.LessOrEqual(danger, pal.MaxDanger, $"recipe {recipe} seed {seed} danger cap");
                    Assert.LessOrEqual(shielded, pal.MaxShielded, $"recipe {recipe} seed {seed} shielded cap");
                    Assert.LessOrEqual(superShielded, pal.MaxSuperShielded, $"recipe {recipe} seed {seed} supershield cap");
                }
        }

        [Test]
        public void StructureMetadata_StaysInLockstepWithPrismPoints()
        {
            // The painter keys every structural scheme (per-structure domains, danger tips, armoured
            // frames, taper) off Metas - a desync would silently mis-paint, so lockstep is locked here.
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int budget in Budgets)
                    foreach (int seed in Seeds)
                    {
                        var plan = MicroscenePatterns.Plan(recipe, new System.Random(seed), budget, Radius, MaxCrystals);
                        Assert.AreEqual(plan.PrismPoints.Count, plan.Metas.Count,
                            $"recipe {recipe} ({MicroscenePatterns.RecipeName(recipe)}), budget {budget}, seed {seed}");
                        foreach (var meta in plan.Metas)
                        {
                            Assert.Less(meta.Structure, plan.StructureCount,
                                $"recipe {recipe} seed {seed}: structure id out of range");
                            Assert.That(meta.T, Is.InRange(0f, 1f), $"recipe {recipe} seed {seed}: t out of range");
                        }
                    }
        }

        [Test]
        public void Theming_ProducesMultiDomainAndKindVariety_AcrossTheBag()
        {
            // The regression this build exists to prevent: every scene mono-coloured in one domain
            // and all-plain. Across a full recipe bag × seeds, the default palette must land BOTH
            // multi-domain scenes and danger/shield accents somewhere. Deterministic (fixed seeds).
            bool anyMultiDomain = false, anyDanger = false, anyShieldedKind = false;
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int seed in Seeds)
                {
                    var plan = Plan(recipe, seed);
                    var first = plan.Prisms[0].Domain;
                    foreach (var p in plan.Prisms)
                    {
                        if (p.Domain != first) anyMultiDomain = true;
                        if (p.Kind == PrismKind.Danger) anyDanger = true;
                        if (p.Kind is PrismKind.Shielded or PrismKind.SuperShielded) anyShieldedKind = true;
                    }
                }
            Assert.IsTrue(anyMultiDomain, "no scene in the whole bag used more than one domain");
            Assert.IsTrue(anyDanger, "no scene in the whole bag laid a danger prism");
            Assert.IsTrue(anyShieldedKind, "no scene in the whole bag laid a shielded/supershielded prism");
        }

        [Test]
        public void Theming_OnlyColoursFromTheAllowedDomains()
        {
            // Prisms may be any of the supplied playable domains OR neutral Blue (the veined scheme),
            // but never a domain outside the set.
            var pal = new MicroscenePalette { PlayableDomains = new[] { Domains.Jade, Domains.Ruby } };
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int seed in Seeds)
                    foreach (var p in MicroscenePatterns.Plan(recipe, new System.Random(seed), 42, Radius, MaxCrystals, pal).Prisms)
                        Assert.IsTrue(p.Domain is Domains.Jade or Domains.Ruby or Domains.Blue,
                            $"recipe {recipe} seed {seed}: unexpected domain {p.Domain}");
        }

        [Test]
        public void GrandAssemblies_FillTheirBudgetWithArchitecture_NotAmbientPad()
        {
            // The failure mode a monument-scale recipe must not have: emitting a few hundred points
            // and letting FitToBudget pad the rest with an ambient scatter sphere - which reads as
            // confetti around a small structure, exactly what the grand family exists to replace.
            // Proxies, both cheap and robust: the plan must be built from MANY substructures, and no
            // single one (the pad is one) may dominate it.
            const int ShippedBudget = 1500;

            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
            {
                if (!MicroscenePatterns.IsGrandRecipe(recipe)) continue;
                string name = MicroscenePatterns.RecipeName(recipe);

                foreach (int seed in Seeds)
                {
                    var plan = MicroscenePatterns.Plan(recipe, new System.Random(seed), ShippedBudget,
                        Radius, MaxCrystals);

                    Assert.GreaterOrEqual(plan.StructureCount, 6,
                        $"{name} (seed {seed}) built only {plan.StructureCount} substructures");

                    var perStructure = new int[plan.StructureCount];
                    foreach (var meta in plan.Metas) perStructure[meta.Structure]++;

                    int biggest = 0;
                    foreach (int n in perStructure) biggest = Mathf.Max(biggest, n);
                    Assert.Less(biggest, plan.PrismPoints.Count * 0.6f,
                        $"{name} (seed {seed}): one substructure holds {biggest} of " +
                        $"{plan.PrismPoints.Count} points - the recipe is padding, not building");
                }
            }
        }

        [Test]
        public void GrandRecipes_AreArchitecture_NotLifeformScenes()
        {
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                if (MicroscenePatterns.IsGrandRecipe(recipe))
                    Assert.IsFalse(MicroscenePatterns.IsLifeformRecipe(recipe),
                        $"{MicroscenePatterns.RecipeName(recipe)} must not be a living recipe");
        }
    }
}
#endif
