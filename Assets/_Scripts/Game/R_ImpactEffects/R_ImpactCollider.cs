using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Separating this component from R_IImpactor lets us to get colliders
    /// </summary>
    public class R_ImpactCollider : MonoBehaviour, R_IImpactCollider
    {
        [SerializeField, RequireInterface(typeof(R_IImpactor))] 
        private Object impactorObject;
        
        public R_IImpactor Impactor => impactorObject as R_IImpactor;
    }
}