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

        [Header("Ship Meta")]
        [SerializeField] protected string _name;
        [SerializeField] protected ShipTypes _shipType;

        [Header("Ship Components")]
        [SerializeField] protected Skimmer nearFieldSkimmer;
        [SerializeField] protected GameObject orientationHandle;

        [Header("Optional Ship Components")]
        [SerializeField] protected GameObject AOEPrefab;
        [SerializeField] protected Skimmer farFieldSkimmer;

        [Header("Configuration")]
        [SerializeField] protected int resourceIndex = 0;
        [SerializeField] protected int ammoResourceIndex = 0;
        [SerializeField] protected int boostResourceIndex = 0;
        [SerializeField] protected float boostMultiplier = 4f;
        [SerializeField] protected bool bottomEdgeButtons = false;
        [SerializeField] protected float Inertia = 70f;


        [Serializable]
        public struct ElementStat
        {
            public string StatName;
            public Element Element;

            public ElementStat(string statName, Element element)
            {
                StatName = statName;
                Element = element;
            }
        }

        [Header("Elemental Stats")]
        [SerializeField] protected List<ElementStat> ElementStats = new();

        [Header("Event Channels")]
        [SerializeField] protected BoolEventChannelSO onBottomEdgeButtonsEnabled;

        [Header("Refactored Components")]
        [SerializeField] protected R_ShipActionHandler actionHandler;
        [SerializeField] protected R_ShipImpactHandler impactHandler;
        [SerializeField] protected R_ShipCustomization customization;

        protected const float speedModifierDuration = 2f;

        protected IShipStatus _shipStatus;
        public IShipStatus ShipStatus
        {
            get
            {
                _shipStatus ??= GetComponent<IShipStatus>();
                _shipStatus.Name = _name;
                _shipStatus.ShipType = _shipType;
                _shipStatus.BoostMultiplier = boostMultiplier;
                return _shipStatus;
            }
        }

        public Transform Transform => transform;

        protected void SetTeamToShipStatusAndSkimmers(Teams team)
        {
            ShipStatus.Team = team;
            if (nearFieldSkimmer != null) nearFieldSkimmer.Team = team;
            if (farFieldSkimmer != null) farFieldSkimmer.Team = team;
        }

        protected void SetPlayerToShipStatusAndSkimmers(IPlayer player)
        {
            ShipStatus.Player = player;
            if (nearFieldSkimmer != null) nearFieldSkimmer.Player = player;
            if (farFieldSkimmer != null) farFieldSkimmer.Player = player;
        }

        public abstract void Initialize(IPlayer player);

        public virtual void Teleport(Transform targetTransform) =>
            ShipHelper.Teleport(transform, targetTransform);

        public virtual void SetResourceLevels(ResourceCollection resources) =>
            ShipStatus.ResourceSystem.InitializeElementLevels(resources);

        public virtual void SetShipUp(float angle) =>
            orientationHandle.transform.localRotation = Quaternion.Euler(0, 0, angle);

        public virtual void DisableSkimmer()
        {
            nearFieldSkimmer?.gameObject.SetActive(false);
            farFieldSkimmer?.gameObject.SetActive(false);
        }

        public void SetBoostMultiplier(float multiplier) => boostMultiplier = multiplier;

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

        public virtual void BindElementalFloat(string name, Element element)
        {
            if (ElementStats.TrueForAll(es => es.StatName != name))
                ElementStats.Add(new ElementStat(name, element));
        }

        protected void InvokeShipInitializedEvent() => OnShipInitialized?.Invoke(ShipStatus);

        public void PerformShipControllerActions(InputEvents controlType)
        {
            actionHandler.PerformShipControllerActions(controlType);
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            actionHandler.StopShipControllerActions(controlType);
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties) =>
            impactHandler.PerformCrystalImpactEffects(crystalProperties);

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties) =>
            impactHandler.PerformTrailBlockImpactEffects(trailBlockProperties);
    }

    [Serializable]
    public struct InputEventShipActionMapping
    {
        public InputEvents InputEvent;
        public List<ShipAction> ShipActions;
    }

    [Serializable]
    public struct ResourceEventShipActionMapping
    {
        public ResourceEvents ResourceEvent;
        public List<ShipAction> ClassActions;
    }
}
