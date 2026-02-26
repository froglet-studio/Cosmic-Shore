using UnityEngine;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Models.Enums;
namespace CosmicShore.Game.ImpactEffects
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