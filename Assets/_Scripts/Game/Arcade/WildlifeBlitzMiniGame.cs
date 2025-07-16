using CosmicShore.Environment.FlowField;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class WildlifeBlitzMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] Cell node;

        [SerializeField] SO_CellType Intensity1Cell;
        [SerializeField] SO_CellType Intensity2Cell;
        [SerializeField] SO_CellType Intensity3Cell;
        [SerializeField] SO_CellType Intensity4Cell;

        public static new ShipClassType PlayerShipType = ShipClassType.Rhino;

        protected override void Awake()
        {

            base.Awake();
            node.CellType = IntensityLevel switch
            {
                1 => Intensity1Cell,
                2 => Intensity2Cell,
                3 => Intensity3Cell,
                4 => Intensity4Cell,
                _ => Intensity1Cell
            };
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();

            TrailSpawner.NukeTheTrails();
            Crystal.transform.position = CrystalStartPosition;

        }
    }
}
