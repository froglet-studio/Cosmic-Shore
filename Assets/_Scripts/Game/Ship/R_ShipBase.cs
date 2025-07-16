using CosmicShore.Core;
using CosmicShore.Models.Enums;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShipStatus))]
    public abstract class R_ShipBase : NetworkBehaviour, IShip
    {
        public event Action<IShipStatus> OnShipInitialized;

        [Header("Event Channels")]
        [SerializeField] protected BoolEventChannelSO onBottomEdgeButtonsEnabled;

        protected IShipStatus _shipStatus;
        public IShipStatus ShipStatus
        {
            get
            {
                _shipStatus ??= GetComponent<IShipStatus>();
                return _shipStatus;
            }
        }

        public Transform Transform => transform;

        public abstract void Initialize(IPlayer player, bool isAI);

        public virtual void Teleport(Transform targetTransform) =>
            ShipHelper.Teleport(transform, targetTransform);

        public virtual void SetResourceLevels(ResourceCollection resources) =>
            ShipStatus.ResourceSystem.InitializeElementLevels(resources);

        public virtual void SetShipUp(float angle) =>
            ShipStatus.OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, angle);

        public virtual void DisableSkimmer()
        {
            ShipStatus.NearFieldSkimmer?.gameObject.SetActive(false);
            ShipStatus.FarFieldSkimmer?.gameObject.SetActive(false);
        }

        public void SetBoostMultiplier(float multiplier) => ShipStatus.BoostMultiplier = multiplier;

        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);

        public void SetShipMaterial(Material material) =>
            ShipStatus.ShipMaterial = material;

        public void SetBlockSilhouettePrefab(GameObject prefab) =>
            ShipStatus.Silhouette.SetBlockPrefab(prefab);

        public void SetAOEExplosionMaterial(Material material) =>
            ShipStatus.AOEExplosionMaterial = material;

        public virtual void SetAOEConicExplosionMaterial(Material material) =>
                ShipStatus.AOEConicExplosionMaterial = material;

        public virtual void SetSkimmerMaterial(Material material) =>
                ShipStatus.SkimmerMaterial = material;

        public virtual void AssignCaptain(SO_Captain captain)
        {
            ShipStatus.Captain = captain;
            SetResourceLevels(captain.InitialResourceLevels);
        }

        public virtual void BindElementalFloat(string name, Element element) =>
            ShipStatus.ElementalStatsHandler.BindElementalFloat(name, element);

        protected void InvokeShipInitializedEvent() => OnShipInitialized?.Invoke(ShipStatus);

        public void PerformShipControllerActions(InputEvents controlType)
        {
            ShipStatus.ActionHandler.PerformShipControllerActions(controlType);
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            ShipStatus.ActionHandler.StopShipControllerActions(controlType);
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties) =>
            ShipStatus.ImpactHandler.PerformCrystalImpactEffects(crystalProperties);

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties) =>
            ShipStatus.ImpactHandler.PerformTrailBlockImpactEffects(trailBlockProperties);

        public abstract void PerformButtonActions(int buttonNumber);

        public void OnButtonPressed(int buttonNumber)
        {
            throw new NotImplementedException();
        }
    }
}
