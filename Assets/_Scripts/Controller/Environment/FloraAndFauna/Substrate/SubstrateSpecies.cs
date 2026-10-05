// Round 11b (Docs/SUBSTRATE_FAUNA.md): a SPECIES is data. Two regimes (the solitary and the gregarious end of ONE
// parameter set) plus drive gains, a quorum rule and the bestiary's primitives. Nothing in SubstrateCore branches on
// which species it is stepping - it reads these numbers.
//
// Pure C# (no UnityEngine): the same file compiles and RUNS in Tools/Build/substrate_harness, which asserts the
// research numbers below against the Python they were ported from (research_params.json, test F).
using System;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Weights at ONE end of the phase axis (research <c>substrate/core.py</c> <c>Regime</c>). Every weight the steering
    /// reads is <c>lerp(solitary, gregarious, phase)</c>, so a phase change IS a behaviour change. Colours live in the
    /// ScriptableObject (glue), not here.
    /// </summary>
    [Serializable]
    public struct SubstrateRegime
    {
        public float Speed, Burst, Turn, Accel;
        public float WFood, WCoh, WAlign, WSep, WWander, WCurious, Comfort, WFlee, WHunt, WRing, RingR;
        public float WTrail, WAlarm, WThreat, WHome, Crowd, GaitHz, GaitAmp, Aspect, Size;

        /// <summary>The research Regime's defaults (core.py), which a species overrides field by field.</summary>
        public static SubstrateRegime Default => new SubstrateRegime
        {
            Speed = 60f, Burst = 1f, Turn = 3f, Accel = 120f, WFood = 1f, WCoh = 0.3f, WAlign = 0.3f, WSep = 0.6f,
            WWander = 0.3f, WCurious = 0f, Comfort = 120f, WFlee = 1f, WHunt = 0f, WRing = 0f, RingR = 90f, WTrail = 0f,
            WAlarm = 0.5f, WThreat = 0.5f, WHome = 0f, Crowd = 1f, GaitHz = 0f, GaitAmp = 0f, Aspect = 1.5f, Size = 3f,
        };

        public static SubstrateRegime Lerp(in SubstrateRegime a, in SubstrateRegime b, float t)
        {
            SubstrateRegime r;
            r.Speed = a.Speed + (b.Speed - a.Speed) * t; r.Burst = a.Burst + (b.Burst - a.Burst) * t;
            r.Turn = a.Turn + (b.Turn - a.Turn) * t; r.Accel = a.Accel + (b.Accel - a.Accel) * t;
            r.WFood = a.WFood + (b.WFood - a.WFood) * t; r.WCoh = a.WCoh + (b.WCoh - a.WCoh) * t;
            r.WAlign = a.WAlign + (b.WAlign - a.WAlign) * t; r.WSep = a.WSep + (b.WSep - a.WSep) * t;
            r.WWander = a.WWander + (b.WWander - a.WWander) * t; r.WCurious = a.WCurious + (b.WCurious - a.WCurious) * t;
            r.Comfort = a.Comfort + (b.Comfort - a.Comfort) * t; r.WFlee = a.WFlee + (b.WFlee - a.WFlee) * t;
            r.WHunt = a.WHunt + (b.WHunt - a.WHunt) * t; r.WRing = a.WRing + (b.WRing - a.WRing) * t;
            r.RingR = a.RingR + (b.RingR - a.RingR) * t; r.WTrail = a.WTrail + (b.WTrail - a.WTrail) * t;
            r.WAlarm = a.WAlarm + (b.WAlarm - a.WAlarm) * t; r.WThreat = a.WThreat + (b.WThreat - a.WThreat) * t;
            r.WHome = a.WHome + (b.WHome - a.WHome) * t; r.Crowd = a.Crowd + (b.Crowd - a.Crowd) * t;
            r.GaitHz = a.GaitHz + (b.GaitHz - a.GaitHz) * t; r.GaitAmp = a.GaitAmp + (b.GaitAmp - a.GaitAmp) * t;
            r.Aspect = a.Aspect + (b.Aspect - a.Aspect) * t; r.Size = a.Size + (b.Size - a.Size) * t;
            return r;
        }

        /// <summary>(research name, value) for every field - the fidelity test's and the authoring check's one list.</summary>
        public void Visit(Action<string, float> f)
        {
            f("speed", Speed); f("burst", Burst); f("turn", Turn); f("accel", Accel); f("w_food", WFood);
            f("w_coh", WCoh); f("w_align", WAlign); f("w_sep", WSep); f("w_wander", WWander); f("w_curious", WCurious);
            f("comfort", Comfort); f("w_flee", WFlee); f("w_hunt", WHunt); f("w_ring", WRing); f("ring_r", RingR);
            f("w_trail", WTrail); f("w_alarm", WAlarm); f("w_threat", WThreat); f("w_home", WHome); f("crowd", Crowd);
            f("gait_hz", GaitHz); f("gait_amp", GaitAmp); f("aspect", Aspect); f("size", Size);
        }
    }

    /// <summary>
    /// One species (research <c>SpeciesParams</c>) plus the four bestiary primitives the research named as what
    /// would put the whole bestiary on the substrate (DISCOVERIES.md "Bestiary", "Cost and the substrate"): a
    /// CLOSURE quorum input (the pack's ring), a POSTURE clock (stamina -> rest: the pack's winded window, the
    /// lurker's spent snap), a GAZE sensor (creep while unwatched, freeze when watched) and MIMICRY (drawn as a
    /// crystal while calm). Every one is a number a species sets; zero switches it off.
    /// </summary>
    [Serializable]
    public sealed class SubstrateSpeciesParams
    {
        public string Name = "species";
        public int N0 = 200, Capacity = 400;
        public SubstrateRegime Solitary = SubstrateRegime.Default, Gregarious = SubstrateRegime.Default;
        public float NbrR = 30f, DensNorm = 6f;
        // drives
        public float Metabolism = 0.01f, EatR = 8f, EatHunger = 0.25f, HungerPerVol = 0.02f, FearGain = 1f, AggrBase = 0f;
        public float FearDecay = 0.5f, Sense = 250f, CuriosityRate = 0.3f;
        // quorum: s = (QWDens*density + QWProx*proximity + QWAlarm*alarm + QWClose*closure) * hunger^QHunger, hysteresis
        public float QUp = 9f, QDown = 9f, QWidth = 0.05f, QRate = 0.4f, QContagion = 0.5f;
        public float QWDens = 1f, QWProx = 0f, QWAlarm = 0f, QHunger = 1f;
        // world
        public float BiteR = 10f, BiteCool = 1.5f, StarveS = 30f, BirthStock = 60f, Stock0 = 25f, GrowS = 2f;
        public float DepositThreat = 0f, DepositAlarm = 0.5f;
        public int NDirs = 18, FracK = 4, RingRoles = 0;
        public bool SpacingSpring = true;
        public float Momentum = 0.15f, IntentBlend = 0f, AttnR = 0f, AttnUrg = 1f;

        // ── the bestiary primitives (0 = off; none of them is in the research substrate - Docs/SUBSTRATE_FAUNA.md §2) ──
        /// <summary>Quorum weight of the pack's CLOSURE around its pilot (bestiary pack.py: how evenly packmates' bearings
        /// cover the sphere around the pilot, times how many there are).</summary>
        public float QWClose = 0f;
        /// <summary>Only packmates within this distance of the pilot count toward its closure (bestiary 350 u).</summary>
        public float CloseR = 350f;
        /// <summary>Posture clock: seconds of strike (phase above <see cref="DangerPhase"/>) before the agent is spent.</summary>
        public float StaminaS = 0f;
        /// <summary>Posture clock: seconds a spent (or just-bitten-with) agent rests - slow, harmless, the payoff window.</summary>
        public float RestS = 0f;
        /// <summary>Resting: speed multiplier and the weight of the interest AWAY from the nearest pilot (the pack falls back).</summary>
        public float RestSpeed = 0.5f, WRestRetreat = 0f;
        /// <summary>A bite sends every striking packmate near that pilot to rest too ("the pack backs off together").</summary>
        public bool RestTogether;
        /// <summary>Gaze sensor: creep toward where a pilot will be in <see cref="CreepLeadS"/> s while OUTSIDE its forward cone.</summary>
        public float WCreep = 0f, CreepR = 600f, CreepMin = 120f, CreepSpeed = 35f, CreepLeadS = 3f;
        /// <summary>Cosine of the half-angle of a pilot's forward view cone (bestiary 50 degrees).</summary>
        public float GazeCos = 0.6428f;
        /// <summary>1 = a calm agent a pilot looks at stops dead (it moves only when you are not looking).</summary>
        public float Freeze = 0f;
        /// <summary>Drawn body scale while calm, as a fraction of its true body - small enough that the heart crystal is
        /// what you see (a crystal mimic). 1 = no mimicry.</summary>
        public float MimicBody = 1f;
        /// <summary>Food web: the species this one PREYS on (by name; empty = it grazes flora only). A hungry predator
        /// follows the prey's scent and, within <see cref="PreySense"/>, makes straight for the nearest; one inside EatR is
        /// eaten - the owner kills it through its proxy (crystal and all) and its body becomes the predator's.</summary>
        public string PreyName = "";
        public float WPrey = 0f, PreySense = 150f;
        /// <summary>Scent this species leaves in the cell's SCENT field per second (what its predators smell).</summary>
        public float ScentDeposit = 0f;
        /// <summary>The phase above which an aggressive agent is DANGEROUS (a danger prism; research harm = aggression above 0.5).</summary>
        public float DangerPhase = 0.5f;

        public SubstrateSpeciesParams Clone()
        {
            var c = (SubstrateSpeciesParams)MemberwiseClone();
            return c;
        }

        /// <summary>(research name, value) for every scalar the research has. Game-only primitives are visited by
        /// <see cref="VisitPrimitives"/>.</summary>
        public void Visit(Action<string, float> f)
        {
            f("n0", N0); f("capacity", Capacity); f("nbr_r", NbrR); f("dens_norm", DensNorm);
            f("metabolism", Metabolism); f("eat_r", EatR); f("eat_hunger", EatHunger); f("hunger_per_vol", HungerPerVol);
            f("fear_gain", FearGain); f("aggr_base", AggrBase); f("fear_decay", FearDecay); f("sense", Sense);
            f("curiosity_rate", CuriosityRate); f("q_up", QUp); f("q_down", QDown); f("q_width", QWidth); f("q_rate", QRate);
            f("q_contagion", QContagion); f("q_w_dens", QWDens); f("q_w_prox", QWProx); f("q_w_alarm", QWAlarm);
            f("q_hunger", QHunger); f("bite_r", BiteR); f("bite_cool", BiteCool); f("starve_s", StarveS);
            f("birth_stock", BirthStock); f("stock0", Stock0); f("grow_s", GrowS); f("deposit_threat", DepositThreat);
            f("deposit_alarm", DepositAlarm); f("n_dirs", NDirs); f("frac_k", FracK); f("ring_roles", RingRoles);
            f("spacing_spring", SpacingSpring ? 1f : 0f); f("momentum", Momentum); f("intent_blend", IntentBlend);
            f("attn_r", AttnR); f("attn_urg", AttnUrg);
        }

        public void VisitPrimitives(Action<string, float> f)
        {
            f("q_w_close", QWClose); f("close_r", CloseR); f("stamina_s", StaminaS); f("rest_s", RestS);
            f("rest_speed", RestSpeed); f("w_rest_retreat", WRestRetreat); f("rest_together", RestTogether ? 1f : 0f);
            f("w_creep", WCreep); f("creep_r", CreepR); f("creep_min", CreepMin); f("creep_speed", CreepSpeed);
            f("creep_lead_s", CreepLeadS); f("gaze_cos", GazeCos); f("freeze", Freeze); f("mimic_body", MimicBody);
            f("danger_phase", DangerPhase); f("w_prey", WPrey); f("prey_sense", PreySense); f("scent_deposit", ScentDeposit);
        }
    }

    /// <summary>
    /// The species as the research left them (<c>Tools/Ecology/substrate/species.py</c> on cece/eco-substrate), quoted
    /// number for number - and the game's ports of them, which change only the fields listed in
    /// <see cref="GameDeltas"/>, each for a stated reason (Docs/SUBSTRATE_FAUNA.md §1). The harness asserts both: the
    /// research sets equal research_params.json, and each port differs from its research set in exactly its listed
    /// fields.
    /// </summary>
    public static class SubstrateResearch
    {
        // ── the research, verbatim ──────────────────────────────────────────────────────────────────────
        public static SubstrateSpeciesParams Locust()
        {
            var sol = SubstrateRegime.Default;
            sol.Speed = 31.4f; sol.Burst = 2.44f; sol.Turn = 2.2f; sol.Accel = 50f; sol.WFood = 1.0f; sol.WCoh = 0.05f;
            sol.WAlign = 0.05f; sol.WSep = 1.4f; sol.WWander = 0.5f; sol.WCurious = 2.43f; sol.Comfort = 68.6f; sol.WFlee = 1.4f;
            sol.WHunt = 0f; sol.WAlarm = 0.6f; sol.WThreat = 0.3f; sol.Crowd = 0.5f; sol.GaitHz = 2.15f; sol.GaitAmp = 27.6f;
            sol.Aspect = 1.06f; sol.Size = 2.2f;
            var gre = SubstrateRegime.Default;
            gre.Speed = 95f; gre.Burst = 1.5f; gre.Turn = 4.0f; gre.Accel = 160f; gre.WFood = 0.6f; gre.WCoh = 0.9f;
            gre.WAlign = 1.2f; gre.WSep = 0.6f; gre.WWander = 0.08f; gre.WCurious = 0f; gre.Comfort = 90f; gre.WFlee = 0f;
            gre.WHunt = 1.8f; gre.WAlarm = 0f; gre.WThreat = 0f; gre.Crowd = 2.0f; gre.Aspect = 2.2f; gre.Size = 3.2f;
            return new SubstrateSpeciesParams
            {
                Name = "locust", N0 = 400, Capacity = 640, Solitary = sol, Gregarious = gre, NbrR = 30f, DensNorm = 5f,
                Metabolism = 0.02f, EatR = 7f, EatHunger = 0.15f, HungerPerVol = 0.02f, FearGain = 1.45f, FearDecay = 1.27f,
                Sense = 220f, CuriosityRate = 0.33f, QUp = 0.55f, QDown = 0.30f, QWidth = 0.06f, QRate = 0.5f,
                QContagion = 0.6f, BiteR = 8f, BiteCool = 1.2f, BirthStock = 90f, DepositAlarm = 0.3f, FracK = 4,
                AttnR = 150f, AttnUrg = 0.6f,
            };
        }

        public static SubstrateSpeciesParams Pack()
        {
            var stalk = SubstrateRegime.Default;
            stalk.Speed = 105f; stalk.Burst = 1.25f; stalk.Turn = 2.8f; stalk.Accel = 140f; stalk.WFood = 0f; stalk.WCoh = 0.2f;
            stalk.WAlign = 0.3f; stalk.WSep = 1.0f; stalk.WWander = 0.15f; stalk.WCurious = 0f; stalk.WFlee = 0f;
            stalk.WHunt = 0.25f; stalk.WRing = 1.4f; stalk.RingR = 110f; stalk.WAlarm = 0f; stalk.WThreat = 0f; stalk.Size = 5.0f;
            var strike = stalk;   // research: replace(stalk, ...)
            strike.Speed = 150f; strike.Burst = 1.6f; strike.Turn = 3.6f; strike.Accel = 260f; strike.WHunt = 2.0f;
            strike.WRing = 0.3f; strike.RingR = 40f; strike.WSep = 0.4f; strike.Size = 5.5f;
            return new SubstrateSpeciesParams
            {
                Name = "pack", N0 = 8, Capacity = 16, Solitary = stalk, Gregarious = strike, NbrR = 150f, DensNorm = 3.0f,
                Metabolism = 0.03f, EatR = 8f, EatHunger = 0.9f, HungerPerVol = 0.01f, Sense = 900f, QUp = 0.75f, QDown = 0.35f,
                QWidth = 0.08f, QRate = 0.8f, QContagion = 0.5f, BiteR = 10f, BiteCool = 2.0f, BirthStock = 1e9f, RingRoles = 8,
                DepositThreat = 0.4f, DepositAlarm = 0f, FracK = 2, AttnR = 250f, AttnUrg = 0.6f, StarveS = 1e9f, NDirs = 26,
            };
        }

        public static SubstrateSpeciesParams Lurker()
        {
            var sit = SubstrateRegime.Default;
            sit.Speed = 4f; sit.Burst = 1.0f; sit.Turn = 1.0f; sit.Accel = 20f; sit.WFood = 0f; sit.WCoh = 0f; sit.WAlign = 0f;
            sit.WSep = 0.5f; sit.WWander = 0.05f; sit.WCurious = 0f; sit.WFlee = 0f; sit.WHunt = 0f; sit.WAlarm = 0f;
            sit.WThreat = 0f; sit.WHome = 3.0f; sit.Size = 2.5f;
            var lunge = sit;
            lunge.Speed = 230f; lunge.Burst = 1.0f; lunge.Turn = 5.0f; lunge.Accel = 900f; lunge.WHunt = 3.0f; lunge.WHome = 0f;
            lunge.WSep = 0.2f; lunge.Size = 7.0f;
            return new SubstrateSpeciesParams
            {
                Name = "lurker", N0 = 12, Capacity = 24, Solitary = sit, Gregarious = lunge, NbrR = 40f, Metabolism = 0.004f,
                EatR = 10f, EatHunger = 0.2f, Sense = 300f, AggrBase = 1.0f, QWDens = 0f, QWProx = 1.0f, QHunger = 0f,
                QUp = 0.5f, QDown = 0.25f, QWidth = 0.04f, QRate = 1.6f, QContagion = 0f, BiteR = 12f, BiteCool = 2.5f,
                BirthStock = 1e9f, StarveS = 1e9f, DepositAlarm = 0f, FracK = 1,
            };
        }

        // ── the game's ports ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every field a port changes from its research set, with the reason (also Docs/SUBSTRATE_FAUNA.md §1). The
        /// harness fails if a port differs anywhere else, so a silent drift from the research cannot ship.
        /// </summary>
        public static readonly (string species, string field, string why)[] GameDeltas =
        {
            ("locust", "n0", "seed 40 (bestiary seeds 30): the swarm's size is the food it FINDS, not what it is given"),
            ("locust", "capacity", "pool 360 = the bestiary cap; births are gated by free slots (production gating)"),
            ("pack", "n0", "6 hunters (the brief: 5-7 long hunters; bestiary n=7)"),
            ("pack", "capacity", "7: the pack never grows past seven"),
            ("pack", "ring_roles", "one ring slot per pool slot, so a newborn takes a free bearing"),
            ("pack", "solitary.size", "12 u long bodies (bestiary size 12; research finding 11: big + long reads as dread)"),
            ("pack", "gregarious.size", "13 u while striking (finding 11's 13 u)"),
            ("pack", "solitary.aspect", "2.6 long, low bodies (bestiary pack.py)"),
            ("pack", "gregarious.aspect", "2.6"),
            ("pack", "solitary.speed", "85: with aggression 0.6 the stalk cruises ~98 u/s - SLOWER than a 120 u/s pilot (bestiary CRUISE 95): it catches you by geometry, never by speed"),
            ("pack", "gregarious.burst", "1.2: the strike sprints ~168 u/s (bestiary SPRINT 175); research 1.6 at aggression 0.6 is 204 u/s, which no pilot outruns"),
            ("pack", "solitary.w_food", "0.5: every lifeform eats (the research pack never ate, starve_s 1e9)"),
            ("pack", "eat_hunger", "0.4: eats on the way, not only at the brink"),
            ("pack", "q_w_dens", "0: the strike reads the ring's CLOSURE, not crowding (bestiary quorum)"),
            ("pack", "q_w_close", "1: the bestiary's closure quorum"),
            ("pack", "q_up", "0.65, between the bestiary's 0.55 and the research's 0.75: the ring is HELD >= 8 s (one emotion-probe window) before the first strike, so the pack reads its menace->terror arc (DISCOVERIES finding 11: menace only while the ring holds without striking); 0.55 struck after 2.9 s in one seed"),
            ("pack", "q_hunger", "0: a closed ring strikes whether or not the pack is hungry (bestiary)"),
            ("pack", "aggr_base", "0.6: a pack is always a hunter; its aggression is what drives the ring term"),
            ("pack", "stamina_s", "3 s of sprint (bestiary stamina)"),
            ("pack", "rest_s", "3 s winded: the payoff window (bestiary cool)"),
            ("pack", "w_rest_retreat", "1.5: a winded hunter falls back and widens (bestiary)"),
            ("pack", "rest_together", "1: the pack backs off together after a bite (bestiary)"),
            ("pack", "starve_s", "60: a real lifeform starves (research 1e9 = never)"),
            ("pack", "birth_stock", "300: a real lifeform breeds from food (research 1e9 = never)"),
            ("pack", "stock0", "150: a hunter's body is a 12 u prism, and its body IS its stock"),
            ("locust", "scent_deposit", "0.2: locusts are the pack's prey - the food web the research had no second species for"),
            ("pack", "w_prey", "1.5: a hungry pack follows the locusts' scent and eats them (its body is theirs; mass moves, never vanishes)"),
            ("lurker", "capacity", "16 (bestiary n=16)"),
            ("lurker", "starve_s", "120: a real lifeform starves (research 1e9)"),
            ("lurker", "birth_stock", "100: a real lifeform breeds from food (research 1e9)"),
            ("lurker", "stamina_s", "0.6 s lunge (= 138 u reach at 230 u/s; bestiary snap 133 u)"),
            ("lurker", "rest_s", "3 s spent after a snap (bestiary)"),
            ("lurker", "rest_speed", "0.2: spent is slack and slow"),
            ("lurker", "w_creep", "2: creep while unwatched (bestiary; ablating it took wanderer hits to 0)"),
            ("lurker", "freeze", "1: looked at, it stops dead"),
            ("lurker", "mimic_body", "0.15: while calm its body is a sliver behind its heart - it reads as a crystal"),
            ("lurker", "solitary.w_food", "0.5 (the pack's port value): every lifeform eats - the research lurker never ate (w_food 0, starve_s 1e9), and with a real starve_s it starved at its seat once the inner swarm had eaten that crystal (showcase cell: extinct by minute 7, 7 bites in 30 min); hunger-weighted, so a fed lurker still sits"),
        };

        public static SubstrateSpeciesParams GameLocust()
        {
            var p = Locust();
            p.N0 = 40; p.Capacity = 360; p.ScentDeposit = 0.2f;
            return p;
        }

        public static SubstrateSpeciesParams GamePack()
        {
            var p = Pack();
            p.N0 = 6; p.Capacity = 7; p.RingRoles = 7;
            p.Solitary.Speed = 85f; p.Gregarious.Burst = 1.2f;
            p.Solitary.Size = 12f; p.Gregarious.Size = 13f; p.Solitary.Aspect = 2.6f; p.Gregarious.Aspect = 2.6f;
            p.Solitary.WFood = 0.5f; p.EatHunger = 0.4f;
            p.QWDens = 0f; p.QWClose = 1f; p.QUp = 0.65f; p.QHunger = 0f; p.AggrBase = 0.6f;
            p.StaminaS = 3f; p.RestS = 3f; p.WRestRetreat = 1.5f; p.RestTogether = true;
            p.StarveS = 60f; p.BirthStock = 300f; p.Stock0 = 150f;
            p.PreyName = "locust"; p.WPrey = 1.5f;
            return p;
        }

        public static SubstrateSpeciesParams GameLurker()
        {
            var p = Lurker();
            p.Capacity = 16; p.StarveS = 120f; p.BirthStock = 100f;
            p.StaminaS = 0.6f; p.RestS = 3f; p.RestSpeed = 0.2f; p.WCreep = 2f; p.Freeze = 1f; p.MimicBody = 0.15f;
            p.Solitary.WFood = 0.5f;
            return p;
        }

        public static SubstrateSpeciesParams ByName(string name, bool game) => name switch
        {
            "locust" => game ? GameLocust() : Locust(),
            "pack" => game ? GamePack() : Pack(),
            "lurker" => game ? GameLurker() : Lurker(),
            _ => throw new ArgumentException("no such substrate species: " + name),
        };
    }
}
