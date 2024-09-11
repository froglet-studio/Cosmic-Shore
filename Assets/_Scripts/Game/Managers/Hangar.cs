using CosmicShore.App.Systems.Squads;
using CosmicShore.Game.AI;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models;
using CosmicShore.Utility.Singleton;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Core
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        Teams AITeam;

        [Header("Ship Type Settings")]
        [SerializeField] SO_ShipList AllShips;
        [SerializeField] Teams PlayerTeam = Teams.Jade;
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
        int AISkillLevel = 1;

        /// <summary>
        /// Intensity Level is defined by Arcade Games, Difficulty Level is defined by Missions
        /// </summary>
        /// <param name="level">Range from 1-4</param>
        public void SetAiIntensityLevel(int level)
        {
            AISkillLevel = level;
        }

        /// <summary>
        /// Intensity Level is defined by Arcade Games, Difficulty Level is defined by Missions
        /// This method maps the 9 scale difficulty to the 4 scale skill level
        /// </summary>
        /// <param name="level">Range from 1-9</param>
        public void SetAiDifficultyLevel(int level)
        {
            AISkillLevel = Mathf.FloorToInt((level/9f)*4);
        }

        public Ship SelectedShip { get; private set; }
        public override void Awake()
        {
            base.Awake();

            TeamShipTypes.Add(Teams.Jade, GreenTeamShipTypes);
            TeamShipTypes.Add(Teams.Ruby, RedTeamShipTypes);
            TeamShipTypes.Add(Teams.Gold, GoldTeamShipTypes);

            if (PlayerPrefs.HasKey(SelectedShipPlayerPrefKey))
                PlayerShipType = (ShipTypes) PlayerPrefs.GetInt(SelectedShipPlayerPrefKey);

            
            if (PlayerTeam == Teams.None)
            {
                Debug.LogError("Player Team is set to None. Defaulting to Green Team");
                PlayerTeam = Teams.Jade;
            }

            foreach (var ship in ShipPrefabs)
            {
                ships.Add(ship.name, ship);
                shipTypeMap.Add(ship.ShipType, ship);
            }

            AITeam = PlayerTeam == Teams.Jade ? Teams.Ruby : Teams.Jade;
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

            var materialSet = ThemeManager.Instance.TeamMaterialSets[team];

            ship.SetShipMaterial(materialSet.ShipMaterial);
            ship.SetBlockMaterial(materialSet.BlockMaterial);
            ship.SetBlockSilhouettePrefab(materialSet.BlockSilhouettePrefab);
            ship.SetShieldedBlockMaterial(materialSet.ShieldedBlockMaterial);
            ship.SetAOEExplosionMaterial(materialSet.AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(materialSet.AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(materialSet.SkimmerMaterial);

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

        public SO_Captain HostileAI1Captain { get; private set; }
        public SO_Captain HostileAI2Captain { get; private set; }
        public SO_Captain HostileAI3Captain { get; private set; }
        public Ship LoadHostileAI1Ship(Teams Team)
        {
            var ship = LoadAIShip(HostileAI1ShipType, Team);
            HostileAI1Captain = ship.Captain;
            return ship;
        }
        public Ship LoadHostileAI2Ship()
        {
            var ship = LoadAIShip(HostileAI2ShipType, AITeam);
            HostileAI2Captain = ship.Captain;
            return ship;
        }
        public Ship LoadHostileAI3Ship()
        {
            var ship = LoadAIShip(HostileAI3ShipType, AITeam);
            HostileAI3Captain = ship.Captain;
            return ship;
        }
        public Ship LoadHostileManta()
        {
            return LoadAIShip(HostileMantaShipType, AITeam);
        }
        Ship LoadAIShip(ShipTypes shipType, Teams team, SO_Captain captain=null)
        {
            if (shipType == ShipTypes.Random)
            {
                Array values = Enum.GetValues(typeof(ShipTypes));
                System.Random random = new System.Random();
                shipType = (ShipTypes) values.GetValue(random.Next(1, values.Length));
            }

            if (captain == null)
            {
                var captains = CaptainManager.Instance.GetAllSOCaptains().Where(x => x.Ship.Class == shipType).ToList();
                captain = captains[UnityEngine.Random.Range(0, 3)];
            }

            var materialSet = ThemeManager.Instance.TeamMaterialSets[team];

            Ship ship = Instantiate(shipTypeMap[shipType]);
            if (captain != null)
                ship.AssignCaptain(captain);
            ship.SetShipMaterial(materialSet.ShipMaterial);
            ship.SetBlockMaterial(materialSet.BlockMaterial);
            ship.SetBlockSilhouettePrefab(materialSet.BlockSilhouettePrefab);
            ship.SetShieldedBlockMaterial(materialSet.ShieldedBlockMaterial);
            ship.SetAOEExplosionMaterial(materialSet.AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(materialSet.AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(materialSet.SkimmerMaterial);

            AIPilot pilot = ship.GetComponent<AIPilot>();
            pilot.SkillLevel = ((float)AISkillLevel-1) / 3; // this assumes that levels remain from 1-4
            pilot.AutoPilotEnabled = true;

            return ship;
        }

        public SO_Ship GetShipSOByShipType(ShipTypes shipClass)
        {
            return AllShips.ShipList.Where(x => x.Class == shipClass).FirstOrDefault();
        }
    }
}