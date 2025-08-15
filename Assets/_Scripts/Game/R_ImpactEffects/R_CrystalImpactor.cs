using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent((typeof(Crystal)))]
    public abstract class R_CrystalImpactor : R_ImpactorBase
    {
        Crystal crystal;
        public Crystal Crystal => crystal ??= GetComponent<Crystal>();
    }
}