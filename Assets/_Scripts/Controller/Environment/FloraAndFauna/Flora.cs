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
        [Tooltip("OUTER edge of this species' planting band, as a fraction of the cell's membrane " +
                 "radius. Flora disperse inside the band so domain clusters form in distinct " +
                 "locations - fauna schools of different domains then get directed at different " +
                 "clusters instead of comingling around the centre. 0 = use the flora's legacy " +
                 "fixed planting radius.")]
        [Range(0f, 1f)] [SerializeField] protected float plantRadiusCellFraction = 0.6f;

        [Tooltip("INNER edge of the planting band, as a fraction of the membrane radius. 0 (the " +
                 "default) collapses the band to a single SHELL at the outer fraction - the legacy " +
                 "behaviour every cell had before bands existed. Set it below the outer fraction " +
                 "and this species fills the whole shell of space between the two, which is what a " +
                 "cell needs when its plants are meant to read as a forest with depth rather than " +
                 "a soap bubble at one radius.\n\n" +
                 "The inner edge is always clamped OUTSIDE the cell's nucleus: nucleus-interior " +
                 "mass is the territorial claim, is excluded from the fauna targeting grids, and " +
                 "is where a standard crystal respawns - so a plant rooted in there is mass the " +
                 "food web can never reach and clutter in the one volume that has to stay legible.")]
        [Range(0f, 1f)] [SerializeField] protected float plantRadiusCellFractionMin = 0f;

        protected bool isGrowing = true;

        /// <summary>
        /// The prism size this species' leaves grow to, and the unit its lattice bonds at: the
        /// per-element leaf PRISM size (authored on the prefab, overridden by
        /// <c>FloraVariantTuning.LeafSize</c>, scaled by level). Exposed so a flora that shapes
        /// its prisms per ROLE - a stem segment is not a leaf - can derive those shapes from the
        /// element's identity instead of re-authoring it. See <see cref="PhyllotacticFlora"/>.
        /// </summary>
        protected Vector3 LeafSize => leafSize;

        public abstract void Grow();
        public abstract void Plant();

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

        /// <summary>
        /// The surface normal at the pinned spot, when one was supplied - an authored garden bed
        /// has an UP and a plant rooted in it should grow away from the bed, not toward the cell
        /// crystal. Vector3.zero when the caller pinned a position only.
        /// </summary>
        Vector3 _plantUpOverride;

        /// <summary>Pin where this flora plants itself. Call before Initialize.</summary>
        public void SetPlantPositionOverride(Vector3 position) => _plantPositionOverride = position;

        /// <summary>
        /// Pin where this flora plants itself AND which way is up there (an authored planting
        /// site - <see cref="FloraPlantingSite"/>). Call before Initialize.
        /// </summary>
        public void SetPlantPositionOverride(Vector3 position, Vector3 up)
        {
            _plantPositionOverride = position;
            _plantUpOverride = up;
        }

        /// <summary>True (with the spot) when a caller pinned the planting position.</summary>
        protected bool TryGetPlantPositionOverride(out Vector3 position)
        {
            position = _plantPositionOverride ?? default;
            return _plantPositionOverride.HasValue;
        }

        /// <summary>
        /// The pinned growth axis: the site's normal when one was supplied, else the direction
        /// away from the cell centre (a plant on an unstructured cell still grows outward, which
        /// is what the legacy shell dispersal implies). Only meaningful after Plant().
        /// </summary>
        protected Vector3 GrowthUp
        {
            get
            {
                if (_plantUpOverride.sqrMagnitude > 0.0001f) return _plantUpOverride.normalized;
                if (cell)
                {
                    var radial = transform.position - cell.transform.position;
                    if (radial.sqrMagnitude > 0.0001f) return radial.normalized;
                }
                return Vector3.up;
            }
        }

        /// <summary>
        /// Planting radius for <see cref="Plant"/>: a fraction of the owning cell's membrane
        /// radius when configured (disperses flora across the whole cell), falling back to the
        /// flora's legacy fixed radius when the outer fraction is 0 or the cell/membrane is
        /// unavailable.
        ///
        /// <para>With <see cref="plantRadiusCellFractionMin"/> set, this draws a radius from the
        /// BAND between the two fractions instead of returning the single outer shell. The draw is
        /// uniform by VOLUME (<c>r = cbrt(lerp(min³, max³, u))</c>), not by radius: a shell's
        /// available space grows as r², so a uniform-in-radius draw crowds plants onto the inner
        /// edge and leaves the outer band — most of the cell — looking empty. Volume-uniform gives
        /// an even spatial density through the whole band, which naturally puts most plants in the
        /// outer reaches (that is where the space is) while still landing some in close.</para>
        ///
        /// <para>The inner edge is clamped outside the nucleus — see the field tooltip.</para>
        /// </summary>
        protected float ResolvePlantRadius(float legacyRadius)
        {
            if (plantRadiusCellFraction <= 0f || !cell || cell.MembraneRadius <= 0f)
                return legacyRadius;

            float membrane = cell.MembraneRadius;
            float outer = membrane * plantRadiusCellFraction;

            // Never inside the nucleus: that mass is the territorial claim, is kept out of the
            // fauna targeting grids, and shares its volume with the standard crystal respawn.
            // ExpectedNucleusWorldRadius (not NucleusWorldRadius) so the clamp is correct even if
            // a caller plants before the cell has instantiated its nucleus.
            float inner = Mathf.Max(membrane * plantRadiusCellFractionMin,
                                    cell.ExpectedNucleusWorldRadius);
            if (inner >= outer) return outer;

            float t = Random.value;
            float innerCubed = inner * inner * inner;
            float outerCubed = outer * outer * outer;
            return Mathf.Pow(Mathf.Lerp(innerCubed, outerCubed, t), 1f / 3f);
        }

        /// <summary>
        /// The point <see cref="ResolvePlantRadius"/>'s shell is measured FROM: the owning
        /// cell's centre, which is what "a fraction of the cell's membrane radius" has always
        /// meant. Every <see cref="Plant"/> implementation used to measure from
        /// <c>cellData.CrystalTransform.position</c> instead - harmless while a mode's crystals
        /// sit in the cell core, and wrong the moment a mode lets its crystal roam: the whole
        /// planting shell then rides the crystal and a plant lands at
        /// <c>crystalRadius + fraction x membraneRadius</c>, i.e. OUTSIDE the membrane, where
        /// <c>Cell.ContainsPosition</c> rejects its prisms and neither the volume ladder nor
        /// the fauna density grids can see them. Rampage's contested crystal is the mode that
        /// exposed it.
        ///
        /// Falls back to the crystal (legacy) and then to this flora's own position, so a cell
        /// that never resolved is still a planting anchor rather than a
        /// <see cref="System.NullReferenceException"/> (<c>CrystalTransform</c> returns null
        /// when a cell holds no crystal at all).
        /// </summary>
        protected Vector3 ResolvePlantCenter()
        {
            if (cell) return cell.transform.position;

            var crystalTransform = cellData ? cellData.CrystalTransform : null;
            return crystalTransform ? crystalTransform.position : transform.position;
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
            if (tuning.PlantRadiusCellFractionMin >= 0f)
                plantRadiusCellFractionMin = Mathf.Clamp01(tuning.PlantRadiusCellFractionMin);
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
