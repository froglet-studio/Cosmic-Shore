using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselChangeSpeedByExplosionEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselChangeSpeedByExplosionEffectSO")]
    public class VesselChangeSpeedByExplosionEffectSO : VesselExplosionEffectSO
    {
        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            impactor.Vessel.ModifyThrottle(_amount, _duration);
        }
    }
}
