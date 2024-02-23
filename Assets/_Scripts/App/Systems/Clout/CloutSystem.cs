using System;
using UnityEngine;
using CosmicShore.Integrations.Playfab.PlayStream;
using VContainer;
using VContainer.Unity;

namespace CosmicShore.App.Systems.Clout
{
    public class CloutSystem : IPostInitializable, IDisposable
    {
        // Player clout model
        public Clout PlayerClout;

        // Min and Max Value
        const int MinCloutValue = 0;
        const int MaxCloutValue = 999;
        
        // Clout related event
        public static event Action<Clout> OnUpdatingMasterClout;
        
        // Network condition
        private bool isConnected;

        [Inject] private PlayerDataController _dataController;

        public CloutSystem(PlayerDataController dataController)
        {
            _dataController = dataController;
        }

        public void PostInitialize()
        {
            // Events that talking to PlayFab
            PlayerDataController.OnLoadingPlayerClout += PopulatePlayerClouts;
            OnUpdatingMasterClout += _dataController.UpdatePlayerClout;
            
            // Network detector
            NetworkMonitor.NetworkConnectionFound += SetOnline;
            NetworkMonitor.NetworkConnectionLost += SetOffline;
            
            // John's test method
            if(isConnected) Test();
        }

        public void Dispose()
        {
            // Events that talking to PlayFab
            OnUpdatingMasterClout -= _dataController.UpdatePlayerClout;
            PlayerDataController.OnLoadingPlayerClout -= PopulatePlayerClouts;
            
            // Network detector
            NetworkMonitor.NetworkConnectionFound -= SetOnline;
            NetworkMonitor.NetworkConnectionLost -= SetOffline;
        }

        void SetOnline()
        {
            isConnected = true;
        }

        void SetOffline()
        {
            isConnected = false;
        }

        void PopulatePlayerClouts(Clout playerClout)
        {
            PlayerClout = playerClout;
        }

        // Adds or Removes clout value
        public void UpdateShipClout(ShipTypes shipType, int cloutValue)
        {
            if (!isConnected) return;
            
            // Check if ship clout is null, if yes give a new instance
            if (PlayerClout.ShipClouts == null)
            {
                PlayerClout.ShipClouts = new();
            }
            
            // Get new value into the ship clout
            if (PlayerClout.ShipClouts.TryGetValue(shipType, out var previousCloutValue))
            {
                PlayerClout.ShipClouts[shipType] = Math.Clamp(previousCloutValue + cloutValue, MinCloutValue, MaxCloutValue);
            }                                                           
            else
            {
                PlayerClout.ShipClouts.Add(shipType, Math.Clamp(cloutValue,MinCloutValue, MaxCloutValue));
            }
            
            // Calculate the master player clout value
            CalculateMasterClout();
            
            // Let the other systems handle the player clout data upon updates
            OnUpdatingMasterClout?.Invoke(PlayerClout);
        }

        private void CalculateMasterClout()
        {
            if (!isConnected) return;
            
            if (PlayerClout.ShipClouts == null) return;

            foreach (var value in PlayerClout.ShipClouts.Values)
            {
                PlayerClout.MasterCloutValue += value;
            }
            
            PlayerClout.MasterCloutValue = Math.Clamp(PlayerClout.MasterCloutValue, MinCloutValue, MaxCloutValue);
        }

        // Gets clout value
        public int GetShipCloutValue(ShipTypes ship)
        {
            var value = MinCloutValue;
            
            if (PlayerClout.ShipClouts == null) return value;

            if (PlayerClout.ShipClouts.TryGetValue(ship, out value))
            {
                return value;
            }
            
            return value;
        }
        
        
        void Test() 
        {
            //adding clout
            UpdateShipClout(ShipTypes.Manta, 20);

            //getting clout value
            GetShipCloutValue(ShipTypes.Manta); //a changed dictionary entry

            GetShipCloutValue(ShipTypes.Grizzly); // a default dictionary entry

            //removing clout
            UpdateShipClout(ShipTypes.Manta, -20);

            int mcsValue = GetShipCloutValue(ShipTypes.Manta);
            Debug.Log("Manta - Charge - Sport : Value = " + mcsValue);

        }
    }
}