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
        // Every lifeform declares an ELEMENT as DATA, so one base prefab serves all FOUR of a
        // species' variants instead of a prefab per element. The element lives on the
        // lifeform's crystal (the LifeFormCrystal invariant). See Docs/ECOSYSTEM.md §3.
        //
        // THERE IS NO LEVEL. A lifeform is its species and its element, and nothing else — a
        // creature you meet is exactly what its four-variant config says it is, with no hidden
        // per-individual history multiplying its body, its leaves or its heart
        // (Docs/ECOSYSTEM.md §40, which retires §33). Every value an element needs — including
        // the size of its heart — is authored ONCE in that element's own tuning block.

        /// <summary>The element this lifeform carries (its crystal's element; None if uncrystaled).</summary>
        Element Element { get; }

        /// <summary>
        /// The WORLD scale this lifeform's heart renders at — authored per element in the
        /// species' own variant tuning and sized to suit that lifeform's body, so a tadpole's
        /// heart is a tadpole's heart and a shark's is a shark's (Docs/ECOSYSTEM.md §40.2).
        /// Read at the one gate every heart passes through (<see cref="Crystal.SetEmbeddedIn"/>).
        /// A non-positive value means 'no authored size' and falls back to the set's default.
        /// </summary>
        float HeartWorldScale { get; }

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

        /// <summary>
        /// NOURISH this lifeform — an own-domain pilot feeding the life it shepherds (the
        /// Squirrel's Space-5 'Shepherd' joust). Returns true if the nourishment landed.
        ///
        /// <para>This replaces the level-up that used to be the ally joust's whole effect
        /// (Docs/ECOSYSTEM.md §40.4). It is deliberately a FOOD-WEB event rather than a size
        /// bump: a creature's starvation clock resets and its birth counter advances, a plant's
        /// growth quota advances toward its next seeding. So shepherding pays out as a
        /// POPULATION — more of the thing you protected — which is a change the food web makes,
        /// not one a designer scripts onto an individual.</para>
        /// </summary>
        bool Nourish();
    }
}
