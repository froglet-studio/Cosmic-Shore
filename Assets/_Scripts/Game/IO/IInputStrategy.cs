using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.IO
{
    public interface IInputStrategy
    {
        void Initialize(IInputStatus inputStatus);
        void ProcessInput();
        void SetPortrait(bool portrait);
        void OnStrategyActivated();
        void OnStrategyDeactivated();
        void OnPaused();
        void OnResumed();
        void SetInvertY(bool status);
        void SetInvertThrottle(bool status);
    }
}
