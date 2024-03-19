using Unity.Netcode.Components;
using UnityEngine;

namespace CosmicShore.Multiplayer.UnityMultiplayer.Netcode
{
    public enum AuthorityMode
    {
        Server,
        Client
    }
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        public AuthorityMode authorityMode = AuthorityMode.Client;
        protected override bool OnIsServerAuthoritative()
        {
            return authorityMode == AuthorityMode.Server;
        }
    }
}
