using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Common contract for all lifeform entities in the game (Flora, Fauna, and Managers).
    /// Provides a unified interface that external systems (spawners, scoring, turn monitors)
    /// can depend on without knowing the concrete lifeform type.
    /// </summary>
    public interface ILifeFormEntity : ITeamAssignable
    {
        Domains Domain { get; }
        GameObject GetGameObject();
        void Initialize(Cell cell);
    }
}
