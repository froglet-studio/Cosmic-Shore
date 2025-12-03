using System;
using System.Linq;
using System.Threading;
using CosmicShore.App.Systems;
using CosmicShore.Core;
using CosmicShore.SOAP;
using CosmicShore.Utility.ClassExtensions;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;


namespace CosmicShore.Game
{
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField] 
        ThemeManagerDataContainerSO themeManagerData;
        
        [SerializeField]
        protected GameDataSO gameData;
        
        /// <summary>
        /// // This is the new client, and we have to initialize all the other client's vessel and player clones in this client.
        /// </summary>
        [ClientRpc]
        internal void InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(ClientRpcParams clientRpcParams = default)
        {
            foreach (var networkPlayer in Player.NppList.Cast<Player>())
            {
                InitializePlayerAndVessel(networkPlayer.OwnerClientId);
            }
            
            DelayInvokeClientReady(this.GetCancellationTokenOnDestroy()).Forget();
        }
        
        /// <summary>
        /// // A new client joined in this client, we need to initialize the new client's vessel and player clone only.
        /// </summary>
        [ClientRpc]
        internal void InitializeNewPlayerAndVesselInThisClient_ClientRpc(ulong clientId, ClientRpcParams clientRpcParams = default)
        {
            InitializePlayerAndVessel(clientId);
        }
        
        void InitializePlayerAndVessel(ulong clientId)
        {
            var networkPlayer = NetworkPlayerClientCache.GetInstanceByClientId(clientId);
            var networkShip = NetworkVesselClientCache.GetInstanceByClientId(clientId);
            networkPlayer.InitializeForMultiplayerMode(networkShip);
            networkShip.Initialize(networkPlayer, false);
            VesselInitializeHelper.SetShipProperties(themeManagerData, networkShip);
            gameData.AddPlayer(networkPlayer);
        }
        
        async UniTaskVoid DelayInvokeClientReady(CancellationToken token)
        {
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime, PlayerLoopTiming.LastPostLateUpdate ,token);
            if (token.IsCancellationRequested)
                return;
            
            gameData.InvokeClientReady();
                
        }
    }
}
