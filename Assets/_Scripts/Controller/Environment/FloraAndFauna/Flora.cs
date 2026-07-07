using System.Collections;
using CosmicShore.Gameplay;
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
                 "across the cell so domain clusters form in distinct locations — fauna schools of " +
                 "different domains then get directed at different clusters instead of comingling " +
                 "around the centre. 0 = use the flora's legacy fixed planting radius.")]
        [Range(0f, 1f)] [SerializeField] protected float plantRadiusCellFraction = 0.6f;

        protected bool isGrowing = true;

        public abstract void Grow();
        public abstract void Plant();

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

        public override void AddHealthBlock(HealthPrism healthPrism)
        {
            base.AddHealthBlock(healthPrism);
            healthPrism.TargetScale = leafSize;
        }

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell);

            // Android stripped-performance branch: flora never plant or grow — a creation-side
            // pause (sanctioned: "not creating mass is allowed; aging it out is not"). Existing
            // mass is untouched; scene-placed flora sit as inert roots. Covers the menu cell's
            // 6 scene-placed BranchingFlora, whose otherwise-unbounded growth (12,000-volume
            // ceiling, 0s intervals) dominates CPU + collider load on a mid phone.
            if (CosmicShore.Utility.PerfStrip.Enabled)
            {
                isGrowing = false;
                return;
            }

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
