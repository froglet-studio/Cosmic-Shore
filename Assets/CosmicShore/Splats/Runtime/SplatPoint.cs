using System;
using UnityEngine;

namespace CosmicShore.Splats
{
    [Serializable]
    public struct SplatPoint
    {
        public Vector3 position;
        public Vector3 scale;
        public Quaternion rotation;
        public Color color;
        public float opacity;
    }
}
