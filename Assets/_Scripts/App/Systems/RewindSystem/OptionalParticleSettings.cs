using System;
using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    [Serializable]
    public struct OptionalParticleSettings
    {
        [SerializeField] private bool enabled;

        public OptionalParticleSettings(bool enabled)
        {
            this.enabled = enabled;
        }
    }
}

