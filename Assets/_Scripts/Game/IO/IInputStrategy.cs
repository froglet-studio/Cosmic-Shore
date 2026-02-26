using CosmicShore.Game.Environment;
using UnityEngine;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Settings;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility;
using CosmicShore.Utility.PoolsAndBuffers;
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
