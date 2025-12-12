using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class WildlifeBlitzMiniGame : SinglePlayerMiniGameControllerBase
    {
        [SerializeField]
        CellDataSO cellData; 

        [SerializeField] SO_CellType Intensity1Cell;
        [SerializeField] SO_CellType Intensity2Cell;
        [SerializeField] SO_CellType Intensity3Cell;
        [SerializeField] SO_CellType Intensity4Cell;

        protected override void Start()
        {
            cellData.CellType = gameData.SelectedIntensity.Value switch
            {
                1 => Intensity1Cell,
                2 => Intensity2Cell,
                3 => Intensity3Cell,
                4 => Intensity4Cell,
                _ => Intensity1Cell
            };
            
            base.Start();
        }
    }
}
