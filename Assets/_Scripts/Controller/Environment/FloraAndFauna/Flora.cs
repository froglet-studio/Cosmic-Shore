using System.Collections;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Abstract base for plant-like lifeforms.
    /// Provides periodic growth cycles and planting behavior on top of LifeForm's
    /// health/spindle infrastructure.
    /// </summary>
    public abstract class Flora : LifeForm
    {
        [Header("Flora Settings")]
        [SerializeField] Vector3 leafSize = new Vector3(4f, 4f, 1f);
        [SerializeField] protected float growPeriod = 3f;
        [SerializeField] public float PlantPeriod = 15f;
        [SerializeField] float stunDuration = 1f;

        [Header("Planting")]
        [Tooltip("Flora plant within this fraction of the cell's membrane radius, dispersing them " +
                 "across the cell so domain clusters form in distinct locations - fauna schools of " +
                 "different domains then get directed at different clusters instead of comingling " +
                 "around the centre. 0 = use the flora's legacy fixed planting radius.")]
        [Range(0f, 1f)] [SerializeField] protected float plantRadiusCellFraction = 0.6f;

        protected bool isGrowing = true;

        public abstract void Grow();
        public abstract void Plant();

        /// <summary>The prism size this species' leaves grow to. Also the unit its lattice bonds at.</summary>
        protected Vector3 LeafSize => leafSize;

        /// <summary>
        /// A <b>pure preview of this species' growth pattern</b>: run the growth rule in local
        /// space and report where prisms WOULD land, spawning nothing at all.
        ///
        /// Flora have no art model - a species IS its growth rule - so this is the only honest way
        /// to draw one as an icon (the Lifeform bench's stations and its toy emblem). It is not a
        /// second implementation of growth for gameplay: no prism, no spindle, no GameObject, no
        /// cell, no spatial-index reservation, no config mutation, and it must never touch
        /// <c>UnityEngine.Random</c> (that would perturb a shared deterministic sequence) - the
        /// caller passes a seed and gets the same shape every time.
        ///
        /// Default is false = "this species has no preview", and the caller falls back. Overrides
        /// mirror their own <see cref="Grow"/> and are expected to drift only in ways a thumbnail
        /// cannot show.
        /// </summary>
        /// <param name="budget">Maximum prisms to report.</param>
        /// <param name="seed">Deterministic seed for any branching choices.</param>
        /// <param name="into">Receives local-space prism poses.</param>
        public virtual bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into) => false;

        // Optional pinned planting spot. Plant() implementations normally disperse the flora
        // across the cell; a caller that needs it to root at a KNOWN spot (the Lifeform Matrix
        // toy's spawn-here stations) sets this before Initialize and Plant() honors it.
        Vector3? _plantPositionOverride;

        /// <summary>Pin where this flora plants itself. Call before Initialize.</summary>
        public void SetPlantPositionOverride(Vector3 position) => _plantPositionOverride = position;

        /// <summary>True (with the spot) when a caller pinned the planting position.</summary>
        protected bool TryGetPlantPositionOverride(out Vector3 position)
        {
            position = _plantPositionOverride ?? default;
            return _plantPositionOverride.HasValue;
        }

        /// <summary>
        /// Planting radius for <see cref="Plant"/>: a fraction of the owning cell's
        /// membrane radius when configured (disperses flora across the whole cell),
        /// falling back to the flora's legacy fixed radius when the fraction is 0 or
        /// the cell/membrane is unavailable.
        /// </summary>
        protected float ResolvePlantRadius(float legacyRadius)
        {
            if (plantRadiusCellFraction > 0f && cell && cell.MembraneRadius > 0f)
                return cell.MembraneRadius * plantRadiusCellFraction;
            return legacyRadius;
        }

        /// <summary>
        /// Flora layer of the variant expression: the per-element leaf PRISM shape, growth
        /// tempo, and planting radius (the fields that differ between the four GyroidFlora
        /// prefabs). Runs before Initialize, so every leaf grows to the variant's size.
        /// </summary>
        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;

            if (tuning.LeafSize != Vector3.zero) leafSize = tuning.LeafSize;
            if (tuning.GrowPeriod >= 0f) growPeriod = tuning.GrowPeriod;
            if (tuning.PlantRadiusCellFraction >= 0f)
                plantRadiusCellFraction = Mathf.Clamp01(tuning.PlantRadiusCellFraction);
        }

        /// <summary>Flora level: leaf prisms grow with the level (crystal handled by base).</summary>
        public override void ApplyLevel(int level, float bodyScalePerLevel, float crystalScalePerLevel)
        {
            base.ApplyLevel(level, bodyScalePerLevel, crystalScalePerLevel);
            if (Level > 1)
                leafSize *= Mathf.Pow(Mathf.Max(1f, bodyScalePerLevel), Level - 1);
        }

        /// <summary>In-world level-up: future leaves grow a step too (existing leaves keep their
        /// size - growth flows through the normal spawn channel, nothing is re-scaled in place).</summary>
        public override bool LevelUp()
        {
            if (!base.LevelUp()) return false;
            leafSize *= BodyScalePerLevel;
            return true;
        }

        public override void AddHealthBlock(HealthPrism healthPrism)
        {
            base.AddHealthBlock(healthPrism);
            healthPrism.TargetScale = leafSize;
        }

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell);
            Plant();
            StartCoroutine(GrowCoroutine());
        }

        public override void RemoveHealthBlock(HealthPrism healthPrism, string killername = "")
        {
            base.RemoveHealthBlock(healthPrism);
            isGrowing = false;
        }

        IEnumerator GrowCoroutine()
        {
            while (true)
            {
                if (isGrowing)
                {
                    Grow();
                    yield return new WaitForSeconds(growPeriod);
                }
                else
                {
                    isGrowing = true;
                    yield return new WaitForSeconds(stunDuration);
                }
            }
        }
    }
}
