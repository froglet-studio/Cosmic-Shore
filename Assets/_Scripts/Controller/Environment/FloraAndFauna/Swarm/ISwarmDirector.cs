using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A MODE that steers a swarm (Tandava, Assets/_Scripts/Controller/Arcade/TANDAVA.md). Registered per swarm config
    /// with <see cref="SwarmFauna.SetDirector"/>; every swarm hatched from that config - and any already alive - asks it
    /// where to hatch, where to swim, and tells it each published tick and each committed form. A swarm with no
    /// director (every shipped cell) grazes its band exactly as before.
    ///
    /// The director decides PLACE and SHAPE, never life: it cannot kill, spawn or feed a member. The swarm still grows
    /// only from flora it eats and dies only to pilots, predators and starvation.
    /// </summary>
    public interface ISwarmDirector
    {
        /// <summary>Where the swarm hatches and which way it first faces (world). False = where the spawner put it.</summary>
        bool TryGetSeed(SwarmFauna swarm, out Vector3 position, out Vector3 heading);

        /// <summary>A SCRIPTED swarm: the form it hatches as (an index into its scripted plans - Tandava draws one of three
        /// variants per match). False, or an index outside the list = its first form. Asked once, before the first member
        /// exists.</summary>
        bool TryGetSeedForm(SwarmFauna swarm, out int form);

        /// <summary>The swarm's current swim goal (world). False = its own grazing goal.</summary>
        bool TryGetGoal(SwarmFauna swarm, out Vector3 goal);

        /// <summary>Once per published tick, on the main thread, after the swarm has fed and shed.</summary>
        void OnTickPublished(SwarmFauna swarm);

        /// <summary>A form change committed in the core (a scripted form list's indices; the elemental plans otherwise).</summary>
        void OnFormCommitted(SwarmFauna swarm, int fromForm, int toForm);
    }

    /// <summary>
    /// A director that can hatch a swarm as members MOVED from another (Tandava's severing, TANDAVA.md §3.11): the piece a
    /// cut parted from the body crawls off as its own swarm. Asked once, in <see cref="SwarmFauna"/>'s seed, before the
    /// first member exists (a sort core only). True with grafts = the swarm hatches as exactly those members, grown, where
    /// they were, with that stomach; false = it hatches as a fresh brood, as every other swarm does.
    /// Moving is not spawning: <see cref="ISwarmDirector"/>'s rule holds - the members were alive a moment ago in the
    /// other swarm, which released them without a death.
    /// </summary>
    public interface ISwarmSeedGraft
    {
        bool TryGetSeedGraft(SwarmFauna swarm, List<SwarmGraft> grafts, float[] stomach);
    }
}
