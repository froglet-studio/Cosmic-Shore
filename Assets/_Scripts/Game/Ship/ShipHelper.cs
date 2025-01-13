using CosmicShore.Game;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// This class helps in method execution for IShip instances
    /// </summary>
    public static class ShipHelper
    {

        public static void InitializeShipGeometries(IShip ship, List<GameObject> shipGeometryObjects)
        {
            foreach (var shipGeometry in shipGeometryObjects)
                shipGeometry.AddComponent<ShipGeometry>().Ship = ship;
        }

        public static void InitializeShipControlActions(IShip ship,
            List<InputEventShipActionMapping> inputEventShipActions,
            Dictionary<InputEvents, List<ShipAction>> shipControlActions)
        {
            foreach (var inputEventShipAction in inputEventShipActions)
                if (!shipControlActions.ContainsKey(inputEventShipAction.InputEvent))
                    shipControlActions.Add(inputEventShipAction.InputEvent, inputEventShipAction.ShipActions);
                else
                    shipControlActions[inputEventShipAction.InputEvent].AddRange(inputEventShipAction.ShipActions);

            foreach (var shipAction in shipControlActions.Keys.SelectMany(key => shipControlActions[key]))
                shipAction.Ship = ship;
        }

        public static void InitializeClassResourceActions(IShip ship,
            List<ResourceEventShipActionMapping> resourceEventShipActionMappings,
            Dictionary<ResourceEvents, List<ShipAction>> classResourceActions)
        {
            foreach (var resourceEventClassAction in resourceEventShipActionMappings)
                if (!classResourceActions.ContainsKey(resourceEventClassAction.ResourceEvent))
                    classResourceActions.Add(resourceEventClassAction.ResourceEvent, resourceEventClassAction.ClassActions);
                else
                    classResourceActions[resourceEventClassAction.ResourceEvent].AddRange(resourceEventClassAction.ClassActions);

            foreach (var classAction in classResourceActions.Keys.SelectMany(key => classResourceActions[key]))
                classAction.Ship = ship;
        }

        public static void Teleport(Transform shipTransform, Transform targetTransform) => shipTransform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);

        public static void PerformShipControllerActions(InputEvents @event, out float time, Dictionary<InputEvents, List<ShipAction>> events)
        {
            time = Time.time;

            if (!events.TryGetValue(@event, out var shipActions)) return;

            foreach (var action in shipActions)
                action.StartAction();
        }

        public static void StopShipControllerActions(InputEvents @event, Dictionary<InputEvents, List<ShipAction>> events)
        {
            if (events.TryGetValue(@event, out var shipActions))
            {
                foreach (var action in shipActions)
                    action.StopAction();
            }
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