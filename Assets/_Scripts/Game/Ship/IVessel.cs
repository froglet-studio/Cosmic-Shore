using CosmicShore.Core;
using CosmicShore.Models.Enums;
using CosmicShore.Utility;
using System;
using UnityEngine;


namespace CosmicShore.Game
{
    public interface IVessel : ITransform
    {
        event Action OnInitialized;
        event Action OnBeforeDestroyed;

        IVesselStatus VesselStatus { get; }

        /// <summary>
        /// In multiplayer mode, true -> owner client, false -> other clients
        /// In singleplayer mode, always false.
        /// </summary>
        bool IsOwnerClient { get; }
        void Initialize(IPlayer player, bool enableAIPilot = false);
        void PerformShipControllerActions(InputEvents @event);
        void StopShipControllerActions(InputEvents @event);
        void Teleport(Transform transform);
        void SetResourceLevels(ResourceCollection resources);
        void SetShipUp(float angle);
        void DisableSkimmer();
        void SetBoostMultiplier (float boostMultiplier);
        void SetShipMaterial(Material material);
        void SetBlockSilhouettePrefab(GameObject prefab);
        void SetAOEExplosionMaterial(Material material);
        void SetAOEConicExplosionMaterial(Material material);
        void SetSkimmerMaterial(Material material);
        void AssignCaptain(SO_Captain captain);
        void BindElementalFloat(string name, Element element);
        // void PerformButtonActions(int buttonNumber);
        void ToggleAutoPilot(bool toggle);
        bool AllowClearPrismInitialization();
        void DestroyVessel();
        void ResetForPlay();
        void SetPose(Pose pose);
    }
}