using StarWriter.Core.IO;
using StarWriter.Utility.Singleton;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        Teams AITeam;

        [SerializeField] Teams PlayerTeam = Teams.Green;
        [SerializeField] ShipTypes PlayerShipType = ShipTypes.Random;
        [SerializeField] ShipTypes FriendlyAIShipType = ShipTypes.Manta;
        [SerializeField] ShipTypes HostileAI1ShipType = ShipTypes.MantaAI;
        [SerializeField] ShipTypes HostileAI2ShipType = ShipTypes.Random;

        [SerializeField] Material GreenTeamMaterial;
        [SerializeField] Material RedTeamMaterial;
        [SerializeField] Material BlueTeamMaterial;
        [SerializeField] Material YellowTeamMaterial;
        [SerializeField] Material GreenTeamBlockMaterial;
        [SerializeField] Material RedTeamBlockMaterial;
        [SerializeField] Material BlueTeamBlockMaterial;
        [SerializeField] Material YellowTeamBlockMaterial;
        [SerializeField] Material GreenTeamShieldedBlockMaterial;
        [SerializeField] Material RedTeamShieldedBlockMaterial;
        [SerializeField] Material BlueTeamShieldedBlockMaterial;
        [SerializeField] Material YellowTeamShieldedBlockMaterial;
        [SerializeField] Material GreenTeamAOEExplosionMaterial;
        [SerializeField] Material RedTeamAOEExplosionMaterial;
        [SerializeField] Material BlueTeamAOEExplosionMaterial;
        [SerializeField] Material YellowTeamAOEExplosionMaterial;
        [SerializeField] Material GreenTeamAOEConicExplosionMaterial;
        [SerializeField] Material RedTeamAOEConicExplosionMaterial;
        [SerializeField] Material BlueTeamAOEConicExplosionMaterial;
        [SerializeField] Material YellowTeamAOEConicExplosionMaterial;

        Dictionary<Teams, Material> TeamsMaterials;
        Dictionary<Teams, Material> TeamBlockMaterials;
        Dictionary<Teams, Material> TeamShieldedBlockMaterials;
        Dictionary<Teams, Material> TeamAOEExplosionMaterials;
        Dictionary<Teams, Material> TeamAOEConicExplosionMaterials;

        Dictionary<string, Ship> ships = new Dictionary<string, Ship>();
        Dictionary<ShipTypes, Ship> shipTypeMap = new Dictionary<ShipTypes, Ship>();

        [SerializeField] int SelectedBayIndex = 0;
        [SerializeField] public List<Ship> ShipPrefabs;

        readonly string SelectedShipPlayerPrefKey = "SelectedShip";

        public void SetPlayerShip(int shipType)
        {
            Debug.Log($"Hangar.SetPlayerShip: {(ShipTypes)shipType}");
            PlayerShipType = (ShipTypes)shipType;
            PlayerPrefs.SetInt(SelectedShipPlayerPrefKey, shipType);
        }

        public ShipTypes GetPlayerShip()
        {
            return PlayerShipType;
        }

        [Range(0,10)]
        [SerializeField]
        int AIDifficultyLevel = 5;

        public void SetAiDifficultyLevel(int level)
        {
            AIDifficultyLevel = level;
        }

        public Ship SelectedShip { get; private set; }
        public int BayIndex { get => SelectedBayIndex; }

        public override void Awake()
        {
            base.Awake();

            if (PlayerPrefs.HasKey(SelectedShipPlayerPrefKey))
                PlayerShipType = (ShipTypes) PlayerPrefs.GetInt(SelectedShipPlayerPrefKey);

            TeamsMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamMaterial },
                { Teams.Red,   RedTeamMaterial },
                { Teams.Blue,  BlueTeamMaterial },
                { Teams.Yellow,  YellowTeamMaterial },
            };
            TeamBlockMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamBlockMaterial },
                { Teams.Red,   RedTeamBlockMaterial },
                { Teams.Blue,  BlueTeamBlockMaterial },
                { Teams.Yellow,  YellowTeamBlockMaterial },
            };
            TeamShieldedBlockMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamShieldedBlockMaterial },
                { Teams.Red,   RedTeamShieldedBlockMaterial},
                { Teams.Blue,  BlueTeamShieldedBlockMaterial},
                { Teams.Yellow, YellowTeamShieldedBlockMaterial},
            };
            TeamAOEExplosionMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamAOEExplosionMaterial },
                { Teams.Red,   RedTeamAOEExplosionMaterial },
                { Teams.Blue,  BlueTeamAOEExplosionMaterial },
                { Teams.Yellow,  YellowTeamAOEExplosionMaterial },
            };
            TeamAOEConicExplosionMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamAOEConicExplosionMaterial },
                { Teams.Red,   RedTeamAOEConicExplosionMaterial },
                { Teams.Blue,  BlueTeamAOEConicExplosionMaterial },
                { Teams.Yellow,  YellowTeamAOEConicExplosionMaterial },
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

            Ship ship = Instantiate(shipTypeMap[PlayerShipType]);
            ship.SetShipMaterial(TeamsMaterials[PlayerTeam]);
            ship.SetBlockMaterial(TeamBlockMaterials[PlayerTeam]);
            ship.SetShieldedBlockMaterial(TeamShieldedBlockMaterials[PlayerTeam]);
            ship.SetAOEExplosionMaterial(TeamAOEExplosionMaterials[PlayerTeam]);
            ship.SetAOEConicExplosionMaterial(TeamAOEConicExplosionMaterials[PlayerTeam]);

            SelectedShip = ship;

            return ship;
        }

        public Ship LoadPlayerShip(ShipTypes shipType, Teams team)
        {
            Ship ship = Instantiate(shipTypeMap[shipType]);
            ship.SetShipMaterial(TeamsMaterials[team]);
            ship.SetBlockMaterial(TeamBlockMaterials[team]);
            ship.SetShieldedBlockMaterial(TeamShieldedBlockMaterials[team]);
            ship.SetAOEExplosionMaterial(TeamAOEExplosionMaterials[team]);
            ship.SetAOEConicExplosionMaterial(TeamAOEConicExplosionMaterials[team]);

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
                System.Array values = System.Enum.GetValues(typeof(ShipTypes));
                System.Random random = new System.Random();
                shipType = (ShipTypes)values.GetValue(random.Next(1, values.Length));
            }

            Ship ship = Instantiate(shipTypeMap[shipType]);
            ship.SetShipMaterial(TeamsMaterials[team]);
            ship.SetBlockMaterial(TeamBlockMaterials[team]);
            ship.SetShieldedBlockMaterial(TeamShieldedBlockMaterials[team]);
            ship.SetAOEExplosionMaterial(TeamAOEExplosionMaterials[team]);
            ship.SetAOEConicExplosionMaterial(TeamAOEConicExplosionMaterials[team]);

            AIPilot pilot = ship.GetComponent<AIPilot>();
            pilot.DifficultyLevel = AIDifficultyLevel;
            pilot.autoPilotEnabled = true;

            return ship;
        }

        public Ship LoadSecondPlayerShip(ShipTypes PlayerShipType)
        {
            return Instantiate(shipTypeMap[PlayerShipType]);
        }

        public Material GetTeamBlockMaterial(Teams team)
        {
            return TeamBlockMaterials[team];
        }
        
        public Material GetTeamShieldedBlockMaterial(Teams team)
        {
            return TeamShieldedBlockMaterials[team];
        }
    }
}