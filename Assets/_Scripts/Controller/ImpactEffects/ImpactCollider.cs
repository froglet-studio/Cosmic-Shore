using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Separating this component from R_IImpactor lets us to get colliders
    /// </summary>
    public class ImpactCollider : MonoBehaviour, IImpactCollider
    {
        [SerializeField, RequireInterface(typeof(IImpactor))] 
        private Object impactorObject;
        
        public IImpactor Impactor => impactorObject as IImpactor;
    }
}