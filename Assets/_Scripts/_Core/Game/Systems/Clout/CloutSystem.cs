using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarWriter.Core.CloutSystem
{
    public class CloutSystem : MonoBehaviour
    {
        int minCloutValue = 0;
        int maxCloutValue = 999;


        // Dictionary to store clout value with a ref to a string key "ShipType_Element_CloutType"
        private Dictionary<string, VesselClout> vesselClouts = new Dictionary<string, VesselClout>();

        private void Start()
        {
            PopulateDefaultDictionary();

        }
        void PopulateDefaultDictionary()
        {
            int playerCloutValue = minCloutValue;  //TODO get player clout from server or save file
            playerCloutValue = Math.Clamp(playerCloutValue, minCloutValue, maxCloutValue);

            vesselClouts.Clear();
            foreach (ShipTypes ship in Enum.GetValues(typeof(ShipTypes)))
            {
                foreach (Element element in Enum.GetValues(typeof(ShipTypes)))
                {
                    foreach (CloutType cloutType in Enum.GetValues(typeof(CloutType)))
                    {
                        VesselClout vesselClout = new VesselClout(ship, element, cloutType, playerCloutValue);

                        string key = CreateKey(ship, element, cloutType);
                        vesselClouts.Add(key, vesselClout);
                    }
                }

            }

        }

        // Adds or Removes clout value to a VesselClout 
        public void AddClout(ShipTypes ship, Element element, CloutType cloutType, int amountToAdd)
        {
            string key = CreateKey(ship, element, cloutType);
            if (vesselClouts.ContainsKey(key))
            {
                vesselClouts.TryGetValue(key, out VesselClout oldVesselClout);
                int oldValue = oldVesselClout.GetValue();                                                           
                int newValue = oldValue + amountToAdd;
                
                VesselClout newVesselClout = new VesselClout(ship, element, cloutType, newValue);
                vesselClouts.Add(key, newVesselClout);
            }
        }

        public int GetCloutValue(ShipTypes ship, Element element, CloutType cloutType)
        {
            int value = 0;

            return value;
        }

        private string CreateKey(ShipTypes ship, Element element, CloutType cloutType)
        {
            string d = "_";
            string key = ((Enum.GetValues(typeof(ShipTypes)).ToString()) + d + (Enum.GetValues(typeof(ShipTypes)).ToString()) + d + (Enum.GetValues(typeof(CloutType)).ToString()));
            return key;
        }
    }
}

       






    

   


