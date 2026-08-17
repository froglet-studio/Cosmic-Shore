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
        //
        // The two are acquired differently, and deliberately (Docs/ECOSYSTEM.md §33): the
        // element is an IDENTITY a lifeform is born with (and passes to its offspring), while
        // the level is an ACHIEVEMENT it earns after birth and cannot pass on.

        /// <summary>The element this lifeform carries (its crystal's element; None if uncrystaled).</summary>
        Element Element { get; }

        /// <summary>
        /// This lifeform's level, 1..5. Every lifeform is BORN at 1 and earns the rest in-world:
        /// a plant per reproduction, a creature per FeedsPerLevel feeds, and either by an
        /// own-domain Crystal Joust (the Squirrel's Space-5 'Shepherd').
        /// </summary>
        int Level { get; }

        /// <summary>This lifeform's current travel speed (world units/s). 0 for rooted flora -
        /// which is what makes them trivially joustable (the jouster must be moving faster).</summary>
        float CurrentSpeed { get; }

        /// <summary>
        /// A vessel jousted this lifeform's embedded crystal (its heart) while moving faster than
        /// it - the creature withers and dies through its normal death path (crystal drop, mass
        /// conserved, continuity honored). Returns true only if it actually died to this joust.
        ///
        /// A joust never detonates its target (Docs/ECOSYSTEM.md §26): the heart is freed at the
        /// strike so the joust chain can award it to the pilot who took it, the soft tissue
        /// withers FROM THE HEART OUTWARD around the hole it left, and the body prisms are left
        /// standing as a skeleton. That is the mirror of the outside-in starvation wither, where
        /// nobody takes the heart and it becomes an ordinary pickup once the wither exposes it.
        /// </summary>
        bool Jousted(string killerName);

        /// <summary>Raise this lifeform's level by one (capped at 5). Returns false at the cap.
        /// Call only from an EARNING event - reproduction, feeding, or an ally's joust.</summary>
        bool LevelUp();
    }
}
