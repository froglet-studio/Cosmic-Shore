using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Decides when to fire each of the vessel's button abilities. Each button has
    /// independent cooldown / fire-when-locked / fire-on-threat genes so the search
    /// can specialize per ability.
    ///
    /// The pilot is responsible for translating these requests into the actual
    /// ShipActionSO calls — this policy just emits InputEvents.
    /// </summary>
    public class AbilitySchedulerPolicy : IDecisionPolicy
    {
        public string ModuleName => "AbilityScheduler";

        struct AbilitySpec
        {
            public InputEvents Event;
            public string CooldownGene;
            public string FireWhenLockedGene;
            public string FireWhenThreatenedGene;
            public string LockDotGene;
        }

        static readonly AbilitySpec[] s_Specs =
        {
            new() { Event = InputEvents.Button1Action,
                    CooldownGene = "ability.btn1.cooldown",
                    FireWhenLockedGene = "ability.btn1.fire_locked",
                    FireWhenThreatenedGene = "ability.btn1.fire_threatened",
                    LockDotGene = "ability.btn1.lock_dot" },
            new() { Event = InputEvents.Button2Action,
                    CooldownGene = "ability.btn2.cooldown",
                    FireWhenLockedGene = "ability.btn2.fire_locked",
                    FireWhenThreatenedGene = "ability.btn2.fire_threatened",
                    LockDotGene = "ability.btn2.lock_dot" },
            new() { Event = InputEvents.Button3Action,
                    CooldownGene = "ability.btn3.cooldown",
                    FireWhenLockedGene = "ability.btn3.fire_locked",
                    FireWhenThreatenedGene = "ability.btn3.fire_threatened",
                    LockDotGene = "ability.btn3.lock_dot" }
        };

        struct AbilityRuntime
        {
            public float Cooldown;
            public float FireLocked;
            public float FireThreatened;
            public float LockDot;
            public float NextFireTime;
            public bool Held;
            public float HoldUntil;
        }

        readonly AbilityRuntime[] _runtime = new AbilityRuntime[s_Specs.Length];

        public void RegisterGenes()
        {
            foreach (var s in s_Specs)
            {
                GeneRegistry.Register(ModuleName, new GeneSpec(s.CooldownGene, 0.5f, 15f, 4f));
                GeneRegistry.Register(ModuleName, new GeneSpec(s.FireWhenLockedGene, 0f, 1f, 0.6f));
                GeneRegistry.Register(ModuleName, new GeneSpec(s.FireWhenThreatenedGene, 0f, 1f, 0.4f));
                GeneRegistry.Register(ModuleName, new GeneSpec(s.LockDotGene, 0.7f, 0.99f, 0.92f));
            }
            GeneRegistry.Register(ModuleName, new GeneSpec("ability.hold_seconds", 0.1f, 1.5f, 0.4f));
        }

        float _holdSeconds;

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _holdSeconds = genome.Get("ability.hold_seconds");
            for (int i = 0; i < s_Specs.Length; i++)
            {
                _runtime[i] = new AbilityRuntime
                {
                    Cooldown = genome.Get(s_Specs[i].CooldownGene),
                    FireLocked = genome.Get(s_Specs[i].FireWhenLockedGene),
                    FireThreatened = genome.Get(s_Specs[i].FireWhenThreatenedGene),
                    LockDot = genome.Get(s_Specs[i].LockDotGene),
                    NextFireTime = 0f,
                    Held = false,
                    HoldUntil = 0f
                };
            }
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            DecisionOutput output = DecisionOutput.Zero;

            for (int i = 0; i < s_Specs.Length; i++)
            {
                ref var rt = ref _runtime[i];
                var spec = s_Specs[i];

                // Stop holding if our hold window has elapsed.
                if (rt.Held && ctx.EpisodeTime >= rt.HoldUntil)
                {
                    output = output.RequestStop(spec.Event);
                    rt.Held = false;
                }

                if (ctx.EpisodeTime < rt.NextFireTime || rt.Held) continue;

                bool locked = ctx.HasTarget && ctx.DotForwardObjective >= rt.LockDot;
                bool threatened = ctx.Threats.Count > 0;

                float fireProb = 0f;
                if (locked) fireProb = Mathf.Max(fireProb, rt.FireLocked);
                if (threatened) fireProb = Mathf.Max(fireProb, rt.FireThreatened);

                if (UnityEngine.Random.value < fireProb)
                {
                    output = output.RequestStart(spec.Event);
                    rt.Held = true;
                    rt.HoldUntil = ctx.EpisodeTime + _holdSeconds;
                    rt.NextFireTime = ctx.EpisodeTime + rt.Cooldown;
                }
            }

            return output;
        }

        public void OnEpisodeEnd()
        {
            // No allocation — runtime structs are zeroed on next OnEpisodeStart.
        }
    }
}
