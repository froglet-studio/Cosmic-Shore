using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        // TODO: let player objects pass in a ship type to the hangar and load it
        // TODO: player object or the GameManager should inform the Hanger what the player's team is
        [SerializeField] Teams PlayerTeam = Teams.Green;
        [SerializeField] ShipTypes PlayerShipType = ShipTypes.Manta;
        [SerializeField] ShipTypes PlayerTeammateShipType = ShipTypes.Manta;
        [SerializeField] ShipTypes AI1ShipType = ShipTypes.MantaAI;
        [SerializeField] ShipTypes AI2ShipType = ShipTypes.Random;

        [SerializeField] string PlayerShipName = "GreenManta";
        [SerializeField] string AIShipName = "RedMantaAI";
        [SerializeField] Material GreenTeamMaterial;
        [SerializeField] Material RedTeamMaterial;

        Dictionary<Teams, Material> TeamsMaterials;

        Dictionary<string, Ship> ships = new Dictionary<string, Ship>();
        Dictionary<ShipTypes, Ship> shipTypeMap = new Dictionary<ShipTypes, Ship>();

        [SerializeField] int SelectedBayIndex = 0;
        [SerializeField] public List<Ship> ShipPrefabs;

        Teams AITeam;

        public Ship selectedShip { get; private set; }
        public int BayIndex { get => SelectedBayIndex; }

        private void Start()
        {
            TeamsMaterials = new Dictionary<Teams, Material>() {
                { Teams.Green, GreenTeamMaterial },
                { Teams.Red,   RedTeamMaterial },
            };
            if (PlayerTeam == Teams.None) {
                Debug.LogError("Player Team is set to None. Defaulting to Green team");
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
            return Instantiate(shipTypeMap[PlayerShipType]);
        }
        public Ship LoadPlayerTeammateShip()
        {
            return Instantiate(shipTypeMap[PlayerTeammateShipType]);
        }
        public Ship LoadAI1Ship()
        {
            return LoadAIShip(AI1ShipType);
        }
        public Ship LoadAI2Ship()
        {
            return LoadAIShip(AI2ShipType);
        }
        public Ship LoadAIShip(ShipTypes shipType)
        {
            if (shipType == ShipTypes.Random)
            {
                System.Array values = System.Enum.GetValues(typeof(ShipTypes));
                System.Random random = new System.Random();
                shipType = (ShipTypes)values.GetValue(random.Next(1, values.Length));
            }
            Ship ship = Instantiate(shipTypeMap[shipType]);
            ship.SetShipMaterial(TeamsMaterials[AITeam]);

            return ship;
        }
    }
}