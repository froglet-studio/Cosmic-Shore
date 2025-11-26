using UnityEngine;

namespace CosmicShore.Game
{
    /// Base for "X damages Prism" effects.
    /// Derive and just provide how to fetch the attacker's IShipStatus.
    public abstract class DamagePrismEffectBase<TImpactor>
        : ImpactEffectSO<TImpactor, PrismImpactor>
        where TImpactor : class, IImpactor
    {
        [SerializeField] float inertia = 70f; // global scalar you can tune per effect
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private float overrideSpeed;


        /// Provide the attacking ship status from the given impactor.
        protected abstract IShipStatus GetAttackerStatus(TImpactor impactor);

        /// Override if you want a different damage formula.
        protected virtual Vector3 ComputeDamage(IShipStatus status, TImpactor impactor, PrismImpactor prism)
        {
            // Default: Course * Speed * inertia
            return (status?.Course ?? overrideCourse) * ((status?.Speed ?? overrideSpeed) * inertia);
        }

        protected sealed override void ExecuteTyped(TImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = GetAttackerStatus(impactor);
            var prism = prismImpactee?.Prism;

            if (status == null || prism == null || prism.TrailBlockProperties == null ||
                prism.TrailBlockProperties.trailBlock == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(DamagePrismEffectBase<TImpactor>)}: Missing status or prism/trailBlock.",
                    this);
#endif
                return;
            }

            var dmg = ComputeDamage(status, impactor, prismImpactee);
            prism.TrailBlockProperties.trailBlock.Damage(dmg, status.Team, status.PlayerName);
        }
    }
}