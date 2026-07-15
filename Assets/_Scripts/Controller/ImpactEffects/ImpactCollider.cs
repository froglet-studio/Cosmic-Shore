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
        /// Wires the impactor on a runtime-built collider (prefab instances author this in the
        /// inspector; runtime spawns - e.g. the conveyor toy's elemental-crystal pickups - have
        /// no serialized state to author). Mirrors the ToyDefinitionSO.SetRuntimeMetadata pattern.
        /// </summary>
        internal void SetImpactor(Object impactor) => impactorObject = impactor;
    }
}