using CosmicShore.Integrations.PlayFab.PlayerData;
using System;
using UnityEngine;


namespace CosmicShore.App.Systems.Clout
{
    public class CloutSystem
    {
        // Player clout model
        private Clout _playerClout;

        // Min and Max Value
        private const int MinCloutValue = 0;
        private const int MaxCloutValue = 999;
        
        // Clout related event
        public static event Action<Clout> OnUpdatingMasterClout;
        
        // Network condition
        private bool _isConnected;

        private static PlayerDataController DataController => PlayerDataController.Instance;
        
        public void PostInitialize()
        {
            // Events that talking to PlayFab
            PlayerDataController.OnLoadingPlayerClout += PopulatePlayerClouts;
            OnUpdatingMasterClout += DataController.UpdatePlayerClout;
            
            // Network detector
            NetworkMonitor.NetworkConnectionFound += SetOnline;
            NetworkMonitor.NetworkConnectionLost += SetOffline;
            
            // John's test method
            if(_isConnected) Test();
        }

        public void Dispose()
        {
            // Events that talking to PlayFab
            OnUpdatingMasterClout -= DataController.UpdatePlayerClout;
            PlayerDataController.OnLoadingPlayerClout -= PopulatePlayerClouts;
            
            // Network detector
            NetworkMonitor.NetworkConnectionFound -= SetOnline;
            NetworkMonitor.NetworkConnectionLost -= SetOffline;
        }

        void SetOnline()
        {
            _isConnected = true;
        }

        void SetOffline()
        {
            _isConnected = false;
        }

        void PopulatePlayerClouts(Clout playerClout)
        {
            _playerClout = playerClout;
        }

        // Adds or Removes clout value
        public void UpdateShipClout(ShipTypes shipType, int cloutValue)
        {
            if (!_isConnected) return;
            
            // Check if ship clout is null, if yes give a new instance
            if (_playerClout.ShipClouts == null)
            {
                _playerClout.ShipClouts = new();
            }
            
            // Get new value into the ship clout
            if (_playerClout.ShipClouts.TryGetValue(shipType, out var previousCloutValue))
            {
                _playerClout.ShipClouts[shipType] = Math.Clamp(previousCloutValue + cloutValue, MinCloutValue, MaxCloutValue);
            }                                                           
            else
            {
                _playerClout.ShipClouts.Add(shipType, Math.Clamp(cloutValue,MinCloutValue, MaxCloutValue));
            }
            
            // Calculate the master player clout value
            CalculateMasterClout();
            
            // Let the other systems handle the player clout data upon updates
            OnUpdatingMasterClout?.Invoke(_playerClout);
        }

        private void CalculateMasterClout()
        {
            if (!_isConnected) return;
            
            if (_playerClout.ShipClouts == null) return;

            foreach (var value in _playerClout.ShipClouts.Values)
            {
                _playerClout.MasterCloutValue += value;
            }
            
            _playerClout.MasterCloutValue = Math.Clamp(_playerClout.MasterCloutValue, MinCloutValue, MaxCloutValue);
        }

        // Gets clout value
        public int GetShipCloutValue(ShipTypes ship)
        {
            var value = MinCloutValue;
            
            if (_playerClout.ShipClouts == null) return value;

            if (_playerClout.ShipClouts.TryGetValue(ship, out value))
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