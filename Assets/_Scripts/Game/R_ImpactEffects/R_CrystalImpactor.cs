using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class R_CrystalImpactor : R_ImpactorBase
    {
        [SerializeField]
        Crystal crystal;
        
        public Crystal Crystal => crystal;
    }
}