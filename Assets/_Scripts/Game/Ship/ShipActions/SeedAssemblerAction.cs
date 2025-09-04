using System;
using UnityEngine;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;


namespace CosmicShore
{
    public class SeedAssemblerAction : ShipAction
    {
        public event Action OnAssembleStarted;
        public event Action OnAssembleCompleted;
        
        public event Action OnSeedStarted;    // fire when we begin placing
        public event Action OnSeedCompleted; 
        
        [Header("Placement")]
        [SerializeField] Assembler assemblerPrefab;
        [SerializeField] int depth = 50;

        [Header("Resource")]
        [Tooltip("Which Resource index funds Seed Walls")]
        [SerializeField] int shieldResourceIndex = 0;
        [Tooltip("How many walls per full resource bar (e.g., 4)")]
        [SerializeField] float wallsPerFullResource = 4f;
        
        TrailSpawner _spawner;
        Assembler _activeAssembler;
        
        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);
            _spawner = Ship.ShipStatus.TrailSpawner;
        }

        public override void StartAction()
        {
           StartSeed();
        }

        public override void StopAction() 
        {
            StopSeed();
        }

        //void CopyComponentValues(Assembler sourceComp, Assembler targetComp)
        //{
        //    FieldInfo[] sourceFields = sourceComp.GetType().GetFields(BindingFlags.Public |
        //                                                  BindingFlags.NonPublic |
        //                                                  BindingFlags.Instance);
        //    for (var i = 0; i < sourceFields.Length; i++)
        //    {
        //        var value = sourceFields.GetValue(i);
        //        sourceFields.SetValue(value, i);
        //    }
        //}
        
        public void StartSeed()
        {
            var rs = ResourceSystem;
            if (rs == null) return;
            if (shieldResourceIndex < 0 || shieldResourceIndex >= rs.Resources.Count) return;

            var res = rs.Resources[shieldResourceIndex];
            if (wallsPerFullResource <= 0f || res.MaxAmount <= 0f) return;

            // Spend a single "wall chunk" from the resource bar (e.g., 1/4th of max)
            float cost = res.MaxAmount / wallsPerFullResource;
            if (res.CurrentAmount < cost) return;

            rs.ChangeResourceAmount(shieldResourceIndex, -cost); // will emit OnResourceChanged

            OnSeedStarted?.Invoke();

            // Attach a runtime Assembler on the latest trail block
            var trailBlockGO = _spawner?.Trail?.TrailList?.LastOrDefault()?.gameObject;
            if (trailBlockGO == null || assemblerPrefab == null) return;

            var newAsm = trailBlockGO.AddComponent(assemblerPrefab.GetType()) as Assembler;
            if (newAsm != null)
            {
                newAsm.Depth = depth;
                _activeAssembler = newAsm;
            }
        }

        public void StopSeed()
        {
            if (_activeAssembler == null) return;

            var seed = _activeAssembler.GetComponent<TrailBlock>();
            if (seed != null)
            {
                seed.ActivateSuperShield();
                seed.transform.localScale *= 2f;
            }
            _activeAssembler.SeedBonding();
            _activeAssembler = null;

            OnSeedCompleted?.Invoke();
        }
    }
}


