using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselChangeSpeedByExplosionEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselChangeSpeedByExplosionEffectSO")]
    public class VesselChangeSpeedByExplosionEffectSO : VesselExplosionEffectSO
    {
        public static event Action<VesselImpactor, ExplosionImpactor> OnVesselSlowedByExplosion;

        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            impactor.Vessel.ModifyThrottle(_amount, _duration);

            OnVesselSlowedByExplosion?.Invoke(impactor, impactee);
        }
    }
}