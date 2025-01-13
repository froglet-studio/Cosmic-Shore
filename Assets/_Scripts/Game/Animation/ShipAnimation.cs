using CosmicShore.Core;
using CosmicShore.Game.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace CosmicShore.Game.Animation
{
    [RequireComponent(typeof(Ship))]
    public abstract class ShipAnimation : MonoBehaviour
    {
        [SerializeField] public SkinnedMeshRenderer SkinnedMeshRenderer;
        [SerializeField] bool SaveNewPositions; // TODO: remove after all models have shape keys support
        [SerializeField] bool UseShapeKeys; // TODO: remove after all models have shape keys support
        [SerializeField] protected float brakeThreshold = .65f;
        [SerializeField] protected float lerpAmount = 2f;
        [SerializeField] protected float smallLerpAmount = .7f;

        protected List<Transform> Transforms = new(); // TODO: use this to populate the ship geometries on ship.cs
        protected List<Quaternion> InitialRotations = new(); // TODO: use this to populate the ship geometries on ship.cs
        protected IShip Ship;
        protected InputController inputController;
        protected IInputStatus inputStatus;

        protected virtual void Update()
        {
            if (inputController == null) inputController = GetComponent<Ship>().InputController;
            if (inputController != null) // the line above makes this run the moment it has the handle
            {
                if (inputController.Idle)
                    Idle();
                else
                    if (Ship.ShipStatus.SingleStickControls) PerformShipPuppetry(inputController.EasedLeftJoystickPosition.y, inputController.EasedLeftJoystickPosition.x, 0, 0);
                    else PerformShipPuppetry(inputController.YSum, inputController.XSum, inputController.YDiff, inputController.XDiff);
            }
        }

        public virtual void Initialize(IShip ship)
        {
            Ship = ship;
            inputController = Ship.InputController;
            inputStatus = inputController.InputStatus;
            Ship.ResourceSystem.OnElementLevelChange += UpdateShapeKey;

            AssignTransforms();
        }
        protected abstract void AssignTransforms();

        // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.
        protected abstract void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle);
        protected virtual void Idle()
        {
            if (SaveNewPositions)
            {
                for (var i = 0; i < Transforms.Count; i++)
                {
                    // Debug.LogWarningFormat("{0} - {1} - index: {2}", "ShipAnimation" , nameof(Idle), i.ToString());
                    // Debug.LogWarningFormat("{0} - {1} - transform value: {2}", "ShipAnimation" , nameof(Idle), Transforms[i]);
                    // Debug.LogWarningFormat("{0} - {1} - transform value: {2}", "ShipAnimation" , nameof(Idle), InitialRotations[i].ToString());
                    // Debug.LogWarningFormat("{0} - {1} - initial rotations max index: {2}", "ShipAnimation" , nameof(Idle), InitialRotations.Count.ToString());
                    if (i < InitialRotations.Count)
                    {
                        ResetAnimation(Transforms[i], InitialRotations[i]);
                    }
                    else
                    {
                        ResetAnimation(Transforms[i]);
                    }
                }

            }
            else
            {
                foreach (Transform transform in Transforms)
                    ResetAnimation(transform);
            }
        }

        protected virtual float Brake(float throttle)
        {
            return (throttle < brakeThreshold) ? throttle - brakeThreshold : 0;
        }

        protected virtual void ResetAnimation(Transform part)
        {
            part.localRotation = Quaternion.Lerp(part.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        }

        protected virtual void ResetAnimation(Transform part, Quaternion resetQuaternion)
        {
            part.localRotation = Quaternion.Lerp(part.localRotation, resetQuaternion, smallLerpAmount * Time.deltaTime);
        }

        protected virtual void RotatePart(Transform part, float pitch, float yaw, float roll)
        {
            var targetRotation = Quaternion.Euler(pitch, yaw, roll);

            part.localRotation = Quaternion.Lerp(
                                        part.localRotation,
                                        targetRotation,
                                        lerpAmount * Time.deltaTime);
        }

        protected virtual void RotatePart(Transform part, float pitch, float yaw, float roll, Quaternion initialRotation)
        {
            var targetRotation = Quaternion.Euler(pitch, roll, yaw) * initialRotation;

            part.localRotation = Quaternion.Lerp(
                                        part.localRotation,
                                        targetRotation,
                                        lerpAmount * Time.deltaTime);
        }

        public virtual void UpdateShapeKey(Element element, int level)
        {
            if (!UseShapeKeys) return;

            var index = 0;
            switch (element)
            {
                case Element.Mass: index = 0; break;
                case Element.Charge: index = 1; break;
                case Element.Space: index = 2; break;
                case Element.Time: index = 3; break;
            }
            SkinnedMeshRenderer.SetBlendShapeWeight(index, level / 10f);
        }
    }
}