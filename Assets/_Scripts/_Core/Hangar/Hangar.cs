using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using UnityEngine.Serialization;
using StarWriter.Core.Input;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        // TODO: let player objects pass in a ship type to the hangar and load it
        // TODO: player object or the GameManager should inform the Hanger what the player's team is
        [SerializeField] Teams PlayerTeam = Teams.Green;
        Teams AITeam;
        [SerializeField] ShipTypes PlayerShipType = ShipTypes.Manta;
        [FormerlySerializedAs("PlayerTeammateShipType")]
        [SerializeField] ShipTypes FriendlyAIShipType = ShipTypes.Manta;
        [FormerlySerializedAs("AI1ShipType")]
        [SerializeField] ShipTypes HostileAI1ShipType = ShipTypes.MantaAI;
        [FormerlySerializedAs("AI2ShipType")]
        [SerializeField] ShipTypes HostileAI2ShipType = ShipTypes.Random;

        [SerializeField] Material GreenTeamMaterial;
        [SerializeField] Material RedTeamMaterial;
        [SerializeField] Material GreenTeamBlockMaterial;
        [SerializeField] Material RedTeamBlockMaterial;
        [SerializeField] Material GreenTeamAOEExplosionMaterial;
        [SerializeField] Material RedTeamAOEExplosionMaterial;

        Dictionary<Teams, Material> TeamsMaterials;
        Dictionary<Teams, Material> TeamBlockMaterials;
        Dictionary<Teams, Material> TeamAOEExplosionMaterials;

        Dictionary<string, Ship> ships = new Dictionary<string, Ship>();
        Dictionary<ShipTypes, Ship> shipTypeMap = new Dictionary<ShipTypes, Ship>();

        [SerializeField] int SelectedBayIndex = 0;
        [SerializeField] public List<Ship> ShipPrefabs;

        public void SetPlayerShip(int shipType)
        {
            PlayerShipType = (ShipTypes)shipType;
        }

        [Range(0,10)]
        [SerializeField]
        int AIDifficultyLevel = 5;

        public void SetAiDifficultyLevel(int level)
        {
            AIDifficultyLevel = level;
        }

        public Ship selectedShip { get; private set; }
        public int BayIndex { get => SelectedBayIndex; }

        public override void Awake()
        {
            base.Awake();

            TeamsMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamMaterial },
                { Teams.Red,   RedTeamMaterial },
            };
            TeamBlockMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamBlockMaterial },
                { Teams.Red,   RedTeamBlockMaterial },
            };
            TeamAOEExplosionMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamAOEExplosionMaterial },
                { Teams.Red,   RedTeamAOEExplosionMaterial },
            };
            if (PlayerTeam == Teams.None) {
                Debug.LogError("Player Team is set to None. Defaulting to Green team");
                PlayerTeam = Teams.Green;
            }

            foreach (var ship in ShipPrefabs)
            {
                ships.Add(ship.name, ship);
                shipTypeMap.Add(ship.ShipType, ship);
                Debug.Log($"Adding ship to shiptypemap - {ship.ShipType}, {ship}");
            }



            AITeam = PlayerTeam == Teams.Green ? Teams.Red : Teams.Green;
        }
        public Ship LoadPlayerShip()
        {
            if (PlayerShipType == ShipTypes.Random)
                PlayerShipType = ShipTypes.Manta;

            Ship ship = Instantiate(shipTypeMap[PlayerShipType]);
            ship.SetShipMaterial(TeamsMaterials[PlayerTeam]);
            ship.SetBlockMaterial(TeamBlockMaterials[PlayerTeam]);
            ship.SetAOEExplosionMaterial(TeamAOEExplosionMaterials[PlayerTeam]);

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
            ship.SetAOEExplosionMaterial(TeamAOEExplosionMaterials[team]);

            AIPilot pilot = ship.GetComponent<AIPilot>();
            pilot.DifficultyLevel = AIDifficultyLevel;

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
    }
}