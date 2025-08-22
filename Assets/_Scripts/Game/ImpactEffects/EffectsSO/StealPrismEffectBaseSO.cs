using UnityEngine;

namespace CosmicShore.Game
{
    /// Ties any impactor → Prism by stealing with the impactor's identity.
    public abstract class StealPrismEffectBaseSO<TImpactor> 
        : ImpactEffectSO<TImpactor, PrismImpactor>
        where TImpactor : class, IImpactor
    {
        /// Implement this to fetch the attacking ship status from the impactor.
        protected abstract IShipStatus GetShipStatus(TImpactor impactor);

        protected sealed override void ExecuteTyped(TImpactor impactor, PrismImpactor impactee)
        {
            var status = GetShipStatus(impactor);
            if (status == null || impactee?.Prism == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(StealPrismEffectBaseSO<TImpactor>)}: Missing ShipStatus or Prism.");
#endif
                return;
            }

            impactee.Prism.Steal(status.PlayerName, status.Team);
        }
    }
}