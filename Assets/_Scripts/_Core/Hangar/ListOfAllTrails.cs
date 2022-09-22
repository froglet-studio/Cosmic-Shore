using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This script builds a  public dictionary
/// </summary>

namespace StarWriter.Core.HangerBuilder
{
    public class ListOfAllTrails : MonoBehaviour
    {
        public List<SO_Trail_Base> AllSO_Trails;
        [SerializeField]
        private List<string> TrailNames;
        public Dictionary<string, SO_Trail_Base> AllTrails;


        // Start is called before the first frame update
        void Start()
        {
            IntializeAllTrailsDictionary();
        }

        private void IntializeAllTrailsDictionary()
        {
            int idx = 0;
            AllTrails = new Dictionary<string, SO_Trail_Base>();

            foreach (SO_Trail_Base trail in AllSO_Trails)
            {
                //Add trail to list
                TrailNames.Add(trail.name);
                //add trail to dictionary
                AllTrails.Add(TrailNames[idx], AllSO_Trails[idx]);
                idx++;
            }
        }

        public SO_Trail_Base GetTrailSOFromAllTrails(string trailName)
        {
            AllTrails.TryGetValue(trailName, out SO_Trail_Base newTrail);
            return newTrail;
        }
    }
}