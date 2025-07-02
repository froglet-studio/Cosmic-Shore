using CosmicShore;
using CosmicShore.Game.UI;
using UnityEngine;

namespace CosmicShore.Game
{
    public interface IShipHUDView
    {
        // Called right after spawn to set up logic/event hooks
        void Initialize(IShipHUDController controller);
        ResourceDisplay GetResourceDisplay(string resourceName);
        Transform GetSilhouetteContainer();
        Transform GetTrailContainer();
        void AnimateBoostFillDown(int resourceIndex, float duration, float startingAmount);
        void AnimateBoostFillUp(int resourceIndex, float duration, float endingAmount);
    }
}
