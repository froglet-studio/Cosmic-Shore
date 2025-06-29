using CosmicShore.Core;
using CosmicShore.Models.Enums;
using CosmicShore.Utility;
using System;
using UnityEngine;


namespace CosmicShore.Game
{
    public interface IShip : ITransform
    {
        public event Action<IShipStatus> OnShipInitialized;

        public IShipStatus ShipStatus { get; }

        public void Initialize(IPlayer player);
        public void PerformShipControllerActions(InputEvents @event);
        public void StopShipControllerActions(InputEvents @event);
        public void Teleport(Transform transform);
        public void SetResourceLevels(ResourceCollection resources);
        public void SetShipUp(float angle);
        public void DisableSkimmer();
        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties);
        public void SetBoostMultiplier (float boostMultiplier);
        public void ToggleGameObject(bool toggle);
        public void SetShipMaterial(Material material);
        public void SetBlockSilhouettePrefab(GameObject prefab);
        public void SetAOEExplosionMaterial(Material material);
        public void SetAOEConicExplosionMaterial(Material material);
        public void SetSkimmerMaterial(Material material);
        public void AssignCaptain(SO_Captain captain);
        public void BindElementalFloat(string name, Element element);
        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties);

        public void PerformButtonActions(int buttonNumber);
    }
}