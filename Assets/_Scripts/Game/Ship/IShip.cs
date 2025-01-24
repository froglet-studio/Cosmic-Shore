using CosmicShore.Core;
using CosmicShore.Game.AI;
using CosmicShore.Game.IO;
using CosmicShore.Models.Enums;
using CosmicShore.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace CosmicShore.Game
{
    public interface IShip : ITransform
    {
        public event Action OnShipInitialized;

        public string ShipName { get; }
        public ShipTypes GetShipType { get; }
        public Transform FollowTarget {  get; }
        public Teams Team { get; }
        public AIPilot AIPilot { get; }
        public ShipTransformer ShipTransformer { get; }
        public TrailSpawner TrailSpawner { get; }
        public ResourceSystem ResourceSystem { get; }
        public ShipStatus ShipStatus { get; }
        public IPlayer Player { get; }
        public InputController InputController { get; }
        public IInputStatus InputStatus { get; }
        public Silhouette Silhouette { get; }
        public Material AOEExplosionMaterial { get; }
        public Material AOEConicExplosionMaterial { get; }
        public Material SkimmerMaterial { get; }
        public float GetInertia {  get; }
        public float BoostMultiplier { get; set; }      // TODO - Set should not be public
        public SO_Captain Captain { get; }
        public ShipCameraCustomizer ShipCameraCustomizer { get; }
        public CameraManager CameraManager { get; }
        public List<GameObject> ShipGeometries { get; }


        public void Initialize(IPlayer player, Teams team);
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
    }
}