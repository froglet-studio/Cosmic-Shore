using StarWriter.Core.CloutSystem;
using StarWriter.Core.HangerBuilder;
using StarWriter.Utility.Singleton;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct Loadout
{
    public int Intensity;
    public int PlayerCount;
    public ShipTypes ShipType;
    public MiniGames GameMode;

    public Loadout(int intensity, int playerCount, ShipTypes shipType, MiniGames gameMode)
    {
        Intensity = intensity;
        PlayerCount = playerCount;
        ShipType = shipType;
        GameMode = gameMode;
    }
    public override string ToString()
    {
        return Intensity + "_" + PlayerCount + "_" + ShipType + "_" + GameMode ;
    }
}
namespace StarWriter.Core.Favoriting
{
    public class LoadoutSystem : MonoBehaviour
    {
        int loadoutIndex = 0;

        Loadout activeLoadout = new();

        List<Loadout> loadouts;
        void Start()
        {
            loadouts = new List<Loadout>();
            activeLoadout = loadouts[0];
        }

        public bool CheckLoadoutsExist(int idx)
        {
            return loadouts.Count > idx;
        }

        /*public void ResetAllLoadouts()
        {
            int intensity = 1; int playerCount = 1; int shipType = 1; int gameMode = 1;

            for (int i = 0; i < 4; i++)
            {                
                Loadout loadout = new Loadout(intensity, playerCount, (ShipTypes)shipType, (MiniGames)gameMode);
                loadouts.Add(loadout);
                Debug.Log("Loaded loadout " + loadout.ToString() + " currently " + loadouts.Count + "loadouts");
                intensity++;
                playerCount++;
                shipType++;
                gameMode++;
            }
            
        }*/

        public Loadout GetActiveLoadout()
        {
            return activeLoadout;
        }

        public Loadout GetLoadout(int idx)
        {
            return loadouts[idx];
        }

        public List<Loadout> GetFullListOfLoadouts()
        {
            return loadouts;
        }
        public int GetActiveLoadoutsIndex()    //Loadout Select Buttions set index 1-4
        {
            return loadoutIndex;
        }
        

        public void SetCurrentlySelectedLoadout(Loadout loadout, int loadoutIndex)
        {
            int idx = loadoutIndex--;  //change 1-4 to 0-3
            idx = Mathf.Clamp(idx, 0, loadouts.Count);
            loadouts[idx] = loadout;
        }

        
        public void SetActiveLoadoutIndex(int loadoutIndex) 
        {
            Debug.Log("Loadout Index changed to " + loadoutIndex);

            int idx = loadoutIndex--;  //change 1-4 to 0-3
            idx = Mathf.Clamp(idx, 0, loadouts.Count);            
            activeLoadout = loadouts[idx];
        }

    }
}



