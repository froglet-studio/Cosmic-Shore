using StarWriter.Core;
using StarWriter.Core.IO;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Ship))]
public abstract class ShipAnimation : MonoBehaviour
{
    protected InputController inputController;

    [SerializeField] protected float brakeThreshold = .65f;
    [SerializeField] protected float lerpAmount = 2f;
    [SerializeField] protected float smallLerpAmount = .7f;

    protected List<Transform> Transforms = new(); // TODO: use this to populate the ship geometries on ship.cs 

    protected virtual void Start()
    {
        inputController = GetComponent<Ship>().InputController;

        AssignTransforms();
    }

    protected virtual void Update()
    {
        if (inputController == null) inputController = GetComponent<Ship>().InputController; 
        if (inputController != null) // the line above makes this run the moment it has the handle
        {
            if (inputController.Idle)
                Idle();
            else
                PerformShipAnimations(inputController.YSum, inputController.XSum, inputController.YDiff, inputController.XDiff);
        }
    }
    protected abstract void AssignTransforms();

    // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.
    protected abstract void PerformShipAnimations(float YSum, float XSum, float YDiff, float XDiff);
    protected virtual void Idle()
    {
        foreach (Transform transform in Transforms)
            ResetAnimation(transform);
    }

    protected virtual float Brake(float throttle)
    {
        return (throttle < brakeThreshold) ? throttle - brakeThreshold : 0;
    }

    protected virtual void ResetAnimation(Transform part)
    {
        part.localRotation = Quaternion.Lerp(part.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
    }

    protected virtual void AnimatePart(Transform part, float pitch, float yaw, float roll)
    {
        var targetRotation = Quaternion.Euler(pitch, yaw, roll);

        part.localRotation = Quaternion.Lerp(
                                    part.localRotation,
                                    targetRotation,
                                    lerpAmount * Time.deltaTime);
    }
}