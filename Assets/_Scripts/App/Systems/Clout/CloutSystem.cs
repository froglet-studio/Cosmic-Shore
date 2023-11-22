using System;
using System.Collections.Generic;
using CosmicShore.Integrations.Playfab.Player_Models;
using UnityEngine;

namespace CosmicShore.App.Systems.Clout
{
    public class CloutSystem : MonoBehaviour
    {
        // Dictionary to store clout value with a ref to a string key "ShipType_Element_CloutType"
        // public Dictionary<ShipTypes, int> ShipClouts { get; set; }
        // public int MasterCloutValue { get; private set; } = 0;
        public static Clout PlayerClout;

        const int MinCloutValue = 0;
        const int MaxCloutValue = 999;
        
        // Clout related event
        public static event Action<Clout> OnUpdatingMasterClout;

        private void OnEnable()
        {
            PlayerDataController.OnLoadingPlayerClout += PopulatePlayerClouts;
            OnUpdatingMasterClout += PlayerDataController.Instance.UpdatePlayerClout;
            Test();
        }

        private void OnDisable()
        {
            OnUpdatingMasterClout -= PlayerDataController.Instance.UpdatePlayerClout;
            PlayerDataController.OnLoadingPlayerClout -= PopulatePlayerClouts;
        }

        void PopulatePlayerClouts(Clout playerClout)
        {
            PlayerClout = playerClout;
        }

        // Adds or Removes clout value
        public void UpdateShipClout(ShipTypes shipType, int cloutValue)
        {
            // Check if ship clout is null, if yes give a new instance
            if (PlayerClout.shipClouts == null)
            {
                PlayerClout.shipClouts = new();
            }
            
            // Get new value into the ship clout
            if (PlayerClout.shipClouts.TryGetValue(shipType, out var previousCloutValue))
            {
                PlayerClout.shipClouts[shipType] = Math.Clamp(previousCloutValue + cloutValue, MinCloutValue, MaxCloutValue);
            }                                                           
            else
            {
                PlayerClout.shipClouts.Add(shipType, Math.Clamp(cloutValue,MinCloutValue, MaxCloutValue));
            }
            
            // Calculate the master player clout value
            CalculateMasterClout();
            OnUpdatingMasterClout?.Invoke(PlayerClout);
        }

        private void CalculateMasterClout()
        {
            if (PlayerClout.shipClouts == null) return;

            foreach (var value in PlayerClout.shipClouts.Values)
            {
                PlayerClout.MasterCloutValue += value;
            }
            
            PlayerClout.MasterCloutValue = Math.Clamp(PlayerClout.MasterCloutValue, MinCloutValue, MaxCloutValue);
        }

        // Gets clout value
        public int GetShipCloutValue(ShipTypes ship)
        {
            var value = MinCloutValue;
            
            if (PlayerClout.shipClouts == null) return value;

            if (PlayerClout.shipClouts.TryGetValue(ship, out value))
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