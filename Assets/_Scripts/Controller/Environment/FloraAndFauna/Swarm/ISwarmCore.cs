// What the swarm's Unity glue (SwarmFauna) needs from a SIMULATION CORE. Two cores implement it:
//   SwarmFieldCore - the research's `field` model (designed attractor fields, greedy slot assignment);
//   SwarmGridCore  - the research's `hgrid2` model (a coarse class-deficit grid + a fine per-class
//                    morphogen; nothing assigns a tadpole a place).
// A swarm picks one through SwarmFaunaConfigSO.Model, so a cell can host both side by side
// (Docs/SWARM_FAUNA.md §8). Like both cores, this file is free of UnityEngine: it compiles and runs
// headless in Tools/Build/swarm_core_harness.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>Which simulation drives a swarm. Explicit values: the enum is serialized.</summary>
    public enum SwarmModel
    {
        /// <summary>Designed attractor fields + boids (research `field`): crisp, every tadpole owns a slot.</summary>
        Field = 0,
        /// <summary>The grid morphogen (research `hgrid2`): every tadpole reads only fields at its own
        /// position - a coarse class deficit and a fine per-class morphogen. Looser, more organic.</summary>
        Grid = 1,
    }

    /// <summary>The per-swarm simulation the glue drives. Arrays are struct-of-arrays over the core's
    /// fixed capacity, indexed by member slot; a member is live while <see cref="Alive"/> is true.</summary>
    public interface ISwarmCore
    {
        int Cap { get; }
        Vector3[] Pos { get; }
        Vector3[] Vel { get; }
        Vector3[] Facing { get; }
        int[] Elem { get; }
        bool[] Alive { get; }
        float[] Startle { get; }
        /// <summary>Molt progress (0 = not molting). Always 0 in a core that never molts.</summary>
        float[] Molt { get; }
        int[] MoltTo { get; }
        /// <summary>Eaten volume banked per research element (0 Charge .. 3 Time) - what pays for eggs.</summary>
        float[] Stomach { get; }
        List<SwarmEvent> Events { get; }

        int Clock { get; }
        int PlanIx { get; }
        SwarmPlanData Plan { get; }
        int AliveCount { get; }
        Vector3 Anchor { get; }
        Vector3 BX { get; }
        Vector3 BY { get; }
        Vector3 BZ { get; }
        Vector3 SwimTarget { get; set; }

        void Seed(int planElement, int count, Vector3 anchor, Vector3 heading);
        void Step(ReadOnlySpan<SwarmPredator> preds);
        void Kill(int i);
        int EffectiveElement(int i);
        int[] Counts(bool eff);

        /// <summary>
        /// The prism member <paramref name="i"/> should wear as element <paramref name="element"/>:
        /// half-extents [long, wide, thin] in sim units and its Charge tier (0 plain, 1 danger, 2 shield).
        /// False when the core has no specific look for it (the glue then uses a typical prism).
        /// </summary>
        bool TryGetLook(int i, int element, out Vector3 half, out int tier);

        /// <summary>
        /// Starvation has come (the HOST decided: the swarm went unfed for StarvationSeconds) - which
        /// member does it shed? -1 when there is nobody to shed. A core never kills on its own clock.
        /// </summary>
        int StarvationVictim();
    }
}
