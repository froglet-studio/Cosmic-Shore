using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class ProjectileEndEffectSO : ScriptableObject
    {
        public abstract void Execute(ProjectileImpactor impactor, ImpactorBase impactee);
    }
}