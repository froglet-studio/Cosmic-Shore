using System.Collections.Generic;
using UnityEngine;


namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "SilhouetteEventChannel", menuName = "ScriptableObjects/Event Channels/SilhouetteEventChannelSO")]
    public class SilhouetteEventChannelSO : GenericEventChannelSO<SilhouetteData>
    { }

    public struct SilhouetteData
    {
        public Silhouette Sender;
        public bool IsSilhouetteActive;
        public bool IsTrailDisplayActive;
        public List<GameObject> Silhouettes;
    }
}