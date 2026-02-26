using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class SeedAssemblerMono : ShipAction
    {
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