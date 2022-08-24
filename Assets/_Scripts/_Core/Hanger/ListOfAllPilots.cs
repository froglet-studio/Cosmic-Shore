using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This script builds a  public dictionary
/// </summary>

namespace StarWriter.Core.HangerBuilder
{
    public class ListOfAllPilots : MonoBehaviour
    {
        public List<SO_Pilot> AllSO_Pilots;
        [SerializeField]
        private List<string> pilotNames;
        public Dictionary<string, SO_Pilot> AllPilots;


        // Start is called before the first frame update
        void Start()
        {
            IntializeAllPilotsDictionary();
        }

        private void IntializeAllPilotsDictionary()
        {
            int idx = 0;
            AllPilots = new Dictionary<string, SO_Pilot>();

            foreach (SO_Pilot pilot in AllSO_Pilots)
            {
                //Add pilot to list
                pilotNames.Add(pilot.CharacterName);
                //add pilot to dictionary
                AllPilots.Add(pilotNames[idx], AllSO_Pilots[idx]);
                idx++;
            }
        }

        public SO_Pilot GetPilotSOFromAllPilots(string pilotName)
        {
            AllPilots.TryGetValue(pilotName, out SO_Pilot newPilot);
            return newPilot;
        }
    }
}

