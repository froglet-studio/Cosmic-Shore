using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Serialization;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime executor for seeding/bonding trail blocks into a wall.
    /// Exposes a stable API used by other actions (Cloak, etc.).
    /// </summary>
    public class SeedAssemblerActionExecutor : ShipActionExecutorBase
    {
        [FormerlySerializedAs("prismSpawner")]
        [FormerlySerializedAs("trailSpawner")]
        [Header("Scene Refs")]
        [SerializeField] private VesselPrismController vesselPrismController;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        // Runtime
        IVesselStatus _status;
        ResourceSystem _resources;

        public Prism ActiveSeedBlock { get; private set; }
        Assembler _activeAssembler;

        public event Action<Prism> OnSeedStarted;
        public event Action<Prism> OnBondingBegan;
        public event Action OnSeedStopped;

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            End();
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        void OnTurnEndOfMiniGame() => End();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status    = shipStatus;
            _resources = shipStatus?.ResourceSystem;
            if (vesselPrismController == null)
                vesselPrismController = shipStatus?.VesselPrismController;
        }

        /// <summary>
        /// Entry from SeedWallActionSO: computes cost, validates block, sets ActiveSeedBlock.
        /// Does NOT start bonding yet (so other actions can "protect" it before bond starts).
        /// Returns true if a seed was set.
        /// </summary>
        public bool StartSeed(SeedWallActionSO so, IVesselStatus status)
        {
            if (!so || status == null || !_resources || !vesselPrismController)
                return false;

            var cost = so.ComputeCost(_resources);
            if (!HasResource(so.ResourceIndex, cost)) return false;
            
            var last = GetLatestBlock();
            if (!last && so.RequireExistingTrailBlock)
                return false;

            ActiveSeedBlock = last;
            if (!ActiveSeedBlock) return false;

            ApplyShield(ActiveSeedBlock, so.ShieldOnSeed);

            // consume resource
            if (cost > 0f)
                _resources.ChangeResourceAmount(so.ResourceIndex, -cost);

            _activeAssembler = EnsureAssembler(ActiveSeedBlock, so.AssemblerType);
            if (_activeAssembler)
                _activeAssembler.Depth = so.BondingDepth;

            if (_activeAssembler is SerpentWallAssembler wall)
                wall.Configure(SnapshotWallShape(so, status));

            OnSeedStarted?.Invoke(ActiveSeedBlock);
            return true;
        }

        /// <summary>
        /// Begin the actual bonding/growth routine in the assembler.
        /// </summary>
        public void BeginBonding()
        {
            if (!_activeAssembler) return;
            _activeAssembler.StartBonding();
            OnBondingBegan?.Invoke(ActiveSeedBlock);
        }

        /// <summary>
        /// Full stop via public End() to align with other executors.
        /// </summary>
        public void End() => StopSeedCompletely();

        /// <summary>
        /// Full stop. Clears references and stops any assembler work.
        /// </summary>
        public void StopSeedCompletely()
        {
            if (_activeAssembler)
            {
                try { _activeAssembler.StopBonding(); }
                catch { /* ignore */ }
            }

            _activeAssembler = null;
            ActiveSeedBlock  = null;

            OnSeedStopped?.Invoke();
        }

        // ===== Helpers =====

        Prism GetLatestBlock()
        {
            var listA = vesselPrismController?.Trail?.TrailList;
            if (listA != null && listA.Count > 0) return listA[^1];

            var trail2Field = typeof(VesselPrismController).GetField(
                "Trail2",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            var trail2 = trail2Field?.GetValue(vesselPrismController) as Trail;
            if (trail2 != null && trail2.TrailList.Count > 0) return trail2.TrailList[^1];

            return null;
        }

        bool HasResource(int index, float cost)
        {
            if (!_resources || index < 0 || index >= _resources.Resources.Count) return false;
            var res = _resources.Resources[index];
            return (res != null && res.CurrentAmount >= cost);
        }

        void ApplyShield(Prism block, SeedWallActionSO.ShieldMode mode)
        {
            if (!block) return;
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

        /// <summary>
        /// The wall's shape, fixed at placement from THIS vessel's elemental state (MASS is the
        /// Serpent's wall element: R_VesselActions/SERPENT_SEED_WALL.md). Read here, on the
        /// per-vessel executor, because the SO is shared by every Serpent.
        /// </summary>
        static SerpentWallAssembler.Snapshot SnapshotWallShape(SeedWallActionSO so, IVesselStatus status)
        {
            float mass = ElementalScaling.Multiplier(
                status, Element.Mass,
                atFull: so.MassSizeMultiplierAtFull,
                minMul: so.MinMassSizeMultiplier);

            return new SerpentWallAssembler.Snapshot
            {
                Config = so,
                Owner = status,
                Domain = status.Domain,
                PlayerName = status.PlayerName,
                ShortSide = so.BrickShortSide * mass,
                Depth = so.BrickDepth * mass,
                MassMultiplier = mass,
            };
        }

        Assembler EnsureAssembler(Prism block, SeedWallActionSO.AssemblerKind kind)
        {
            if (!block) return null;

            if (kind != SeedWallActionSO.AssemblerKind.SerpentLattice)
            {
                var existing = block.GetComponent<Assembler>();
                if (existing) return existing;
            }

            if (kind == SeedWallActionSO.AssemblerKind.SerpentLattice)
            {
                // A pooled prism can carry a dissolved wall from a previous life; Configure
                // re-arms it, and keeps the shape of one that is still live.
                return block.TryGetComponent(out SerpentWallAssembler wall)
                    ? wall
                    : block.gameObject.AddComponent<SerpentWallAssembler>();
            }

            return kind switch
            {
                SeedWallActionSO.AssemblerKind.Wall   => block.gameObject.AddComponent<WallAssembler>(),
                SeedWallActionSO.AssemblerKind.Gyroid => block.gameObject.AddComponent<WallAssembler>(),
                _                                     => block.gameObject.AddComponent<WallAssembler>()
            };
        }
    }
}
