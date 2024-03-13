using Unity.Netcode.Components;
using UnityEngine;

namespace CosmicShore
{
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
