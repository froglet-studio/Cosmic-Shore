using PlayFab;
using UnityEngine;

namespace CosmicShore.Core
{
    public class PlayFabAccount{
        public string ID { get; set; }
        public string UniqueID => SystemInfo.deviceUniqueIdentifier;
        public bool IsHost { get; set; }
        public PlayFabAuthenticationContext AuthContext { get; set; }
    }
}