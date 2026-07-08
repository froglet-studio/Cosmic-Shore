using CosmicShore.Utility;
﻿using UnityEngine;

namespace CosmicShore.Game
{
    public static class PrismEffectHelper
    {
        /// Override if you want a different damage formula.
        public static void Damage(IVesselStatus status, PrismImpactor prismImpactor, float inertia, Vector3 course, float speed)
        {
            // Skip silently when player hasn't been assigned yet (e.g. during initialization)
            if (status.Player == null)
                return;
            
            var damage= course * speed * inertia;
            prismImpactor.Prism.Damage(damage, status.Domain, status.PlayerName);
        }
        
        public static void Damage(IVesselStatus status, PrismImpactor prismImpactor, float inertia, Vector3 Velocity)
        {
            // Skip silently when player hasn't been assigned yet (e.g. during initialization)
            if (status.Player == null)
                return;
            
            var damage= Velocity * inertia;
            prismImpactor.Prism.Damage(damage, status.Domain, status.PlayerName);
        }

        public static void Steal(PrismImpactor impactee, IVesselStatus status)
        {
            impactee.Prism.Steal(status.PlayerName, status.Domain);
        }
    }
}