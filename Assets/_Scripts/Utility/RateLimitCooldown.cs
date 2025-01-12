
using UnityEngine;

namespace CosmicShore.Utilities
{
    public class RateLimitCooldown
    {
        public float CooldownTime => _cooldownTimeLength;

        private readonly float _cooldownTimeLength;
        private float _cooldownFinishedTime;

        public RateLimitCooldown(float cooldownTimeLength)
        {
            _cooldownTimeLength = cooldownTimeLength;
            _cooldownFinishedTime = -1f;
        }

        public bool CanCall => Time.unscaledTime > _cooldownFinishedTime;

        public void PutOnCooldown()
        {
            _cooldownFinishedTime = Time.unscaledTime + _cooldownTimeLength;
        }

        
    }
}
