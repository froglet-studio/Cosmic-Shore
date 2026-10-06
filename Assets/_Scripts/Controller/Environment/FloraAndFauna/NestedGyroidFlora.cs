using System.Collections;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using NVec = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A plant that grows a THREE-DIMENSIONAL prismscape: a stack of nested gyroid sheets G = t_i, woven together
    /// by fibers that run along ∇G through every sheet at right angles (Docs/ECOSYSTEM.md §58).
    ///
    /// <para>The plain <see cref="GyroidAssembler"/> flora tiles the single sheet G = 0, a 2D prismscape. This one
    /// tiles N sheets inside |t| &lt; √2 - where ∇G never vanishes, so the sheets never touch - and the fibers are
    /// what make them ONE object: cut them and the stack falls apart into separate shells (the harness's negative
    /// control). Plates lie IN their sheet with local +z on the sheet normal, their long axes combed across the
    /// sheet and turned a fixed angle per sheet (helicoidal plywood); struts lie ALONG their fiber. Every size
    /// comes from the local layer gap Δt/|∇G| and is then FITTED so no prism interpenetrates another.</para>
    ///
    /// <para><b>It grows the way a flora withers, run backwards.</b> The crystal sits at G(0) = 0, on the t = 0
    /// sheet; the plant grows that sheet outward from it, then ring by ring - each new sheet spreading out of the
    /// fibers that reached it - and every prism's parent is already standing (<see cref="NestedGyroidLattice"/>).
    /// The limbs are the BONDS that are limbs: out of the heart, and along every fiber. A plate beside a plate is
    /// tiling, not a limb, and carries none.</para>
    ///
    /// <para><b>It is a COMPACT species.</b> The cube it is clipped to is finished, so like the Borromean it
    /// completes, stops, and funds an ordinary per-plant offspring from its growth quota. No frontier, no claim
    /// book, no lattice-scale tolerances. Its geometry is a measured table in absolute units, so
    /// <see cref="PrismSizeFixedByGrowthRule"/> is true; it resizes through FloraVariantTuning.LatticeScale, a
    /// uniform similarity of the whole lattice.</para>
    ///
    /// <para><b>The Urchin rides it in three directions.</b> The plant is an <see cref="ILayeredPrismscape"/>: it
    /// tells the ride kernel which layer each prism is on, so the rider holds its sheet while steering and steps
    /// through the stack only when the pilot pitches toward the next layer (§58.4).</para>
    /// </summary>
    public class NestedGyroidFlora : Flora, ILayeredPrismscape
    {
        [Header("Nested Gyroid")]
        [Tooltip("The stack, its fibers, its growth rate, budget and look. Required.")]
        [SerializeField] NestedGyroidConfigSO config;

        // Per-element overrides (FloraVariantTuning). -1 / 0 = keep the config's.
        int _budgetOverride = -1;
        float _budgetScale;
        float _lengthScale = 1f;

        NestedGyroidLattice _lattice;
        HealthPrism[] _occupant;
        Spindle[] _limb;
        readonly Dictionary<HealthPrism, int> _siteOf = new();
        int _laidCount;
        bool _spotChecked;

        // ---- the build cache: one lattice per settings key, built once, shared by every plant of the config.
        static readonly Dictionary<string, NestedGyroidLattice> s_built = new();
        static readonly Dictionary<string, NestedGyroidBuilder> s_building = new();
        static readonly Dictionary<string, int> s_steppedFrame = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_built.Clear();
            s_building.Clear();
            s_steppedFrame.Clear();
            BranchReach.Clear();
        }

        // Measured once per spindle PREFAB (see ResolveBranchReach).
        static readonly Dictionary<int, float> BranchReach = new();

        /// <summary>The live-prism budget: the lattice this plant grows (the base reads it for maturity).</summary>
        protected override int PrismBudget => _lattice?.Count ?? (config ? config.PrismBudget : 0);

        /// <summary>Every prism's size and place come out of the fitted lattice, in absolute units - a per-cell
        /// leaf scale would lay prisms the lattice no longer describes.</summary>
        protected override bool PrismSizeFixedByGrowthRule => true;

        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;
            if (tuning.LatticeScale > 0f) _lengthScale = tuning.LatticeScale;
            if (tuning.MaxTotalSpawnedObjects >= 0) _budgetOverride = tuning.MaxTotalSpawnedObjects;
            if (tuning.MaxTotalSpawnedObjectsScale > 0f) _budgetScale = tuning.MaxTotalSpawnedObjectsScale;
        }

        NestedGyroidSettings ResolveSettings()
        {
            int budget = _budgetOverride >= 0 ? _budgetOverride : config.PrismBudget;
            // Round half UP explicitly (Mathf.RoundToInt is banker's rounding) - the Borromean's rule.
            if (_budgetScale > 0f) budget = Mathf.Max(16, Mathf.FloorToInt(budget * _budgetScale + 0.5f));
            return config.ToSettings(_lengthScale, budget);
        }

        public override void Initialize(Cell cell)
        {
            if (!config)
            {
                // A plant with no rule is a crystal and nothing else - loud, never silent.
                CSDebug.LogError($"{name}: NestedGyroidFlora has no NestedGyroidConfigSO assigned; it will not grow.", this);
            }
            base.Initialize(cell);
            if (config) StartCoroutine(BuildLatticeCoroutine(ResolveSettings()));
        }

        /// <summary>
        /// Builds (or fetches) the lattice in time slices - <see cref="NestedGyroidConfigSO.BuildSliceMilliseconds"/>
        /// per frame - so planting never hitches. Plants that share a config share ONE in-flight build: whichever
        /// asks first in a frame steps it.
        /// </summary>
        IEnumerator BuildLatticeCoroutine(NestedGyroidSettings settings)
        {
            string key = settings.Key();
            if (!s_built.TryGetValue(key, out var lattice))
            {
                if (!s_building.TryGetValue(key, out var builder))
                {
                    builder = new NestedGyroidBuilder(settings);
                    s_building[key] = builder;
                }
                while (true)
                {
                    if (s_built.TryGetValue(key, out lattice)) break;
                    if (!s_steppedFrame.TryGetValue(key, out int frame) || frame != Time.frameCount)
                    {
                        s_steppedFrame[key] = Time.frameCount;
                        if (builder.Step(config.BuildSliceMilliseconds))
                        {
                            lattice = builder.Result;
                            s_built[key] = lattice;
                            s_building.Remove(key);
                            s_steppedFrame.Remove(key);
                            Report(lattice, builder);
                            break;
                        }
                    }
                    yield return null;
                }
            }

            _lattice = lattice;
            _occupant = new HealthPrism[lattice.Count];
            _limb = new Spindle[lattice.Count];
        }

        /// <summary>The acceptance numbers, once per built lattice. A broken guarantee is a WARNING (a real fault);
        /// the counts themselves are a fact about a finished build and ride the Ecology channel.</summary>
        void Report(NestedGyroidLattice lattice, NestedGyroidBuilder builder)
        {
            var s = lattice.Stats;
            if (CSDebug.IsVerbose(CSLogChannel.Ecology))
                CSDebug.LogVerbose(CSLogChannel.Ecology,
                    $"[NestedGyroid] {config.name}: {NestedGyroidBuilder.Describe(lattice)}; " +
                    $"{builder.Slices} slices, worst {builder.MaxSliceMilliseconds:F2} ms");

            if (s.Components != 1 || s.MaxEdgeOverReach >= 1f || s.RemainingOverlaps > 0)
                CSDebug.LogWarning(
                    $"{name}: nested gyroid '{config.name}' broke a guarantee - {s.Components} component(s), worst bond " +
                    $"{s.MaxEdgeOverReach:P0} of the Urchin's reach, {s.RemainingOverlaps} overlapping prism pair(s). " +
                    "Run Tools/Build/nested_gyroid_harness/run.sh with these settings.", this);
            if (s.TruncatedByBudget > 0)
                CSDebug.LogWarning(
                    $"{name}: nested gyroid '{config.name}' could not fit its stack in {config.PrismBudget} prisms even " +
                    $"after coarsening; {s.TruncatedByBudget} outer prisms were cut, so its outer skin is incomplete. " +
                    "Raise PrismBudget or lower CellsPerSide / SheetCount.", this);
        }

        public override void Plant()
        {
            if (TryGetPlantPositionOverride(out var pinned))
                transform.position = pinned;
            // No cell: a plant dropped into a scene by hand (a test scene) roots where it was placed.
            else if (cell)
                transform.position = ResolveDispersalPoint(legacyRadius: 200f);

            // One fixed lattice per config; a random attitude per plant shows each a different face of it.
            if (cell) transform.rotation = Random.rotationUniform;
        }

        int PrismsPerTick =>
            config ? Mathf.Max(1, Mathf.RoundToInt(config.GrowthRate * Mathf.Max(0.05f, growPeriod))) : 1;

        public override void Grow()
        {
            if (_lattice == null) return;
            if (cell && !cell.FloraGrowingEnabled) return;

            int quota = PrismsPerTick;
            int laid = 0;
            for (int i = 0; i < _lattice.Count && laid < quota; i++)
            {
                if (_occupant[i]) continue;
                int parent = _lattice.Parent[i];
                // A prism never grows off a parent that is not standing (grazed): it regrows from the heart outward.
                if (parent >= 0 && !_occupant[parent]) continue;
                if (!LayAt(i)) break;
                laid++;
            }

            if (laid > 0)
            {
                _laidCount += laid;
                NotifyGrew(laid);
            }

            if (!_spotChecked && _laidCount >= _lattice.Count)
            {
                _spotChecked = true;
                if (CSDebug.IsVerbose(CSLogChannel.Ecology)) SpotCheckOverlaps();
            }
        }

        static Vector3 V(NVec v) => new Vector3(v.X, v.Y, v.Z);

        Vector3 SiteWorld(int site) => transform.TransformPoint(V(_lattice.Position[site]));

        Quaternion SiteRotation(int site) =>
            transform.rotation * Quaternion.LookRotation(V(_lattice.Forward[site]), V(_lattice.Up[site]));

        bool LayAt(int site)
        {
            Vector3 pos = SiteWorld(site);
            Quaternion rot = SiteRotation(site);
            int parent = _lattice.Parent[site];

            Transform holder = transform;
            if (_lattice.LimbBond[site])
            {
                // A LIMB: out of the heart, or along a fiber - rooted at the parent, aimed at this prism, stretched
                // to span the bond. Up is this prism's +z (the sheet normal / fiber axis), which keeps the branch's
                // cross-section turned with the structure instead of rolling about the bond.
                Vector3 root = parent >= 0 ? SiteWorld(parent) : transform.position;
                Vector3 bond = pos - root;
                Spindle limb = _limb[site];
                if (!limb)
                {
                    limb = AddSpindle();
                    if (!limb) return false;
                    limb.LifeForm = this;
                    _limb[site] = limb;
                    limb.transform.position = root;
                    Vector3 up = rot * Vector3.up;
                    if (!SafeLookRotation.TrySet(limb.transform, bond, up, this))
                        limb.transform.rotation = rot;
                    StretchToBond(limb, bond.magnitude);
                }
                holder = limb.transform;
            }

            var prism = EnvironmentPrismPool.Get(healthPrism, pos, rot);
            if (!prism) return false;

            // Parented to its LIMB or to the plant root, never to another prism: a prism wears its size as
            // localScale, and a non-uniform scale above a rotated child is a shear (Docs/ECOSYSTEM.md 37.9).
            prism.transform.SetParent(holder, true);
            prism.LifeForm = this;
            prism.ChangeTeam(domain);

            _pendingPrismScale = V(_lattice.Size[site]);
            AddHealthBlock(prism);
            prism.Initialize("flora");
            ApplyShade(prism, site);

            _occupant[site] = prism;
            _siteOf[prism] = site;
            return true;
        }

        void ApplyShade(HealthPrism prism, int site)
        {
            if (_lattice.Kind[site] == NestedGyroidPrismKind.Fiber)
            {
                prism.SetColorShade(1f, config.FiberWhiten);
                return;
            }
            float gain = config.ColorMode == NestedGyroidColorMode.AlternatingSheets
                ? (_lattice.Sheet[site] % 2 == 0 ? 1f : config.AlternateSheetGain)
                : Mathf.Lerp(config.GradedGain.x, config.GradedGain.y, _lattice.Gradient01(site));
            prism.SetColorShade(gain, 0f);
        }

        // The size the next AddHealthBlock stamps: this species fits a size per prism (MandelbulbFlora's pattern).
        Vector3? _pendingPrismScale;

        public override void AddHealthBlock(HealthPrism healthPrism)
        {
            base.AddHealthBlock(healthPrism);
            if (healthPrism && _pendingPrismScale.HasValue)
            {
                // STATED, not grown into: must survive PrismScaleAnimator's silent per-axis clamp.
                healthPrism.AdmitTargetScale(_pendingPrismScale.Value);
                healthPrism.TargetScale = _pendingPrismScale.Value;
            }
            _pendingPrismScale = null;
        }

        /// <inheritdoc/>
        public bool TryGetStackCoordinate(Prism prism, out int coordinate)
        {
            if (_lattice != null && prism is HealthPrism hp && _siteOf.TryGetValue(hp, out int site))
            {
                coordinate = _lattice.Stack[site];
                return true;
            }
            coordinate = 0;
            return false;
        }

        public override void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "")
        {
            // Free the site BEFORE the base call (it may decide this plant is dead). The limb stays: a branch
            // whose leaf was eaten is still a branch, and keeping it stops a regrowth minting a second one.
            if (healthPrism && _siteOf.TryGetValue(healthPrism, out int site))
            {
                _siteOf.Remove(healthPrism);
                if (_occupant != null && _occupant[site] == healthPrism) _occupant[site] = null;
                _laidCount = Mathf.Max(0, _laidCount - 1);
            }
            base.RemoveHealthBlock(healthPrism, killerName);
        }

        public override void RemoveSpindle(Spindle limb)
        {
            if (_limb != null && limb)
                for (int i = 0; i < _limb.Length; i++)
                    if (_limb[i] == limb) { _limb[i] = null; break; }
            base.RemoveSpindle(limb);
        }

        /// <summary>
        /// Physics spot-check of the zero-overlap guarantee, once per plant at full growth, on the Ecology channel:
        /// an OverlapBox per sampled prism, counting hits on prisms of THIS plant on a different layer. The harness
        /// proves the property exactly over every pair; this checks the engine laid what the lattice says. Prisms
        /// whose collider is LOD-culled are skipped and counted, so a quiet result is never a vacuous one.
        /// </summary>
        void SpotCheckOverlaps()
        {
            const int samples = 64;
            int step = Mathf.Max(1, _lattice.Count / samples);
            int checkedCount = 0, culled = 0, overlaps = 0;
            var hits = new Collider[16];
            for (int i = 0; i < _lattice.Count; i += step)
            {
                var prism = _occupant[i];
                if (!prism) continue;
                if (!prism.TryGetComponent(out Collider own) || !own.enabled) { culled++; continue; }
                checkedCount++;
                // Shrunk slightly so a shared face (there are none by construction) or float noise never counts.
                Vector3 half = 0.49f * prism.transform.lossyScale;
                int n = Physics.OverlapBoxNonAlloc(prism.transform.position, half, hits, prism.transform.rotation,
                    ~0, QueryTriggerInteraction.Collide);
                for (int h = 0; h < n; h++)
                {
                    if (!hits[h] || hits[h] == own || !hits[h].TryGetComponent(out HealthPrism other)) continue;
                    if (other.LifeForm != this || !_siteOf.TryGetValue(other, out int j)) continue;
                    if (_lattice.Stack[j] != _lattice.Stack[i]) overlaps++;
                }
            }

            if (overlaps > 0)
                CSDebug.LogWarning($"{name}: nested gyroid physics spot-check found {overlaps} cross-layer overlap(s) " +
                                   $"in {checkedCount} sampled prisms.", this);
            else
                CSDebug.LogVerbose(CSLogChannel.Ecology,
                    $"[NestedGyroid] physics spot-check: 0 cross-layer overlaps in {checkedCount} prisms " +
                    $"({culled} skipped, collider LOD-culled)");
        }

        // ---- limbs (BorromeanFlora's measured stretch; the branch runs along the spindle's local +z)

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

        float ResolveBranchReach(Spindle limb)
        {
            int key = spindle ? spindle.GetInstanceID() : 0;
            if (BranchReach.TryGetValue(key, out float cached)) return cached;

            Transform root = limb.transform;
            float reach = 0f;
            foreach (var mf in limb.GetComponentsInChildren<MeshFilter>(true))
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

            if (reach <= 0f)
                CSDebug.LogWarning(
                    $"{name}: spindle prefab '{(spindle ? spindle.name : "none")}' has no branch geometry along its " +
                    "local +z, so NestedGyroidFlora cannot stretch its limbs to their bonds. Wire Branch.prefab.", this);
            BranchReach[key] = reach;
            return reach;
        }

        /// <summary>
        /// Pure preview - see <see cref="Flora.TryPreviewGrowth"/>. An icon asks for a few hundred prisms (the Spawn
        /// Matrix asks 220), and the first 220 of the real growth order are a disc of the central sheet - nothing
        /// that says "stack". So the preview grows the WHOLE stack on a tiling coarsened to fit the request
        /// (<see cref="NestedGyroidSettings.PreviewOf"/>): every sheet and fiber, fewer and bigger plates. It is the
        /// same rule on the same field, cached per key, and cheap enough to build synchronously (~1/30 of the full
        /// build - the voxel grid shrinks with the spacing). Never touches UnityEngine.Random.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (!config || into == null || budget <= 0) return false;
            var settings = ResolveSettings().PreviewOf(budget);
            string key = settings.Key();
            if (!s_built.TryGetValue(key, out var lattice))
            {
                lattice = NestedGyroidBuilder.BuildNow(settings);
                s_built[key] = lattice;
            }
            int n = Mathf.Min(budget, lattice.Count);
            for (int i = 0; i < n; i++)
                into.Add(new SpawnPoint(V(lattice.Position[i]),
                    Quaternion.LookRotation(V(lattice.Forward[i]), V(lattice.Up[i])), V(lattice.Size[i])));
            return n > 0;
        }
    }
}
