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

        [Header("Material Settings")]
        [SerializeField] SO_MaterialSet BaseMaterialSet;
        [SerializeField] SO_ColorSet ColorSet;

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

            GreenTeamMaterialSet = GenerateDomainMaterialSet(ColorSet.JadeColors, "Green");
            RedTeamMaterialSet = GenerateDomainMaterialSet(ColorSet.RubyColors, "Red");
            GoldTeamMaterialSet = GenerateDomainMaterialSet(ColorSet.GoldColors, "Gold");
            BlueTeamMaterialSet = GenerateDomainMaterialSet(ColorSet.BlueColors, "Blue");

            TeamShipTypes.Add(Teams.Jade, GreenTeamShipTypes);
            TeamShipTypes.Add(Teams.Ruby, RedTeamShipTypes);
            TeamShipTypes.Add(Teams.Gold, GoldTeamShipTypes);

            if (PlayerPrefs.HasKey(SelectedShipPlayerPrefKey))
                PlayerShipType = (ShipTypes) PlayerPrefs.GetInt(SelectedShipPlayerPrefKey);

            TeamMaterialSets = new() {
                { Teams.Jade, GreenTeamMaterialSet },
                { Teams.Ruby,   RedTeamMaterialSet },
                { Teams.Blue,  BlueTeamMaterialSet },
                { Teams.Gold,  GoldTeamMaterialSet },
                { Teams.Unassigned,  BlueTeamMaterialSet },
            };

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

        private SO_MaterialSet GenerateDomainMaterialSet(DomainColorSet colorSet, string domainName)
        {
            SO_MaterialSet materialSet = ScriptableObject.CreateInstance<SO_MaterialSet>();
            materialSet.name = $"{domainName}TeamMaterialSet";

            // Copy all materials from the base set
            materialSet.ShipMaterial = new Material(BaseMaterialSet.ShipMaterial);
            materialSet.BlockMaterial = new Material(BaseMaterialSet.BlockMaterial);
            materialSet.TransparentBlockMaterial = new Material(BaseMaterialSet.TransparentBlockMaterial);
            materialSet.CrystalMaterial = new Material(BaseMaterialSet.CrystalMaterial);
            materialSet.ExplodingBlockMaterial = new Material(BaseMaterialSet.ExplodingBlockMaterial);
            materialSet.ShieldedBlockMaterial = new Material(BaseMaterialSet.ShieldedBlockMaterial);
            materialSet.TransparentShieldedBlockMaterial = new Material(BaseMaterialSet.TransparentShieldedBlockMaterial);
            materialSet.SuperShieldedBlockMaterial = new Material(BaseMaterialSet.SuperShieldedBlockMaterial);
            materialSet.TransparentSuperShieldedBlockMaterial = new Material(BaseMaterialSet.TransparentSuperShieldedBlockMaterial);
            materialSet.DangerousBlockMaterial = new Material(BaseMaterialSet.DangerousBlockMaterial);
            materialSet.TransparentDangerousBlockMaterial = new Material(BaseMaterialSet.TransparentDangerousBlockMaterial);
            materialSet.AOEExplosionMaterial = new Material(BaseMaterialSet.AOEExplosionMaterial);
            materialSet.AOEConicExplosionMaterial = new Material(BaseMaterialSet.AOEConicExplosionMaterial);
            materialSet.SpikeMaterial = new Material(BaseMaterialSet.SpikeMaterial);
            materialSet.SkimmerMaterial = new Material(BaseMaterialSet.SkimmerMaterial);

            // Copy prefab reference
            materialSet.BlockSilhouettePrefab = BaseMaterialSet.BlockSilhouettePrefab;

            // Set colors for materials that use domain-specific colors
            materialSet.BlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.BlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.TransparentBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.TransparentBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.ExplodingBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.ExplodingBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.DangerousBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.TransparentDangerousBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.ShieldedBlockMaterial.SetColor("_BrightColor", colorSet.ShieldedInsideBlockColor);
            materialSet.ShieldedBlockMaterial.SetColor("_DarkColor", colorSet.ShieldedOutsideBlockColor);

            materialSet.TransparentShieldedBlockMaterial.SetColor("_BrightColor", colorSet.ShieldedInsideBlockColor);
            materialSet.TransparentShieldedBlockMaterial.SetColor("_DarkColor", colorSet.ShieldedOutsideBlockColor);

            materialSet.SuperShieldedBlockMaterial.SetColor("_BrightColor", colorSet.SuperShieldedInsideBlockColor);
            materialSet.SuperShieldedBlockMaterial.SetColor("_DarkColor", colorSet.SuperShieldedOutsideBlockColor);

            materialSet.TransparentSuperShieldedBlockMaterial.SetColor("_BrightColor", colorSet.SuperShieldedInsideBlockColor);
            materialSet.TransparentSuperShieldedBlockMaterial.SetColor("_DarkColor", colorSet.SuperShieldedOutsideBlockColor);

            materialSet.ShipMaterial.SetColor("_Color1", colorSet.OutsideBlockColor);
            materialSet.ShipMaterial.SetColor("_Color2", colorSet.InsideBlockColor);

            // Set colors for other materials

            materialSet.CrystalMaterial.SetColor("_DullCrystalColor", colorSet.ShieldedOutsideBlockColor);

            return materialSet;
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

            Ship ship = Instantiate(shipTypeMap[shipType]);
            if (captain != null)
                ship.AssignCaptain(captain);
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

        public Material GetTeamTransparentShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].TransparentShieldedBlockMaterial;
        }

        public Material GetTeamDangerousBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].DangerousBlockMaterial;
        }

        public Material GetTeamTransparentDangerousBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].TransparentDangerousBlockMaterial;
        }
        
        public Material GetTeamSuperShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].SuperShieldedBlockMaterial;
        }

        public Material GetTeamTransparentSuperShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].TransparentSuperShieldedBlockMaterial;
        }

        public SO_Ship GetShipSOByShipType(ShipTypes shipClass)
        {
            return AllShips.ShipList.Where(x => x.Class == shipClass).FirstOrDefault();
        }
    }
}