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
                 "across the cell so domain clusters form in distinct locations - fauna schools of " +
                 "different domains then get directed at different clusters instead of comingling " +
                 "around the centre. 0 = use the flora's legacy fixed planting radius.")]
        [Range(0f, 1f)] [SerializeField] protected float plantRadiusCellFraction = 0.6f;

        protected bool isGrowing = true;

        /// <summary>
        /// True when this flora's root pose was authored externally (a client
        /// reconstructing a server-replicated plant event) - Plant() implementations
        /// must then SKIP their own random positioning so the structure roots at the
        /// replicated position on every peer. (Docs/ECOSYSTEM_NETWORK_SYNC.md, flora.)
        /// </summary>
        public bool UseAuthoredPlacement { get; set; }

        /// <summary>
        /// Number of Grow() cycles this flora has run (natural cadence + fast-forward).
        /// Mirrored by FloraNetworkSync so late-joining clients can catch a plant up to
        /// the server's SIZE. Shape stays locally emergent - growth consults the local
        /// spatial index, so structures are same-species/same-place/same-size across
        /// peers, not byte-identical.
        /// </summary>
        public int GrowthTicks { get; private set; }

        /// <summary>
        /// Runs <paramref name="ticks"/> extra Grow() cycles paced one per frame - a
        /// visible bloom-in (continuity law: nothing pops in), which also spreads the
        /// instantiation cost (the initial-batch frame-spike lesson). Grow()'s own
        /// gates (live-prism budget, Frenzy) keep applying throughout.
        /// </summary>
        public void FastForwardGrowth(int ticks)
        {
            if (ticks <= 0 || !isActiveAndEnabled) return;
            StartCoroutine(FastForwardGrowthCoroutine(ticks));
        }

        IEnumerator FastForwardGrowthCoroutine(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                if (IsDying) yield break;
                Grow();
                GrowthTicks++;
                yield return null;
            }
        }

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
                    GrowthTicks++;
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
