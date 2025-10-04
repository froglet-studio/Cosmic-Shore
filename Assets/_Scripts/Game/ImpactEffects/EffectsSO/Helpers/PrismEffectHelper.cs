using UnityEngine;

namespace CosmicShore.Game
{
    public static class PrismEffectHelper
    {
        /// Override if you want a different damage formula.
        public static void Damage(IVesselStatus status, PrismImpactor prismImpactor, float inertia, Vector3 course, float speed)
        {
            // Default: Course * Speed * inertia
            if (status.Player == null)
            {
                Debug.LogError("No player found to deal damage to prism!");
                return;
            }
            
            var damage= course * speed * inertia;
            prismImpactor.Prism.Damage(damage, status.Domain, status.PlayerName);
        }

        public static void Steal(PrismImpactor impactee, IVesselStatus status)
        {
            impactee.Prism.Steal(status.PlayerName, status.Domain);
        }
    }
}