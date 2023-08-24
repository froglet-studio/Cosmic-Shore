using UnityEngine;
using System.Collections.Generic;

public class Bone
{
    public Transform Transform { get; private set; }
    public Bone Parent { get; private set; }
    public List<Bone> Children { get; private set; } = new List<Bone>();

    private float animationSpeed = 1.0f;
    private float animationAmplitude = 50.0f;

    public Bone(Transform transform, Bone parent = null)
    {
        this.Transform = transform;
        this.Parent = parent;
        if (parent != null)
        {
            parent.Children.Add(this);
        }
    }

    public void Animate(float deltaTime)
    {
        // Simple oscillation using sine function
        float offset = Mathf.Sin(Time.time * animationSpeed) * animationAmplitude;

        // If it's a child bone, the animation amplitude is increased
        if (Parent != null)
        {
            offset *= 1.5f;
        }

        // Apply the offset to the bone's position
        Transform.localPosition = new Vector3(Transform.localPosition.x + offset, Transform.localPosition.y, Transform.localPosition.z);

        // Animate the child bones
        foreach (var child in Children)
        {
            child.Animate(deltaTime);
        }
    }
}
