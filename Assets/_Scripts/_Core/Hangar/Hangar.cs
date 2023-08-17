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
        [SerializeField] SO_Pilot PlayerPilot;  // Serialized for inspection in hierarchy
        [SerializeField] ShipTypes PlayerShipType = ShipTypes.Random;
        [SerializeField] ShipTypes FriendlyAIShipType = ShipTypes.Manta;
        [SerializeField] ShipTypes HostileAI1ShipType = ShipTypes.MantaAI;
        [SerializeField] ShipTypes HostileAI2ShipType = ShipTypes.Random;

        // TODO: P1 - clean this up
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
        [SerializeField] Material GreenTeamSkimmerMaterial;
        [SerializeField] Material RedTeamSkimmerMaterial;
        [SerializeField] Material BlueTeamSkimmerMaterial;
        [SerializeField] Material YellowTeamSkimmerMaterial;

        Dictionary<Teams, Material> TeamsMaterials;
        Dictionary<Teams, Material> TeamBlockMaterials;
        Dictionary<Teams, Material> TeamShieldedBlockMaterials;
        Dictionary<Teams, Material> TeamAOEExplosionMaterials;
        Dictionary<Teams, Material> TeamAOEConicExplosionMaterials;
        Dictionary<Teams, Material> TeamSkimmerMaterials;

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

            TeamsMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamMaterial },
                { Teams.Red,   RedTeamMaterial },
                { Teams.Blue,  BlueTeamMaterial },
                { Teams.Yellow,  YellowTeamMaterial },
                { Teams.Unassigned,  BlueTeamMaterial },
            };
            TeamBlockMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamBlockMaterial },
                { Teams.Red,   RedTeamBlockMaterial },
                { Teams.Blue,  BlueTeamBlockMaterial },
                { Teams.Yellow,  YellowTeamBlockMaterial },
                { Teams.Unassigned,  BlueTeamBlockMaterial },
            };
            TeamShieldedBlockMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamShieldedBlockMaterial },
                { Teams.Red,   RedTeamShieldedBlockMaterial},
                { Teams.Blue,  BlueTeamShieldedBlockMaterial},
                { Teams.Yellow, YellowTeamShieldedBlockMaterial},
                { Teams.Unassigned,  BlueTeamShieldedBlockMaterial},
            };
            TeamAOEExplosionMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamAOEExplosionMaterial },
                { Teams.Red,   RedTeamAOEExplosionMaterial },
                { Teams.Blue,  BlueTeamAOEExplosionMaterial },
                { Teams.Yellow,  YellowTeamAOEExplosionMaterial },
                { Teams.Unassigned,  BlueTeamAOEExplosionMaterial },
            };
            TeamAOEConicExplosionMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamAOEConicExplosionMaterial },
                { Teams.Red,   RedTeamAOEConicExplosionMaterial },
                { Teams.Blue,  BlueTeamAOEConicExplosionMaterial },
                { Teams.Yellow,  YellowTeamAOEConicExplosionMaterial },
                { Teams.Unassigned,  BlueTeamAOEConicExplosionMaterial },
            };
            TeamSkimmerMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamSkimmerMaterial },
                { Teams.Red,   RedTeamSkimmerMaterial },
                { Teams.Blue,  BlueTeamSkimmerMaterial },
                { Teams.Yellow,  YellowTeamSkimmerMaterial },
                { Teams.Unassigned,  BlueTeamSkimmerMaterial },
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

            ship.SetShipMaterial(TeamsMaterials[team]);
            ship.SetBlockMaterial(TeamBlockMaterials[team]);
            ship.SetShieldedBlockMaterial(TeamShieldedBlockMaterials[team]);
            ship.SetAOEExplosionMaterial(TeamAOEExplosionMaterials[team]);
            ship.SetAOEConicExplosionMaterial(TeamAOEConicExplosionMaterials[team]);
            ship.SetSkimmerMaterial(TeamSkimmerMaterials[team]);

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
            return TeamBlockMaterials[team];
        }
        
        public Material GetTeamShieldedBlockMaterial(Teams team)
        {
            return TeamShieldedBlockMaterials[team];
        }
    }
}