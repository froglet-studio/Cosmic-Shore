using CosmicShore.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// This class helps in method execution for IVesselStatus instances
    /// </summary>
   public static class ShipHelper
    {
        public static void InitializeShipControlActions(
            IVesselStatus vesselStatus,
            List<InputEventShipActionMapping> inputEventShipActions,
            Dictionary<InputEvents, List<ShipActionSO>> shipControlActions)
        {
            shipControlActions.Clear();

            foreach (var map in inputEventShipActions)
            {
                if (!shipControlActions.TryGetValue(map.InputEvent, out var list))
                {
                    list = new List<ShipActionSO>();
                    shipControlActions.Add(map.InputEvent, list);
                }

                foreach (var asset in map.ShipActions)
                {
                    if (!asset) continue;
                    asset.Initialize(vesselStatus);
                    list.Add(asset);
                }
            }
        }

        public static void InitializeClassResourceActions(
            List<ResourceEventShipActionMapping> resourceEventShipActionMappings,
            Dictionary<ResourceEvents, List<ShipActionSO>> classResourceActions)
        {
            classResourceActions.Clear();

            foreach (var map in resourceEventShipActionMappings)
            {
                if (!classResourceActions.TryGetValue(map.ResourceEvent, out var list))
                {
                    list = new List<ShipActionSO>();
                    classResourceActions.Add(map.ResourceEvent, list);
                }

                foreach (var asset in map.ClassActions)
                {
                    if (asset == null) continue;
                    list.Add(asset);
                }
            }
        }

        public static void DestroyRuntimeActions(List<ShipActionSO> runtimeInstances)
        {
            runtimeInstances?.Clear();
        }


        public static void Teleport(Transform shipTransform, Transform targetTransform) => shipTransform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);

        public static void PerformShipControllerActions(
            InputEvents controlType,
            Dictionary<InputEvents, float> inputAbilityStartTimes,
            Dictionary<InputEvents, List<ShipAction>> shipControlActions)
        {
            inputAbilityStartTimes[controlType] = Time.time;

            if (!shipControlActions.TryGetValue(controlType, out var shipActions)) return;

            foreach (var action in shipActions)
                action.StartAction();
        }

        public static void StopShipControllerActions(InputEvents controlType, Dictionary<InputEvents, List<ShipAction>> shipControlActions)
        {
            if (shipControlActions.TryGetValue(controlType, out var shipActions))
            {
                foreach (var action in shipActions)
                    action.StopAction();
            }
        }

        public static void PerformClassResourceActions(
            ResourceEvents resourceEvent, 
            Dictionary<ResourceEvents, float> resourceAbilityStartTimes,
            Dictionary<ResourceEvents, List<ShipAction>> classResourceActions)
        {
            resourceAbilityStartTimes[resourceEvent] = Time.time;
            if (!classResourceActions.TryGetValue(resourceEvent, out var actions)) return;
            foreach (var action in actions)
                action.StartAction();
        }

        public static void StopClassResourceActions(
            ResourceEvents resourceEvent,
            Dictionary<ResourceEvents, float> resourceAbilityStartTimes,
            Dictionary<ResourceEvents, List<ShipAction>> classResourceActions)
        {
            if (!classResourceActions.TryGetValue(resourceEvent, out var actions)) return;
            foreach (var action in actions)
                action.StopAction();
        }

        public static void ApplyShipMaterial(Material shipMaterial, List<GameObject> shipGeometries)
        {
            if (shipMaterial == null)
                return;

            foreach (var shipGeometry in shipGeometries)
            {
                if (shipGeometry.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    var materials = shipGeometry.GetComponent<SkinnedMeshRenderer>().materials;
                    materials[2] = shipMaterial;
                    shipGeometry.GetComponent<SkinnedMeshRenderer>().materials = materials;
                }
                else if (shipGeometry.GetComponent<MeshRenderer>() != null)
                {
                    var materials = shipGeometry.GetComponent<MeshRenderer>().materials;
                    materials[1] = shipMaterial;
                    shipGeometry.GetComponent<MeshRenderer>().materials = materials;
                }
            }
        }
    }
}