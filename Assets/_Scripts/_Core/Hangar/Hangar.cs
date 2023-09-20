using StarWriter.Core.IO;
using StarWriter.Utility.Singleton;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// TODO: P1 renamespace this
namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        Teams AITeam;

        [SerializeField] Teams PlayerTeam = Teams.Green;
        [SerializeField] SO_Pilot PlayerPilot;  // Serialized for inspection in hierarchy
        [SerializeField] ShipTypes PlayerShipType = ShipTypes.Random;
        [SerializeField] ShipTypes FriendlyAIShipType = ShipTypes.Manta;
        [SerializeField] ShipTypes HostileAI1ShipType = ShipTypes.MantaAI;
        [SerializeField] ShipTypes HostileAI2ShipType = ShipTypes.Random;

        [SerializeField] public SO_Pilot SoarPilot;
        [SerializeField] public SO_Pilot SmashPilot;
        [SerializeField] public SO_Pilot SportPilot;

        [SerializeField] SO_MaterialSet GreenTeamMaterialSet;
        [SerializeField] SO_MaterialSet RedTeamMaterialSet;
        [SerializeField] SO_MaterialSet BlueTeamMaterialSet;
        [SerializeField] SO_MaterialSet GoldTeamMaterialSet;

        Dictionary<Teams, SO_MaterialSet> TeamMaterialSets;
        Dictionary<string, Ship> ships = new();
        Dictionary<ShipTypes, Ship> shipTypeMap = new();

        [SerializeField] public List<Ship> ShipPrefabs;

        readonly string SelectedShipPlayerPrefKey = "SelectedShip";

        public void SetPlayerShip(int shipType)
        {
            Debug.Log($"Hangar.SetPlayerShip: {(ShipTypes)shipType}");
            PlayerShipType = (ShipTypes)shipType;
            PlayerPrefs.SetInt(SelectedShipPlayerPrefKey, shipType);
        }

        public void SetAIShip(int shipType)
        {
            Debug.Log($"Hangar.SetAIShip: {(ShipTypes)shipType}");
            HostileAI1ShipType = (ShipTypes)shipType;
        }

        public void SetPlayerPilot(SO_Pilot pilot)
        {
            PlayerPilot = pilot;
        }

        public ShipTypes GetPlayerShip()
        {
            return PlayerShipType;
        }

        [Range(1,4)]
        [SerializeField]
        int AIDifficultyLevel = 1;

        public void SetAiDifficultyLevel(int level)
        {
            AIDifficultyLevel = level;
        }

        public Ship SelectedShip { get; private set; }
        public override void Awake()
        {
            base.Awake();

            if (PlayerPrefs.HasKey(SelectedShipPlayerPrefKey))
                PlayerShipType = (ShipTypes) PlayerPrefs.GetInt(SelectedShipPlayerPrefKey);

            TeamMaterialSets = new() {
                { Teams.Green, GreenTeamMaterialSet },
                { Teams.Red,   RedTeamMaterialSet },
                { Teams.Blue,  BlueTeamMaterialSet },
                { Teams.Yellow,  GoldTeamMaterialSet },
                { Teams.Unassigned,  BlueTeamMaterialSet },
            };

            if (PlayerTeam == Teams.None)
            {
                Debug.LogError("Player Team is set to None. Defaulting to Green Team");
                PlayerTeam = Teams.Green;
            }

            foreach (var ship in ShipPrefabs)
            {
                ships.Add(ship.name, ship);
                shipTypeMap.Add(ship.ShipType, ship);
            }

            AITeam = PlayerTeam == Teams.Green ? Teams.Red : Teams.Green;
        }
        public Ship LoadPlayerShip()
        {
            if (PlayerShipType == ShipTypes.Random)
            {
                Array values = Enum.GetValues(typeof(ShipTypes));
                System.Random random = new System.Random();
                PlayerShipType = (ShipTypes)values.GetValue(random.Next(values.Length));
            }

            return LoadPlayerShip(PlayerShipType, PlayerTeam);
        }

        public Ship LoadPlayerShip(ShipTypes shipType, Teams team)
        {
            Ship ship = Instantiate(shipTypeMap[shipType]);

            if (PlayerPilot != null)
                ship.SetPilot(PlayerPilot);

            ship.SetShipMaterial(TeamMaterialSets[team].ShipMaterial);
            ship.SetBlockMaterial(TeamMaterialSets[team].BlockMaterial);
            ship.SetShieldedBlockMaterial(TeamMaterialSets[team].ShieldedBlockMaterial);
            ship.SetAOEExplosionMaterial(TeamMaterialSets[team].AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(TeamMaterialSets[team].AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(TeamMaterialSets[team].SkimmerMaterial);

            SelectedShip = ship;

            return ship;
        }

        public Ship LoadFriendlyAIShip()
        {
            return LoadAIShip(FriendlyAIShipType, PlayerTeam);
        }
        public Ship LoadHostileAI1Ship()
        {
            return LoadAIShip(HostileAI1ShipType, AITeam);
        }
        public Ship LoadHostileAI2Ship()
        {
            return LoadAIShip(HostileAI2ShipType, AITeam);
        }
        public Ship LoadAIShip(ShipTypes shipType, Teams team)
        {
            if (shipType == ShipTypes.Random)
            {
                Array values = Enum.GetValues(typeof(ShipTypes));
                System.Random random = new System.Random();
                shipType = (ShipTypes) values.GetValue(random.Next(1, values.Length));
            }

            Ship ship = Instantiate(shipTypeMap[shipType]);
            ship.SetShipMaterial(TeamMaterialSets[team].ShipMaterial);
            ship.SetBlockMaterial(TeamMaterialSets[team].BlockMaterial);
            ship.SetShieldedBlockMaterial(TeamMaterialSets[team].ShieldedBlockMaterial);
            ship.SetAOEExplosionMaterial(TeamMaterialSets[team].AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(TeamMaterialSets[team].AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(TeamMaterialSets[team].SkimmerMaterial);

            AIPilot pilot = ship.GetComponent<AIPilot>();
            pilot.SkillLevel = ((float)AIDifficultyLevel-1) / 3; // this assumes that levels remain from 1-4
            pilot.AutoPilotEnabled = true;
            //player.GameCanvas.MiniGameHUD.SetPipActive(pip);
            //pipCamera.gameObject.SetActive(pip);

            return ship;
        }

        public Ship LoadSecondPlayerShip(ShipTypes PlayerShipType)
        {
            return Instantiate(shipTypeMap[PlayerShipType]);
        }

        public Material GetTeamBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].BlockMaterial;
        }
        
        public Material GetTeamShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].ShieldedBlockMaterial;
        }
    }
}