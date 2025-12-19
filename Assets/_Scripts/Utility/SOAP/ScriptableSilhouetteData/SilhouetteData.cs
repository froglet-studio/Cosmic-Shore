using System;
using System.Collections.Generic;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Utilities
{
    [Serializable]
    public struct SilhouetteData
    {
        public SilhouetteController Sender;
        public bool IsSilhouetteActive;
        public bool IsTrailDisplayActive;
        public List<GameObject> Silhouettes;
    }
}