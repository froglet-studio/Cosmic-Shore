using System;
using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    [Serializable]
    public class ObjectData
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;

        public ObjectData(Vector3 position, Vector3 rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
}
