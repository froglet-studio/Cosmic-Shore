using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class AnyPrismEffectSO : ImpactEffectSO
    {
        /// Override if you want a different damage formula.
        protected void Damage(IShipStatus status, PrismImpactor prismImpactor, float inertia, Vector3 overrideCourse, float overrideSpeed)
        {
            // Default: Course * Speed * inertia
            
            var damage= (status?.Course ?? overrideCourse) * ((status?.Speed ?? overrideSpeed) * inertia);
            prismImpactor.Prism.Damage(damage, status.Team, status.PlayerName);
            
        }

        protected void Steal(PrismImpactor impactee, IShipStatus status)
        {
            impactee.Prism.Steal(status.PlayerName, status.Team);
        }
    }
}