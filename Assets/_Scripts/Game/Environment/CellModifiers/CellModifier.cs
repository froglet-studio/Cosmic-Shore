using UnityEngine;
using System;
// the following abstract class will be used as a base clasee to create single modifications that will be serialized in the above scriptable objects to create cell modifactions e.g. extra resources, different growth rates, etc.
[Serializable]
public abstract class CellModifier : MonoBehaviour
{
    public abstract void Apply(Node cell);
}
