using CosmicShore.Game;
using CosmicShore.Game.IO;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Base behaviour for ship implementations.  Logic shared between
    /// <see cref="Ship"/> and <see cref="NetworkShip"/> should live here.
    /// </summary>
    [RequireComponent(typeof(ShipStatus))]
    public abstract class R_ShipBase : MonoBehaviour, IShip
    {
        public event Action<IShipStatus> OnShipInitialized;

        [Header("Ship Meta")]
        [SerializeField] protected string _name;
        [SerializeField] protected ShipTypes _shipType;

        [Header("Ship Components")]
        [SerializeField] protected Skimmer nearFieldSkimmer;
        [SerializeField] protected GameObject orientationHandle;
        [SerializeField] protected List<GameObject> shipGeometries;

        [Header("Optional Ship Components")]
        [SerializeField] protected GameObject AOEPrefab;
        [SerializeField] protected Skimmer farFieldSkimmer;

        [Header("Environment Interactions")]
        [SerializeField] public List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField] protected List<TrailBlockImpactEffects> trailBlockImpactEffects;

        protected IShipStatus _shipStatus;
        public IShipStatus ShipStatus
        {
            get
            {
                _shipStatus ??= GetComponent<ShipStatus>();
                _shipStatus.Name = _name;
                _shipStatus.ShipType = _shipType;
                return _shipStatus;
            }
        }

        public Transform Transform => transform;

        public abstract void Initialize(IPlayer player);
        public abstract void PerformShipControllerActions(InputEvents @event);
        public abstract void StopShipControllerActions(InputEvents @event);
        public abstract void Teleport(Transform targetTransform);
        public abstract void SetResourceLevels(ResourceCollection resources);
        public abstract void SetShipUp(float angle);
        public abstract void DisableSkimmer();
        public abstract void PerformCrystalImpactEffects(CrystalProperties properties);
        public abstract void SetBoostMultiplier(float multiplier);
        public abstract void ToggleGameObject(bool toggle);
        public abstract void SetShipMaterial(Material material);
        public abstract void SetBlockSilhouettePrefab(GameObject prefab);
        public abstract void SetAOEExplosionMaterial(Material material);
        public abstract void SetAOEConicExplosionMaterial(Material material);
        public abstract void SetSkimmerMaterial(Material material);
        public abstract void AssignCaptain(SO_Captain captain);
        public abstract void BindElementalFloat(string name, Element element);
        public abstract void PerformTrailBlockImpactEffects(TrailBlockProperties properties);

        protected void RaiseInitialized() => OnShipInitialized?.Invoke(ShipStatus);
    }
}
