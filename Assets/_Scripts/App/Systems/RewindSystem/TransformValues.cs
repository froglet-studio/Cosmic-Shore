using System;
using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    [Serializable]
    public class TransformValues
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public TransformValues(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
}
