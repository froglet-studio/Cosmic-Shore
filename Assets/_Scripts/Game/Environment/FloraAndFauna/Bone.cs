using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Utility;

public class Bone : MonoBehaviour 
{
    public Transform Transform { get; private set; }
    public Bone Parent { get; private set; }
    public int  Index { get; private set; }
    public List<Bone> Children { get; private set; } = new List<Bone>();
    

    Vector3 offsetVector = Vector3.zero;
    Vector3 targetPosition = Vector3.zero;
    public bool dirty = true;

    public Bone(Transform transform, int index, Bone parent = null)
    {
        this.Transform = transform;
        this.Parent = parent;
        this.Index = index;
        if (parent != null)
        {
            parent.Children.Add(this);
        }
    }

    private void Start()
    {
        targetPosition = Transform.localPosition;
        //if (Index == 5) LerpBone(Vector3.one * 10);
    }

    public void LerpBone(Vector3 position)
    {
        dirty = true;
        
        StartCoroutine(LerpUtilities.LerpingCoroutine(Transform.localPosition.x, position.x, 3, (i) => { offsetVector.x = i; }));
        StartCoroutine(LerpUtilities.LerpingCoroutine(Transform.localPosition.y, position.y, 3, (i) => { offsetVector.y = i; }));
        StartCoroutine(LerpUtilities.LerpingCoroutine(Transform.localPosition.z, position.z, 3, (i) => { offsetVector.z = i; }));

        targetPosition = position;
    }
    
    public void Animate()
    {
        //if (Parent != null && (Transform.localPosition - Parent.Transform.localPosition).sqrMagnitude > 2f) LerpBone((Parent.Transform.localPosition + Transform.localPosition) / 2);
        //if (Children.Count > 0 && (Transform.localPosition - Children[0].Transform.localPosition).sqrMagnitude > 2f) LerpBone((Children[0].Transform.localPosition + Transform.localPosition) / 2);
        if ((Transform.localPosition - targetPosition).sqrMagnitude < .1f) dirty = false;
        // else; Transform.localPosition = offsetVector;
        
    }
}
