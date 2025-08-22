using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utilities
{
    [Serializable]
    public struct SilhouetteData
    {
        public Silhouette Sender;
        public bool IsSilhouetteActive;
        public bool IsTrailDisplayActive;
        public List<GameObject> Silhouettes;
    }
}