using CosmicShore.App.Systems.Squads;
using CosmicShore.Game.AI;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models;
using CosmicShore.Utility.Singleton;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        Teams AITeam;

        [Header("Ship Type Settings")]
        [SerializeField] Teams PlayerTeam = Teams.Green;
        [SerializeField] Captain PlayerCaptain;  // Serialized for inspection in hierarchy
        [SerializeField] ShipTypes PlayerShipType = ShipTypes.Random;
        [SerializeField] ShipTypes FriendlyAIShipType = ShipTypes.Manta;
        [SerializeField] ShipTypes HostileAI1ShipType = ShipTypes.Random;
        [SerializeField] ShipTypes HostileAI2ShipType = ShipTypes.Random;
        [SerializeField] ShipTypes HostileAI3ShipType = ShipTypes.Random;
        ShipTypes HostileMantaShipType = ShipTypes.Manta;

        [SerializeField] List<ShipTypes> GreenTeamShipTypes = new List<ShipTypes>() { ShipTypes.Random, ShipTypes.Random, ShipTypes.Random };
        [SerializeField] List<ShipTypes> RedTeamShipTypes = new List<ShipTypes>() { ShipTypes.Random, ShipTypes.Random, ShipTypes.Random };
        [SerializeField] List<ShipTypes> GoldTeamShipTypes = new List<ShipTypes>() { ShipTypes.Random, ShipTypes.Random, ShipTypes.Random };
        Dictionary<Teams, List<ShipTypes>> TeamShipTypes = new();

        [Header("Material Settings")]
        [SerializeField] SO_MaterialSet GreenTeamMaterialSet;
        [SerializeField] SO_MaterialSet RedTeamMaterialSet;
        [SerializeField] SO_MaterialSet BlueTeamMaterialSet;
        [SerializeField] SO_MaterialSet GoldTeamMaterialSet;

        Dictionary<Teams, SO_MaterialSet> TeamMaterialSets;
        Dictionary<string, Ship> ships = new();
        Dictionary<ShipTypes, Ship> shipTypeMap = new();

        // HashSet has only one same value in one set
        [HideInInspector] public HashSet<Transform> SlowedShipTransforms = new(); // TODO: move to node

        [SerializeField] public List<Ship> ShipPrefabs;

        readonly string SelectedShipPlayerPrefKey = "SelectedShip";

        public void SetPlayerShip(int shipType)
        {
            Debug.Log($"Hangar.SetPlayerShip: {(ShipTypes)shipType}");
            PlayerShipType = (ShipTypes)shipType;
            PlayerPrefs.SetInt(SelectedShipPlayerPrefKey, shipType);
        }

        public void SetTeamShipType(Teams team, ShipTypes shipType, int slot=0)
        {
            Debug.Log($"Hangar.SetTeamShipType: shipType:{shipType}, team:{team}, slot:{slot}");

            TeamShipTypes[team][slot] = shipType;
        }

        public void SetPlayerCaptain(Captain captain)
        {
            PlayerCaptain = captain;
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

            TeamShipTypes.Add(Teams.Green, GreenTeamShipTypes);
            TeamShipTypes.Add(Teams.Red, RedTeamShipTypes);
            TeamShipTypes.Add(Teams.Gold, GoldTeamShipTypes);

            if (PlayerPrefs.HasKey(SelectedShipPlayerPrefKey))
                PlayerShipType = (ShipTypes) PlayerPrefs.GetInt(SelectedShipPlayerPrefKey);

            TeamMaterialSets = new() {
                { Teams.Green, GreenTeamMaterialSet },
                { Teams.Red,   RedTeamMaterialSet },
                { Teams.Blue,  BlueTeamMaterialSet },
                { Teams.Gold,  GoldTeamMaterialSet },
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
        public Ship LoadPlayerShip(bool useSquad=false)
        {
            if (useSquad)
            {
                return LoadPlayerShip(SquadSystem.SquadLeader.Ship.Class, PlayerTeam);
            }
            else
            {
                if (PlayerShipType == ShipTypes.Random)
                {
                    Array values = Enum.GetValues(typeof(ShipTypes));
                    System.Random random = new System.Random();
                    PlayerShipType = (ShipTypes)values.GetValue(random.Next(values.Length));
                }

                return LoadPlayerShip(PlayerShipType, PlayerTeam);
            }
        }

        public Ship LoadPlayerShip(ShipTypes shipType, Teams team)
        {
            Ship ship = Instantiate(shipTypeMap[shipType]);

            if (PlayerCaptain != null)
                ship.SetResourceLevels(PlayerCaptain.ResourceLevels);

            ship.SetShipMaterial(TeamMaterialSets[team].ShipMaterial);
            ship.SetBlockMaterial(TeamMaterialSets[team].BlockMaterial);
            ship.SetBlockSilhouettePrefab(TeamMaterialSets[team].BlockSilhouettePrefab);
            ship.SetShieldedBlockMaterial(TeamMaterialSets[team].ShieldedBlockMaterial);
            ship.SetAOEExplosionMaterial(TeamMaterialSets[team].AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(TeamMaterialSets[team].AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(TeamMaterialSets[team].SkimmerMaterial);

            SelectedShip = ship;

            return ship;
        }

        public Ship LoadShip(ShipTypes shipType, Teams team)
        {
            return LoadAIShip(shipType, team);
        }

        public Ship LoadFriendlyAIShip()
        {
            return LoadAIShip(FriendlyAIShipType, PlayerTeam);
        }
        public Ship LoadSquadMateOne()
        {
            SquadSystem.LoadSquad();
            return LoadAIShip(SquadSystem.RogueOne.Ship.Class, PlayerTeam, SquadSystem.RogueOne);
        }
        public Ship LoadSquadMateTwo()
        {
            SquadSystem.LoadSquad(); 
            return LoadAIShip(SquadSystem.RogueTwo.Ship.Class, PlayerTeam, SquadSystem.RogueTwo);
        }

        public Ship LoadHostileAI1Ship(Teams Team)
        {
            return LoadAIShip(HostileAI1ShipType, Team);
        }
        public Ship LoadHostileAI2Ship()
        {
            return LoadAIShip(HostileAI2ShipType, AITeam);
        }
        public Ship LoadHostileAI3Ship()
        {
            return LoadAIShip(HostileAI3ShipType, AITeam);
        }
        public Ship LoadHostileManta()
        {
            return LoadAIShip(HostileMantaShipType, AITeam);
        }
        public Ship LoadAIShip(ShipTypes shipType, Teams team, SO_Captain captain=null)
        {
            if (shipType == ShipTypes.Random)
            {
                Array values = Enum.GetValues(typeof(ShipTypes));
                System.Random random = new System.Random();
                shipType = (ShipTypes) values.GetValue(random.Next(1, values.Length));
            }

            Ship ship = Instantiate(shipTypeMap[shipType]);
            if (captain != null)
                ship.AssignCaptain(CaptainManager.Instance.GetCaptainByName(captain.Name));
            ship.SetShipMaterial(TeamMaterialSets[team].ShipMaterial);
            ship.SetBlockMaterial(TeamMaterialSets[team].BlockMaterial);
            ship.SetBlockSilhouettePrefab(TeamMaterialSets[team].BlockSilhouettePrefab);
            ship.SetShieldedBlockMaterial(TeamMaterialSets[team].ShieldedBlockMaterial);
            ship.SetAOEExplosionMaterial(TeamMaterialSets[team].AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(TeamMaterialSets[team].AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(TeamMaterialSets[team].SkimmerMaterial);

            AIPilot pilot = ship.GetComponent<AIPilot>();
            pilot.SkillLevel = ((float)AIDifficultyLevel-1) / 3; // this assumes that levels remain from 1-4
            pilot.AutoPilotEnabled = true;

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

        public Material GetTeamTransparentBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].TransparentBlockMaterial;
        }

        public GameObject GetTeamBlockSilhouettePrefab(Teams team)
        {
            return TeamMaterialSets[team].BlockSilhouettePrefab;
        }

        public Material GetTeamCrystalMaterial(Teams team)
        {
            return TeamMaterialSets[team].CrystalMaterial;
        }

        public Material GetTeamExplodingBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].ExplodingBlockMaterial;
        }

        public Material GetTeamSpikeMaterial(Teams team)
        {
            return TeamMaterialSets[team].SpikeMaterial;
        }
        
        public Material GetTeamShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].ShieldedBlockMaterial;
        }

        public Material GetTeamDangerousBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].DangerousBlockMaterial;
        }
        
        public Material GetTeamSuperShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].SuperShieldedBlockMaterial;
        }
    }
}