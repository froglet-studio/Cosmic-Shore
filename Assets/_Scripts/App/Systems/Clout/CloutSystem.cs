using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.Clout
{
    public class CloutSystem : MonoBehaviour
    {
        // Dictionary to store clout value with a ref to a string key "ShipType_Element_CloutType"
        Dictionary<ShipTypes, int> shipClouts;
        public int MasterCloutValue { get; private set; } = 0;

        const int MinCloutValue = 0;
        const int MaxCloutValue = 999;

        void Start()
        {
            // Populate Default player clout value is 0
            PopulateShipClouts();
            Test();
        }
        
        void PopulateShipClouts(int playerCloutValue = MinCloutValue)
        {
            shipClouts = new();
            playerCloutValue = Math.Clamp(playerCloutValue, MinCloutValue, MaxCloutValue);

            shipClouts.Clear();
            foreach (ShipTypes ship in Enum.GetValues(typeof(ShipTypes)))
            {
                shipClouts.Add(ship, playerCloutValue);
            }
        }

        // Adds or Removes clout value
        public void AccumulateShipClout(ShipTypes ship, int playerCloutValue = 0)
        {
            // Adding to master Clout
            MasterCloutValue += playerCloutValue;
            MasterCloutValue = Math.Clamp(MasterCloutValue, MinCloutValue, MaxCloutValue);
            
            if (shipClouts == null)
            {
                PopulateShipClouts();
            };

            if (shipClouts.TryGetValue(ship, out var previousCloutValue))
            {
                shipClouts[ship] = Math.Clamp(previousCloutValue + playerCloutValue, MinCloutValue, MaxCloutValue);
            }                                                           
            else
            {
                shipClouts.Add(ship, Math.Clamp(playerCloutValue,MinCloutValue, MaxCloutValue));
            }
        }

        // Gets clout value
        public int GetShipCloutValue(ShipTypes ship)
        {
            var value = MinCloutValue;
            
            if (shipClouts == null) return value;

            if (shipClouts.TryGetValue(ship, out value))
            {
                return value;
            }
            
            return value;
        }
        
        
        void Test() 
        {
            //adding clout
            AccumulateShipClout(ShipTypes.Manta, 20);

            //getting clout value
            GetShipCloutValue(ShipTypes.Manta); //a changed dictionary entry

            GetShipCloutValue(ShipTypes.Grizzly); // a default dictionary entry

            //removing clout
            AccumulateShipClout(ShipTypes.Manta, -20);

            int mcsValue = GetShipCloutValue(ShipTypes.Manta);
            Debug.Log("Manta - Charge - Sport : Value = " + mcsValue);

        }
    }
}