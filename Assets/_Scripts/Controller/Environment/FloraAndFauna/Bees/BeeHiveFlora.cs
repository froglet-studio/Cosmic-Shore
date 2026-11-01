using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE BEE HIVE (Garrett, 2026-10-31; Docs/ECOSYSTEM.md §66): the home of a POLLINATOR population, and the reason
    /// that population takes a FLORA slot. The hive is a plant in every way the cell can see - listed in a spawn
    /// profile's <c>SupportedFloras</c>, counted against the flora budget and caps, frozen by the Frenzy production
    /// freeze, reproducing through the one flora offspring path - and its bees are its members, raised from it the way
    /// a colony raises its members, never seeded by the fauna spawner.
    ///
    /// <para><b>The comb.</b> A stack of hex combs (<see cref="BeeMath.CellPosition"/>) round the heart, which sits in
    /// the first comb's centre cell like a queen cell. The first <see cref="seedCells"/> cells grow the way any plant
    /// grows (a producer), in the hive's own domain; every cell past them is BUILT FROM THE STORE: the mass the bees carry
    /// home, which they sting off opposing fauna, laid in the DOMAIN of the mass that paid for it, so the comb is made of
    /// all three domains (Garrett, 2026-11-01). <see cref="BeeMath.HoneyLedger"/> keeps that ledger per domain, so the
    /// comb's growth past its seed is exactly what was carried in, colour for colour, and nothing is minted. The hive
    /// itself stays ONE plant of one domain for the cell's accounting (its slot, its cap, its heart).</para>
    ///
    /// <para><b>The bees.</b> One more bee for every <see cref="cellsPerBee"/> cells of comb, up to <see cref="maxBees"/>,
    /// hatched one per <see cref="Cell.CurrentFaunaSpawnPeriod"/> (× <see cref="hatchPeriodScale"/>) - the period every
    /// population reads, so it is right under both spawners. A bee of the hive's own element, from
    /// <see cref="beeConfigs"/>.</para>
    ///
    /// <para><b>Native, and adopted.</b> A cell grows hives through its spawn profile like any flora; the Ratel's raided
    /// hive and the Honeyguide's Mass hive become one through <see cref="SeedAt"/>, which hands the standing prisms
    /// over as the first comb (<see cref="QueueAdoption"/>).</para>
    /// </summary>
    public class BeeHiveFlora : Flora
    {
        [Header("The comb")]
        [Tooltip("Each comb cell's prism (local units): a tube along the comb's axis.")]
        [SerializeField] Vector3 cellLeaf = new(2.4f, 2.4f, 6f);
        [Tooltip("Centre-to-centre spacing of neighbouring cells in a comb.")]
        [SerializeField, Min(0.5f)] float cellSpacing = 4.2f;
        [Tooltip("Spacing between the hive's stacked combs, along their axis.")]
        [SerializeField, Min(0.5f)] float combSpacing = 7f;
        [Tooltip("Cells per comb: a whole hexagon of rings (19 = 2 rings, 37 = 3 rings).")]
        [SerializeField, Min(1)] int cellsPerComb = 19;
        [Tooltip("Cells the hive grows on its own, as any plant grows (the hive's seed). Every cell past these is built " +
                 "from the mass its bees carry home.")]
        [SerializeField, Min(0)] int seedCells = 12;
        [Tooltip("The most cells the comb can hold (its prism budget).")]
        [SerializeField, Min(1)] int maxCells = 57;

        [Header("The bees")]
        [Tooltip("The bee species per element (Charge, Mass, Space, Time): the hive raises bees of its own element.")]
        [SerializeField] FaunaConfigurationSO[] beeConfigs = new FaunaConfigurationSO[4];
        [Tooltip("Bees the seed comb keeps.")]
        [SerializeField, Min(0)] int minBees = 3;
        [Tooltip("One more bee for every this many cells past the seed.")]
        [SerializeField, Min(1)] int cellsPerBee = 9;
        [Tooltip("The most bees one hive keeps.")]
        [SerializeField, Min(0)] int maxBees = 8;
        [Tooltip("A bee hatches every (the cell's fauna spawn period × this) seconds while the hive is short of bees.")]
        [SerializeField, Min(0.05f)] float hatchPeriodScale = 0.25f;
        [Tooltip("Bees hatch within this radius of the hive.")]
        [SerializeField, Min(0f)] float hatchRadius = 12f;

        [Header("Territory")]
        [Tooltip("An opposing creature this close to the hive is raided: the bees fly at it and sting its body prisms off, " +
                 "carrying them home.")]
        [SerializeField, Min(0f)] float guardRadius = 260f;
        [Tooltip("Flowers within this radius of the hive are the bees' to pollinate.")]
        [SerializeField, Min(0f)] float forageRadius = 420f;
        [Tooltip("Seconds between the hive's re-reads of its intruders and flowers (shared by all its bees).")]
        [SerializeField, Min(0.1f)] float senseSeconds = 0.5f;

        [Header("Audio (empty = silent; wired by the audio owner)")]
        [Tooltip("A comb cell built from brought-home mass.")]
        [SerializeField] EventReference hiveGrowSfx;

        readonly BeeMath.HoneyLedger _store = new();
        Domains? _layDomain;
        int _nextCell;
        float _nextHatchAt = -1f;
        readonly List<BeeFauna> _bees = new();
        readonly List<Flora> _flowers = new();
        Fauna _intruder;
        float _sensedAt = -99f;
        Spindle _comb;
        IReadOnlyList<HealthPrism> _adopt;

        /// <summary>The ledger of what the bees brought home, per domain (read by tests and the codex).</summary>
        public BeeMath.HoneyLedger Store => _store;

        /// <summary>A comb cell is laid in the domain of the mass that paid for it (the seed cells, in the hive's own).</summary>
        protected override Domains NewBlockDomain => _layDomain ?? domain;
        public int CombCells => HealthBlockCount;
        public int LiveBees => _bees.Count;
        public float GuardRadius => guardRadius;
        float CellVolume => Mathf.Abs(cellLeaf.x * cellLeaf.y * cellLeaf.z);

        protected override int PrismBudget => maxCells;

        /// <summary>The comb is a measured lattice in absolute units, so a per-cell leaf scale must not resize it.</summary>
        protected override bool PrismSizeFixedByGrowthRule => true;

        // ------------------------------------------------------------------ the entry point the vessels use

        /// <summary>
        /// Found a hive of <paramref name="hiveConfig"/>'s species at <paramref name="position"/>, in
        /// <paramref name="domain"/>, through the one canonical flora spawn path (so the cell's caps, its registry and the
        /// variant all apply). <paramref name="adopt"/>, if given, are standing prisms that become the hive's first comb:
        /// the Ratel's raided hive (what was left of it) and, later, the Honeyguide's Mass hive. Null when the cell
        /// refuses (no config, not the sim authority, or the hive could not be planted).
        /// </summary>
        public static BeeHiveFlora SeedAt(Cell cell, FloraConfigurationSO hiveConfig, Vector3 position, Domains domain,
            IReadOnlyList<HealthPrism> adopt = null)
        {
            if (!cell || !hiveConfig || hiveConfig.FloraPrefab is not BeeHiveFlora) return null;
            var flora = CellLifeSpawnerBase.SpawnFlora(cell, hiveConfig.FloraPrefab, null, hiveConfig, position,
                preInitialize: f => { if (f is BeeHiveFlora hive) hive.QueueAdoption(adopt); },
                domainOverride: domain);
            return flora as BeeHiveFlora;
        }

        /// <summary>Hand standing prisms to this hive as its first comb (before Initialize; taken on Plant).</summary>
        public void QueueAdoption(IReadOnlyList<HealthPrism> prisms) => _adopt = prisms;

        /// <summary>A bee is home with <paramref name="volume"/> of mass stung off opposing fauna of <paramref name="from"/>:
        /// comb of that domain is built from it.</summary>
        public void Deposit(float volume, Domains from) => _store.Deposit((int)from, volume);

        // ------------------------------------------------------------------ planting and growth

        public override void Plant()
        {
            if (TryGetPlantPositionOverride(out var pinned)) transform.position = pinned;
            else transform.position = ResolveDispersalPoint(legacyRadius: 200f);
            transform.rotation = Random.rotationUniform;
            TakeAdoption();
        }

        void TakeAdoption()
        {
            var adopt = _adopt;
            _adopt = null;
            if (adopt == null) return;
            for (int i = 0; i < adopt.Count; i++)
            {
                var p = adopt[i];
                if (!p || p.destroyed) continue;
                p.transform.SetParent(Comb().transform, true);
                p.LifeForm = this;
                _layDomain = p.Domain;   // an adopted prism keeps its own domain: the hive is made of all three
                AddHealthBlock(p);
                _layDomain = null;
            }
            // the adopted comb stands in for as much of the seed as it covers
            _nextCell = Mathf.Max(_nextCell, Mathf.Min(maxCells, HealthBlockCount));
        }

        Spindle Comb()
        {
            if (_comb) return _comb;
            _comb = AddSpindle();
            _comb.transform.SetPositionAndRotation(transform.position, transform.rotation);
            return _comb;
        }

        public override void Grow()
        {
            if (IsDying || !healthPrism) return;
            TryHatch();
            // the production freeze: a plant stops growing at Frenzy; brought-home mass waits in the store
            if (cell && !cell.FloraGrowingEnabled) return;
            if (_nextCell >= maxCells) return;
            bool seed = _nextCell < seedCells;
            float cost = CellVolume;
            int from = (int)domain;
            if (!seed && !_store.TrySpend(cost, out from)) return;
            if (LayCell(_nextCell, (Domains)from))
            {
                _nextCell++;
                if (!seed) Play(hiveGrowSfx, transform.position);
            }
            else if (!seed) _store.Refund(from, cost);
        }

        bool LayCell(int index, Domains cellDomain)
        {
            // cell 0 of the first comb is the heart's (the queen cell): the comb is laid round it
            BeeMath.CellPosition(index + 1, cellsPerComb, cellSpacing, combSpacing, out float x, out float y, out float z);
            var t = transform;
            var position = t.TransformPoint(new Vector3(x, y, z));
            var prism = EnvironmentPrismPool.Get(healthPrism, position, t.rotation);
            if (!prism) return false;
            _layDomain = cellDomain;
            AddHealthBlock(prism);
            _layDomain = null;
            prism.AdmitTargetScale(cellLeaf);
            prism.TargetScale = cellLeaf;
            prism.transform.SetParent(Comb().transform, true);
            prism.LifeForm = this;
            prism.Initialize("flora");
            NotifyGrew();
            return true;
        }

        // ------------------------------------------------------------------ the colony

        void TryHatch()
        {
            if (!cell || !FloraNetworkSync.IsSimAuthority) return;
            float now = Time.time;
            // stamp the clock when the period elapses, BEFORE the can-I-produce gates (a hive at its bee count must not
            // bank time and then hatch a burst the moment a bee dies)
            if (_nextHatchAt < 0f) _nextHatchAt = now + Mathf.Max(0.5f, cell.CurrentFaunaSpawnPeriod * hatchPeriodScale);
            if (now < _nextHatchAt) return;
            _nextHatchAt = now + Mathf.Max(0.5f, cell.CurrentFaunaSpawnPeriod * hatchPeriodScale);

            _bees.RemoveAll(b => !b);
            if (!cell.FloraPlantingEnabled) return;
            if (_bees.Count >= BeeMath.BeesFor(HealthBlockCount, seedCells, cellsPerBee, minBees, maxBees)) return;
            var cfg = BeeConfigFor(Element);
            if (!cfg || !cfg.FaunaPrefab) return;

            var at = transform.position + Random.insideUnitSphere * hatchRadius;
            var child = Instantiate(cfg.FaunaPrefab, at, Random.rotationUniform);
            child.domain = domain;
            if (child is BeeFauna bee)
            {
                bee.BindHive(this);
                _bees.Add(bee);
            }
            child.Goal = transform.position;
            child.Initialize(cell);
            child.AssignLineage(cell, cfg);
            cell.RegisterSpawnedObject(child.gameObject);
            FaunaNetworkSync.ServerSpawn(child);
        }

        FaunaConfigurationSO BeeConfigFor(Element element)
        {
            if (beeConfigs == null || beeConfigs.Length == 0) return null;
            int i = element >= Element.Charge && element <= Element.Time ? (int)element - 1 : 1;   // no element: Mass
            i = Mathf.Clamp(i, 0, beeConfigs.Length - 1);
            return beeConfigs[i] ? beeConfigs[i] : System.Array.Find(beeConfigs, c => (bool)c);
        }

        internal void Unbind(BeeFauna bee) => _bees.Remove(bee);

        // ------------------------------------------------------------------ what the bees read (shared, re-read on a clock)

        void Sense()
        {
            float now = Time.time;
            if (now - _sensedAt < senseSeconds) return;
            _sensedAt = now;
            _intruder = null;
            var c = cell;
            Vector3 home = transform.position;
            if (c)
            {
                float best = guardRadius * guardRadius;
                var live = c.LiveFauna;
                for (int i = 0; i < live.Count; i++)
                {
                    var f = live[i];
                    if (!f || f.Domain == domain || !f.IsAlivePrey) continue;
                    float d = (f.transform.position - home).sqrMagnitude;
                    if (d < best) { best = d; _intruder = f; }
                }
            }
            _flowers.Clear();
            float reach = forageRadius * forageRadius;
            var flora = FloraHeartRegistry.Live;
            for (int i = 0; i < flora.Count; i++)
            {
                var f = flora[i];
                if (!f || f is BeeHiveFlora || f.IsDying) continue;
                if ((f.transform.position - home).sqrMagnitude <= reach) _flowers.Add(f);
            }
        }

        /// <summary>The nearest opposing creature inside the guard radius, or false.</summary>
        public bool TryGetIntruder(out Fauna intruder)
        {
            Sense();
            intruder = _intruder && _intruder.IsAlivePrey ? _intruder : null;
            return intruder;
        }

        /// <summary>Flower <paramref name="visit"/> of bee <paramref name="salt"/>'s trip (<see cref="BeeMath.PickIndex"/>), or false.</summary>
        public bool TryPickFlower(int salt, int visit, out Flora flower)
        {
            Sense();
            int i = BeeMath.PickIndex(_flowers.Count, salt, visit);
            flower = i >= 0 ? _flowers[i] : null;
            return flower && !flower.IsDying;
        }

        // ------------------------------------------------------------------ lifecycle

        protected override void Die(string killerName = "")
        {
            // the bees outlive their hive: they keep pollinating where they are (BeeFauna with no hive)
            for (int i = 0; i < _bees.Count; i++) if (_bees[i]) _bees[i].BindHive(null);
            _bees.Clear();
            base.Die(killerName);
        }

        protected override void OnDestroy()
        {
            for (int i = 0; i < _bees.Count; i++) if (_bees[i]) _bees[i].BindHive(null);
            _bees.Clear();
            base.OnDestroy();
        }

        static void Play(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, at);
        }
    }
}
