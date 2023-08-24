using UnityEngine;
using System.Collections.Generic;

public class Bone
{
    public Transform Transform { get; private set; }
    public Bone Parent { get; private set; }
    public List<Bone> Children { get; private set; } = new List<Bone>();

    private float animationSpeed = .4f;
    private float animationAmplitude = .02f;

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

        // If it's a child bone, the animation amplitude is decreased
        if (Parent != null)
        {
            offset *= .8f;
            //Transform.localPosition = Vector3.one*offset + Transform.localPosition ;
        }

        // Apply the offset to the bone's position
        

        // Animate the child bones
        foreach (var child in Children)
        {
            child.Animate(deltaTime);
        }

    }
}
