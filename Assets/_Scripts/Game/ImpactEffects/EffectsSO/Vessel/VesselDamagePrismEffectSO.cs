using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/VesselDamagePrismEffectSO")]
    public class VesselDamagePrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] float inertia = 70f;   // global scalar you can tune per effect
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private bool useOverrideCourse;
        [SerializeField] private float overrideSpeed;
        [SerializeField] private bool useOverrideSpeed;
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Ship.ShipStatus;
            var course = useOverrideCourse ? overrideCourse : status.Course;
            var speed = useOverrideSpeed ? overrideSpeed : status.Speed;
            PrismEffectHelper.Damage(status, prismImpactee, inertia, course, speed);
        }
    }
    
    
}