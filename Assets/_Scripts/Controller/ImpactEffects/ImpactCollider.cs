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

        /// <summary>
        /// Runtime wiring for impactors attached after authoring — e.g. a fauna's dropped
        /// crystal made collectible on wither. Authored prefabs set <c>impactorObject</c> in
        /// the inspector; this is the equivalent code path.
        /// </summary>
        public void Configure(IImpactor impactor) => impactorObject = impactor as Object;
    }
}