using CosmicShore.App.Systems.Squads;
using CosmicShore.Game;
using CosmicShore.Game.AI;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Core
{
    public class Hangar : SingletonPersistent<Hangar>
    {
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

        Dictionary<string, IShip> ships = new();
        Dictionary<ShipTypes, IShip> shipTypeMap = new();

        // HashSet has only one same value in one set
        [HideInInspector] public HashSet<Transform> SlowedShipTransforms = new(); // TODO: move to node

        [SerializeField] public List<GameObject> ShipPrefabs;

        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        public IShip SelectedShip { get; private set; }

        readonly string SelectedShipPlayerPrefKey = "SelectedShip";
        Teams AITeam;

        public override void Awake()
        {
            base.Awake();

            TeamShipTypes.Add(Teams.Jade, GreenTeamShipTypes);
            TeamShipTypes.Add(Teams.Ruby, RedTeamShipTypes);
            TeamShipTypes.Add(Teams.Gold, GoldTeamShipTypes);

            if (PlayerPrefs.HasKey(SelectedShipPlayerPrefKey))
                PlayerShipType = (ShipTypes)PlayerPrefs.GetInt(SelectedShipPlayerPrefKey);


            if (PlayerTeam == Teams.None)
            {
                Debug.LogError("Player Team is set to None. Defaulting to Green Team");
                PlayerTeam = Teams.Jade;
            }

            foreach (var go in ShipPrefabs.Where(ship => ship != null))
            {
                if (!go.TryGetComponent(out IShip ship))
                    continue;

                ships.Add(ship.ShipStatus.Name, ship);
                shipTypeMap.Add(ship.ShipStatus.ShipType, ship);
            }

            AITeam = PlayerTeam == Teams.Jade ? Teams.Ruby : Teams.Jade;
        }

        public void SetPlayerShip(int shipType)
        {
            Debug.Log($"Hangar.SetPlayerShip: {(ShipTypes)shipType}");
            PlayerShipType = (ShipTypes)shipType;
            PlayerPrefs.SetInt(SelectedShipPlayerPrefKey, shipType);
        }

        public void SetTeamShipType(Teams team, ShipTypes shipType, int slot = 0)
        {
            Debug.Log($"Hangar.SetTeamShipType: shipType:{shipType}, team:{team}, slot:{slot}");

            TeamShipTypes[team][slot] = shipType;
        }

        public void SetPlayerCaptain(Captain captain)
        {
            PlayerCaptain = captain;
        }

        public ShipTypes GetPlayerShipType()
        {
            return PlayerShipType;
        }

        [Range(1, 4)]
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
            AISkillLevel = Mathf.FloorToInt(level / 9f * 4);
        }


        public IShip LoadPlayerShip(bool useSquad = false)
        {
            if (useSquad)
            {
                return LoadPlayerShip(SquadSystem.SquadLeader.Ship.Class, PlayerTeam);
            }

            if (PlayerShipType != ShipTypes.Random) return LoadPlayerShip(PlayerShipType, PlayerTeam);

            var values = Enum.GetValues(typeof(ShipTypes));
            var random = new System.Random();
            PlayerShipType = (ShipTypes)values.GetValue(random.Next(values.Length));

            return LoadPlayerShip(PlayerShipType, PlayerTeam);
        }

        public IShip LoadPlayerShip(ShipTypes shipType, Teams team)
        {
            Instantiate(shipTypeMap[shipType].Transform).TryGetComponent(out IShip ship);
            return LoadPlayerShip(ship, team, true);  // for single player, isOwner is default to true
        }

        /// <summary>
        /// This method is used when ship is loaded for multiplayer gameplay
        /// </summary>
        public IShip LoadPlayerShip(IShip ship, Teams team, bool isOwner)
        {
            if (PlayerCaptain != null)
                ship.SetResourceLevels(PlayerCaptain.ResourceLevels);

            var materialSet = _themeManagerData.TeamMaterialSets[team];

            ship.SetShipMaterial(materialSet.ShipMaterial);
            ship.SetBlockSilhouettePrefab(materialSet.BlockSilhouettePrefab);
            ship.SetAOEExplosionMaterial(materialSet.AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(materialSet.AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(materialSet.SkimmerMaterial);

            if (isOwner)
                SelectedShip = ship;
            return ship;
        }

        public IShip LoadShip(ShipTypes shipType, Teams team)
        {
            return LoadAIShip(shipType, team);
        }

        public IShip LoadFriendlyAIShip()
        {
            return LoadAIShip(FriendlyAIShipType, PlayerTeam);
        }
        public IShip LoadSquadMateOne()
        {
            SquadSystem.LoadSquad();
            return LoadAIShip(SquadSystem.RogueOne.Ship.Class, PlayerTeam, SquadSystem.RogueOne);
        }
        public IShip LoadSquadMateTwo()
        {
            SquadSystem.LoadSquad();
            return LoadAIShip(SquadSystem.RogueTwo.Ship.Class, PlayerTeam, SquadSystem.RogueTwo);
        }

        public SO_Captain HostileAI1Captain { get; private set; }
        public SO_Captain HostileAI2Captain { get; private set; }
        public SO_Captain HostileAI3Captain { get; private set; }

        public IShip LoadHostileAI1Ship(Teams Team)
        {
            var ship = LoadAIShip(HostileAI1ShipType, Team);
            HostileAI1Captain = ship.ShipStatus.Captain;
            return ship;
        }
        public IShip LoadHostileAI2Ship()
        {
            var ship = LoadAIShip(HostileAI2ShipType, AITeam);
            HostileAI2Captain = ship.ShipStatus.Captain;
            return ship;
        }
        public IShip LoadHostileAI3Ship()
        {
            var ship = LoadAIShip(HostileAI3ShipType, AITeam);
            HostileAI3Captain = ship.ShipStatus.Captain;
            return ship;
        }
        public IShip LoadHostileManta()
        {
            return LoadAIShip(HostileMantaShipType, AITeam);
        }
        IShip LoadAIShip(ShipTypes shipType, Teams team, SO_Captain captain = null)
        {
            if (shipType == ShipTypes.Random)
            {
                var values = Enum.GetValues(typeof(ShipTypes));
                var random = new System.Random();
                shipType = (ShipTypes)values.GetValue(random.Next(1, values.Length));
            }

            if (captain == null)
            {
                var captains = CaptainManager.Instance.GetAllSOCaptains().Where(x => x.Ship.Class == shipType).ToList();
                captain = captains[UnityEngine.Random.Range(0, 3)];
            }

            var materialSet = _themeManagerData.TeamMaterialSets[team];
            
            Instantiate(shipTypeMap[shipType].Transform).TryGetComponent(out IShip ship);

            if (captain != null)
                ship.AssignCaptain(captain);
            ship.SetResourceLevels(captain.InitialResourceLevels);
            ship.SetShipMaterial(materialSet.ShipMaterial);
            ship.SetBlockSilhouettePrefab(materialSet.BlockSilhouettePrefab);
            ship.SetAOEExplosionMaterial(materialSet.AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(materialSet.AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(materialSet.SkimmerMaterial);

            AIPilot pilot = ship.ShipStatus.AIPilot;
            pilot.SkillLevel = ((float)AISkillLevel - 1) / 3; // this assumes that levels remain from 1-4
            pilot.AssignShip(ship);
            //pilot.Initialize(true);


            return ship;
        }

        public SO_Ship GetShipSOByShipType(ShipTypes shipClass)
        {
            return AllShips.ShipList.FirstOrDefault(x => x.Class == shipClass);
        }
    }
}