using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A plant that grows the MINIMAL SURFACE SPANNING THE BORROMEAN RINGS.
    ///
    /// <para>Its three ellipses are the boundaries of three mutually perpendicular golden
    /// rectangles whose twelve corners are an icosahedron's vertices - the canonical
    /// Borromean realization, and a forced one: by the Freedman-Skora theorem the
    /// Borromean rings cannot be built from three round CIRCLES at all. The membrane
    /// stretched across them is a level set of the rings' summed solid angle relaxed to
    /// zero mean curvature, which comes out at genus 1 over three boundary loops - the
    /// minimal-genus Seifert surface of the link. Every number is measured offline by
    /// <c>Tools/Build/measure_borromean_minimal_surface.py</c> and lands in
    /// <see cref="BorromeanSurfaceData"/>; nothing here describes the shape.</para>
    ///
    /// <para><b>It is NOT a lattice species.</b> The three <see cref="AssembledFlora"/>
    /// families tile a periodic surface indefinitely and reproduce as a COLONY, one
    /// daughter per fauna-wave period, because their growth rule has an opinion about
    /// where the next plant belongs. A Borromean surface is COMPACT - it closes on itself
    /// and is finished - so this plant completes, stops growing, and funds an ordinary
    /// per-plant offspring out of its growth quota like every branching and phyllotactic
    /// species (Docs/ECOSYSTEM.md 32). There is no frontier, no claim book and no
    /// mate-snap here, and there is deliberately nothing to add: a species whose form is
    /// bounded does not need them.</para>
    ///
    /// <para><b>A half-grown plant is exactly as symmetric as a finished one.</b> The site
    /// table is a union of whole ORBITS of the surface's order-6 symmetry group, ordered
    /// outward from the heart, and one grow tick lays one whole orbit
    /// (<see cref="BorromeanSurfaceData.OrbitSize"/>). So the plant blooms from its centre
    /// to its rim and reads as the same object at every size, rather than as a lopsided
    /// fragment that eventually becomes symmetric.</para>
    ///
    /// <para>Grazing FREES a site, so a plant eaten back regrows into its own vacancies
    /// from the heart outward instead of staying a permanent stub - the live-prism budget
    /// rule every flora family follows.</para>
    /// </summary>
    public class BorromeanFlora : Flora
    {
        [Tooltip("Maximum LIVE prisms this plant can hold. Clamped to the site table's own " +
                 "SiteCount - the surface is a COMPACT object with a fixed number of places " +
                 "to put a prism, so a larger budget would simply never be spent. Lower it " +
                 "and the plant is a partially grown membrane: still exactly symmetric, " +
                 "because growth runs orbit by orbit.")]
        [SerializeField] int maxTotalSpawnedObjects = BorromeanSurfaceData.SiteCount;

        [Tooltip("Uniform scale on the whole surface. Overridden per element by " +
                 "FloraVariantTuning.LatticeScale (sentinel -1 = keep this). It scales the " +
                 "site offsets AND the leaf TOGETHER - scaling either alone ships a " +
                 "different plant (Docs/ECOSYSTEM.md 34.8).")]
        [SerializeField] float surfaceScale = 1f;

        /// <summary>The live-prism budget this individual resolved to - the base reads it for
        /// the reproduction maturity gate (see <see cref="Flora.PrismBudget"/>).</summary>
        protected override int PrismBudget => Budget;

        /// <summary>
        /// The surface's site offsets are a measured table in ABSOLUTE local units, so a
        /// per-cell leaf scale would lay prisms the table no longer describes - exactly the
        /// hazard <see cref="Flora.PrismSizeFixedByGrowthRule"/> is the standing guard for.
        /// Resizing this species goes through <see cref="surfaceScale"/>, which moves the
        /// sites and the leaf together.
        /// </summary>
        protected override bool PrismSizeFixedByGrowthRule => true;

        int Budget => Mathf.Clamp(maxTotalSpawnedObjects, 1, BorromeanSurfaceData.SiteCount);

        // Turns a site's own frame into its spindle's: the branch points along the site's +y
        // (the grain) instead of its +z (the surface normal). Because it is a constant offset
        // the prism's compensating LOCAL rotation is the exact inverse - also a constant - so
        // the prism lands on the measured site pose with no runtime LookRotation and no
        // degenerate case to guard.
        static readonly Quaternion SpindleAlign = Quaternion.Euler(-90f, 0f, 0f);
        static readonly Quaternion PrismInSpindle = Quaternion.Euler(90f, 0f, 0f);

        // Which prism occupies each site, and the spindle that carries it. Indexed by site,
        // so a grazed site is simply a null the next grow tick refills.
        HealthPrism[] _occupant;
        readonly Dictionary<HealthPrism, int> _siteOf = new();

        /// <summary>
        /// Borromean layer of the variant expression: the live-prism budget and the uniform
        /// surface scale. <see cref="FloraVariantTuning.LatticeScale"/> is reused rather than
        /// given a new field because it already means exactly this on the three lattice
        /// families - "scale this species' geometry, keeping its identity" - and a second
        /// field meaning the same thing is a second place to author it.
        /// </summary>
        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;
            if (tuning.LatticeScale > 0f) surfaceScale = tuning.LatticeScale;
            if (tuning.MaxTotalSpawnedObjects >= 0) maxTotalSpawnedObjects = tuning.MaxTotalSpawnedObjects;
            // Cell density scalar, applied AFTER the absolute so it scales whatever won.
            // Round half UP explicitly: Mathf.RoundToInt is banker's rounding, which would
            // turn an authored 360 x 0.9 into 324 on one species and 323 on the next.
            if (tuning.MaxTotalSpawnedObjectsScale > 0f)
                maxTotalSpawnedObjects = Mathf.Max(1, Mathf.FloorToInt(
                    maxTotalSpawnedObjects * tuning.MaxTotalSpawnedObjectsScale + 0.5f));
        }

        public override void Initialize(Cell cell)
        {
            _occupant = new HealthPrism[BorromeanSurfaceData.SiteCount];
            base.Initialize(cell);
        }

        public override void Plant()
        {
            // A pinned position (the Lifeform Matrix toy's spawn-here stations) wins over
            // dispersal, exactly as it does for every other family.
            if (TryGetPlantPositionOverride(out var pinned))
                transform.position = pinned;
            else
            {
                // Shell measured from the CELL CENTRE (Flora.ResolvePlantCenter), not the
                // crystal - see BranchingFlora.Plant.
                float radius = ResolvePlantRadius(legacyRadius: 200f);
                transform.position = ResolvePlantCenter() + radius * Random.onUnitSphere;
            }

            // A random attitude per plant. The surface is one fixed shape, so without this
            // a stand of them reads as a row of stamped copies; with it every plant shows a
            // different face of the same object and the species still has ONE form.
            transform.rotation = Random.rotationUniform;
        }

        public override void Grow()
        {
            if (_occupant == null) return;

            // Frenzy gate: growth runs at a steady rate until Frenzy, then pauses and resumes
            // when an active force (grazing, a vessel ability) brings the cell back under the
            // hysteresis floor. Cell.FloraGrowingEnabled is the single source of truth.
            if (cell && !cell.FloraGrowingEnabled) return;

            int budget = Budget;
            if (healthTracker != null && healthTracker.Count >= budget) return;

            // One whole ORBIT per tick, taken in the table's own order - which is outward
            // from the heart - so the plant is exactly symmetric at every stage rather than
            // only when it is finished. After a graze this refills the innermost vacancies
            // first, so a plant eaten at its rim regrows from the inside out.
            int laid = 0;
            for (int i = 0; i < budget && laid < BorromeanSurfaceData.OrbitSize; i++)
            {
                if (_occupant[i]) continue;
                if (!LayAt(i)) break;
                laid++;
            }

            // Growth is this plant's feeding - see Flora.NotifyGrew.
            if (laid > 0) NotifyGrew(laid);
        }

        bool LayAt(int site)
        {
            Vector3 pos = transform.TransformPoint(BorromeanSurfaceData.Positions[site] * surfaceScale);
            Quaternion rot = transform.rotation * BorromeanSurfaceData.Rotations[site];

            // The spindle is the plant's connective tissue and it lies IN the membrane: its
            // own +z is aimed along the plate's long axis - the grain the sites are laid to -
            // so the branches read as veins running through the surface. Posed at the prism's
            // own rotation instead (which is what every lattice family does) it would stand
            // along the surface NORMAL and skewer its own plate.
            Quaternion spindleRot = rot * SpindleAlign;

            Spindle newSpindle = AddSpindle();
            newSpindle.transform.SetPositionAndRotation(pos, spindleRot);

            HealthPrism prism = EnvironmentPrismPool.Get(healthPrism, pos, rot);
            if (!prism) return false;

            AddHealthBlock(prism);
            // SetParent(worldPositionStays:false) KEEPS the local values, which at this point
            // are whatever the pooled prism carried, so both locals are set explicitly. The
            // prism is parented to the SPINDLE and never to another prism: a prism wears its
            // leaf as localScale, and a non-uniform scale above a rotated child is a shear
            // (Docs/ECOSYSTEM.md 37.9).
            prism.transform.SetParent(newSpindle.transform, false);
            prism.transform.localPosition = Vector3.zero;
            prism.transform.localRotation = PrismInSpindle;
            prism.LifeForm = this;
            prism.Initialize("flora");

            _occupant[site] = prism;
            _siteOf[prism] = site;
            return true;
        }

        public override void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "")
        {
            // Free the site BEFORE the base call: the base may decide this plant is dead, and
            // a freed site costs nothing either way, where a site left claimed by a prism that
            // no longer exists is a permanent hole the plant could never regrow into.
            if (healthPrism && _siteOf.TryGetValue(healthPrism, out int site))
            {
                _siteOf.Remove(healthPrism);
                if (_occupant != null && _occupant[site] == healthPrism) _occupant[site] = null;
            }
            base.RemoveHealthBlock(healthPrism, killerName);
        }

        /// <summary>
        /// Pure preview of the surface - see <see cref="Flora.TryPreviewGrowth"/>. This
        /// species' growth rule is DETERMINISTIC, so unlike the branching and phyllotactic
        /// previews there is nothing to mirror and nothing to seed: the preview is the first
        /// <paramref name="budget"/> entries of the same table the plant grows, which makes
        /// it exact rather than representative. The seed is accepted and unused.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0) return false;
            Vector3 leaf = LeafSize != Vector3.zero ? LeafSize : BorromeanSurfaceData.LeafSize;
            int n = Mathf.Min(budget, BorromeanSurfaceData.SiteCount);
            for (int i = 0; i < n; i++)
                into.Add(new SpawnPoint(BorromeanSurfaceData.Positions[i] * surfaceScale,
                                        BorromeanSurfaceData.Rotations[i], leaf));
            return n > 0;
        }
    }
}
