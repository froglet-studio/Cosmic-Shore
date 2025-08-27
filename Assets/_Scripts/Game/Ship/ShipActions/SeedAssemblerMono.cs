using System;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class SeedAssemblerMono : ShipAction
    {
        public event Action OnAssembleStarted;
        public event Action OnAssembleCompleted;

        [Header("Config")]
        [SerializeField] private float enhancementsPerFullAmmo = 4f;
        [SerializeField] private Assembler assembler;  
        [SerializeField] private int depth = 50;
        [SerializeField] private int resourceIndex = 0;

        private TrailSpawner spawner;
        private Assembler currentAssembler;
        private IShip ship;


        public override void StartAction()
        {
        }

        public override void StopAction()
        {
        }

     
    }
}