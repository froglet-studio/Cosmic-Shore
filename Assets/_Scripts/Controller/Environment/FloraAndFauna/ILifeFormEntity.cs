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

        // --- Elemental contract (mirrors the vessel contract) ---
        // Every lifeform declares an ELEMENT and a LEVEL (1..MaxLifeformLevel) as DATA, so one
        // base prefab serves every variant (4 elements x 5 levels = 20) instead of a prefab per
        // element. The element lives on the lifeform's crystal (the LifeFormCrystal invariant);
        // the level scales the creature via its species config. See Docs/ECOSYSTEM.md §3.

        /// <summary>The element this lifeform carries (its crystal's element; None if uncrystaled).</summary>
        Element Element { get; }

        /// <summary>This lifeform's level, 1..5. Raised in-world (e.g. an own-domain Crystal Joust).</summary>
        int Level { get; }
    }
}
