using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;

namespace StarWriter.Core
{
    public class EnvironmentManager : SingletonPersistent<EnvironmentManager>
    {
        [SerializeField] private bool snowEnabled = false; 
        [SerializeField] private bool cageEnabled = false;
        [SerializeField] private bool dynamicBackgroundEnabled = false;


        public void SetEnvironment(char shipType) //TODO Does Pilot choice effect environment also?
        {

            switch (shipType)
            {
                case 'M': // Manta
                    snowEnabled = true;
                    dynamicBackgroundEnabled = false;
                    cageEnabled = false;
                    break;
                case 'S': // Shark
                    snowEnabled = false;
                    dynamicBackgroundEnabled = false;
                    cageEnabled = true;
                    break;
                case 'D': // Dolphin
                    snowEnabled = false;
                    dynamicBackgroundEnabled = true;
                    cageEnabled = false;
                    break;
            }

        }
    }
}

