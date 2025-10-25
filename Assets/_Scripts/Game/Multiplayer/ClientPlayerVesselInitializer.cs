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
        
        [ClientRpc]
        internal void InitializeAllPlayersAndVessels_ClientRpc(ClientRpcParams clientRpcParams = default)
        {
            InitializeAllPlayersAndVessels();
        }
        
        [ClientRpc]
        internal void InitializePlayerAndVessel_ClientRpc(ulong clientId, ClientRpcParams clientRpcParams = default)
        {
            InitializePlayerAndVessel(clientId);
        }
        
        [ClientRpc]
        internal void InvokeClientReady_ClientRpc(ClientRpcParams clientRpcParams = default) => gameData.InvokeClientReady();

        void InitializeAllPlayersAndVessels()
        {
            foreach (var networkPlayer in Player.NppList.Cast<Player>())
            {
                InitializePlayerAndVessel(networkPlayer.OwnerClientId);
            }
        }
        
        void InitializePlayerAndVessel(ulong clientId)
        {
            var networkPlayer = NetworkPlayerClientCache.GetInstanceByClientId(clientId);
            var networkShip = NetworkVesselClientCache.GetInstanceByClientId(clientId);
            networkPlayer.InitializeForMultiplayerMode(networkShip);
            networkShip.Initialize(networkPlayer, false);
            PlayerVesselInitializeHelper.SetShipProperties(themeManagerData, networkShip);
            
            
            var roundStats = networkPlayer.gameObject.GetOrAdd<NetworkRoundStats>();
            if (!roundStats)
            {
                Debug.LogError(
                    "No network round stats found in network player!. Add a NetworkRoundStats component to the prefab!");
                return;
            }
            gameData.AddPlayer(networkPlayer);

            /*if (IsServer)
            {
                roundStats.Name = networkPlayer.Name;
                roundStats.Domain = networkPlayer.Domain;
            }*/

            if (!clientId.IsLocalClient())
                return;
            
            DelayInvokeClientReady(clientId, this.GetCancellationTokenOnDestroy()).Forget();
        }
        
        async UniTaskVoid DelayInvokeClientReady(ulong clientId, CancellationToken token)
        {
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime, PlayerLoopTiming.LastPostLateUpdate ,token);
            if (token.IsCancellationRequested)
                return;
            
            gameData.InvokeClientReady();
                
        }
    }
}
