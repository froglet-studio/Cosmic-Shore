using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.CloutSystem
{
    public class CloutSystem : MonoBehaviour
    {
        int minCloutValue = 0;
        int maxCloutValue = 999;

        // Dictionary to store clout value with a ref to a string key "ShipType_Element_CloutType"
        Dictionary<string, Clout> Clouts = new();

        void Start()
        {
            PopulateDefaultDictionary();
            Test();
        }

        void Test() 
        {
            //adding clout
            AddClout(ShipTypes.Manta, Element.Charge, CloutType.Sport, 20);

            //getting clout value
            GetCloutValue(ShipTypes.Manta, Element.Charge, CloutType.Sport); //a changed dictionary entry

            GetCloutValue(ShipTypes.Grizzly, Element.Charge, CloutType.Sport); // a default dictionary entry

            //removing clout
            AddClout(ShipTypes.Manta, Element.Charge, CloutType.Sport, -20);

            int MCSValue = GetCloutValue(ShipTypes.Manta, Element.Charge, CloutType.Sport);
            Debug.Log("Manta - Charge - Sport : Value = " + MCSValue);

        }

        void PopulateDefaultDictionary()
        {
            int playerCloutValue = minCloutValue;  // TODO get player clout from server or save file
            playerCloutValue = Math.Clamp(playerCloutValue, minCloutValue, maxCloutValue);

            Clouts.Clear();
            foreach (ShipTypes ship in Enum.GetValues(typeof(ShipTypes)))
            {
                foreach (Element element in Enum.GetValues(typeof(ShipTypes)))
                {
                    foreach (CloutType cloutType in Enum.GetValues(typeof(CloutType)))
                    {
                        Clout clout = new Clout(ship, element, cloutType, playerCloutValue);

                        string key = CreateKey(ship, element, cloutType);
                        Clouts.Add(key, clout);
                    }
                }
            }
        }

        // Adds or Removes clout value
        public void AddClout(ShipTypes ship, Element element, CloutType cloutType, int amountToAdd)
        {
            string key = CreateKey(ship, element, cloutType);
            if (Clouts.ContainsKey(key))
            {
                Clouts.TryGetValue(key, out Clout oldVesselClout);
                int oldValue = oldVesselClout.GetValue();                                                           
                int newValue = oldValue + amountToAdd;
                
                Clout newClout = new Clout(ship, element, cloutType, newValue);
                Clouts[key] = newClout;

            }
            else
            {
                Clouts.Add(key, new Clout(ship, element, cloutType, amountToAdd));
            }
        }

        // Gets clout value
        public int GetCloutValue(ShipTypes ship, Element element, CloutType cloutType)
        {
            int value = 0;

            return value;
        }

        // Creates a clouts dictionary key and returns it 
        private string CreateKey(ShipTypes ship, Element element, CloutType cloutType)
        {
            string d = "_";
            return string.Join(d, ship.ToString(), element.ToString(), cloutType.ToString());
        }
        
    }
}