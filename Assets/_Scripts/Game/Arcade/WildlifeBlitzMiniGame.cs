using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// MiniGame is deprecated, use new architecture and create WildLifeBlitz mini game from there.
    /// </summary>
    public class WildlifeBlitzMiniGame : MiniGame
    {
        // [SerializeField] Crystal Crystal;
        // [SerializeField] Vector3 CrystalStartPosition;
        // [SerializeField] Cell node;
        
        [SerializeField]
        CellDataSO cellData; 

        [SerializeField] SO_CellType Intensity1Cell;
        [SerializeField] SO_CellType Intensity2Cell;
        [SerializeField] SO_CellType Intensity3Cell;
        [SerializeField] SO_CellType Intensity4Cell;

        // public static new VesselClassType PlayerVesselType = VesselClassType.Rhino;

        protected override void Awake()
        {
            base.Awake();
            
            // TODO - CellData should contain cell information.
            cellData.CellType = IntensityLevel switch
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

            // VesselPrismController.ClearTrails();
            // Crystal.transform.position = CrystalStartPosition;

        }
    }
}
