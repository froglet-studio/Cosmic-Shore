using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
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