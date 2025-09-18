using CosmicShore.Game;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models;
using CosmicShore.Utilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Core
{
    /// <summary>
    /// DEPRECATED - Try remove all dependencies to this script
    /// </summary>
    public class Hangar : Singleton<Hangar>
    {
        const string SELECTED_SHIP_KEY = "SelectedShip";

        // HashSet has only one same value in one set
        // TODO: move to cell
        // [HideInInspector] public HashSet<Transform> SlowedShipTransforms = new();

        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        // TODO - Store in separate data container
        // public ShipClassType ChoosenClassType;

        /// <summary>
        /// This is the vessel that is currently selected in the hangar by local owner client.
        /// Store it in separate data container.
        /// </summary>
        public IVessel LocalPlayerVessel { get; private set; }


        public override void Awake()
        {
            base.Awake();
            // ChoosenClassType = (ShipClassType)PlayerPrefs.GetInt(SELECTED_SHIP_KEY);
        }

        // TODO - Store in separate data container
        /*public void SetPlayerShip(int shipType)
        {
            ChoosenClassType = (ShipClassType)shipType;
            PlayerPrefs.SetInt(SELECTED_SHIP_KEY, shipType);
        }*/

        // TODO - Store in data container. Remove the method
        public void SetPlayerCaptain(Captain captain)
        {
            // PlayerCaptain = captain;
        }

        // TODO - Store in data container. Remove the method
        /*/// <summary>
        /// Intensity Level is defined by Arcade Games, Difficulty Level is defined by Missions
        /// </summary>
        /// <param name="level">Range from 1-4</param>
        public void SetAiIntensityLevel(int level)
        {
            _aiSkillLevel = level;
        }*/
        
        /*public IVessel SetShipProperties(IVessel vessel, Teams team, bool isOwner, SO_Captain so_captain = null)
        {
            // TODO - Get Captains from data containers
            /*if (so_captain == null && CaptainManager.Instance != null)
            {
                var so_captains = CaptainManager.Instance.GetAllSOCaptains().Where(x => x.Vessel.Class == vessel.VesselStatus.ShipType).ToList();
                so_captain = so_captains[Random.Range(0, 3)];
                var captain = CaptainManager.Instance.GetCaptainByName(so_captain.Name);
                if (captain != null)
                {
                    vessel.AssignCaptain(so_captain);
                    vessel.SetResourceLevels(captain.ResourceLevels);
                }
            }#1#

            var materialSet = _themeManagerData.TeamMaterialSets[team];
            vessel.SetShipMaterial(materialSet.ShipMaterial);
            vessel.SetBlockSilhouettePrefab(materialSet.BlockSilhouettePrefab);
            vessel.SetAOEExplosionMaterial(materialSet.AOEExplosionMaterial);
            vessel.SetAOEConicExplosionMaterial(materialSet.AOEConicExplosionMaterial);
            vessel.SetSkimmerMaterial(materialSet.SkimmerMaterial);

            if (isOwner)
                LocalPlayerVessel = vessel;
            return vessel;
        }*/
    }
}