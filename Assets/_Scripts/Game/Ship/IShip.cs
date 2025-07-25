using CosmicShore.Core;
using CosmicShore.Models.Enums;
using CosmicShore.Utility;
using System;
using UnityEngine;


namespace CosmicShore.Game
{
    public interface IShip : ITransform
    {
        event Action<IShipStatus> OnShipInitialized;

        IShipStatus ShipStatus { get; }

        void Initialize(IPlayer player, bool enableAIPilot = false);
        void PerformShipControllerActions(InputEvents @event);
        void StopShipControllerActions(InputEvents @event);
        void Teleport(Transform transform);
        void SetResourceLevels(ResourceCollection resources);
        void SetShipUp(float angle);
        void DisableSkimmer();
        void PerformCrystalImpactEffects(CrystalProperties crystalProperties);
        void SetBoostMultiplier (float boostMultiplier);
        void ToggleGameObject(bool toggle);
        void SetShipMaterial(Material material);
        void SetBlockSilhouettePrefab(GameObject prefab);
        void SetAOEExplosionMaterial(Material material);
        void SetAOEConicExplosionMaterial(Material material);
        void SetSkimmerMaterial(Material material);
        void AssignCaptain(SO_Captain captain);
        void BindElementalFloat(string name, Element element);
        void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties);
        void PerformButtonActions(int buttonNumber);
        void OnButtonPressed(int buttonNumber);
        void ToggleAutoPilot(bool toggle);
    }
}