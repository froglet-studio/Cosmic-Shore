using System;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Runtime executor for seeding/bonding trail blocks into a wall.
    /// Exposes a stable API used by other actions (Cloak, etc.).
    /// </summary>
    public class SeedAssemblerActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [SerializeField] private TrailSpawner trailSpawner;

        // Runtime
        IVesselStatus _status;
        ResourceSystem _resources;

        public TrailBlock ActiveSeedBlock { get; private set; }
        Assembler _activeAssembler;

        public event Action<TrailBlock> OnSeedStarted;
        public event Action<TrailBlock> OnBondingBegan;
        public event Action OnSeedStopped;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _resources = shipStatus?.ResourceSystem;
            if (trailSpawner == null)
                trailSpawner = shipStatus?.TrailSpawner;
        }

        /// <summary>
        /// Entry from SeedWallActionSO: computes cost, validates block, sets ActiveSeedBlock.
        /// Does NOT start bonding yet (so other actions can "protect" it before bond starts).
        /// Returns true if a seed was set.
        /// </summary>
        public bool StartSeed(SeedWallActionSO so, IVesselStatus status)
        {
            if (so == null || status == null || _resources == null || trailSpawner == null)
                return false;

            // resource check
            var cost = so.ComputeCost(_resources);
            if (!HasResource(so.ResourceIndex, cost)) return false;

            // trail block check
            var last = GetLatestBlock();
            if (last == null && so.RequireExistingTrailBlock)
                return false;

            // set active and apply shield
            ActiveSeedBlock = last;
            if (ActiveSeedBlock == null) return false;

            ApplyShield(ActiveSeedBlock, so.ShieldOnSeed);

            // consume resource
            if (cost > 0f)
                _resources.ChangeResourceAmount(so.ResourceIndex, -cost);

            // attach assembler script based on config (if not already present)
            _activeAssembler = EnsureAssembler(ActiveSeedBlock, so.AssemblerType);
            if (_activeAssembler != null)
                _activeAssembler.Depth = so.BondingDepth;

            OnSeedStarted?.Invoke(ActiveSeedBlock);
            return true;
        }

        /// <summary>
        /// Begin the actual bonding/growth coroutine in the assembler.
        /// </summary>
        public void BeginBonding()
        {
            if (_activeAssembler == null) return;
            _activeAssembler.StartBonding();
            OnBondingBegan?.Invoke(ActiveSeedBlock);
        }

        /// <summary>
        /// Full stop. Clears references and stops any assembler work.
        /// </summary>
        public void StopSeedCompletely()
        {
            // best-effort: stop assembler if present
            if (_activeAssembler != null)
            {
                try { _activeAssembler.StopBonding(); }
                catch { /* no-op */ }
            }

            _activeAssembler = null;
            ActiveSeedBlock = null;

            OnSeedStopped?.Invoke();
        }

        // ===== Helpers =====

        TrailBlock GetLatestBlock()
        {
            var listA = trailSpawner?.Trail?.TrailList;
            if (listA != null && listA.Count > 0) return listA[^1];

            // In case you keep a secondary trail internally (compat with older code)
            var trail2Field = typeof(TrailSpawner).GetField("Trail2",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var trail2 = trail2Field?.GetValue(trailSpawner) as Trail;
            if (trail2 != null && trail2.TrailList.Count > 0) return trail2.TrailList[^1];

            return null;
        }

        bool HasResource(int index, float cost)
        {
            if (_resources == null || index < 0 || index >= _resources.Resources.Count) return false;
            var res = _resources.Resources[index];
            return (res != null && res.CurrentAmount >= cost);
        }

        void ApplyShield(TrailBlock block, SeedWallActionSO.ShieldMode mode)
        {
            if (block == null) return;
            switch (mode)
            {
                case SeedWallActionSO.ShieldMode.None:
                    break;
                case SeedWallActionSO.ShieldMode.Shield:
                    block.ActivateShield();
                    break;
                case SeedWallActionSO.ShieldMode.SuperShield:
                    block.ActivateSuperShield();
                    break;
            }
        }

        Assembler EnsureAssembler(TrailBlock block, SeedWallActionSO.AssemblerKind kind)
        {
            if (block == null) return null;

            // Reuse if already present
            var existing = block.GetComponent<Assembler>();
            if (existing != null) return existing;

            // Attach configured assembler
            switch (kind)
            {
                case SeedWallActionSO.AssemblerKind.Wall:
                    return block.gameObject.AddComponent<WallAssembler>();
                case SeedWallActionSO.AssemblerKind.Gyroid:
                    // If you have a GyroidAssembler type, add it here:
                    // return block.gameObject.AddComponent<GyroidAssembler>();
                    // Fallback to Wall if Gyroid not present in this build:
                    return block.gameObject.AddComponent<WallAssembler>();
                default:
                    return block.gameObject.AddComponent<WallAssembler>();
            }
        }
    }
}