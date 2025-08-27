using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/ShipDamagePrismEffectSO")]
    public class ShipDamagePrismEffectSO : ShipPrismEffectSO
    {
        [SerializeField] float inertia = 70f;   // global scalar you can tune per effect
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private float overrideSpeed;
        public override void Execute(ShipImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Ship.ShipStatus;
            Damage(status, prismImpactee, inertia, overrideCourse, overrideSpeed);
        }
    }
    
    
}