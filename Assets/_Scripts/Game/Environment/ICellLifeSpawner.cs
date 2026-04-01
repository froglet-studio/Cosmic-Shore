// ICellLifeSpawner.cs
using CosmicShore.Soap;

namespace CosmicShore.Game
{
    /// <summary>
    /// Spawners now operate on:
    /// - Cell (host)
    /// - CellConfigDataSO (design-time config)
    /// - CellRuntimeDataSO (runtime state + events + crystal lists)
    /// - GameDataSO (global runtime)
    /// </summary>
    public interface ICellLifeSpawner
    {
        void Start(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData);
        void Stop(Cell host);
    }
}