using System;
using UnityEngine;
using CosmicShore.Game.Assemblers;
namespace CosmicShore.Game.Ship
{
    public class SeedAssemblerMono : ShipAction
    {
        public event Action OnAssembleStarted;
        public event Action OnAssembleCompleted;

        [Header("Config")]
        [SerializeField] private float enhancementsPerFullAmmo = 4f;
        [SerializeField] private Assembler assembler;  
        [SerializeField] private int depth = 50;

        public override void StartAction()
        {
        }

        public override void StopAction()
        {
        }
    }
}