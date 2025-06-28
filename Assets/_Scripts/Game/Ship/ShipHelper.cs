using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// This class helps in method execution for IShipStatus instances
    /// </summary>
    public static class ShipHelper
    {
        public static void InitializeShipControlActions(IShipStatus shipStatus,
            List<InputEventShipActionMapping> inputEventShipActions,
            Dictionary<InputEvents, List<ShipAction>> shipControlActions)
        {
            foreach (var inputEventShipAction in inputEventShipActions)
                if (!shipControlActions.ContainsKey(inputEventShipAction.InputEvent))
                    shipControlActions.Add(inputEventShipAction.InputEvent, inputEventShipAction.ShipActions);
                else
                    shipControlActions[inputEventShipAction.InputEvent].AddRange(inputEventShipAction.ShipActions);

            foreach (var shipAction in shipControlActions.Keys.SelectMany(key => shipControlActions[key]))
                shipAction.Initialize(shipStatus.Ship);
        }

        public static void InitializeClassResourceActions(IShipStatus shipStatus,
            List<ResourceEventShipActionMapping> resourceEventShipActionMappings,
            Dictionary<ResourceEvents, List<ShipAction>> classResourceActions)
        {
            foreach (var resourceEventClassAction in resourceEventShipActionMappings)
                if (!classResourceActions.ContainsKey(resourceEventClassAction.ResourceEvent))
                    classResourceActions.Add(resourceEventClassAction.ResourceEvent, resourceEventClassAction.ClassActions);
                else
                    classResourceActions[resourceEventClassAction.ResourceEvent].AddRange(resourceEventClassAction.ClassActions);

            foreach (var classAction in classResourceActions.Keys.SelectMany(key => classResourceActions[key]))
                classAction.Initialize(shipStatus.Ship);
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