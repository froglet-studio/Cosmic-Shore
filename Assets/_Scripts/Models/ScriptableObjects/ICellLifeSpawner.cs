using CosmicShore.Soap;

namespace CosmicShore.Game
{
    public interface ICellLifeSpawner
    {
        void Start(Cell host, SO_CellType cellType, CellDataSO cellData, GameDataSO gameData);
        void Stop(Cell host);
    }
}