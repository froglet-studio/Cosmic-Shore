using System;
using System.Collections.Generic;
using CosmicShore.Game.Ship;
using UnityEngine;

namespace CosmicShore.Utility.SOAP
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