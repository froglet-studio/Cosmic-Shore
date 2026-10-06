using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using NVec = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A plant that grows ONE UNIT of a three-dimensional prismscape: one octagon tile of the gyroid flora's own
    /// tiling, on every one of a stack of nested gyroid sheets G = t_i, stitched through the stack along its gradient
    /// lines (Docs/ECOSYSTEM.md §58).
    ///
    /// <para><b>A plant is one octagon, exactly like a gyroid flora.</b> <see cref="NestedGyroidTemplate"/> is the
    /// gyroid flora's tiling measured as one period: 576 sites, 24 octagon rings, each ring OWNING the 23-25 sites
    /// nearest it along the bond graph (the gyroid's "24 prisms a lifeform", §32.7). This plant is the tile of one
    /// octagon - its ring and its patch - on every nested sheet, the images of those sites carried out along ∇G,
    /// plus the struts of its strutted columns: ~170-180 prisms. Its CRYSTAL sits at the octagon's centre, the plant's
    /// origin.</para>
    ///
    /// <para><b>Every prism hangs off the crystal through spindles.</b> The growth plan
    /// (<see cref="NestedGyroidPeriod.Plant"/>) is one tree rooted at the crystal: limbs out of the crystal to the
    /// ring's images on the heart's own sheet, then along the tile's own bonds, then out along each site's column,
    /// ring by ring. EVERY bond is a limb, posed on the bond and parented under its parent's limb, so the plant is one
    /// spindle tree hanging off its heart and a prism is never parented to another prism.</para>
    ///
    /// <para><b>It reproduces as a COLONY through the triply periodic structure</b>, the gyroid flora's model
    /// (<see cref="NestedGyroidColony"/>): every plant of a (cell, species) shares one lattice frame; a complete plant
    /// offers its four neighbouring tiles; once per cycle the population births ONE plant at a uniformly random open
    /// tile. So a population wanders through the periodic lattice tile by tile instead of filling a cube, and since
    /// the whole period is fitted as one periodic structure, neighbouring plants' prisms never overlap.</para>
    ///
    /// <para><b>The Urchin rides it in three directions.</b> The plant is an <see cref="ILayeredPrismscape"/> whose layer
    /// SPACE is its colony: stack coordinates agree across every plant of one colony, so the rider holds its sheet
    /// across plant boundaries and steps through the stack only when the pilot pitches toward the next layer.</para>
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

        NestedGyroidPeriod _period;
        NestedGyroidLattice _lattice;
        HealthPrism[] _occupant;
        Spindle[] _limb;
        readonly Dictionary<HealthPrism, int> _siteOf = new();
        int _laidCount;
        bool _spotChecked;

        // The colony this plant belongs to, and the tile it owns in it.
        NestedGyroidColony _colony;
        NestedGyroidColony.Tile _tile;
        bool _tileAssigned;
        bool _matured;
        NestedGyroidColony.Tile? _pendingBirth;

        // ---- the build cache: one PERIOD per settings key, built once, shared by every plant of the config.
        static readonly Dictionary<string, NestedGyroidPeriod> s_built = new();
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

        /// <summary>The live-prism budget: this plant's tile (the base reads it for maturity).</summary>
        protected override int PrismBudget => _lattice?.Count ?? (config ? config.PrismBudget : 0);

        /// <summary>Every prism's size and place come out of the fitted period, in absolute units - a per-cell leaf
        /// scale would lay prisms the lattice no longer describes.</summary>
        protected override bool PrismSizeFixedByGrowthRule => true;

        /// <summary>The octagon tile this plant owns in its colony.</summary>
        public NestedGyroidColony.Tile Tile => _tile;

        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;
            if (tuning.LatticeScale > 0f) _lengthScale = tuning.LatticeScale;
            if (tuning.MaxTotalSpawnedObjects >= 0) _budgetOverride = tuning.MaxTotalSpawnedObjects;
            if (tuning.MaxTotalSpawnedObjectsScale > 0f) _budgetScale = tuning.MaxTotalSpawnedObjectsScale;
        }

        /// <summary>
        /// The growth rule's inputs. The element speaks through the gyroid flora's own per-element numbers, carried on
        /// this plant's config: its LEAF (<see cref="Flora.LeafSize"/>, set from <c>Variant.LeafSize</c> by
        /// <see cref="Flora.ApplyVariantTuning"/>) and its LATTICE scale (<c>Variant.LatticeScale</c>) - the template's
        /// proportions, element by element (Tools/Build/author_nested_gyroid_flora_assets.py copies them across).
        /// </summary>
        NestedGyroidSettings ResolveSettings()
        {
            int budget = _budgetOverride >= 0 ? _budgetOverride : config.PrismBudget;
            // Round half UP explicitly (Mathf.RoundToInt is banker's rounding) - the Borromean's rule.
            if (_budgetScale > 0f) budget = Mathf.Max(16, Mathf.FloorToInt(budget * _budgetScale + 0.5f));
            return config.ToSettings(LeafSize, _lengthScale, budget);
        }

        /// <summary>World size of one period for this plant's element.</summary>
        float WorldPeriod => config ? config.CellSize * (_lengthScale > 0f ? _lengthScale : 1f) : 240f;

        // The grow period as AUTHORED, before the element's tempo law rescales it (Flora.OnElementResolved, inside
        // base.Initialize). A tick lays GrowthRate x THIS many prisms and fires every SCALED period, so the element's
        // law changes the plant's rate - sizing the tick from the scaled period would cancel it exactly, and every
        // element would grow at one speed.
        float _authoredGrowPeriod;

        public override void Initialize(Cell cell)
        {
            _authoredGrowPeriod = growPeriod;
            if (!config)
            {
                // A plant with no rule is a crystal and nothing else - loud, never silent.
                CSDebug.LogError($"{name}: NestedGyroidFlora has no NestedGyroidConfigSO assigned; it will not grow.", this);
            }
            base.Initialize(cell);
            if (config) StartCoroutine(BuildPeriodCoroutine(ResolveSettings()));
        }

        /// <summary>
        /// Builds (or fetches) the period in time slices - <see cref="NestedGyroidConfigSO.BuildSliceMilliseconds"/>
        /// per frame - so planting never hitches. Plants that share a config share ONE in-flight build: whichever
        /// asks first in a frame steps it. Then cuts this plant's tile out of it.
        /// </summary>
        IEnumerator BuildPeriodCoroutine(NestedGyroidSettings settings)
        {
            string key = settings.Key();
            if (!s_built.TryGetValue(key, out var period))
            {
                if (!s_building.TryGetValue(key, out var builder))
                {
                    builder = new NestedGyroidBuilder(settings);
                    s_building[key] = builder;
                }
                while (true)
                {
                    if (s_built.TryGetValue(key, out period)) break;
                    if (!s_steppedFrame.TryGetValue(key, out int frame) || frame != Time.frameCount)
                    {
                        s_steppedFrame[key] = Time.frameCount;
                        if (builder.Step(config.BuildSliceMilliseconds))
                        {
                            period = builder.Result;
                            s_built[key] = period;
                            s_building.Remove(key);
                            s_steppedFrame.Remove(key);
                            Report(period, builder);
                            break;
                        }
                    }
                    yield return null;
                }
            }

            _period = period;
            _lattice = period.Plant(_tile.Octagon);
            _occupant = new HealthPrism[_lattice.Count];
            _limb = new Spindle[_lattice.Count];
            if (_lattice.Stats.RootedPrisms != _lattice.Count || _lattice.Stats.TruncatedByBudget > 0)
                CSDebug.LogWarning(
                    $"{name}: nested gyroid tile {_tile} - {_lattice.Stats.RootedPrisms}/{_lattice.Count} prisms on the " +
                    $"crystal's spindle tree, {_lattice.Stats.TruncatedByBudget} cut by PrismBudget {config.PrismBudget}.", this);
        }

        /// <summary>The acceptance numbers, once per built period. A broken guarantee is a WARNING (a real fault); the
        /// counts themselves are a fact about a finished build and ride the Ecology channel.</summary>
        void Report(NestedGyroidPeriod period, NestedGyroidBuilder builder)
        {
            var s = period.Stats;
            if (CSDebug.IsVerbose(CSLogChannel.Ecology))
                CSDebug.LogVerbose(CSLogChannel.Ecology,
                    $"[NestedGyroid] {config.name}: {NestedGyroidBuilder.Describe(period.Plant(_tile.Octagon), period)}; " +
                    $"{builder.Slices} slices, worst {builder.MaxSliceMilliseconds:F2} ms");
            if (s.RemainingOverlaps > 0)
                CSDebug.LogWarning(
                    $"{name}: nested gyroid '{config.name}' broke its zero-overlap guarantee - {s.RemainingOverlaps} " +
                    "overlapping prism pair(s) in the period. Run Tools/Build/nested_gyroid_harness/run.sh with these settings.", this);
        }

        // ------------------------------------------------------------------ the colony

        /// <summary>Hands a daughter her tile BEFORE Initialize (Flora.ConfigureOffspring), so her Plant() roots her on
        /// it and her growth plan is that tile's.</summary>
        void AssignTile(NestedGyroidColony colony, NestedGyroidColony.Tile tile)
        {
            _colony = colony;
            _tile = tile;
            _tileAssigned = colony.TryClaim(tile, this);
        }

        public override void Plant()
        {
            if (_tileAssigned && _colony != null)
            {
                transform.SetPositionAndRotation(_colony.TileWorld(_tile), _colony.Rotation);
                return;
            }

            bool pinned = TryGetPlantPositionOverride(out var at);
            // No cell: a plant dropped into a scene by hand (a test scene) roots where it was placed.
            if (!pinned) at = cell ? ResolveDispersalPoint(legacyRadius: 200f) : transform.position;
            var rotation = cell || pinned ? Random.rotationUniform : transform.rotation;

            // A seeded plant JOINS its species' living colony at a random open tile, so the population stays one
            // periodic structure; a pinned one (the Spawn Matrix station) founds its own where it was asked to be.
            var existing = pinned ? null : NestedGyroidColony.Find(cell, SourceConfig);
            if (existing != null && existing.TryAnyOpenTile(out var open, t => InPlantingBand(existing, t))
                && existing.TryClaim(open, this))
            {
                _colony = existing;
                _tile = open;
            }
            else
            {
                // Founder: a new frame whose tile (0,0,0, random octagon) has its crystal exactly here.
                _tile = new NestedGyroidColony.Tile(0, 0, 0, Random.Range(0, NestedGyroidTemplate.OctagonCount));
                var c = NestedGyroidLattice.TileCenter(0, 0, 0, _tile.Octagon, WorldPeriod);
                var origin = at - rotation * new Vector3(c.X, c.Y, c.Z);
                bool register = existing == null && NestedGyroidColony.Find(cell, SourceConfig) == null;
                _colony = NestedGyroidColony.Found(register ? cell : null, register ? SourceConfig : null,
                    origin, rotation, WorldPeriod);
                _colony.TryClaim(_tile, this);
            }
            _tileAssigned = true;
            transform.SetPositionAndRotation(_colony.TileWorld(_tile), _colony.Rotation);
        }

        /// <summary>The colony's births ride the cell's fauna-wave cadence scaled by the CONFIG's element (the gyroid
        /// colony's rule, AssembledFlora.ColonyCyclePeriod): Time breeds fastest.</summary>
        float ColonyCyclePeriod
        {
            get
            {
                float period = cell ? cell.CurrentFaunaSpawnPeriod : 0f;
                if (period <= 0f) period = 30f;
                var element = SourceConfig ? SourceConfig.Element : Element.None;
                return FloraReproductionRules.ScaleCostPerChild(period, FloraReproductionRules.ReproductionRateFor(element));
            }
        }

        const float PopulationCycleStagger = 0.35f;

        /// <summary>A tile the colony may grow into: its crystal inside this species' planting band (never the
        /// nucleus, never past the band's outer edge) - the same band a dispersed seed and a default offspring keep
        /// to. A plant with no cell has no band and accepts every tile.</summary>
        bool InPlantingBand(NestedGyroidColony colony, NestedGyroidColony.Tile tile)
        {
            if (!cell) return true;
            Vector3 p = colony.TileWorld(tile);
            return (ClampToPlantingBand(p) - p).sqrMagnitude < 1e-4f;
        }

        /// <summary>
        /// The population drive, from every plant's grow tick (Docs/ECOSYSTEM.md §58.9). A plant offers its four
        /// neighbouring tiles the first tick it is COMPLETE (every prism of its tile laid once); once per cycle the
        /// population births one plant at a uniformly random open tile. Production gates (Frenzy freeze, the cell's
        /// cap) are checked BEFORE popping, so a capped colony burns no frontier.
        /// </summary>
        void TickColony()
        {
            if (_colony == null || !SourceConfig || !cell) return;
            if (!_matured && _lattice != null && _laidCount >= _lattice.Count)
            {
                _matured = true;
                _colony.ContributeNeighbors(_tile);
            }
            if (!_colony.TryBeginCycle(ColonyCyclePeriod, PopulationCycleStagger)) return;
            if (!cell.FloraPlantingEnabled || cell.IsFloraAtCap(SourceConfig)) return;
            if (!_colony.TryPopRandom(out var tile, t => InPlantingBand(_colony, t))) return;

            _pendingBirth = tile;
            bool born = TrySpawnOneOffspring();
            _pendingBirth = null;
            if (!born) _colony.Requeue(tile);
        }

        protected override bool TryResolveOffspringPlacement(out Vector3 position, out Quaternion rotation, out Vector3? up)
        {
            if (_pendingBirth.HasValue && _colony != null)
            {
                position = _colony.TileWorld(_pendingBirth.Value);
                rotation = _colony.Rotation;
                up = null;
                return true;
            }
            return base.TryResolveOffspringPlacement(out position, out rotation, out up);
        }

        protected override void ConfigureOffspring(Flora child)
        {
            if (child is NestedGyroidFlora daughter && _pendingBirth.HasValue && _colony != null)
                daughter.AssignTile(_colony, _pendingBirth.Value);
        }

        protected override void Die(string killerName = "")
        {
            ReleaseTile();
            base.Die(killerName);
        }

        protected override void OnDestroy()
        {
            ReleaseTile();
            base.OnDestroy();
        }

        void ReleaseTile()
        {
            if (_colony == null || !_tileAssigned) return;
            _colony.Release(_tile, this);
            _tileAssigned = false;
        }

        // ------------------------------------------------------------------ growth

        int PrismsPerTick =>
            config ? Mathf.Max(1, Mathf.RoundToInt(config.GrowthRate * Mathf.Max(0.05f, _authoredGrowPeriod))) : 1;

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

            TickColony();
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

            // EVERY prism hangs off a LIMB on its bond: rooted at its parent (the crystal, for the ring's first
            // prisms), aimed at it, stretched to span the gap, and parented under the parent's own limb - so the plant
            // is one spindle tree out of its heart. Up is this prism's +y, which keeps the branch's cross-section
            // turned with the structure instead of rolling about the bond. A limb outlives its grazed prism and is
            // reused when the prism regrows.
            Vector3 root = parent >= 0 ? SiteWorld(parent) : transform.position;
            Vector3 bond = pos - root;
            Spindle limb = _limb[site];
            if (!limb)
            {
                limb = AddSpindle();
                if (!limb) return false;
                limb.LifeForm = this;
                _limb[site] = limb;
                Transform under = parent >= 0 && _limb[parent] ? _limb[parent].transform : transform;
                limb.transform.SetParent(under, false);
                limb.transform.position = root;
                if (!SafeLookRotation.TrySet(limb.transform, bond, rot * Vector3.up, this))
                    limb.transform.rotation = rot;
                StretchToBond(limb, bond.magnitude);
            }

            var prism = EnvironmentPrismPool.Get(healthPrism, pos, rot);
            if (!prism) return false;

            // Parented to its LIMB, never to another prism: a prism wears its size as localScale, and a non-uniform
            // scale above a rotated child is a shear (Docs/ECOSYSTEM.md 37.9). The limb ROOT is never scaled - only
            // its own branch children are (StretchToBond) - so nothing leaks into the leaf.
            prism.transform.SetParent(limb.transform, true);
            prism.LifeForm = this;

            // DANGER, as the gyroid flora draws it: the template's octagon ring on every sheet, and every strut.
            // Stated true OR false before the team stamp - a pooled prism keeps its previous life's IsDangerous
            // (Prism.Initialize does not clear it: spawners request it), so a plain plate must say it is plain.
            bool danger = _lattice.DangerRing[site] || _lattice.Kind[site] == NestedGyroidPrismKind.Fiber;
            prism.prismProperties.IsDangerous = danger;
            if (danger) prism.MakeDangerous();
            prism.ChangeTeam(domain);

            _pendingPrismScale = V(_lattice.Size[site]);
            AddHealthBlock(prism);
            prism.Initialize("flora");
            ApplyShade(prism, site);

            _occupant[site] = prism;
            _siteOf[prism] = site;
            return true;
        }

        /// <summary>The through-thickness grade: a DARKENING of the domain colour only (gain <= 1, never lit), on plain and
        /// danger prisms alike - <see cref="Prism.SetColorShade"/> re-applies after every material sync, so a danger
        /// prism keeps its sheet's grade.</summary>
        void ApplyShade(HealthPrism prism, int site)
        {
            float gain = config.ColorMode == NestedGyroidColorMode.AlternatingSheets
                ? (_lattice.Sheet[site] % 2 == 0 ? 1f : config.AlternateSheetGain)
                : Mathf.Lerp(config.GradedInnerGain, 1f, _lattice.Gradient01(site));
            prism.SetColorShade(Mathf.Min(1f, gain));
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
        public object LayerSpace => (object)_colony ?? this;

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
        /// Pure preview - see <see cref="Flora.TryPreviewGrowth"/>: ONE PLANT, the same tile a planted one grows (an
        /// icon's 220-prism ask holds a whole ~180-prism plant). Built from a three-sheet period so a synchronous icon
        /// build stays cheap; cached per key. Never touches UnityEngine.Random.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (!config || into == null || budget <= 0) return false;
            var settings = ResolveSettings();
            settings.MaxSheets = Mathf.Min(settings.MaxSheets, 3);
            string key = settings.Key();
            if (!s_built.TryGetValue(key, out var period))
            {
                period = NestedGyroidBuilder.BuildNow(settings);
                s_built[key] = period;
            }
            var plant = period.Plant(0);
            int n = Mathf.Min(budget, plant.Count);
            for (int i = 0; i < n; i++)
                into.Add(new SpawnPoint(V(plant.Position[i]),
                    Quaternion.LookRotation(V(plant.Forward[i]), V(plant.Up[i])), V(plant.Size[i])));
            return n > 0;
        }
    }
}
