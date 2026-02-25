using UnityEngine;
using System;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.Environment.CellModifiers
{
    // the following abstract class will be used as a base clasee to create single modifications that will be serialized in the above scriptable objects to create cell modifications. e.g. extra resources, different growth rates, etc.
    [Serializable]
    public abstract class CellModifier : MonoBehaviour
    {
        public abstract void Apply(Cell cell);
    }    
}