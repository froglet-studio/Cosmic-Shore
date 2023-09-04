using StarWriter.Core;
using StarWriter.Core.IO;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Ship))]
public abstract class ShipAnimation : MonoBehaviour
{
    protected InputController inputController;

    [SerializeField] public SkinnedMeshRenderer SkinnedMeshRenderer;
    [SerializeField] bool SaveNewPositions; // TODO: remove after all models have shape keys support
    [SerializeField] bool UseShapeKeys; // TODO: remove after all models have shape keys support
    [SerializeField] protected float brakeThreshold = .65f;
    [SerializeField] protected float lerpAmount = 2f;
    [SerializeField] protected float smallLerpAmount = .7f;

    protected List<Transform> Transforms = new(); // TODO: use this to populate the ship geometries on ship.cs
    protected List<Quaternion> InitialRotations = new(); // TODO: use this to populate the ship geometries on ship.cs

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
        if (SaveNewPositions)
        {
            for (var i = 0; i < Transforms.Count; i++)
                ResetAnimation(Transforms[i], InitialRotations[i]);
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

    protected virtual void AnimatePart(Transform part, float pitch, float yaw, float roll)
    {
        var targetRotation = Quaternion.Euler(pitch, yaw, roll);

        part.localRotation = Quaternion.Lerp(
                                    part.localRotation,
                                    targetRotation,
                                    lerpAmount * Time.deltaTime);
    }

    protected virtual void AnimatePart(Transform part, float pitch, float yaw, float roll, Quaternion initialRotation)
    {
        var targetRotation = Quaternion.Euler(pitch, roll, yaw) * initialRotation;

        part.localRotation = Quaternion.Lerp(
                                    part.localRotation,
                                    targetRotation,
                                    lerpAmount * Time.deltaTime);
    }

    public virtual void UpdateShapeKey(Element element, float percent)
    {
        if (!UseShapeKeys) return;

        var index = 0;
        switch(element)
        {
            case Element.Mass:   index = 0; break;
            case Element.Charge: index = 1; break;
            case Element.Space:  index = 2; break;
            case Element.Time:   index = 3; break;
        }
        SkinnedMeshRenderer.SetBlendShapeWeight(index, percent);
    }
}