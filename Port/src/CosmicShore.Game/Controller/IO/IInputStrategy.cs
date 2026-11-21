using CosmicShore.Gameplay;
using CosmicShore.Engine;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
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
