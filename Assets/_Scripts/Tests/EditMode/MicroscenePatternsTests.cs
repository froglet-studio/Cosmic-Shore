#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="MicroscenePatterns.Plan"/> — the pure recipe generators behind the
    /// freestyle conveyor toy (Docs/ToySystem/ARCHITECTURE.md ▸ Wanderway). Two guarantees are
    /// load-bearing for the conveyor's closed-system recycling and are locked here:
    ///
    ///   1. BUDGET EXACTNESS — every recipe emits exactly prismBudget points, so a recycled
    ///      scene can re-pose its fixed stock of prisms into ANY recipe (mass is conserved:
    ///      the belt never needs to create or destroy prisms to change arrangement).
    ///   2. DETERMINISM — same seed → identical plan (instance-local System.Random only; the
    ///      generators must never touch the global UnityEngine.Random).
    ///
    /// Plus sanity bounds: crystal clamp, lifeform counts confined to the living recipes, and
    /// prism points staying within the scene's advertised extent (the conveyor's bounds math
    /// subtracts that extent from the cell's sense radius).
    /// </summary>
    [TestFixture]
    public class MicroscenePatternsTests
    {
        static readonly int[] Budgets = { 12, 42, 60 };
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
                        Assert.AreEqual(budget, Plan(recipe, seed, budget).PrismPoints.Count,
                            $"recipe {recipe} ({MicroscenePatterns.RecipeName(recipe)}), budget {budget}, seed {seed}");
        }

        [Test]
        public void EveryRecipe_RespectsTheCrystalClamp()
        {
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int seed in Seeds)
                {
                    Assert.LessOrEqual(Plan(recipe, seed).CrystalPoints.Count, MaxCrystals,
                        $"recipe {recipe}, seed {seed}");
                    Assert.AreEqual(0, Plan(recipe, seed, maxCrystals: 0).CrystalPoints.Count,
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

                Assert.AreEqual(a.PrismPoints.Count, b.PrismPoints.Count, $"recipe {recipe}");
                for (int i = 0; i < a.PrismPoints.Count; i++)
                {
                    Assert.AreEqual(a.PrismPoints[i].Position, b.PrismPoints[i].Position, $"recipe {recipe} point {i}");
                    Assert.AreEqual(a.PrismPoints[i].Rotation, b.PrismPoints[i].Rotation, $"recipe {recipe} rot {i}");
                    Assert.AreEqual(a.PrismPoints[i].Scale, b.PrismPoints[i].Scale, $"recipe {recipe} scale {i}");
                }
                Assert.AreEqual(a.CrystalPoints.Count, b.CrystalPoints.Count, $"recipe {recipe} crystals");
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

                    if (name == "Meadow")
                    {
                        Assert.That(plan.FloraCount, Is.InRange(1, 2), $"seed {seed}");
                        Assert.AreEqual(0, plan.FaunaCount, $"seed {seed}");
                    }
                    else if (name == "Menagerie")
                    {
                        Assert.AreEqual(0, plan.FloraCount, $"seed {seed}");
                        Assert.That(plan.FaunaCount, Is.InRange(1, 2), $"seed {seed}");
                    }
                    else
                    {
                        Assert.AreEqual(0, plan.FloraCount, $"{name} seed {seed}");
                        Assert.AreEqual(0, plan.FaunaCount, $"{name} seed {seed}");
                    }
                }
        }

        [Test]
        public void PrismPoints_StayInsideTheAdvertisedSceneExtent()
        {
            // The conveyor clamps scene anchors to (bounds − sceneRadius×1.1 − margin), so a
            // generator escaping this envelope would push registered mass outside the cell's
            // sense radius — invisible to the ecosystem.
            float lateral = Radius * 1.1f;
            float along = Radius * 1.1f * 1.2f; // 2.2×radius length / 2, small jitter allowance

            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (int seed in Seeds)
                    foreach (var p in Plan(recipe, seed).PrismPoints)
                    {
                        Assert.LessOrEqual(Mathf.Abs(p.Position.x), lateral, $"recipe {recipe} seed {seed} x");
                        Assert.LessOrEqual(Mathf.Abs(p.Position.y), lateral, $"recipe {recipe} seed {seed} y");
                        Assert.LessOrEqual(Mathf.Abs(p.Position.z), along, $"recipe {recipe} seed {seed} z");
                    }
        }

        [Test]
        public void PrismScales_AreStrictlyPositive()
        {
            for (int recipe = 0; recipe < MicroscenePatterns.RecipeCount; recipe++)
                foreach (var p in Plan(recipe, 3).PrismPoints)
                {
                    Assert.Greater(p.Scale.x, 0f);
                    Assert.Greater(p.Scale.y, 0f);
                    Assert.Greater(p.Scale.z, 0f);
                }
        }
    }
}
#endif
