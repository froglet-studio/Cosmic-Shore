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
        /// <summary>Above 0.5: contact HARMS a pilot when the agent is fast (a stampede tramples), whatever its
        /// aggression (research core.py Regime.trample).</summary>
        public float Trample;

        /// <summary>The research Regime's defaults (core.py), which a species overrides field by field.</summary>
        public static SubstrateRegime Default => new SubstrateRegime
        {
            Speed = 60f, Burst = 1f, Turn = 3f, Accel = 120f, WFood = 1f, WCoh = 0.3f, WAlign = 0.3f, WSep = 0.6f,
            WWander = 0.3f, WCurious = 0f, Comfort = 120f, WFlee = 1f, WHunt = 0f, WRing = 0f, RingR = 90f, WTrail = 0f,
            WAlarm = 0.5f, WThreat = 0.5f, WHome = 0f, Crowd = 1f, GaitHz = 0f, GaitAmp = 0f, Aspect = 1.5f, Size = 3f,
            Trample = 0f,
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
            r.Trample = a.Trample + (b.Trample - a.Trample) * t;
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
            f("trample", Trample);
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

        // ── the research's BODY layer (core.py SpeciesParams attach_* + BodyPlan; Docs/SUBSTRATE_FAUNA.md §9.4) ──
        /// <summary>Per second, how fast each agent's attachment relaxes toward the group's assembling target.</summary>
        public float AttachRate = 0.5f;
        /// <summary>The body quorum: assemble when the group's mean hunger falls below AttachOnH (or its mean fear rises
        /// above AttachOnF); dissolve when mean hunger rises above AttachOffH and mean fear is under half AttachOnF.</summary>
        public float AttachOnH = 0.3f, AttachOffH = 0.55f, AttachOnF = 0.35f;
        /// <summary>The body plan's slots in the body frame (+z forward), x,y,z flattened; agent k of the block holds slot
        /// k mod (length / 3). Empty = no body (every species but the leviathan).</summary>
        public float[] BodySlots = new float[0];
        /// <summary>Body plan: slot scale, the flat-bottom well a member stops pulling inside (u), the body's cruise (u/s).</summary>
        public float BodyScale = 1f, BodyWell = 4f, BodySpeed = 50f;

        // ── round 11-11 primitives (0 / off by default; Docs/SUBSTRATE_FAUNA.md §9) ──
        /// <summary>Strike ROLE: only every Nth agent of the block arms a ramp and climbs the alarm gradient (the
        /// stampede's bulls, every 4th). 0 = every agent.</summary>
        public int ChargeEvery = 0;
        /// <summary>RAMP (the windup before a strike: a bull's head-down, a mobber's pull-up): seconds an agent must hold
        /// its arming condition - gregarious, not spent, a pilot inside RampR - before it may turn dangerous. 0 = no ramp.</summary>
        public float RampS = 0f;
        /// <summary>Speed multiplier while ramping (the bull slows to face you).</summary>
        public float RampSpeed = 1f;
        /// <summary>A pilot inside this distance arms the ramp.</summary>
        public float RampR = 0f;
        /// <summary>The strike role arms on SIGHT - a pilot inside RampR - whatever its herd's phase (a bull faces what
        /// it sees: bestiary stampede.py arms a bull on distance alone); its alarm climb then runs on max(phase, fear).
        /// Off = it arms only at the gregarious end.</summary>
        public bool RampOnSight;
        /// <summary>Weight of the aim at the pilot's lead point while ramping and striking (a charge, a dive).</summary>
        public float WStrike = 0f;
        /// <summary>Speed (u/s), acceleration (u/s^2) and turn rate (rad/s) of a ramped strike; 0 = the regime's.</summary>
        public float StrikeSpeed = 0f, StrikeAccel = 0f, StrikeTurn = 0f;
        /// <summary>The hunt lead's clip: aim at pilot + velocity x clip(distance / 150, 0, this) (research 2).</summary>
        public float HuntLeadMax = 2f;
        /// <summary>Interest UP the alarm gradient for the strike role, scaled by phase (and its own flee/alarm danger muted
        /// by phase): the herd's panic leads the bulls TO the threat that spooked it instead of away from it.</summary>
        public float WAlarmClimb = 0f;
        /// <summary>A trample needs the agent moving INTO the pilot: velocity . bearing > this x speed (bestiary 0.3; 0 = any).</summary>
        public float TrampleClose = 0f;
        /// <summary>Quorum weight of PROVOCATION: a pilot inside Sense that is slower than SlowBelow, or inside RoostR of
        /// the agent's roost (its home) - the mobber's trigger.</summary>
        public float QWProvoke = 0f, SlowBelow = 100f, RoostR = 260f;
        /// <summary>Resting keeps the phase (a mobber pulls out of its dive and keeps orbiting; it does not go home).</summary>
        public bool RestHoldsPhase;
        /// <summary>JINK: a pilot pointing at the agent (cos > JinkCos) inside JinkR makes it break sideways.</summary>
        public float WJink = 0f, JinkR = 60f, JinkCos = 0.9f;
        /// <summary>CLING: a pouncing agent (or one met slower than ClingRelSpeed) that touches a pilot latches onto its hull
        /// and rides it, at most ClingMax per hull. 0 = never clings.</summary>
        public int ClingMax = 0;
        public float ClingRelSpeed = 100f;
        /// <summary>Grip: lost at GripLoss per (rad/s above GripTurn) of the host's turn rate per second, regained at GripGain/s.</summary>
        public float GripTurn = 1f, GripLoss = 1.6f, GripGain = 0.3f;
        /// <summary>Shaken off at zero grip: flung sideways at FlingSpeed and dazed (resting, harmless) DazeS seconds.</summary>
        public float DazeS = 2.5f, FlingSpeed = 90f;
        /// <summary>A rider SIPS its host every SipS seconds - a danger contact of weight SipWeight (burn rules: a drain is 0.25).</summary>
        public float SipS = 1.5f, SipWeight = 0.25f;
        /// <summary>The burn-rule weight of a contact with this species' danger plate (bite 1; a mobber's peck is a drain,
        /// 0.25; 0 = its plate never burns - a leech harms only by sipping).</summary>
        public float ContactWeight = 1f;
        /// <summary>An ASSEMBLED member (attachment above 0.5, the body formed) is dangerous (bestiary leviathan: touching
        /// the body burns).</summary>
        public bool DangerAttached;
        /// <summary>The body turns toward the nearest pilot inside BodyCuriousR with this weight (bestiary 0.8 within 700 u).</summary>
        public float BodyCurious = 0f, BodyCuriousR = 700f;
        /// <summary>GULP: a pilot inside GulpR ahead of the mouth (cos > GulpCos) holds GulpRampS seconds (the jaws flare),
        /// then the body surges at GulpSpeed for GulpS seconds and rests GulpRestS. GulpR 0 = no gulp.</summary>
        public float GulpR = 0f, GulpCos = 0.7f, GulpRampS = 1.2f, GulpS = 1.6f, GulpSpeed = 115f, GulpRestS = 4f;
        /// <summary>TURNS: at most this many of the block wind up or strike at one pilot at once; an armed agent past
        /// the limit WAITS its turn and the longest waiter goes next (bestiary mobber: "each mobbing bird, in turn").
        /// 0 = no limit.</summary>
        public int RampTurns = 0;
        /// <summary>Opt-in designed beat, NOT in the research (0 = the research's behaviour, bit for bit): once the ring's
        /// closure around a pilot crosses <see cref="QUp"/>, the pack HOLDS the strike for this many seconds of saturated
        /// closure while the ring circles slowly and tightens (SubstrateKernel.HoldSpeed / HoldOrbit / HoldTighten; phase held
        /// at or below SubstrateCore.HoldPhase x <see cref="DangerPhase"/>, so no one strikes), then all strike together. A pilot that breaks the ring (closure
        /// below <see cref="QDown"/>) resets the hold: the ring re-forms rather than striking. Docs/SUBSTRATE_FAUNA.md §7.7.</summary>
        public float RingHoldSeconds = 0f;

        public SubstrateSpeciesParams Clone()
        {
            var c = (SubstrateSpeciesParams)MemberwiseClone();
            c.BodySlots = BodySlots != null ? (float[])BodySlots.Clone() : new float[0];
            return c;
        }

        /// <summary>Slots in the body plan (0 = no body).</summary>
        public int BodyK => BodySlots != null ? BodySlots.Length / 3 : 0;

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
            f("attn_r", AttnR); f("attn_urg", AttnUrg); f("attach_rate", AttachRate); f("attach_on_h", AttachOnH);
            f("attach_off_h", AttachOffH); f("attach_on_f", AttachOnF);
        }

        /// <summary>The body plan's scalars under the research's names (BodyPlan); compared only where the research has a body.</summary>
        public void VisitBody(Action<string, float> f)
        {
            f("scale", BodyScale); f("well", BodyWell); f("speed", BodySpeed);
        }

        public void VisitPrimitives(Action<string, float> f)
        {
            f("q_w_close", QWClose); f("close_r", CloseR); f("stamina_s", StaminaS); f("rest_s", RestS);
            f("rest_speed", RestSpeed); f("w_rest_retreat", WRestRetreat); f("rest_together", RestTogether ? 1f : 0f);
            f("w_creep", WCreep); f("creep_r", CreepR); f("creep_min", CreepMin); f("creep_speed", CreepSpeed);
            f("creep_lead_s", CreepLeadS); f("gaze_cos", GazeCos); f("freeze", Freeze); f("mimic_body", MimicBody);
            f("danger_phase", DangerPhase); f("w_prey", WPrey); f("prey_sense", PreySense); f("scent_deposit", ScentDeposit);
            f("body_scale", BodyScale);
            f("charge_every", ChargeEvery); f("ramp_s", RampS); f("ramp_speed", RampSpeed); f("ramp_r", RampR); f("ramp_on_sight", RampOnSight ? 1f : 0f);
            f("w_strike", WStrike); f("strike_speed", StrikeSpeed); f("strike_accel", StrikeAccel); f("strike_turn", StrikeTurn);
            f("hunt_lead_max", HuntLeadMax); f("w_alarm_climb", WAlarmClimb); f("trample_close", TrampleClose);
            f("q_w_provoke", QWProvoke); f("slow_below", SlowBelow); f("roost_r", RoostR);
            f("rest_holds_phase", RestHoldsPhase ? 1f : 0f); f("w_jink", WJink); f("jink_r", JinkR); f("jink_cos", JinkCos);
            f("cling_max", ClingMax); f("cling_rel_speed", ClingRelSpeed); f("grip_turn", GripTurn); f("grip_loss", GripLoss);
            f("grip_gain", GripGain); f("daze_s", DazeS); f("fling_speed", FlingSpeed); f("sip_s", SipS);
            f("sip_weight", SipWeight); f("contact_weight", ContactWeight); f("danger_attached", DangerAttached ? 1f : 0f);
            f("body_curious", BodyCurious); f("body_curious_r", BodyCuriousR); f("gulp_r", GulpR); f("gulp_cos", GulpCos);
            f("gulp_ramp_s", GulpRampS); f("gulp_s", GulpS); f("gulp_speed", GulpSpeed); f("gulp_rest_s", GulpRestS);
            f("ramp_turns", RampTurns);
            f("ring_hold_s", RingHoldSeconds);
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


        public static SubstrateSpeciesParams Stampede()
        {
            var calm = SubstrateRegime.Default;
            calm.Speed = 22f; calm.Burst = 2.0f; calm.Turn = 2.0f; calm.Accel = 50f; calm.WFood = 1.0f; calm.WCoh = 0.4f;
            calm.WAlign = 0.4f; calm.WSep = 0.9f; calm.WWander = 0.3f; calm.WCurious = 0.3f; calm.Comfort = 150f; calm.WFlee = 1.2f;
            calm.WAlarm = 0.6f; calm.WThreat = 0.3f; calm.Size = 4.0f;
            var run = calm;   // research: replace(calm, ...)
            run.Speed = 110f; run.Burst = 1.2f; run.Turn = 1.6f; run.Accel = 120f; run.WCoh = 1.0f; run.WAlign = 2.5f; run.WSep = 0.5f;
            run.WWander = 0.02f; run.WCurious = 0f; run.WFlee = 0.4f; run.WAlarm = 1.5f; run.Trample = 1.0f; run.Size = 4.5f;
            return new SubstrateSpeciesParams
            {
                Name = "stampede", N0 = 220, Capacity = 330, Solitary = calm, Gregarious = run, NbrR = 35f, DensNorm = 6f,
                Metabolism = 0.008f, EatR = 9f, Sense = 260f, FearGain = 1.6f, QWDens = 0f, QWAlarm = 1.0f, QHunger = 0f,
                QUp = 0.35f, QDown = 0.08f, QWidth = 0.04f, QRate = 0.9f, QContagion = 0.7f, BiteR = 9f, BiteCool = 1.0f,
                BirthStock = 120f, DepositAlarm = 1.2f, FracK = 4, AttnR = 150f, AttnUrg = 0.6f,
            };
        }

        /// <summary>The research grazer (species.py grazer) - the leviathan's members before they assemble.</summary>
        public static SubstrateSpeciesParams Grazer(int n0)
        {
            var r = SubstrateRegime.Default;
            r.Speed = 34f; r.Burst = 2.4f; r.Turn = 2.6f; r.Accel = 60f; r.WFood = 1.0f; r.WCoh = 0.35f; r.WAlign = 0.5f;
            r.WSep = 0.8f; r.WWander = 0.35f; r.WCurious = 1.2f; r.Comfort = 70f; r.WFlee = 1.6f; r.WAlarm = 0.8f; r.WThreat = 0.25f;
            r.WHome = 0.15f; r.Size = 2.5f;
            return new SubstrateSpeciesParams
            {
                Name = "grazer", N0 = n0, Capacity = 2 * n0, Solitary = r, Gregarious = r, NbrR = 25f, DensNorm = 6f,
                Metabolism = 0.012f, EatR = 7f, EatHunger = 0.2f, HungerPerVol = 0.025f, FearGain = 1.2f, FearDecay = 0.6f,
                Sense = 200f, CuriosityRate = 0.4f, BirthStock = 80f, DepositAlarm = 0.6f, FracK = 4, AttnR = 150f, AttnUrg = 0.6f,
            };
        }

        /// <summary>Research leviathan: a grazer school with a BODY PLAN it assembles into on satiety. Its slots are
        /// research manta_slots(K) (species.py, rng seed 3); the research binds K to n0, so the table here is the one at
        /// the game's n0 (<see cref="MantaSlots96"/>) - the harness asserts it against the research function's output.</summary>
        public static SubstrateSpeciesParams Leviathan()
        {
            var p = Grazer(160);
            p.Name = "leviathan";
            p.BodySlots = (float[])MantaSlots96.Clone(); p.BodyScale = 1.0f; p.BodyWell = 3.0f; p.BodySpeed = 40f;
            p.AttachRate = 0.8f; p.AttachOnH = 0.36f; p.AttachOffH = 0.55f; p.AttachOnF = 2.0f; p.Metabolism = 0.012f;
            p.EatR = 12f; p.HungerPerVol = 0.03f;
            return p;
        }

        /// <summary>research species.py manta_slots(96): a flat diamond wing (60%), a thick spine (25%) and a tail whip,
        /// 60 u long and 80 u across, +z forward.</summary>
        public static readonly float[] MantaSlots96 =
        {
            -17.443708f, 2.2451057f, -3.5653508f, -14.946379f, -3.0592558f, -1.8776181f, 7.7200737f, -0.5104749f, 2.1043174f, 1.9812404f, -0.91291595f, 11.513526f,
            -26.469515f, 0.7990824f, -4.956338f, -4.3831263f, -3.4185398f, -1.3040298f, -1.1265316f, 1.7617481f, 3.42208f, -25.69865f, 1.600475f, -5.4640284f,
            6.205126f, -1.9531063f, 4.6595697f, -27.603333f, -1.4678228f, -5.271122f, -2.5067108f, -1.201758f, 10.711628f, 1.3056203f, 0.06494385f, -5.272643f,
            -3.9879854f, 0.9614566f, 1.2710543f, 4.8952675f, 3.071829f, 1.3142787f, 18.633144f, -0.29616815f, -5.674378f, 30.867697f, 1.1512538f, -5.595013f,
            -14.636806f, 0.23312671f, -3.4051342f, 1.5422596f, 2.6398895f, 12.350505f, 7.8158937f, 1.1132368f, 3.1515436f, -2.950275f, 2.0528257f, 8.438451f,
            -12.6323395f, -1.6165128f, -5.9389143f, 12.866907f, -0.28836107f, -4.9485555f, -14.495938f, -1.2206587f, -4.188308f, -3.5051866f, 2.257421f, 8.401673f,
            13.388666f, 0.98645985f, -2.2786472f, 4.1518254f, -0.45771664f, 3.7222605f, -2.047509f, -0.6787018f, -2.9479663f, 21.327261f, 0.7269973f, -5.6671677f,
            -29.508928f, -1.0522434f, -5.6092467f, 5.621239f, -1.3958833f, 5.6129017f, -6.5778985f, 0.7219117f, 1.7730162f, -25.651651f, 3.694698f, -4.8208723f,
            1.6183782f, -0.36920032f, 11.802524f, 26.347466f, -0.83379865f, -5.026716f, -11.64127f, -1.7567352f, 0.2534588f, 7.1907883f, -2.0025165f, 0.85940284f,
            -12.193576f, 0.7874749f, -1.6199129f, 14.599053f, 1.2762046f, -2.2013793f, 11.291337f, 0.013762081f, 0.079558976f, -14.896942f, 0.498864f, -1.5645161f,
            8.49866f, 0.17387487f, 0.9199027f, 7.7113204f, 0.20798227f, 1.9817973f, 3.4189663f, -2.2892387f, 8.582504f, 15.335021f, -0.6871774f, -1.6697539f,
            -3.6940877f, 0.16721897f, 3.0905783f, 17.300371f, -1.1747502f, -3.6243677f, 15.766007f, -0.7146446f, -2.505342f, -2.0286615f, -1.2286803f, -0.25227353f,
            17.888348f, -0.50024503f, -2.7486203f, -2.8006947f, 1.2796624f, 9.834831f, -1.3179781f, -0.60987055f, -0.5581016f, -14.601942f, -0.23080602f, -1.7512611f,
            8.070792f, 1.2205776f, 2.8947659f, -15.57201f, 0.96715534f, -4.873927f, 15.441115f, 2.5428114f, -2.2892573f, -8.598006f, -3.1357276f, 2.6170733f,
            2.3619432f, 1.2852885f, 7.732948f, -1.4468522f, 1.0684094f, -18f, 0.40407544f, 0.11809742f, -15.652174f, 2.513158f, 0.8691017f, -13.304348f,
            3.2497594f, -0.5705576f, -10.956522f, 3.118052f, 3.4390512f, -8.608696f, 0.46532008f, 6.0937824f, -6.2608695f, 4.828988f, 3.4929447f, -3.9130435f,
            -0.84892267f, 4.8729753f, -1.5652174f, -0.42294574f, 7.8965225f, 0.7826087f, 2.3980536f, 3.8100793f, 3.1304348f, -1.6541172f, 3.5854871f, 5.478261f,
            6.482719f, 2.1748157f, 7.826087f, 3.0576198f, -1.8247268f, 10.173913f, 6.526726f, 5.279217f, 12.521739f, -0.07976756f, -2.2693813f, 14.869565f,
            -1.1492656f, 4.9580293f, 17.217392f, 0.501144f, 2.9570389f, 19.565218f, 2.2037294f, 6.3803444f, 21.913044f, -1.7622831f, 2.7966893f, 24.26087f,
            1.1391052f, 0.5303995f, 26.608696f, -0.050412837f, 4.073731f, 28.956522f, 4.8470454f, 1.3192916f, 31.304348f, -1.9881054f, 2.4571295f, 33.652172f,
            3.1384854f, 3.1256123f, 36f, 0f, 0f, -18f, 0f, 0f, -21.857143f, 0f, 0f, -25.714285f,
            0f, 0f, -29.571428f, 0f, 0f, -33.42857f, 0f, 0f, -37.285713f, 0f, 0f, -41.142857f,
            0f, 0f, -45f, 0f, 0f, -48.857143f, 0f, 0f, -52.714287f, 0f, 0f, -56.57143f,
            0f, 0f, -60.42857f, 0f, 0f, -64.28571f, 0f, 0f, -68.14286f, 0f, 0f, -72f,
        };

        /// <summary>The MOBBER has no research substrate set (bestiary/species/mobber.py only): it is built on the
        /// substrate's defaults and every number it takes from the bestiary is in <see cref="BestiaryPorts"/>.</summary>
        public static SubstrateSpeciesParams MobberBase() => new SubstrateSpeciesParams { Name = "mobber" };

        /// <summary>The LEECH has no research substrate set (bestiary/species/leech.py only); see <see cref="BestiaryPorts"/>.</summary>
        public static SubstrateSpeciesParams LeechBase() => new SubstrateSpeciesParams { Name = "leech" };

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

            // ── round 11-11: the stampede (research species.py stampede + the bestiary's bulls) ──
            ("stampede", "n0", "48: four herds of 12 (bestiary 6 herds over n=72); the herd's size is the food it finds"),
            ("stampede", "capacity", "72 (bestiary n=72)"),
            ("stampede", "charge_every", "4: every 4th animal is a BULL (bestiary stampede.py, bull = arange(n) % 4 == 0)"),
            ("stampede", "w_alarm_climb", "2: a bull climbs the herd's ALARM gradient toward what spooked it (research open note: the herd flees AWAY, so it almost never tramples - the bull is what brings the charge back)"),
            ("stampede", "ramp_r", "260: a pilot inside BULL_R arms the bull (bestiary)"),
            ("stampede", "ramp_on_sight", "1: a bull arms on SIGHT, before its herd has flipped (bestiary: a bull inside BULL_R faces you) - the herd's alarm flip takes seconds a passing pilot never gives it"),
            ("stampede", "ramp_s", "0.9 s head-down before the charge (bestiary HEAD_DOWN) - the telegraph"),
            ("stampede", "ramp_speed", "0.2: the bull slows to face you while it lowers its head (bestiary: turns to face you at 20 u/s)"),
            ("stampede", "w_strike", "3: the head-down and the charge aim at your lead point (bestiary: a bull leads its target)"),
            ("stampede", "strike_speed", "170 u/s charge (bestiary)"),
            ("stampede", "strike_accel", "400 u/s^2 charge acceleration (bestiary)"),
            ("stampede", "strike_turn", "2.4 rad/s: the charge steers by acceleration 400 at 170 u/s (bestiary) - research run turn 1.6 cannot hold a lead"),
            ("stampede", "hunt_lead_max", "1.2: the charge's lead, clip(distance / speed, 0, 1.2) (bestiary)"),
            ("stampede", "stamina_s", "1.5 s charge (bestiary)"),
            ("stampede", "rest_s", "4 s rest after a charge (bestiary) - the sidestep's payoff"),
            ("stampede", "bite_r", "16: a bull's body reaches 16 u past your hull (bestiary contact: bull size 14 + 2; research 9 is a 4.5 u grazer)"),
            ("stampede", "trample_close", "0.3: a trample is a body running INTO you, not you rear-ending a fleeing animal (bestiary closing test)"),

            // ── the mobber (bestiary mobber.py; no research substrate set - its base is the substrate defaults) ──
            ("mobber", "n0", "40: five roosts of eight (bestiary 50 on 5 roosts)"),
            ("mobber", "capacity", "50 (bestiary n=50)"),
            ("mobber", "solitary.speed", "30: roost flutter (bestiary home 30 u/s)"),
            ("mobber", "solitary.w_flee", "0: a mobber is bold - it never flees a pilot"),
            ("mobber", "solitary.w_alarm", "0: provocation, not alarm, is its signal"),
            ("mobber", "solitary.w_threat", "0"),
            ("mobber", "solitary.w_home", "3: it stays at its roost until provoked (bestiary home)"),
            ("mobber", "solitary.size", "1.5 (bestiary size 1.5)"),
            ("mobber", "gregarious.speed", "110 (bestiary MAXV)"),
            ("mobber", "gregarious.turn", "6: agile in the mob"),
            ("mobber", "gregarious.accel", "500 (bestiary steer 500)"),
            ("mobber", "gregarious.w_food", "0: a mob does not graze"),
            ("mobber", "gregarious.w_coh", "0.1"),
            ("mobber", "gregarious.w_align", "0"),
            ("mobber", "gregarious.w_wander", "0.05"),
            ("mobber", "gregarious.w_flee", "0"),
            ("mobber", "gregarious.w_alarm", "0"),
            ("mobber", "gregarious.w_threat", "0"),
            ("mobber", "gregarious.w_ring", "2: the mob ORBITS you (bestiary orbit)"),
            ("mobber", "gregarious.ring_r", "30 u orbit (bestiary)"),
            ("mobber", "gregarious.size", "1.8: puffed in the mob"),
            ("mobber", "sense", "300: a pilot inside 300 u can provoke it (bestiary)"),
            ("mobber", "aggr_base", "1: a mob always means it (its ring/strike weights gate the aggression)"),
            ("mobber", "q_w_dens", "0: the mob reads PROVOCATION, not crowding"),
            ("mobber", "q_w_provoke", "1: provoked = a slow pilot (< 100 u/s) or one inside 260 u of the roost (bestiary)"),
            ("mobber", "q_hunger", "0: provocation is hunger-independent"),
            ("mobber", "q_up", "0.4: mob above 0.4 (bestiary m > 0.4)"),
            ("mobber", "q_down", "0.4"),
            ("mobber", "q_rate", "2: the drive relaxes at 2/s (bestiary)"),
            ("mobber", "q_contagion", "0.9: a bird follows the loudest of its neighbours at 0.9 (bestiary)"),
            ("mobber", "nbr_r", "120: the neighbours it hears (bestiary 120 u)"),
            ("mobber", "ring_roles", "50: one orbit bearing per bird"),
            ("mobber", "deposit_alarm", "0: a mob does not spread alarm (it would stampede the herds)"),
            ("mobber", "ramp_r", "80: the pull-up starts inside 80 u (bestiary)"),
            ("mobber", "ramp_s", "0.8 s pull-up before the dive (bestiary PULL) - the telegraph"),
            ("mobber", "ramp_speed", "0.45: it hangs in the pull-up (bestiary 50 u/s of 110)"),
            ("mobber", "w_strike", "4: the dive aims at you"),
            ("mobber", "strike_speed", "160 u/s dive (bestiary DIVE_V)"),
            ("mobber", "strike_accel", "500"),
            ("mobber", "hunt_lead_max", "0.5: a dive leads you a little"),
            ("mobber", "stamina_s", "0.6 s dive (bestiary)"),
            ("mobber", "rest_s", "1.9 s back in the orbit: pull 0.8 + dive 0.6 + 1.9 = the bestiary's 3.3 s rhythm (clock 2.5 + PULL)"),
            ("mobber", "rest_speed", "1: it keeps orbiting between dives"),
            ("mobber", "rest_holds_phase", "1: after a dive it stays in the mob"),
            ("mobber", "bite_r", "6: a peck lands inside radius + 6 (bestiary)"),
            ("mobber", "bite_cool", "1: one contact per second per vessel (burn rules)"),
            ("mobber", "contact_weight", "0.25: a peck is a DRAIN, a quarter of a bite (burn rules)"),
            ("mobber", "ramp_turns", "3: the birds dive IN TURN (bestiary: a staggered rhythm, one pulling up above you at a time) - at most three wind up or dive at one hull at once, so the swirl reads and its few divers are the ones the proxy cap must cover"),
            ("mobber", "w_jink", "8: a pilot pointing at it inside 60 u makes it break sideways (bestiary jink: +140 u/s aside, over a 110 u/s cap - it must outvote the mob's hunt and dive)"),

            // ── the leech (bestiary leech.py; no research substrate set) ──
            ("leech", "n0", "48: twelve puddles of four (bestiary 64 in 16 puddles)"),
            ("leech", "capacity", "64 (bestiary n=64)"),
            ("leech", "solitary.speed", "12 u/s puddle drift (bestiary)"),
            ("leech", "solitary.turn", "2"),
            ("leech", "solitary.accel", "60 (bestiary free steer 60)"),
            ("leech", "solitary.w_coh", "0.5: puddles hold together (bestiary cohesion within 60 u)"),
            ("leech", "solitary.w_wander", "0.5"),
            ("leech", "solitary.w_flee", "0: curious, clingy - it never flees"),
            ("leech", "solitary.w_alarm", "0"),
            ("leech", "solitary.w_threat", "0"),
            ("leech", "solitary.w_home", "1: it stays in its puddle"),
            ("leech", "solitary.size", "2"),
            ("leech", "gregarious.speed", "150 u/s pounce (bestiary)"),
            ("leech", "gregarious.turn", "6"),
            ("leech", "gregarious.accel", "900 (bestiary pounce steer 900)"),
            ("leech", "gregarious.w_food", "0"),
            ("leech", "gregarious.w_coh", "0"),
            ("leech", "gregarious.w_align", "0"),
            ("leech", "gregarious.w_sep", "0.2"),
            ("leech", "gregarious.w_wander", "0"),
            ("leech", "gregarious.w_flee", "0"),
            ("leech", "gregarious.w_alarm", "0"),
            ("leech", "gregarious.w_threat", "0"),
            ("leech", "gregarious.w_hunt", "3: the pounce aims at your lead point (bestiary)"),
            ("leech", "gregarious.size", "2.5"),
            ("leech", "sense", "140: it pounces on a pilot inside 140 u (bestiary SENSE)"),
            ("leech", "aggr_base", "1: a pounce always means it"),
            ("leech", "q_w_dens", "0: the pounce reads PROXIMITY"),
            ("leech", "q_w_prox", "1"),
            ("leech", "q_hunger", "0"),
            ("leech", "q_up", "0.02: any pilot inside SENSE (bestiary dist < SENSE)"),
            ("leech", "q_down", "0.01"),
            ("leech", "q_width", "0.01"),
            ("leech", "q_rate", "4: a pounce is instant"),
            ("leech", "q_contagion", "0"),
            ("leech", "attn_r", "200: steered every tick near a pilot"),
            ("leech", "deposit_alarm", "0"),
            ("leech", "hunt_lead_max", "0.8: aim at pilot + velocity x clip(distance / 150, 0, 0.8) (bestiary)"),
            ("leech", "stamina_s", "1.4 s hop (bestiary)"),
            ("leech", "rest_s", "1: then it recovers (bestiary hop +0.4/s)"),
            ("leech", "bite_r", "5: it latches inside hull radius + size + 3 (bestiary)"),
            ("leech", "cling_max", "6 per hull (bestiary MAX_PER_HULL)"),
            ("leech", "contact_weight", "0: its plate never burns - a leech harms only by SIPPING (a drain, sip_weight)"),

            // ── the leviathan (research species.py leviathan: the substrate's BODY layer, + the bestiary's gulp) ──
            ("leviathan", "n0", "96 members: the body plan is manta_slots(96) (research binds K to n0)"),
            ("leviathan", "capacity", "128: room to breed past the plan (two members may share a slot, research i % K)"),
            ("leviathan", "body_scale", "2: a 120 u manta (bestiary: majestic, a body you keep clear of; research 60 u)"),
            ("leviathan", "danger_attached", "1: an assembled member burns to touch (bestiary leviathan)"),
            ("leviathan", "body_curious", "0.8: the body turns toward a pilot inside 700 u (bestiary curiosity)"),
            ("leviathan", "gulp_r", "220: a pilot ahead of the mouth inside 220 u (cos > 0.7) gets the gulp: jaws flare 1.2 s, surge 115 u/s for 1.6 s, rest 4 s (bestiary)"),
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
            return p;
        }

        /// <summary>The stampede: the research herd (an ALARM quorum flips it from grazing to a tight, fast run that
        /// tramples) plus the bestiary's BULLS - every 4th animal climbs the herd's alarm gradient to the threat, lowers
        /// its head for 0.9 s, and charges.</summary>
        public static SubstrateSpeciesParams GameStampede()
        {
            var p = Stampede();
            p.N0 = 48; p.Capacity = 72;
            p.ChargeEvery = 4; p.WAlarmClimb = 2f; p.RampR = 260f; p.RampOnSight = true; p.RampS = 0.9f; p.RampSpeed = 0.2f; p.WStrike = 3f;
            p.StrikeSpeed = 170f; p.StrikeAccel = 400f; p.StrikeTurn = 2.4f; p.HuntLeadMax = 1.2f; p.StaminaS = 1.5f; p.RestS = 4f;
            p.TrampleClose = 0.3f; p.BiteR = 16f;
            return p;
        }

        /// <summary>The mobber: roosting birds that mob a SLOW pilot (or one near the roost) - an orbit at 30 u, a 0.8 s
        /// pull-up, a 0.6 s dive that pecks (a drain, a quarter of a bite), 1.9 s back in the orbit.</summary>
        public static SubstrateSpeciesParams GameMobber()
        {
            var p = MobberBase();
            p.N0 = 40; p.Capacity = 50;
            p.Solitary.Speed = 30f; p.Solitary.WFlee = 0f; p.Solitary.WAlarm = 0f; p.Solitary.WThreat = 0f; p.Solitary.WHome = 3f;
            p.Solitary.Size = 1.5f;
            var g = p.Solitary;
            g.Speed = 110f; g.Turn = 6f; g.Accel = 500f; g.WFood = 0f; g.WCoh = 0.1f; g.WAlign = 0f; g.WWander = 0.05f;
            g.WRing = 2f; g.RingR = 30f; g.WHome = 0f; g.Size = 1.8f;
            p.Gregarious = g;
            p.Sense = 300f; p.AggrBase = 1f; p.QWDens = 0f; p.QWProvoke = 1f; p.QHunger = 0f; p.QUp = 0.4f; p.QDown = 0.4f;
            p.QRate = 2f; p.QContagion = 0.9f; p.NbrR = 120f; p.RingRoles = 50; p.DepositAlarm = 0f;
            p.RampR = 80f; p.RampS = 0.8f; p.RampSpeed = 0.45f; p.WStrike = 4f; p.StrikeSpeed = 160f; p.StrikeAccel = 500f;
            p.HuntLeadMax = 0.5f; p.StaminaS = 0.6f; p.RestS = 1.9f; p.RestSpeed = 1f; p.RestHoldsPhase = true;
            p.BiteR = 6f; p.BiteCool = 1f; p.ContactWeight = 0.25f; p.WJink = 8f; p.RampTurns = 3;
            return p;
        }

        /// <summary>The leech: puddles of cute drifters on the mass clumps that POUNCE on a pilot inside 140 u, latch onto
        /// the hull (at most six), ride it and sip every 1.5 s - until a hard turn shakes them off.</summary>
        public static SubstrateSpeciesParams GameLeech()
        {
            var p = LeechBase();
            p.N0 = 48; p.Capacity = 64;
            p.Solitary.Speed = 12f; p.Solitary.Turn = 2f; p.Solitary.Accel = 60f; p.Solitary.WCoh = 0.5f; p.Solitary.WWander = 0.5f;
            p.Solitary.WFlee = 0f; p.Solitary.WAlarm = 0f; p.Solitary.WThreat = 0f; p.Solitary.WHome = 1f; p.Solitary.Size = 2f;
            var g = p.Solitary;
            g.Speed = 150f; g.Turn = 6f; g.Accel = 900f; g.WFood = 0f; g.WCoh = 0f; g.WAlign = 0f; g.WSep = 0.2f; g.WWander = 0f;
            g.WHunt = 3f; g.WHome = 0f; g.Size = 2.5f;
            p.Gregarious = g;
            p.Sense = 140f; p.AggrBase = 1f; p.QWDens = 0f; p.QWProx = 1f; p.QHunger = 0f; p.QUp = 0.02f; p.QDown = 0.01f;
            p.QWidth = 0.01f; p.QRate = 4f; p.QContagion = 0f; p.AttnR = 200f; p.DepositAlarm = 0f;
            p.HuntLeadMax = 0.8f; p.StaminaS = 1.4f; p.RestS = 1f; p.BiteR = 5f; p.ClingMax = 6; p.ContactWeight = 0f;
            return p;
        }

        /// <summary>The leviathan: the research's grazer school that ASSEMBLES into a manta on satiety (the substrate's
        /// body layer), scaled to 120 u; assembled, it burns to touch, turns curious toward a pilot and GULPS one ahead
        /// of its mouth (bestiary).</summary>
        public static SubstrateSpeciesParams GameLeviathan()
        {
            var p = Leviathan();
            p.N0 = 96; p.Capacity = 128; p.BodyScale = 2f; p.DangerAttached = true; p.BodyCurious = 0.8f; p.GulpR = 220f;
            return p;
        }

        /// <summary>Every species the game ships, in population order.</summary>
        public static readonly string[] Names = { "locust", "pack", "lurker", "stampede", "mobber", "leech", "leviathan" };

        /// <summary>Species with a research SUBSTRATE set (species.py) - asserted number for number (harness F).</summary>
        public static readonly string[] ResearchNames = { "locust", "pack", "lurker", "stampede", "leviathan" };

        /// <summary>
        /// The numbers a port takes from the BESTIARY (bestiary/species/*.py, burn-rules.md) rather than the substrate
        /// research: (species, the fixture's key under research_params.json "bestiary", the game field it sets). The
        /// harness reads each constant out of the bestiary source (research_fixture.py) and asserts the game value.
        /// </summary>
        public static readonly (string species, string key, string field)[] BestiaryPorts =
        {
            ("stampede", "stampede.BULL_R", "ramp_r"), ("stampede", "stampede.HEAD_DOWN", "ramp_s"),
            ("stampede", "stampede.charge_speed", "strike_speed"), ("stampede", "stampede.charge_s", "stamina_s"),
            ("stampede", "stampede.charge_accel", "strike_accel"), ("stampede", "stampede.rest_s", "rest_s"),
            ("stampede", "stampede.bull_every", "charge_every"), ("stampede", "stampede.lead_clip", "hunt_lead_max"),
            ("stampede", "stampede.closing", "trample_close"), ("stampede", "stampede.n", "capacity"),
            ("mobber", "mobber.MAXV", "gregarious.speed"), ("mobber", "mobber.DIVE_V", "strike_speed"),
            ("mobber", "mobber.PULL", "ramp_s"), ("mobber", "mobber.dive_s", "stamina_s"),
            ("mobber", "mobber.period", "dive_period"), ("mobber", "mobber.provoke_r", "sense"),
            ("mobber", "mobber.slow", "slow_below"), ("mobber", "mobber.roost_r", "roost_r"),
            ("mobber", "mobber.orbit_r", "gregarious.ring_r"), ("mobber", "mobber.pull_r", "ramp_r"),
            ("mobber", "mobber.jink_cos", "jink_cos"), ("mobber", "mobber.jink_r", "jink_r"),
            ("mobber", "mobber.peck_r", "bite_r"), ("mobber", "mobber.n", "capacity"), ("mobber", "mobber.mob_m", "q_up"),
            ("mobber", "mobber.loud", "q_contagion"), ("mobber", "mobber.hear_r", "nbr_r"), ("mobber", "mobber.m_rate", "q_rate"),
            ("mobber", "mobber.steer", "gregarious.accel"), ("mobber", "burn.drain", "contact_weight"),
            ("leech", "leech.MAX_PER_HULL", "cling_max"), ("leech", "leech.SENSE", "sense"),
            ("leech", "leech.pounce_v", "gregarious.speed"), ("leech", "leech.lead_clip", "hunt_lead_max"),
            ("leech", "leech.hop", "stamina_s"), ("leech", "leech.drift", "solitary.speed"),
            ("leech", "leech.grip_turn", "grip_turn"), ("leech", "leech.grip_loss", "grip_loss"),
            ("leech", "leech.grip_gain", "grip_gain"), ("leech", "leech.sip", "sip_s"), ("leech", "leech.daze", "daze_s"),
            ("leech", "leech.fling", "fling_speed"), ("leech", "leech.rel", "cling_rel_speed"), ("leech", "leech.n", "capacity"),
            ("leech", "leech.pounce_steer", "gregarious.accel"), ("leech", "burn.drain", "sip_weight"),
            ("leviathan", "leviathan.gulp_r", "gulp_r"), ("leviathan", "leviathan.gulp_cos", "gulp_cos"),
            ("leviathan", "leviathan.gulp_prep", "gulp_ramp_s"), ("leviathan", "leviathan.gulp_s", "gulp_s"),
            ("leviathan", "leviathan.gulp_v", "gulp_speed"), ("leviathan", "leviathan.gulp_rest", "gulp_rest_s"),
            ("leviathan", "leviathan.curious_r", "body_curious_r"), ("leviathan", "leviathan.curious_w", "body_curious"),
        };

        /// <summary>Every number of a parameter set under one flat name space (research names, primitives, "solitary." /
        /// "gregarious." regime fields, body plan, and the derived "dive_period" = ramp + strike + rest).</summary>
        public static System.Collections.Generic.Dictionary<string, float> Flatten(SubstrateSpeciesParams p)
        {
            var d = new System.Collections.Generic.Dictionary<string, float>();
            p.Visit((f, v) => d[f] = v); p.VisitPrimitives((f, v) => d[f] = v);
            p.Solitary.Visit((f, v) => d["solitary." + f] = v); p.Gregarious.Visit((f, v) => d["gregarious." + f] = v);
            d["dive_period"] = p.RampS + p.StaminaS + p.RestS;
            return d;
        }

        public static SubstrateSpeciesParams ByName(string name, bool game) => name switch
        {
            "locust" => game ? GameLocust() : Locust(),
            "pack" => game ? GamePack() : Pack(),
            "lurker" => game ? GameLurker() : Lurker(),
            "stampede" => game ? GameStampede() : Stampede(),
            "mobber" => game ? GameMobber() : MobberBase(),
            "leech" => game ? GameLeech() : LeechBase(),
            "leviathan" => game ? GameLeviathan() : Leviathan(),
            _ => throw new ArgumentException("no such substrate species: " + name),
        };
    }
}
