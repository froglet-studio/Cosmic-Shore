using System;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Utilities
{
    [Serializable]
    public struct  TrailBlockEventData
    {
        public Teams OwnTeam;
        public Teams PlayerTeam;
        public string PlayerName;
        public Vector3 Position;
        public Quaternion Rotation;
        public TrailBlockProperties TrailBlockProperties;
    }
}