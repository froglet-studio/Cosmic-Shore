using System;
using UnityEngine;

namespace CosmicShore.Core
{
    [Serializable]
    public struct OptionalParticleSettings
    {
        [SerializeField] private bool enabled;

        public OptionalParticleSettings(bool enabled = true)
        {
            this.enabled = enabled;
        }
    }
}

