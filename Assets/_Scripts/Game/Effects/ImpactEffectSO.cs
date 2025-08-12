using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Any impact effect should inherit from this class.
    /// Works with base/derived/interface types for both impactor & impactee.
    /// </summary>
    public abstract class ImpactEffectSO<TImpactor, TImpactee> : ScriptableObject, R_IImpactEffect
        where TImpactor : class, R_IImpactor
        where TImpactee : class, R_IImpactor
    {
        public void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            if (impactor == null || impactee == null)
            {
#if UNITY_EDITOR
                Debug.LogError("Impactor or impactee was null.", this);
#endif
                return;
            }

            var impactorTypeOk = typeof(TImpactor).IsAssignableFrom(impactor.GetType());
            var impacteeTypeOk = typeof(TImpactee).IsAssignableFrom(impactee.GetType());

            if (!impactorTypeOk)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    $"Expected impactor assignable to {typeof(TImpactor).Name} " +
                    $"but got {impactor.GetType().Name}", this);
#endif
                return;
            }

            if (!impacteeTypeOk)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    $"Expected impactee assignable to {typeof(TImpactee).Name} " +
                    $"but got {impactee.GetType().Name}", this);
#endif
                return;
            }

            // Safe casts after assignability check
            ExecuteTyped((TImpactor)impactor, (TImpactee)impactee);
        }

        protected abstract void ExecuteTyped(TImpactor impactor, TImpactee impactee);
    }
}