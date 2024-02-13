using System;
using UnityEngine;
using static CosmicShore.App.Systems.RewindSystem.RewindBase;

namespace CosmicShore.App.Systems.RewindSystem
{
    [Serializable]
    public struct OptionalParticleSettings
    {
        [SerializeField] private bool enabled;
        [SerializeField] private ParticlesSetting value;
    
        public bool Enabled => enabled;
        public ParticlesSetting Value => value;
    
        public OptionalParticleSettings(ParticlesSetting initialValue)
        {
            enabled = true;
            value = initialValue;
        }
    }
}

