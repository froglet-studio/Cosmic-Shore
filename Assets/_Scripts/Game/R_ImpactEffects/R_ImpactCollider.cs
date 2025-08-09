using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ImpactCollider : MonoBehaviour, R_IImpactCollider
    {
        [SerializeField, RequireInterface(typeof(R_IImpactor))] 
        private Object impactorObject;
        
        public R_IImpactor Impactor => impactorObject as R_IImpactor;
    }
}