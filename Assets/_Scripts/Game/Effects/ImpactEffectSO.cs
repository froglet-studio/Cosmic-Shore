using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Any impact effect that is defined in the game should inherit from this class.
    /// </summary>
    public abstract class ImpactEffectSO<TImpactor, TImpactee> : ScriptableObject, R_IImpactEffect
    where TImpactor : class, R_IImpactor
    where TImpactee : class, R_IImpactor
    {
        // Do not override this; override ExecuteTyped instead.
        public void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            if (impactor is not TImpactor a)
            {
#if UNITY_EDITOR
                Debug.LogError($"Expected impactor of type {typeof(TImpactor).Name} but got {impactor?.GetType().Name ?? "null"}", this);
#endif
                return;
            }

            if (impactee is not TImpactee b)
            {
#if UNITY_EDITOR
                Debug.LogError($"Expected impactee of type {typeof(TImpactee).Name} but got {impactee?.GetType().Name ?? "null"}", this);
#endif
                return;
            }

            ExecuteTyped(a, b);
        }

        protected abstract void ExecuteTyped(TImpactor impactor, TImpactee crystalImpactee);
    }
}
