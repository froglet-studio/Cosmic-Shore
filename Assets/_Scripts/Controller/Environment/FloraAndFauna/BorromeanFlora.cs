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
    /// <para><b>It grows the way a flora withers, RUN BACKWARDS: the crystal first, then
    /// limbs out of the crystal, then limbs and plates out of limbs.</b> Every site names
    /// the site it hangs off (<see cref="BorromeanSurfaceData.Parents"/>, -1 = the heart),
    /// a parent is always earlier in the table than its child, and a plate is never laid
    /// on the far end of a limb that does not exist yet - so the membrane is ONE connected
    /// object from its first grow tick, expanding outward from the crystal, rather than
    /// several patches that meet up and seal later. The spindle carrying a plate is posed
    /// ON that bond - rooted at the parent, aimed at the child, stretched to span the gap -
    /// so the limbs lie IN the membrane and read as veins running through it.</para>
    ///
    /// <para>That is <see cref="BranchingFlora"/>'s shape, not a lattice species': there a
    /// spindle is instantiated at the PARENT and the child is placed along its forward
    /// axis, so the limb is the bond. A lattice species instead poses its spindle at its
    /// own prism wearing that prism's rotation and lets the branch geometry reach out - the
    /// gyroid covers both directions with a MIRRORED PAIR of half-branches meeting at the
    /// prism (Docs/ECOSYSTEM.md 34.12), which is why it reads as a connected frame. Done
    /// here, the same pose would stand every limb along the surface NORMAL and skewer its
    /// own plate, because on this surface a plate's rotation is a frame OF the membrane
    /// rather than of a bond. The spindle prefab follows from that: this species wires
    /// Branch.prefab, whose branch runs forward from the spindle's origin along local +z.</para>
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
    /// table is a union of whole ORBITS of the surface's order-6 symmetry group and one
    /// grow tick lays one whole orbit (<see cref="BorromeanSurfaceData.OrbitSize"/>). That
    /// survives the hop ordering above because the site graph is G-invariant, which makes
    /// hop distance an ORBIT property rather than a site property.</para>
    ///
    /// <para>Grazing FREES a site, so a plant eaten back regrows into its own vacancies
    /// from the heart outward instead of staying a permanent stub - the live-prism budget
    /// rule every flora family follows. The LIMB is left standing when its plate is eaten
    /// and is re-used when the plate grows back, so grazing can never mint a second
    /// spindle on one bond.</para>
    /// </summary>
    public class BorromeanFlora : Flora
    {
        [Tooltip("Maximum LIVE prisms this plant can hold. Clamped to the site table's own " +
                 "SiteCount - the surface is a COMPACT object with a fixed number of places " +
                 "to put a prism, so a larger budget would simply never be spent. Lower it " +
                 "and the plant is a partially grown membrane: still exactly symmetric, and " +
                 "still connected, because a site's parent is always earlier in the table.")]
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

        // Which prism occupies each site, and the limb that carries it. Indexed by site, so a
        // grazed site is simply a null the next grow tick refills - and the limb survives that,
        // because a branch whose leaf was eaten is still a branch.
        HealthPrism[] _occupant;
        Spindle[] _limb;
        readonly Dictionary<HealthPrism, int> _siteOf = new();

        // How far the spindle prefab's own branch geometry reaches along its local +z, measured
        // once per PREFAB rather than authored: the number is a property of a mesh and a
        // transform chain, and a constant copied out of an asset is true only on the day it is
        // copied. Keyed on the prefab so the whole species shares one measurement.
        static readonly Dictionary<int, float> BranchReach = new();
        static bool _warnedNoBranch;

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
            _limb = new Spindle[BorromeanSurfaceData.SiteCount];
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

            // One whole ORBIT per tick, taken in the table's own order - which is outward from
            // the heart in HOP distance over the surface's own site graph - so the plant is
            // exactly symmetric at every stage AND connected at every stage. The parent gate is
            // what makes the second half true under regrowth as well as from seed: a plate is
            // never laid on the far end of a limb whose own plate was eaten, and since the
            // parent is earlier in the table it is refilled first anyway.
            int laid = 0;
            for (int i = 0; i < budget && laid < BorromeanSurfaceData.OrbitSize; i++)
            {
                if (_occupant[i]) continue;
                int parent = BorromeanSurfaceData.Parents[i];
                if (parent >= 0 && !_occupant[parent]) continue;
                if (!LayAt(i, parent)) break;
                laid++;
            }

            // Growth is this plant's feeding - see Flora.NotifyGrew.
            if (laid > 0) NotifyGrew(laid);
        }

        Vector3 SiteWorld(int site) =>
            transform.TransformPoint(BorromeanSurfaceData.Positions[site] * surfaceScale);

        bool LayAt(int site, int parent)
        {
            Vector3 pos = SiteWorld(site);
            Quaternion rot = transform.rotation * BorromeanSurfaceData.Rotations[site];

            // The limb runs from the parent's plate to this one - or out of the HEART, which
            // sits at the plant's own origin, for the six sites of the innermost orbit. That
            // is the whole of "spindles grow from the crystal, prisms grow from spindles".
            Vector3 root = parent >= 0 ? SiteWorld(parent) : transform.position;
            Vector3 bond = pos - root;

            Spindle limb = _limb[site];
            if (!limb)
            {
                limb = AddSpindle();
                _limb[site] = limb;
                // The branch geometry runs along the spindle's local +z, so LookRotation puts
                // it on the bond. Up is the site's own surface NORMAL (the third column of its
                // measured rotation), which keeps the limb's own cross-section lying in the
                // membrane rather than rolling arbitrarily about the bond.
                if (!SafeLookRotation.TrySet(limb.transform, bond, rot * Vector3.forward, this))
                    limb.transform.rotation = rot;
                limb.transform.position = root;
                StretchToBond(limb, bond.magnitude);
            }

            HealthPrism prism = EnvironmentPrismPool.Get(healthPrism, pos, rot);
            if (!prism) return false;

            AddHealthBlock(prism);
            // Parented to the SPINDLE and never to another prism: a prism wears its leaf as
            // localScale, and a non-uniform scale above a rotated child is a shear
            // (Docs/ECOSYSTEM.md 37.9). worldPositionStays keeps the plate exactly on its
            // measured pose - the spindle root is never scaled, only its branch children are,
            // so nothing can leak into the leaf.
            prism.transform.SetParent(limb.transform, true);
            prism.LifeForm = this;
            prism.Initialize("flora");

            _occupant[site] = prism;
            _siteOf[prism] = site;
            return true;
        }

        /// <summary>
        /// Stretches a limb's branch geometry to span its own bond.
        ///
        /// <para>It scales the spindle's CHILDREN and never the root, for the reason
        /// <c>AssembledFlora.ScaleSpindleToLattice</c> records: a prism parents to the root, so
        /// a scaled root would multiply the authored <c>leafSize</c> and the leaf size in the
        /// config would stop describing the prism. Only the child's LOCAL Z is scaled - on
        /// every spindle prefab in the project that is the branch's length axis - so a long
        /// bond gets a long branch rather than a fat one.</para>
        /// </summary>
        void StretchToBond(Spindle limb, float bond)
        {
            if (!limb || bond <= 0f) return;
            float reach = ResolveBranchReach(limb);
            if (reach <= 0f) return;

            float f = bond / reach;
            Transform root = limb.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Vector3 s = child.localScale;
                s.z *= f;
                child.localScale = s;
            }
        }

        /// <summary>
        /// How far the spindle prefab's branch reaches along the spindle's local +z, measured
        /// from the mesh bounds composed through the transform chain - not authored, because a
        /// number copied out of an asset is true only on the day it is copied.
        ///
        /// <para>A prefab whose branch does NOT run along +z measures zero and is reported
        /// once by name: the limb then stands unstretched rather than silently inverted, which
        /// is a thing a human can see and act on.</para>
        /// </summary>
        float ResolveBranchReach(Spindle limb)
        {
            int key = spindle ? spindle.GetInstanceID() : 0;
            if (BranchReach.TryGetValue(key, out float cached)) return cached;

            Transform root = limb.transform;
            float reach = 0f;
            var filters = limb.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (!mf || !mf.sharedMesh || mf.transform == root) continue;
                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 p = m.MultiplyPoint3x4(new Vector3(
                        (c & 1) == 0 ? b.min.x : b.max.x,
                        (c & 2) == 0 ? b.min.y : b.max.y,
                        (c & 4) == 0 ? b.min.z : b.max.z));
                    if (p.z > reach) reach = p.z;
                }
            }

            if (reach <= 0f && !_warnedNoBranch)
            {
                _warnedNoBranch = true;
                CSDebug.LogWarning(
                    $"{name}: spindle prefab '{(spindle ? spindle.name : "none")}' has no branch " +
                    "geometry along its local +z, so BorromeanFlora cannot stretch its limbs to " +
                    "their bonds. This species expects Branch.prefab (the branch BranchingFlora " +
                    "uses), whose branch runs forward from the spindle's origin.", this);
            }
            BranchReach[key] = reach;
            return reach;
        }

        public override void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "")
        {
            // Free the site BEFORE the base call: the base may decide this plant is dead, and
            // a freed site costs nothing either way, where a site left claimed by a prism that
            // no longer exists is a permanent hole the plant could never regrow into. The LIMB
            // is deliberately left standing - a branch whose leaf was eaten is still a branch,
            // and keeping it is also what stops a regrowth minting a second spindle on one bond.
            if (healthPrism && _siteOf.TryGetValue(healthPrism, out int site))
            {
                _siteOf.Remove(healthPrism);
                if (_occupant != null && _occupant[site] == healthPrism) _occupant[site] = null;
            }
            base.RemoveHealthBlock(healthPrism, killerName);
        }

        /// <summary>
        /// Forgets a limb the tracker retired, so the site it carried can grow a new one.
        /// The parameter is named <c>limb</c> rather than the base's <c>spindle</c> on
        /// purpose: <see cref="LifeForm.spindle"/> is the PREFAB field, and a parameter that
        /// shadows it here would make the two look interchangeable.
        /// </summary>
        public override void RemoveSpindle(Spindle limb)
        {
            if (_limb != null && limb)
                for (int i = 0; i < _limb.Length; i++)
                    if (_limb[i] == limb) { _limb[i] = null; break; }
            base.RemoveSpindle(limb);
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
