using System;
using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    [Serializable]
    public class TransformValues
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public TransformValues(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }
}
