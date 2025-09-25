using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>Metadata attached to each pooled instance.</summary>
    public sealed class PooledObject : MonoBehaviour
    {
        public string PoolKey { get; internal set; }
        public PoolManagerBase Manager { get; internal set; }
    }
}