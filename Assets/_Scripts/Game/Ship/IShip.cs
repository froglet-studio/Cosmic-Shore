using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Game.AI;
using CosmicShore.Game.IO;
using CosmicShore.Models.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Core
{
    public interface IShip
    {
        Dictionary<Element, SO_VesselUpgrade> VesselUpgrades { get; set; }
        Teams Team { get; set; }
        Player Player { get; set; }
        HideFlags hideFlags { get; set; }
        Transform transform { get; }
        GameObject gameObject { get; }
        string tag { get; set; }
        
        CameraManager CameraManager { get; }
        InputController InputController { get; set; }
        ResourceSystem ResourceSystem { get; }
        
        TrailSpawner TrailSpawner { get; }
        ShipTransformer ShipTransformer { get; }
        AIPilot AutoPilot { get; }
        ShipStatus ShipStatus { get; }

        List<GameObject> GetShipGeometries();
        
        Material AOEExplosionMaterial {get; set;}
        Material AOEConicExplosionMaterial {get; set;}
        Material SkimmerMaterial {get; set;}
        
        bool enabled { get; set; }
        bool isActiveAndEnabled { get; }

        CancellationToken destroyCancellationToken { get; }
        bool useGUILayout { get; set; }
        bool didStart { get; }
        bool didAwake { get; }
        bool runInEditMode { get; set; }
        void NotifyElementalFloatBinding(string statName, Element element);
        void PerformCrystalImpactEffects(CrystalProperties crystalProperties);
        void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties);
        void PerformShipControllerActions(InputEvents controlType);
        void StopShipControllerActions(InputEvents controlType);
        void PerformClassResourceActions(ResourceEvents resourceEvent);
        void StopClassResourceActions(ResourceEvents resourceEvent);
        void ToggleCollision(bool enabled);
        void SetVessel(SO_Vessel vessel);
        void UpdateLevel(Element element, int upgradeLevel);
        void SetShipMaterial(Material material);
        void SetBlockMaterial(Material material);
        void SetBlockSilhouettePrefab(GameObject prefab);
        void SetShieldedBlockMaterial(Material material);
        void SetAOEExplosionMaterial(Material material);
        void SetAOEConicExplosionMaterial(Material material);
        void SetSkimmerMaterial(Material material);

        void FlipShipUpsideDown();

        void FlipShipRightsideUp();
        void SetShipUp(float angle);
        void Teleport(Transform targetTransform);
        void DisableSkimmer();
        bool Equals(object other);
        int GetHashCode();
        string ToString();
        int GetInstanceID();
        T GetComponent<T>();
    }
}