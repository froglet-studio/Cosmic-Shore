using PlayFab;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFabV2.Models
{
    public class PlayFabAccount{
        public string ID { get; set; }
        public string UniqueID => SystemInfo.deviceUniqueIdentifier;
        public bool IsHost { get; set; }
        public PlayFabAuthenticationContext AuthContext { get; set; }
    }
}