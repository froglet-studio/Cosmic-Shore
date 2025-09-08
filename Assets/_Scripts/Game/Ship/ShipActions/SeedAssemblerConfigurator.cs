using System;
using System.Collections;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class SeedAssemblerConfigurator : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] Assembler assemblerPrefab;
        [SerializeField] int depth = 50;

        [Header("Resource")]
        [SerializeField] int shieldResourceIndex = 0;
        [SerializeField] float wallsPerFullResource = 4f;

        TrailSpawner _spawner;
        Assembler _activeAssembler;
        IShipStatus _shipStatus;
        TrailBlock _activeSeedBlock;

        public TrailBlock ActiveSeedBlock => _activeSeedBlock;

        public void Initialize(IShip ship)
        {
            _shipStatus = ship.ShipStatus;
            _spawner    = _shipStatus.TrailSpawner;
        }

        public bool StartSeed()
        {
            var rs = _shipStatus?.ResourceSystem;
            if (rs == null) return false;
            if (shieldResourceIndex < 0 || shieldResourceIndex >= rs.Resources.Count) return false;

            var res  = rs.Resources[shieldResourceIndex];
            if (wallsPerFullResource <= 0f || res.MaxAmount <= 0f) return false;

            float cost = res.MaxAmount / wallsPerFullResource;
            if (res.CurrentAmount < cost) return false;

            rs.ChangeResourceAmount(shieldResourceIndex, -cost);

            var trailBlockGO = _spawner?.Trail?.TrailList?.LastOrDefault()?.gameObject;
            if (trailBlockGO == null || assemblerPrefab == null) return false;

            _activeSeedBlock = trailBlockGO.GetComponent<TrailBlock>();

            // add concrete assembler type matching prefab
            var newAsm = trailBlockGO.AddComponent(assemblerPrefab.GetType()) as Assembler;
            if (newAsm == null) return false;

            newAsm.Depth     = depth;
            _activeAssembler = newAsm;
            return true;
        }

        public void BeginBonding()
        {
            if (_activeAssembler == null) return;
            StartCoroutine(BeginBondingNextFrame());
        }

        IEnumerator BeginBondingNextFrame()
        {
            yield return null;
            if (_activeAssembler == null) yield break;
            _activeAssembler.SeedBonding();
        }
        
        public void StopSeed()
        {
            if (_activeAssembler == null) return;
            _activeAssembler.StopBonding();

            if (_activeSeedBlock != null)
            {
                _activeSeedBlock.ActivateSuperShield();
                _activeSeedBlock.transform.localScale *= 2f;
            }

            _activeAssembler.SeedBonding();
        }
        
        public void StopSeedCompletely()
        {
            if (_activeAssembler != null)
            {
                _activeAssembler.StopBonding();
                Destroy(_activeAssembler);
                _activeAssembler = null;
            }

            if (_activeSeedBlock == null) return;
            _activeSeedBlock.ActivateSuperShield();
            _activeSeedBlock = null;
        }
    }
}
