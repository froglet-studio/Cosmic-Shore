using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Any impact effect should inherit from this class.
    /// Works with base/derived/interface types for both impactor & impactee.
    /// Each impact effect assets can be added to only the monobehaviour component of TImpactor.
    /// </summary>
    public abstract class ImpactEffectSO<TImpactor, TImpactee> : ScriptableObject, IImpactEffect
        where TImpactor : class, IImpactor
        where TImpactee : class, IImpactor
    {
        public void Execute(IImpactor impactor, IImpactor impactee)
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