// Tandava's DIRECTOR (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3) - pure C#: no UnityEngine, System.Numerics
// only, so the SAME file compiles and RUNS in Tools/Build/swarm_core_harness beside the shipped sort core (mode
// `tandava`). TandavaSeveringController feeds it the swarm, the pilots and the cell's flora once per published tick and applies
// what it decides.
//
// The creature lives in a CLOSED cell - the membrane is its wall; there is no route and no exit. It decides four things:
//   * WHERE TO EAT - any plant in the cell, scored by food x safety / distance, with a stick bonus so it commits to one;
//   * HOW TO MOVE - by THREAT, read from the pilots (how near they are, how fast they close) and from its own wounds (how
//     fast it is losing members). CALM it cruises; WARY it hurries and prefers food away from the pilots. HEALTHY, it is
//     AGGRESSIVE: a pilot that comes close is LUNGED at - it bares its guard plates and charges, faster and turning
//     harder than it ever cruises. HURT, it FLEES: it bolts away from them. The speed and the turn are the sort core's
//     levers;
//   * HOW TO FEED - at the plant it settles into a meal pose (the same members re-arranged: its plates go out to orbit
//     it as DANGER guards - the protectors) and stops regrowing. A serpent ROLLS UP for each meal in a formation drawn
//     afresh each time (a flat coil, a constrictor's wrap, a figure-eight - the Great Serpent round the plant itself, so
//     most of its body is in reach of the food); a form without coils eats in its strike pose, mouth on the plant. That is
//     the one time it is both dangerous to approach and unable to heal. A meal it is hurt badly enough during is BROKEN:
//     it bolts;
//   * WHEN TO CHANGE - a full body with the form's bank in its stomach takes the next form, never mid-meal. The banked
//     Many-Headed Serpent RISES where it stands into the Lord of the Dance: a halo of rings lights round it and a drum
//     runs, and breaking enough rings breaks the dance. When the drum ends it becomes the Antlion, whose last feast
//     completes the cycle.
// A form, once taken, is REMEMBERED: the director never steps a form back. A cut limb regrows from the stomach (the sort
// core's funded laying at the wound), which is why denying food matters, and why a broken meal matters most.
// It never kills and never feeds: starvation is the swarm's own metabolism, and growth is paid for out of what it ate.
//
// SEVERING (§3.11): a cut clean through the body parts it in two, and the smaller piece crawls off as THE SEVERED - a
// second creature with a director of its own (this class, run on a one-form list whose form is
// TandavaSeveringFormRole.Severed). The two are ONE animal in two bodies: the shatter counts both, a piece that has regrown swims
// home and REJOINS the body it was cut from, and if the body is cut away to nothing while a piece lives, the piece
// SUCCEEDS it - it takes the body's form and regrows into it. So a cut is not free: sever a fed creature and both halves
// regrow out of its stomach (the hydra); only a starved one stays cut.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>The ways a Tandava match can end, from the swarm's side. Explicit values: replicated as an int.</summary>
    public enum TandavaSeveringOutcome
    {
        Running = 0,
        /// <summary>The Antlion ate its last feast: the cycle is complete. The pilots lose.</summary>
        Completed = 1,
        /// <summary>Every member is dead.</summary>
        Wiped = 2,
        /// <summary>Its body fell below the shatter threshold while it was starving.</summary>
        Starved = 3,
        /// <summary>Its body was cut below the shatter threshold.</summary>
        Shattered = 4,
        /// <summary>The pilots broke enough of the halo before the drum stopped.</summary>
        DanceBroken = 5,
        /// <summary>The match clock ran out before the cycle was complete. Unreachable at the shipped
        /// <see cref="TandavaSeveringDirectorSettings.MatchSeconds"/> 0: the hunt has no clock (the prompter's call, 2026-10-06).</summary>
        HeldOff = 6,
    }

    /// <summary>What the swarm is doing. Explicit values: replicated as an int.</summary>
    public enum TandavaSeveringPhase
    {
        /// <summary>Travelling - to a plant, away from the pilots, or wandering.</summary>
        Roam = 0,
        /// <summary>Eating at a plant in its feed pose, guards out, not regrowing.</summary>
        Feed = 1,
        /// <summary>The ascension: the Lord of the Dance assembling where the serpent rose.</summary>
        Rising = 2,
        /// <summary>The halo is lit and the drum is running.</summary>
        Dance = 3,
        Over = 4,
        /// <summary>The Severed only: regrown and fed, it swims home to rejoin the body it was cut from.</summary>
        Rejoin = 5,
        /// <summary>§3.13: on its way up to the dance it passes through the WHALE-JELLY CHIMERA - a body grown by the hybrid
        /// NCA, torn between a whale and a jelly. It drifts to where it will rise, and every few seconds it turns from one
        /// shape to the other.</summary>
        Chimera = 6,
        /// <summary>The chimera mid-turn: its members are crossing from one shape to the other - it neither heals nor
        /// moves well. The window to strike.</summary>
        ChimeraTurning = 7,
    }

    /// <summary>How threatened the swarm feels. Explicit values: replicated as an int.</summary>
    public enum TandavaSeveringMood
    {
        Calm = 0,
        Wary = 1,
        Fleeing = 2,
        /// <summary>Healthy and pressed: it turns on the nearest pilot and charges it, its guard plates out.</summary>
        Lunging = 3,
    }

    /// <summary>Why a meal ended.</summary>
    public enum TandavaSeveringMealEnd
    {
        /// <summary>It ate the meal, or what its form needed.</summary>
        Fed = 0,
        /// <summary>The plant gave it nothing for a while, or died.</summary>
        Bare = 1,
        /// <summary>Its stomach is full.</summary>
        Full = 2,
        /// <summary>The pilots hurt it badly enough during the meal: it bolts.</summary>
        Broken = 3,
        /// <summary>It sat at one plant for the longest a meal may take.</summary>
        TooLong = 4,
    }

    public enum TandavaSeveringEventKind
    {
        /// <summary>A: the form left, B: the form entered (every form change - the rise into the dance included).</summary>
        FormCommitted = 0,
        /// <summary>A: the food's id, B: the formation it rolls up in (an index into the form's coils; -1: its strike pose).</summary>
        FeedBegan = 1,
        /// <summary>A: the food's id, B: the <see cref="TandavaSeveringMealEnd"/>.</summary>
        FeedEnded = 2,
        /// <summary>A: the mood left, B: the mood entered.</summary>
        MoodChanged = 3,
        /// <summary>The banked eater rose into the dance form. A: the form left, B: the dance form.</summary>
        Rising = 4,
        /// <summary>The halo lit and the drum started. A: the dance form.</summary>
        DanceBegan = 5,
        /// <summary>A halo ring was broken. A: the ring, B: rings broken so far.</summary>
        HaloBroken = 6,
        /// <summary>A: the form it ended in, B: the <see cref="TandavaSeveringOutcome"/>.</summary>
        Ended = 7,
        /// <summary>Its jaws snapped shut at the end of a lunge. A: the form, B: how far its jaws were from the pilot
        /// (world, rounded).</summary>
        Snapped = 8,
        /// <summary>A cut parted the body: a piece crawled off as the Severed. A: members in the piece, B: the form it was
        /// cut from.</summary>
        Severed = 9,
        /// <summary>A severed piece swam home and rejoined the body. A: members it brought back.</summary>
        Rejoined = 10,
        /// <summary>The body was cut away to nothing while a piece lived: the piece took the body's form. A: the form,
        /// B: the piece's members.</summary>
        Succession = 11,
        /// <summary>The Severed only: regrown and fed, it turns for home. A: its members.</summary>
        HomeBound = 12,
        /// <summary>It became the whale-jelly chimera on its way up (§3.13). A: the form it is leaving.</summary>
        ChimeraBegan = 14,
        /// <summary>The chimera turned: A = 1 into its jelly shape, 0 into its whale shape.</summary>
        ChimeraTurned = 15,
        /// <summary>A wound has taught it something (its memory of that wound crossed <see cref="TandavaSeveringDirectorSettings.LearnedAt"/>).
        /// A: the <see cref="TandavaSeveringWound"/>.</summary>
        Learned = 13,
    }

    /// <summary>WOUND MEMORY (TandavaSevering/TANDAVA_SEVERING.md §3.12): how the creature has been hurt - what it was doing when it lost members -
    /// which it remembers for the rest of the match (a succession included) and adapts to, so the same kill does not work
    /// twice.</summary>
    public enum TandavaSeveringWound
    {
        /// <summary>Struck while it ate: it bolts from the table sooner, and picks plants farther from the pilots.</summary>
        Feeding = 0,
        /// <summary>Punished while it lunged: it lunges only when nearly whole, and less often.</summary>
        Lunging = 1,
        /// <summary>Run down while it roamed: it senses pilots from farther, and runs sooner.</summary>
        Chased = 2,
        /// <summary>Cut in two: every later piece turns for home sooner.</summary>
        Severed = 3,
    }

    public struct TandavaSeveringEvent
    {
        public TandavaSeveringEventKind Kind;
        public int A, B;
    }

    /// <summary>What a form does in the story.</summary>
    public enum TandavaSeveringFormRole
    {
        /// <summary>It eats, and banks its way to the next form.</summary>
        Eater = 0,
        /// <summary>The ascension: it does not eat; it stands in its halo until the drum ends.</summary>
        Dance = 1,
        /// <summary>The last form: it eats, and its bank is the FEAST that completes the cycle.</summary>
        Final = 2,
        /// <summary>A severed piece (§3.11): it eats and hunts like an eater, but its "next form" is HOME - once its body is
        /// full and its bank eaten it swims back and rejoins the body it was cut from. It is never shattered: a piece is
        /// whittled away, or starves.</summary>
        Severed = 3,
    }

    /// <summary>One form the swarm takes, in order. Counts are GAME members (plan units x density); lengths are world.</summary>
    public sealed class TandavaSeveringForm
    {
        public string Name = "";
        public TandavaSeveringFormRole Role;
        /// <summary>Its travel plan's index in the swarm's scripted plan list.</summary>
        public int PlanIndex;
        /// <summary>Its strike pose's index - the guards out round its jaws: its lunge, and its meal pose when it has no
        /// coils (-1: a form that does not eat).</summary>
        public int FeedPlanIndex = -1;
        /// <summary>The formations it rolls up in to eat, one drawn per meal (empty: it eats in its strike pose).</summary>
        public int[] CoilPlanIndices = Array.Empty<int>();
        /// <summary>Where the plant sits in each of <see cref="CoilPlanIndices"/> (world units along the body axes from
        /// its centre).</summary>
        public Vector3[] CoilMouths = Array.Empty<Vector3>();
        /// <summary>How far from the cell's centre its centre may go to roll up round a plant (world): the wall less its
        /// coils' reach. A coiled body is a third the size of a swimming one, so it can reach a plant by the wall that
        /// <see cref="TandavaSeveringDirectorSettings.RoamRadius"/> (the wall less the swimming body's reach) keeps it off.</summary>
        public float CoilRoamRadius;
        /// <summary>Its own LUNGE (charge) pose's index (-1: it lunges in its strike pose, <see cref="FeedPlanIndex"/>) - the
        /// Many-Headed Serpent's every head reared back, the Antlion's jaws held wide, danger plates on the weapon.</summary>
        public int LungePlanIndex = -1;
        /// <summary>The strike that ends it (-1: none), committed as the weapon reaches the pilot: every head thrown
        /// forward at once, or the jaws SNAPPED shut.</summary>
        public int SnapPlanIndex = -1;
        /// <summary>Where its jaws meet in <see cref="LungePlanIndex"/> (world units along the body axes from its centre):
        /// what a lunge aims at the pilot.</summary>
        public Vector3 LungeMouth;
        /// <summary>Members of its full body.</summary>
        public int PlanCount;
        /// <summary>The body must be at least this share of <see cref="PlanCount"/> to take the next form.</summary>
        public float FillToEvolve = 0.9f;
        /// <summary>Banked stomach volume (every element together) the swarm must hold to take the next form - or, for the
        /// <see cref="TandavaSeveringFormRole.Final"/> form, to complete the cycle. A TOTAL, not per element: what it eats is
        /// whatever the cell grows, and a bank in one element would clog on the other's food.</summary>
        public float Bank;
        /// <summary>What one meal eats (flora volume): it leaves a plant once it has eaten this much there.</summary>
        public float MealVolume = float.PositiveInfinity;
        /// <summary>Where its mouth is, in WORLD units along the body axes (x forward, y up, z side) from its centre: in
        /// the travel pose (how it lines up on a plant) and in the feed pose (where the plant must sit while it eats).</summary>
        public Vector3 Mouth, FeedMouth;
        /// <summary>§3.13: the whale-jelly chimera this form passes through on its way up to the dance - its whale shape's
        /// and its jelly shape's plan indices (the same members re-arranged), or -1 for none (it rises straight).</summary>
        public int ChimeraPlanIndex = -1, ChimeraAltPlanIndex = -1;
    }

    /// <summary>A plant the swarm could eat (the glue lists only what it CAN eat).</summary>
    public struct TandavaSeveringFood
    {
        /// <summary>Stable while the plant lives (the glue's own key).</summary>
        public int Id;
        public Vector3 Position;
        /// <summary>Edible volume left on it.</summary>
        public float Volume;
    }

    /// <summary>A pilot, as the swarm senses it.</summary>
    public struct TandavaSeveringPilot
    {
        public Vector3 Position, Velocity;
    }

    /// <summary>
    /// Every dial the director runs on. Serializable as plain fields so <c>TandavaSeveringSettingsSO.Director</c> carries the SAME
    /// class the harness constructs - one place for a number, read by the game, the harness and the generator alike
    /// (author_tandava_assets.py authors the asset's block from these defaults and asserts it).
    /// </summary>
    [Serializable]
    public sealed class TandavaSeveringDirectorSettings
    {
        // Tandava's dials carry Tandava's values (TandavaDirectorSettings: the world grown x 2.044 round the one heart
        // size, Docs/ECOSYSTEM.md 64.6). The fork's own follow the same rule: a body-sized distance (RejoinReach; SeverLink
        // was also re-measured) and a morph or a journey (the chimera's turns, the Severed's time apart) x 2.044, a volume
        // (RejoinBank) x 6.38; the sever cooldown, the wound memory and every fraction are unchanged.
        // ── the arena
        /// <summary>The cell's centre (world) - set at runtime from the cell, never authored.</summary>
        [NonSerialized] public Vector3 Centre;
        /// <summary>The swarm's centre stays within this of the cell's centre (world): the membrane less the body's reach.</summary>
        public float RoamRadius = 900f;

        // ── threat
        /// <summary>A pilot is felt within this of the swarm's centre (world).</summary>
        public float SenseRadius = 919.8f;
        /// <summary>A pilot closing at this speed (world units/s) reads as a full charge.</summary>
        public float ApproachSpeed = 80f;
        /// <summary>Losing this share of its full body per second reads as full threat.</summary>
        public float DamageRef = 0.04f;
        /// <summary>Seconds the threat takes to rise to a new reading, and to fall from one.</summary>
        public float ThreatRiseSeconds = 0.25f, ThreatFallSeconds = 2.5f;
        /// <summary>Mood thresholds on the threat, with hysteresis (enter above, leave below).</summary>
        public float WaryEnter = 0.3f, WaryExit = 0.2f, FleeEnter = 0.65f, FleeExit = 0.4f;
        /// <summary>Once it bolts it runs at least this long.</summary>
        public float FleeMinSeconds = 8.176f;
        /// <summary>How far ahead it aims when it bolts (world).</summary>
        public float FleeReach = 1022f;

        // ── aggression: a healthy creature turns on a pilot that comes close; a hurt one runs
        /// <summary>A pilot within this of its centre can be lunged at (world).</summary>
        public float LungeRadius = 776.7f;
        /// <summary>It lunges while its body is at least this share of its form; below it, it is HURT and flees instead.</summary>
        public float LungeBodyMin = 0.7f;
        /// <summary>The threat at which a healthy creature lunges - well before a hurt one would bolt.</summary>
        public float LungeEnter = 0.25f;
        /// <summary>The longest one lunge lasts, and the breath it takes before the next.</summary>
        public float LungeSeconds = 5.11f, LungeCooldownSeconds = 12.26f;
        /// <summary>How far ahead of a pilot it aims: seconds of the pilot's own velocity.</summary>
        public float LungeLead = 0.35f;
        /// <summary>Its mouth this close to the pilot (world): it has struck, and the lunge ends.</summary>
        public float LungeReach = 81.76f;
        /// <summary>A form with a strike pose of its own (<see cref="TandavaSeveringForm.SnapPlanIndex"/>: the heads thrown, the jaws
        /// snapped) commits it when its weapon comes this close to the pilot (world) - a beat before it reaches - or this
        /// long before a lunge runs out, and holds it this long: a strike reads whether it lands or not.</summary>
        public float SnapReach = 306.6f, SnapLeadSeconds = 1.022f, SnapHoldSeconds = 2.657f;
        /// <summary>Its turn while its jaws snap shut, x the config's TurnPerStep: barely, it is committed to the bite.</summary>
        public float TurnSnap = 0.3f;

        // ── the levers, per mood: x the config's Cruise and TurnPerStep
        public float CruiseCalm = 1f, CruiseWary = 1.5f, CruiseFlee = 2.1f, CruiseFeed = 0.5f, CruiseLunge = 2.4f;
        public float TurnCalm = 1f, TurnWary = 1.6f, TurnFlee = 2.5f, TurnFeed = 1f, TurnLunge = 3f;

        // ── where to eat
        /// <summary>A pilot within about this of a plant makes it unsafe (world).</summary>
        public float SafeRadius = 715.4f;
        /// <summary>How much a nearby pilot spoils a plant, calm and wary (0 = not at all, 1 = completely).</summary>
        public float FearCalm = 0.5f, FearWary = 0.9f;
        /// <summary>Added to a plant's distance before dividing, so the nearest plant is not chosen at any cost.</summary>
        public float DistanceBias = 613.2f;
        /// <summary>The plant it is already heading for scores this many times higher: it commits.</summary>
        public float StickBonus = 1.3f;
        /// <summary>A plant with less than this to eat is not worth the trip.</summary>
        public float MinFood = 1276f;
        /// <summary>A plant it left is not revisited for this long (it has to grow back).</summary>
        public float RestSeconds = 25f;
        /// <summary>With nothing to eat it wanders to a point at least this far away (world).</summary>
        public float WanderReach = 715.4f;

        // ── feeding
        /// <summary>It starts eating when its mouth is within this of the plant (world).</summary>
        public float ArriveMargin = 143.1f;
        /// <summary>Losing this share of its full body during one meal breaks the meal: it bolts.</summary>
        public float MealBreakFraction = 0.2f;
        /// <summary>A plant it has had no bite from for this long is bare (and one it has sat parked short of, unable to get
        /// its mouth to, for this long is out of its reach)...</summary>
        public float GiveUpSeconds = 6f;
        /// <summary>...counted only after this long at the plant: the body needs a few seconds to settle into its feed pose
        /// (the heads to reach the food) before a quiet mouth means anything.</summary>
        public float FeedSettleSeconds = 8.176f;
        /// <summary>The longest one meal may take.</summary>
        public float MaxFeedSeconds = 30f;
        /// <summary>It stops eating once its stomach is this full (0..1).</summary>
        public float LeaveWhenStomachFill = 0.98f;

        // ── the shatter: the pilots' strike
        /// <summary>The body is shattered below this share of its form's full body...</summary>
        public float ShatterFraction = 0.35f;
        /// <summary>...once it has held this share (so a body still growing into a new form never reads as shattered).</summary>
        public float ArmFraction = 0.6f;

        // ── the ascension
        /// <summary>Seconds the Lord of the Dance takes to assemble before the halo lights.</summary>
        public float RiseSeconds = 10.22f;
        /// <summary>§3.13: how long it is the whale-jelly chimera before it rises (a form with a chimera only).</summary>
        public float ChimeraSeconds = 49.06f;
        /// <summary>The chimera turns from one shape to the other this often...</summary>
        public float ChimeraFlipSeconds = 12.26f;
        /// <summary>...and each turn leaves it unable to heal or swim well for this long - the window to strike.</summary>
        public float ChimeraTurnSeconds = 5.11f;
        /// <summary>...and while it turns it is BRITTLE: it shatters below this share of its body, not ShatterFraction.
        /// Wear it down between turns and finish it mid-turn ("strike it as it turns").</summary>
        public float ChimeraShatterFraction = 0.5f;
        /// <summary>The chimera's speed and turn (the levers' scale): it staggers rather than swims.</summary>
        public float CruiseChimera = 0.7f;
        public float TurnChimera = 0.8f;
        /// <summary>How long the drum runs: the pilots' window to break the halo.</summary>
        public float DrumSeconds = 61.32f;
        public int HaloCount = 12, HaloToBreak = 9;
        /// <summary>The dance's reach from its centre (the halo and the guard posts, world): the dance ground is kept this
        /// far inside the roam radius.</summary>
        public float DanceReach = 511f;

        // ── severing (§3.11)
        /// <summary>Two live members closer than this are one piece of the body (world). A cut that leaves a gap wider than
        /// this has parted it. 8 plan voxels (x UnitScale 4.088): the research 13 u (6.5 voxels) tore the Ten-Headed
        /// Serpent's coils into two pieces at most moments - harness T20 passed only at its 6 s snapshot, and failed at
        /// 9, 12 and 25 s even at the research body - while at 8 voxels no whole body, in any pose but the dance and the
        /// lunges, leaves a knot of more than 17 members, and a 30% slice still parts every form tested.</summary>
        public float SeverLink = 32.7f;
        /// <summary>A piece must hold at least this share of its form's full body, and at least this many members, to crawl
        /// off as the Severed (a smaller knot is just loose flesh: it stays, and the body buds over the wound).</summary>
        public float SeverMinShare = 0.12f;
        public int SeverMinMembers = 45;
        /// <summary>A piece holding more than this share of the form is not a piece - it is the body, cut in half; the
        /// body keeps it (tandava_plans.SEVER_MAX_SHARE sizes the Severed's plan to hold this much of the biggest form).</summary>
        public float SeverMaxShare = 0.4f;
        /// <summary>After a sever the body cannot part again for this long (one piece at a time - the glue also allows at
        /// most one Severed alive).</summary>
        public float SeverCooldownSeconds = 8f;
        /// <summary>A Severed turns for home once it has regrown this share of its full body...</summary>
        public float RejoinFill = 0.75f;
        /// <summary>...and banked this much stomach volume (a meal of its own: it hunts and eats before it goes home)...</summary>
        public float RejoinBank = 6380f;
        /// <summary>...or, whatever it has managed, once it has been apart this long: the pilots' window to finish it is
        /// a clock they can see (the HUD counts it down).</summary>
        public float RejoinAfterSeconds = 81.76f;
        /// <summary>A homebound piece rejoins the body when their centres are this close (world).</summary>
        public float RejoinReach = 183.96f;

        // ── wound memory (§3.12)
        /// <summary>How much of its form's body, lost one way, makes that wound's memory 63% of its full weight (memory =
        /// 1 - exp(-lost share / this)); severs are remembered by count (LearnRejoin). 0 = it never learns.</summary>
        public float ScarRef = 0.15f;
        /// <summary>The memory at which it has LEARNED a wound (the narrator says so, once per wound).</summary>
        public float LearnedAt = 0.5f;
        /// <summary>Struck at the table: the share a meal may cost before it bolts falls by this much at full memory...</summary>
        public float LearnMealBreak = 0.6f;
        /// <summary>...and how far a pilot spoils a plant (SafeRadius) grows by this much; its fear of them is never less
        /// than its wary fear, scaled by the memory.</summary>
        public float LearnSafeRadius = 1.5f;
        /// <summary>Punished in a lunge: the body it must have to lunge rises by this share of its form at full memory...</summary>
        public float LearnLungeBody = 0.25f;
        /// <summary>...and its lunge cooldown grows by this much.</summary>
        public float LearnLungeCooldown = 2f;
        /// <summary>Run down: how far it senses pilots grows by this much at full memory, and the threat it runs at falls by
        /// LearnFlee (a hurt creature runs; a scarred one runs sooner).</summary>
        public float LearnSense = 0.4f;
        public float LearnFlee = 0.35f;
        /// <summary>Cut in two: each sever after the first multiplies the next piece's time apart by this.</summary>
        public float LearnRejoin = 0.6f;

        // ── the match
        /// <summary>The cycle must complete within this many seconds of the go, or the pilots have held it off. 0 = no clock -
        /// the shipped hunt: it ends when the creature is broken or its cycle is complete, never on a timer.</summary>
        public float MatchSeconds = 0f;

        /// <summary>A copy to run a match on (the game's settings asset is never written at runtime).</summary>
        public TandavaSeveringDirectorSettings Clone() => (TandavaSeveringDirectorSettings)MemberwiseClone();
    }

    /// <summary>What the director needs to know about the swarm, sampled once per tick by the glue.</summary>
    public struct TandavaSeveringSwarmState
    {
        public int Alive;
        /// <summary>Members lost since it hatched, to anything (monotone).</summary>
        public int Lost;
        public Vector3 Anchor;
        /// <summary>The body's axes (world, unit): its heading, its up, its side.</summary>
        public Vector3 Forward, Up, Side;
        /// <summary>Banked eaten volume per research element (0 Charge .. 3 Time).</summary>
        public float Stomach0, Stomach1, Stomach2, Stomach3;
        /// <summary>The stomach's fill, 0..1.</summary>
        public float StomachFill;
        /// <summary>Seconds since it last took a bite.</summary>
        public float SinceBite;
        /// <summary>Flora volume eaten since it hatched (monotone).</summary>
        public float EatenTotal;
        /// <summary>True while its own unfed clock has run out (SwarmFauna.IsStarving).</summary>
        public bool Starving;
        /// <summary>Live members of the creature's OTHER bodies - its severed piece (for the body), or the body (for a piece;
        /// unused). The shatter counts the whole animal: a body cut small while its piece lives is not broken.</summary>
        public int Severed;
        public float Stomach(int e) => e switch { 0 => Stomach0, 1 => Stomach1, 2 => Stomach2, _ => Stomach3 };
    }

    public sealed class TandavaSeveringDirectorCore
    {
        public readonly IReadOnlyList<TandavaSeveringForm> Forms;
        public readonly TandavaSeveringDirectorSettings S;
        public readonly List<TandavaSeveringEvent> Events = new();

        public int FormIx { get; private set; }
        public TandavaSeveringPhase Phase { get; private set; } = TandavaSeveringPhase.Roam;
        public TandavaSeveringMood Mood { get; private set; } = TandavaSeveringMood.Calm;
        public TandavaSeveringOutcome Outcome { get; private set; } = TandavaSeveringOutcome.Running;
        /// <summary>0..1, smoothed: the larger of the pilots' pressure and the wound rate.</summary>
        public float Threat { get; private set; }
        /// <summary>Seconds since the go.</summary>
        public float Clock { get; private set; }
        /// <summary>Where the body swims (world).</summary>
        public Vector3 Goal { get; private set; }
        /// <summary>The food it is going to or eating (-1: none).</summary>
        public int TargetFood { get; private set; } = -1;
        /// <summary>The levers this tick: x cruise, x turn, and whether laying is held.</summary>
        public float CruiseScale { get; private set; } = 1f;
        public float TurnScale { get; private set; } = 1f;
        public bool HoldLaying { get; private set; }
        /// <summary>Volume eaten in the current meal.</summary>
        public float EatenHere { get; private set; }
        /// <summary>Where the dance stands (world; set when it rises).</summary>
        public Vector3 DancePoint { get; private set; }
        /// <summary>Seconds the drum has run (0 outside the dance).</summary>
        public float DanceTime { get; private set; }
        public int HaloOutMask { get; private set; }
        public int HaloOut { get; private set; }

        public bool Feeding => Phase == TandavaSeveringPhase.Feed;
        /// <summary>§3.13: it is the whale-jelly chimera (turning or not).</summary>
        public bool InChimera => Phase is TandavaSeveringPhase.Chimera or TandavaSeveringPhase.ChimeraTurning;
        /// <summary>The chimera wears its jelly shape (else its whale shape).</summary>
        public bool ChimeraJelly { get; private set; }
        /// <summary>Seconds of the chimera left before it rises (0 when it is not the chimera).</summary>
        public float ChimeraRemaining => InChimera ? MathF.Max(0f, S.ChimeraSeconds - (Clock - _chimeraSince)) : 0f;
        public bool InDance => Phase == TandavaSeveringPhase.Dance;
        public TandavaSeveringForm Form => Forms[FormIx];
        public int FinalIx => Forms.Count - 1;
        public bool IsFinalForm => FormIx == FinalIx;
        /// <summary>The plan the body should wear now: its coil while it eats (its strike pose, for a form without coils),
        /// while it lunges its lunge pose (the Antlion's gaping jaws, then the same jaws snapped shut) or else its strike pose (it charges a pilot with the
        /// guard plates out round its jaws), else its travel plan.</summary>
        public int WantPlan => InChimera ? (ChimeraJelly ? Form.ChimeraAltPlanIndex : Form.ChimeraPlanIndex)
            : Feeding && Coil >= 0 ? Form.CoilPlanIndices[Coil]
            : Snapping && Form.SnapPlanIndex >= 0 ? Form.SnapPlanIndex
            : Lunging && Form.LungePlanIndex >= 0 ? Form.LungePlanIndex
            : (Feeding || Lunging) && Form.FeedPlanIndex >= 0 ? Form.FeedPlanIndex : Form.PlanIndex;
        /// <summary>The formation the current (or the coming) meal rolls up in - an index into the form's
        /// <see cref="TandavaSeveringForm.CoilPlanIndices"/>, -1 for none.</summary>
        public int Coil { get; private set; } = -1;
        public bool Lunging => Mood == TandavaSeveringMood.Lunging;
        /// <summary>Its jaws are snapped shut: the end of a lunge, held <see cref="TandavaSeveringDirectorSettings.SnapHoldSeconds"/>.</summary>
        public bool Snapping => Clock < _snapUntil;
        public float DrumRemaining => InDance ? MathF.Max(0f, S.DrumSeconds - DanceTime) : 0f;
        public float RiseRemaining => Phase == TandavaSeveringPhase.Rising ? MathF.Max(0f, S.RiseSeconds - (Clock - _phaseSince)) : 0f;
        public float TimeRemaining => S.MatchSeconds > 0f ? MathF.Max(0f, S.MatchSeconds - Clock) : -1f;

        readonly Random _rng;
        readonly Dictionary<int, float> _restUntil = new();
        float _feedSince, _eatenAtMeal, _phaseSince, _fleeUntil = -1f, _lossRate, _lungeUntil = -1f, _lungeReadyAt, _snapUntil = -1f;
        int _lostAtMeal, _lostPrev = -1, _coilFor = -1, _coilForm = -1, _learnedMask, _parkedFor = -1;
        float _parkedSince;
        float _chimeraSince, _nextTurn, _turnUntil;
        bool _chimeraDone;
        /// <summary>The wounds it remembers, by <see cref="TandavaSeveringWound"/>: the share of a form's body lost each way (severs:
        /// a count).</summary>
        readonly float[] _scars = new float[4];
        bool _armed, _haveWander;
        Vector3 _wander;

        public TandavaSeveringDirectorCore(IReadOnlyList<TandavaSeveringForm> forms, TandavaSeveringDirectorSettings settings, int seed = 1)
        {
            if (forms == null || forms.Count == 0) throw new ArgumentException("Tandava needs at least one form", nameof(forms));
            Forms = forms; S = settings ?? new TandavaSeveringDirectorSettings();
            _rng = new Random(seed);
            Goal = S.Centre;
        }

        // ──────────────────────────────────────────────────────────────── the readouts

        /// <summary>How far the current form is toward its next (or, the final form, toward completing the cycle), 0..1:
        /// the lesser of its body fill and its bank. The banks RISE form by form (as shares of the stomach), so what a form
        /// carries over never skips the next one, and every member the pilots cut is regrown out of the bank before the bank
        /// is full - which is how a cut costs it time without costing it its form. The drum's progress while it dances.</summary>
        public float Progress(in TandavaSeveringSwarmState s)
        {
            var f = Form;
            if (InChimera) return S.ChimeraSeconds > 0f ? Math.Clamp((Clock - _chimeraSince) / S.ChimeraSeconds, 0f, 1f) : 1f;
            if (f.Role == TandavaSeveringFormRole.Dance)
                return Phase == TandavaSeveringPhase.Dance && S.DrumSeconds > 0f ? Math.Clamp(DanceTime / S.DrumSeconds, 0f, 1f) : 0f;
            float fill = f.PlanCount > 0 ? s.Alive / (f.FillToEvolve * f.PlanCount) : 1f;
            if (f.Role == TandavaSeveringFormRole.Severed) fill = f.PlanCount > 0 ? s.Alive / (S.RejoinFill * f.PlanCount) : 1f;
            float bank = f.Bank > 0f ? (s.Stomach0 + s.Stomach1 + s.Stomach2 + s.Stomach3) / f.Bank : 1f;
            return Math.Clamp(MathF.Min(fill, bank), 0f, 1f);
        }

        /// <summary>The body against its form's full body, 0..1+.</summary>
        public float BodyFraction(in TandavaSeveringSwarmState s) => Form.PlanCount > 0 ? s.Alive / (float)Form.PlanCount : 1f;

        // ───────────────────────────────────────────────────────────────  severing (§3.11)

        /// <summary>The Severed only: where the body it was cut from is (world) - its way home. Set by the glue every tick.</summary>
        public Vector3 Home;
        /// <summary>The Severed only: it is home-bound (regrown and fed, or out of time).</summary>
        public bool HomeBound => Phase == TandavaSeveringPhase.Rejoin;
        /// <summary>The Severed only: seconds until it turns for home whatever it has managed (0 once it has).</summary>
        public float RejoinRemaining => Form.Role == TandavaSeveringFormRole.Severed && Phase != TandavaSeveringPhase.Rejoin
            ? MathF.Max(0f, RejoinAfterNow - Clock) : 0f;
        float _severReadyAt;

        /// <summary>How well it remembers a wound, 0..1 (§3.12).</summary>
        public float Memory(TandavaSeveringWound w)
        {
            if (S.ScarRef <= 0f) return 0f;
            float scar = _scars[(int)w];
            // a sever is remembered from the second: the first piece had no lesson to come home by
            return w == TandavaSeveringWound.Severed ? (scar <= 1f ? 0f : 1f - MathF.Pow(S.LearnRejoin, scar - 1f))
                                             : 1f - MathF.Exp(-scar / S.ScarRef);
        }

        /// <summary>The raw scar (a share of a form's body; severs: a count).</summary>
        public float Scar(TandavaSeveringWound w) => _scars[(int)w];

        /// <summary>A piece remembers what its body remembers (the glue calls it as the Severed is born), and a body that
        /// takes a piece's place keeps its own memory - the director is the creature's mind.</summary>
        public void RememberFrom(TandavaSeveringDirectorCore other)
        {
            for (int k = 0; k < 4; k++) _scars[k] = MathF.Max(_scars[k], other._scars[k]);
            _learnedMask |= other._learnedMask;
        }

        void Wound(TandavaSeveringWound w, float amount)
        {
            if (amount <= 0f) return;
            _scars[(int)w] += amount;
            int bit = 1 << (int)w;
            // a sever is learned at the second (the first piece had nothing to learn from); every other wound at LearnedAt
            bool learned = w == TandavaSeveringWound.Severed ? _scars[(int)w] >= 2f && S.ScarRef > 0f : Memory(w) >= S.LearnedAt;
            if ((_learnedMask & bit) == 0 && learned && Form.Role != TandavaSeveringFormRole.Severed)
            {
                _learnedMask |= bit;
                Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.Learned, A = (int)w, B = FormIx });
            }
        }

        // the settings as its memory bends them
        float MealBreakShare => S.MealBreakFraction * (1f - S.LearnMealBreak * Memory(TandavaSeveringWound.Feeding));
        float SafeRadiusNow => S.SafeRadius * (1f + S.LearnSafeRadius * Memory(TandavaSeveringWound.Feeding));
        float LungeBodyNow => MathF.Min(0.98f, S.LungeBodyMin + S.LearnLungeBody * Memory(TandavaSeveringWound.Lunging));
        float LungeCooldownNow => S.LungeCooldownSeconds * (1f + S.LearnLungeCooldown * Memory(TandavaSeveringWound.Lunging));
        public float SenseRadiusNow => S.SenseRadius * (1f + S.LearnSense * Memory(TandavaSeveringWound.Chased));
        public float FleeEnterNow => S.FleeEnter * (1f - S.LearnFlee * Memory(TandavaSeveringWound.Chased));
        /// <summary>A piece's time apart: shorter for every sever it remembers past the first.</summary>
        public float RejoinAfterNow => S.RejoinAfterSeconds * (_scars[(int)TandavaSeveringWound.Severed] <= 1f ? 1f
            : MathF.Pow(S.LearnRejoin, _scars[(int)TandavaSeveringWound.Severed] - 1f));

        /// <summary>May the body part now? Never in the dance or the rise (the figure is one sculpture; its attendant packs
        /// already stand apart), never mid-lunge (the heads and jaws are thrown out on stretched necks for that second and
        /// would read as pieces - harness T20), never while a piece lives (s.Severed), never within the cooldown of the last sever, and
        /// never for a piece itself.</summary>
        public bool MaySever(in TandavaSeveringSwarmState s) =>
            Outcome == TandavaSeveringOutcome.Running && Form.Role is TandavaSeveringFormRole.Eater or TandavaSeveringFormRole.Final
            && Phase is TandavaSeveringPhase.Roam or TandavaSeveringPhase.Feed or TandavaSeveringPhase.Chimera or TandavaSeveringPhase.ChimeraTurning && !Lunging && !Snapping && s.Severed <= 0 && Clock >= _severReadyAt;

        /// <summary>The smallest and largest piece (members) the current form may part with (<see cref="TandavaSeveringSever.FindPiece"/>).</summary>
        public int SeverMinMembers => Math.Max(S.SeverMinMembers, (int)MathF.Ceiling(S.SeverMinShare * Form.PlanCount));
        public int SeverMaxMembers => (int)MathF.Floor(S.SeverMaxShare * Form.PlanCount);

        /// <summary>The glue parted the body: <paramref name="members"/> crawled off as the Severed. A meal it was cut at is
        /// broken, and it bolts - being cut in two is the worst wound there is.</summary>
        public void OnSevered(int members)
        {
            _severReadyAt = Clock + S.SeverCooldownSeconds;
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.Severed, A = members, B = FormIx });
            Wound(TandavaSeveringWound.Severed, 1f);
            if (Feeding) EndMeal(TandavaSeveringMealEnd.Broken);
            if (Lunging) EndLunge();
            _fleeUntil = Clock + S.FleeMinSeconds;
            Threat = MathF.Max(Threat, S.FleeEnter);
            SetMood(TandavaSeveringMood.Fleeing);
        }

        /// <summary>The glue grafted a homebound piece back on: <paramref name="members"/> came home.</summary>
        public void OnRejoined(int members) =>
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.Rejoined, A = members, B = FormIx });

        /// <summary>The body was cut away to nothing and the glue handed its piece the body: the director now runs THAT
        /// swarm, in the same form (a form is remembered - the piece regrows into it). It wakes fleeing.</summary>
        public void OnSuccession(int members)
        {
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.Succession, A = FormIx, B = members });
            if (Feeding) EndMeal(TandavaSeveringMealEnd.Broken);
            Phase = TandavaSeveringPhase.Roam; _phaseSince = Clock; TargetFood = -1;
            _armed = false;   // re-armed once the heir has regrown past the arm fraction
            _lostPrev = -1;
            _fleeUntil = Clock + S.FleeMinSeconds;
            Threat = MathF.Max(Threat, S.FleeEnter);
            SetMood(TandavaSeveringMood.Fleeing);
        }

        public bool HaloIsOut(int k) => k >= 0 && k < 32 && (HaloOutMask & (1 << k)) != 0;

        /// <summary>A pilot threaded halo ring <paramref name="k"/> while nothing guarded it (the glue decides both).
        /// Returns true when it broke. Enough broken ends the match: the dance is broken.</summary>
        public bool BreakHalo(int k)
        {
            if (!InDance || Outcome != TandavaSeveringOutcome.Running || k < 0 || k >= S.HaloCount || HaloIsOut(k)) return false;
            HaloOutMask |= 1 << k; HaloOut++;
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.HaloBroken, A = k, B = HaloOut });
            if (HaloOut >= S.HaloToBreak) End(TandavaSeveringOutcome.DanceBroken);
            return true;
        }

        /// <summary>A world offset in the body axes of <paramref name="s"/> (x forward, y up, z side).</summary>
        public static Vector3 InBody(in TandavaSeveringSwarmState s, Vector3 local) => s.Forward * local.X + s.Up * local.Y + s.Side * local.Z;

        // ──────────────────────────────────────────────────────────────── the tick

        /// <summary>One director tick. <paramref name="food"/> is every plant the swarm can eat; <paramref name="pilots"/>
        /// every live pilot. Events of this tick are appended to <see cref="Events"/>.</summary>
        public void Tick(float dt, in TandavaSeveringSwarmState s, IReadOnlyList<TandavaSeveringFood> food, IReadOnlyList<TandavaSeveringPilot> pilots)
        {
            if (Outcome != TandavaSeveringOutcome.Running) return;
            dt = MathF.Max(0f, dt);
            Clock += dt;

            // ── the outcome first. The shatter counts the WHOLE animal - the body and its severed piece - and a body cut
            // away to nothing while its piece lives is not dead: the glue hands the piece the body (OnSuccession)
            if (s.Alive <= 0)
            {
                if (s.Severed > 0 && Form.Role != TandavaSeveringFormRole.Severed) return;   // awaiting the succession
                End(s.Starving ? TandavaSeveringOutcome.Starved : TandavaSeveringOutcome.Wiped);
                return;
            }
            var form = Form;
            int whole = s.Alive + (form.Role == TandavaSeveringFormRole.Severed ? 0 : s.Severed);
            if (form.Role != TandavaSeveringFormRole.Severed)
            {
                if (!_armed && whole >= S.ArmFraction * form.PlanCount) _armed = true;
                float shatter = Phase == TandavaSeveringPhase.ChimeraTurning ? MathF.Max(S.ShatterFraction, S.ChimeraShatterFraction) : S.ShatterFraction;
                if (_armed && whole < shatter * form.PlanCount)
                {
                    End(s.Starving ? TandavaSeveringOutcome.Starved : TandavaSeveringOutcome.Shattered);
                    return;
                }
            }
            if (S.MatchSeconds > 0f && Clock >= S.MatchSeconds) { End(TandavaSeveringOutcome.HeldOff); return; }

            SenseThreat(dt, s, pilots);

            switch (Phase)
            {
                case TandavaSeveringPhase.Rejoin:
                    Goal = Home;
                    break;
                case TandavaSeveringPhase.Roam when form.Role == TandavaSeveringFormRole.Severed && Clock >= RejoinAfterNow:
                    Advance(s);
                    break;
                case TandavaSeveringPhase.Feed when form.Role == TandavaSeveringFormRole.Severed && Clock >= RejoinAfterNow:
                    EndMeal(TandavaSeveringMealEnd.Fed);
                    Advance(s);
                    break;
                case TandavaSeveringPhase.Roam:
                    if (Progress(s) >= 1f) { Advance(s); if (Outcome != TandavaSeveringOutcome.Running) return; }
                    if (Phase == TandavaSeveringPhase.Roam) Roam(s, food, pilots);
                    break;
                case TandavaSeveringPhase.Feed:
                    FeedTick(s, food);
                    if (Phase == TandavaSeveringPhase.Roam) Roam(s, food, pilots);   // the meal ended: be somewhere this tick
                    break;
                case TandavaSeveringPhase.Chimera:
                case TandavaSeveringPhase.ChimeraTurning:
                    ChimeraTick(s);
                    break;
                case TandavaSeveringPhase.Rising:
                    Goal = DancePoint;
                    if (Clock - _phaseSince >= S.RiseSeconds)
                    {
                        Phase = TandavaSeveringPhase.Dance; _phaseSince = Clock; DanceTime = 0f; HaloOut = 0; HaloOutMask = 0;
                        Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.DanceBegan, A = FormIx });
                    }
                    break;
                case TandavaSeveringPhase.Dance:
                    Goal = DancePoint;
                    DanceTime += dt;
                    if (DanceTime >= S.DrumSeconds)
                    {
                        Commit(FinalIx, s);
                        Phase = TandavaSeveringPhase.Roam; _phaseSince = Clock; DanceTime = 0f;
                        Roam(s, food, pilots);
                    }
                    break;
            }
            if (Outcome == TandavaSeveringOutcome.Running) Levers();
        }

        /// <summary>A banked form moves on: an eater to the next form (into the dance: the rise), the final form completes.</summary>
        void Advance(in TandavaSeveringSwarmState s)
        {
            if (Form.Role == TandavaSeveringFormRole.Severed)
            {
                // regrown and fed: home. It swims back at a wary pace and lets the glue graft it on when it arrives
                Phase = TandavaSeveringPhase.Rejoin; _phaseSince = Clock; TargetFood = -1; Goal = Home;
                if (Lunging) EndLunge();
                Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.HomeBound, A = s.Alive });
                return;
            }
            if (Form.Role == TandavaSeveringFormRole.Final) { End(TandavaSeveringOutcome.Completed); return; }
            int next = FormIx + 1;
            if (next >= Forms.Count) { End(TandavaSeveringOutcome.Completed); return; }
            if (Forms[next].Role == TandavaSeveringFormRole.Dance && !_chimeraDone && Form.ChimeraPlanIndex > 0 && Form.ChimeraAltPlanIndex > 0
                && S.ChimeraSeconds > 0f)
            {
                // §3.13: before it rises it is torn between a whale and a jelly - it staggers to where it will rise
                if (Lunging) EndLunge();
                DancePoint = ClampInside(s.Anchor, MathF.Max(0f, S.RoamRadius - S.DanceReach));
                Phase = TandavaSeveringPhase.Chimera; _phaseSince = Clock; _chimeraSince = Clock; TargetFood = -1;
                ChimeraJelly = false; _nextTurn = Clock + S.ChimeraFlipSeconds;
                Goal = DancePoint;
                Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.ChimeraBegan, A = FormIx });
                return;
            }
            if (Forms[next].Role == TandavaSeveringFormRole.Dance)
            {
                int from = FormIx;
                Commit(next, s);
                // it rises WHERE IT STANDS - only pulled in from the wall far enough for the halo and its guards to fit
                DancePoint = ClampInside(s.Anchor, MathF.Max(0f, S.RoamRadius - S.DanceReach));
                Phase = TandavaSeveringPhase.Rising; _phaseSince = Clock; TargetFood = -1;
                Goal = DancePoint;
                Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.Rising, A = from, B = next });
                return;
            }
            Commit(next, s);
        }

        /// <summary>§3.13: the chimera drifts to where it will rise, turning shape every few seconds; when its time is up it
        /// rises as any banked serpent does.</summary>
        void ChimeraTick(in TandavaSeveringSwarmState s)
        {
            Goal = DancePoint;
            if (Clock - _chimeraSince >= S.ChimeraSeconds)
            {
                _chimeraDone = true;
                Phase = TandavaSeveringPhase.Roam; _phaseSince = Clock;
                Advance(s);   // the rise
                return;
            }
            if (Clock >= _nextTurn)
            {
                ChimeraJelly = !ChimeraJelly;
                _nextTurn = Clock + S.ChimeraFlipSeconds;
                _turnUntil = Clock + S.ChimeraTurnSeconds;
                Phase = TandavaSeveringPhase.ChimeraTurning;
                Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.ChimeraTurned, A = ChimeraJelly ? 1 : 0 });
            }
            else if (Phase == TandavaSeveringPhase.ChimeraTurning && Clock >= _turnUntil) Phase = TandavaSeveringPhase.Chimera;
        }

        void Commit(int to, in TandavaSeveringSwarmState s)
        {
            int from = FormIx; FormIx = to;
            _chimeraDone = false;
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.FormCommitted, A = from, B = to });
            // the body it commits with counts toward the new form's arming: a cull the next second can still shatter it
            _armed = s.Alive >= S.ArmFraction * Forms[to].PlanCount;
        }

        // ──────────────────────────────────────────────────────────────── threat and mood

        void SenseThreat(float dt, in TandavaSeveringSwarmState s, IReadOnlyList<TandavaSeveringPilot> pilots)
        {
            float prox = 0f;
            if (pilots != null)
                for (int k = 0; k < pilots.Count; k++)
                {
                    var rel = pilots[k].Position - s.Anchor; float d = rel.Length();
                    float sense = SenseRadiusNow;
                    if (d >= sense) continue;
                    float near = 1f - d / MathF.Max(1f, sense);
                    float closing = d > 1e-3f ? -Vector3.Dot(pilots[k].Velocity, rel / d) : 0f;
                    float charge = Math.Clamp(closing / MathF.Max(1f, S.ApproachSpeed), 0f, 1f);
                    float w = Math.Clamp(near * (0.55f + 0.45f * charge), 0f, 1f);
                    prox = 1f - (1f - prox) * (1f - w);   // a soft OR: two pilots press harder than one
                }
            int lost = _lostPrev < 0 ? 0 : Math.Max(0, s.Lost - _lostPrev);
            _lostPrev = s.Lost;
            // §3.12: what it was doing when it was hurt is what it remembers (hunger's own sheds teach it nothing)
            if (lost > 0 && !s.Starving && Form.PlanCount > 0)
            {
                float share = lost / (float)Form.PlanCount;
                if (Feeding) Wound(TandavaSeveringWound.Feeding, share);
                else if (Lunging || Snapping) Wound(TandavaSeveringWound.Lunging, share);
                else if (Phase == TandavaSeveringPhase.Roam) Wound(TandavaSeveringWound.Chased, share);
            }
            if (dt > 0f)
            {
                float a = 1f - MathF.Exp(-dt / 0.5f);
                _lossRate += (lost / dt - _lossRate) * a;
            }
            float wound = Math.Clamp(_lossRate / MathF.Max(1f, S.DamageRef * Form.PlanCount), 0f, 1f);
            float raw = MathF.Max(prox, wound);
            if (dt > 0f)
            {
                float tau = raw > Threat ? S.ThreatRiseSeconds : S.ThreatFallSeconds;
                Threat += (raw - Threat) * (1f - MathF.Exp(-dt / MathF.Max(1e-3f, tau)));
            }
            Threat = Math.Clamp(Threat, 0f, 1f);

            // the mood, with hysteresis. Feeding it does NOT bolt from pilots it merely sees - its guards are out; only a
            // broken meal (FeedTick) sends a feeding swarm running. Dancing it stands its ground. Roaming, a HEALTHY creature
            // turns on a pilot that comes close (a lunge); only a HURT one bolts.
            var was = Mood; var mood = Mood;
            bool roaming = Phase == TandavaSeveringPhase.Roam;
            bool hurt = s.Alive < S.LungeBodyMin * Form.PlanCount;
            bool canBolt = roaming && hurt;
            float flee = FleeEnterNow;
            bool lungeHurt = s.Alive < LungeBodyNow * Form.PlanCount;   // a scarred lunger wants more of itself first
            float nearest = NearestPilot(s, pilots, out _);
            bool canLunge = roaming && !lungeHurt && Clock >= _lungeReadyAt && nearest <= S.LungeRadius;
            if (Clock < _fleeUntil) mood = TandavaSeveringMood.Fleeing;
            else if (Mood == TandavaSeveringMood.Lunging)
            {
                if (!roaming || lungeHurt || Clock >= _lungeUntil || nearest > 1.5f * S.LungeRadius) { EndLunge(); mood = Threat >= S.WaryExit ? TandavaSeveringMood.Wary : TandavaSeveringMood.Calm; }
            }
            else if (canLunge && Threat >= S.LungeEnter) { mood = TandavaSeveringMood.Lunging; _lungeUntil = Clock + S.LungeSeconds; }
            else switch (Mood)
            {
                case TandavaSeveringMood.Calm:
                    if (canBolt && Threat >= flee) mood = TandavaSeveringMood.Fleeing;
                    else if (Threat >= S.WaryEnter) mood = TandavaSeveringMood.Wary;
                    break;
                case TandavaSeveringMood.Wary:
                    if (canBolt && Threat >= flee) mood = TandavaSeveringMood.Fleeing;
                    else if (Threat < S.WaryExit) mood = TandavaSeveringMood.Calm;
                    break;
                default:
                    if (Threat < S.FleeExit || !canBolt) mood = Threat < S.WaryExit ? TandavaSeveringMood.Calm : TandavaSeveringMood.Wary;
                    break;
            }
            if (mood == TandavaSeveringMood.Fleeing && was != TandavaSeveringMood.Fleeing && _fleeUntil < Clock) _fleeUntil = Clock + S.FleeMinSeconds;
            SetMood(mood);
        }

        void EndLunge() { _lungeUntil = -1f; _lungeReadyAt = Clock + LungeCooldownNow; }

        /// <summary>The distance to the nearest pilot (infinity with none), and its index.</summary>
        static float NearestPilot(in TandavaSeveringSwarmState s, IReadOnlyList<TandavaSeveringPilot> pilots, out int ix)
        {
            ix = -1; float best = float.PositiveInfinity;
            if (pilots == null) return best;
            for (int k = 0; k < pilots.Count; k++)
            {
                float d = Vector3.Distance(pilots[k].Position, s.Anchor);
                if (d < best) { best = d; ix = k; }
            }
            return best;
        }

        void SetMood(TandavaSeveringMood mood)
        {
            if (mood == Mood) return;
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.MoodChanged, A = (int)Mood, B = (int)mood });
            Mood = mood;
        }

        void Levers()
        {
            LeversFor(Phase, Mood, S, out float cruise, out float turn, out bool hold, Snapping && Form.SnapPlanIndex >= 0);
            CruiseScale = cruise; TurnScale = turn; HoldLaying = hold;
        }

        /// <summary>The levers a phase and mood mean - the server's director and every client's swarm (which knows only
        /// the replicated phase and mood, and the plan the body wears) read the same rule. <paramref name="snapping"/>:
        /// its jaws are snapping shut - it commits to the line of the bite (it barely turns), so the long jaws close
        /// instead of trailing a hard turn.</summary>
        public static void LeversFor(TandavaSeveringPhase phase, TandavaSeveringMood mood, TandavaSeveringDirectorSettings s,
                                     out float cruise, out float turn, out bool holdLaying, bool snapping = false)
        {
            switch (phase)
            {
                case TandavaSeveringPhase.Feed:
                    cruise = s.CruiseFeed; turn = s.TurnFeed; holdLaying = true;   // still, and not healing
                    return;
                case TandavaSeveringPhase.Rising:
                case TandavaSeveringPhase.Dance:
                    cruise = s.CruiseFeed; turn = s.TurnFeed; holdLaying = false;
                    return;
                case TandavaSeveringPhase.Over:
                    cruise = 1f; turn = 1f; holdLaying = false;
                    return;
                case TandavaSeveringPhase.Rejoin:
                    cruise = s.CruiseWary; turn = s.TurnWary; holdLaying = false;
                    return;
                case TandavaSeveringPhase.Chimera:
                    cruise = s.CruiseChimera; turn = s.TurnChimera; holdLaying = false;
                    return;
                case TandavaSeveringPhase.ChimeraTurning:
                    cruise = s.CruiseFeed; turn = s.TurnFeed; holdLaying = true;   // mid-turn: it cannot heal
                    return;
            }
            holdLaying = false;
            if (snapping) { cruise = s.CruiseLunge; turn = s.TurnSnap; return; }
            switch (mood)
            {
                case TandavaSeveringMood.Lunging: cruise = s.CruiseLunge; turn = s.TurnLunge; break;
                case TandavaSeveringMood.Fleeing: cruise = s.CruiseFlee; turn = s.TurnFlee; break;
                case TandavaSeveringMood.Wary: cruise = s.CruiseWary; turn = s.TurnWary; break;
                default: cruise = s.CruiseCalm; turn = s.TurnCalm; break;
            }
        }

        // ──────────────────────────────────────────────────────────────── roaming

        void Roam(in TandavaSeveringSwarmState s, IReadOnlyList<TandavaSeveringFood> food, IReadOnlyList<TandavaSeveringPilot> pilots)
        {
            if (Mood == TandavaSeveringMood.Fleeing)
            {
                TargetFood = -1;
                Goal = FleePoint(s, pilots);
                return;
            }
            if (Mood == TandavaSeveringMood.Lunging && NearestPilot(s, pilots, out int prey) < float.PositiveInfinity)
            {
                // at the pilot, led by its own velocity, MOUTH first: the guard ring round its jaws is the weapon
                TargetFood = -1;
                var p = pilots[prey];
                var aim = p.Position + p.Velocity * S.LungeLead;
                var bite = Form.LungePlanIndex >= 0 ? Form.LungeMouth : Form.FeedMouth;
                Goal = ClampInside(aim - InBody(s, bite), S.RoamRadius);
                var jaws = s.Anchor + InBody(s, bite);
                float gap = Vector3.Distance(jaws, p.Position);
                if (Form.SnapPlanIndex >= 0 && !Snapping && (gap <= S.SnapReach || Clock >= _lungeUntil - S.SnapLeadSeconds))
                {
                    // the jaws slam shut on it - a beat before they reach, so the snap has time to read
                    _snapUntil = Clock + S.SnapHoldSeconds;
                    Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.Snapped, A = FormIx, B = (int)MathF.Round(gap) });
                }
                if (gap <= S.LungeReach)
                {
                    EndLunge();
                    SetMood(TandavaSeveringMood.Wary);
                }
                return;
            }
            int k = ChooseFood(s, food, pilots);
            if (k < 0)
            {
                TargetFood = -1;
                Goal = WanderPoint(s);
                return;
            }
            var plant = food[k];
            TargetFood = plant.Id;
            _haveWander = false;
            if (plant.Id != _coilFor || FormIx != _coilForm) DrawCoil(plant.Id);
            // line the meal's MOUTH up on the plant: the body's centre aims at the plant less the mouth's reach. In its
            // strike pose that is the head, so it arrives head first; rolling up, it is the coil's centre, so it swims
            // over the plant until its middle is there and curls round it
            var reach = Coil >= 0 ? Form.CoilMouths[Coil] : Form.Mouth;
            Goal = ClampInside(plant.Position - InBody(s, reach), MealRoamRadius);
            var mouth = s.Anchor + InBody(s, reach);
            if (Form.FeedPlanIndex >= 0 && Vector3.Distance(mouth, plant.Position) <= S.ArriveMargin) { BeginFeed(s, plant); return; }
            // PARKED short of it: the plant lies past the roam, so the goal was clamped, and the body sits on that goal with
            // its mouth off the plant - a body at rest does not turn, so it would wait there for ever (the Antlion did, and
            // starved). After GiveUpSeconds parked it rests the plant and chooses again
            if (plant.Id != _parkedFor || Vector3.Distance(s.Anchor, Goal) > S.ArriveMargin) { _parkedFor = plant.Id; _parkedSince = Clock; }
            else if (Clock - _parkedSince > S.GiveUpSeconds) { _restUntil[plant.Id] = Clock + S.RestSeconds; _parkedFor = -1; }
        }

        /// <summary>The best plant to go to now (an index into <paramref name="food"/>), or -1.</summary>
        int ChooseFood(in TandavaSeveringSwarmState s, IReadOnlyList<TandavaSeveringFood> food, IReadOnlyList<TandavaSeveringPilot> pilots)
        {
            if (food == null) return -1;
            float fear = Mood == TandavaSeveringMood.Wary ? S.FearWary : S.FearCalm;
            float tableMemory = Memory(TandavaSeveringWound.Feeding);
            fear = MathF.Max(fear, S.FearWary * tableMemory);   // §3.12: struck at the table, it is wary of every table
            float safeRadius = SafeRadiusNow;
            int best = -1; float bestScore = 0f;
            for (int k = 0; k < food.Count; k++)
            {
                var f = food[k];
                if (f.Volume < S.MinFood) continue;
                if (_restUntil.TryGetValue(f.Id, out float until) && Clock < until) continue;
                float d = Vector3.Distance(f.Position, s.Anchor);
                float safety = 1f;
                if (pilots != null)
                    for (int q = 0; q < pilots.Count; q++)
                    {
                        float dp = Vector3.Distance(pilots[q].Position, f.Position);
                        safety *= 1f - fear * MathF.Exp(-dp / MathF.Max(1f, safeRadius));
                    }
                float score = MathF.Sqrt(f.Volume) * safety / (d + S.DistanceBias);
                if (f.Id == TargetFood) score *= S.StickBonus;
                if (score > bestScore) { bestScore = score; best = k; }
            }
            return best;
        }

        Vector3 FleePoint(in TandavaSeveringSwarmState s, IReadOnlyList<TandavaSeveringPilot> pilots)
        {
            // away from the pilots, weighted by how near each one is
            Vector3 c = Vector3.Zero; float w = 0f;
            if (pilots != null)
                for (int k = 0; k < pilots.Count; k++)
                {
                    float d = Vector3.Distance(pilots[k].Position, s.Anchor);
                    float wk = 1f / MathF.Max(25f, d);
                    c += pilots[k].Position * wk; w += wk;
                }
            Vector3 away = w > 0f ? s.Anchor - c / w : -s.Forward;
            if (away.LengthSquared() < 1e-4f) away = s.Forward;
            away = Vector3.Normalize(away);
            var target = s.Anchor + away * S.FleeReach;
            var inside = ClampInside(target, S.RoamRadius);
            if (Vector3.Distance(inside, s.Anchor) >= 0.35f * S.FleeReach) return inside;
            // cornered against the wall: run ALONG it, the side the pilots are not
            var radial = s.Anchor - S.Centre;
            radial = radial.LengthSquared() > 1e-4f ? Vector3.Normalize(radial) : Vector3.UnitX;
            var along = away - Vector3.Dot(away, radial) * radial;
            if (along.LengthSquared() < 1e-4f) along = Vector3.Cross(radial, MathF.Abs(radial.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
            along = Vector3.Normalize(along);
            return ClampInside(s.Anchor + along * S.FleeReach - radial * 0.25f * S.FleeReach, S.RoamRadius);
        }

        Vector3 WanderPoint(in TandavaSeveringSwarmState s)
        {
            if (_haveWander && Vector3.Distance(_wander, s.Anchor) > 0.25f * S.WanderReach) return _wander;
            for (int tries = 0; tries < 12; tries++)
            {
                var d = new Vector3((float)(_rng.NextDouble() * 2 - 1), (float)(_rng.NextDouble() * 2 - 1), (float)(_rng.NextDouble() * 2 - 1));
                if (d.LengthSquared() < 1e-3f || d.LengthSquared() > 1f) continue;
                var p = S.Centre + d * S.RoamRadius;
                if (Vector3.Distance(p, s.Anchor) < S.WanderReach) continue;
                _wander = p; _haveWander = true;
                return p;
            }
            _wander = ClampInside(S.Centre - (s.Anchor - S.Centre), S.RoamRadius); _haveWander = true;
            return _wander;
        }

        Vector3 ClampInside(Vector3 p, float radius)
        {
            var d = p - S.Centre; float l = d.Length();
            return l > radius && l > 1e-3f ? S.Centre + d * (radius / l) : p;
        }

        // ──────────────────────────────────────────────────────────────── feeding

        /// <summary>The formation for a meal at <paramref name="plantId"/>: a different one from the last, while the form has
        /// more than one.</summary>
        void DrawCoil(int plantId)
        {
            int n = Form.CoilPlanIndices.Length;
            int last = FormIx == _coilForm ? Coil : -1;
            Coil = n == 0 ? -1 : n == 1 ? 0 : last < 0 ? _rng.Next(n) : (last + 1 + _rng.Next(n - 1)) % n;
            _coilFor = plantId; _coilForm = FormIx;
        }

        /// <summary>Where the plant sits in the pose it eats in now.</summary>
        Vector3 MealMouth => Coil >= 0 ? Form.CoilMouths[Coil] : Form.FeedMouth;

        /// <summary>How far out its centre may go for the meal it is on its way to, or at.</summary>
        float MealRoamRadius => Coil >= 0 ? MathF.Max(S.RoamRadius, Form.CoilRoamRadius) : S.RoamRadius;

        void BeginFeed(in TandavaSeveringSwarmState s, in TandavaSeveringFood plant)
        {
            Phase = TandavaSeveringPhase.Feed; _phaseSince = Clock;
            _feedSince = Clock; _eatenAtMeal = s.EatenTotal; _lostAtMeal = s.Lost; EatenHere = 0f;
            TargetFood = plant.Id;
            Goal = ClampInside(plant.Position - InBody(s, MealMouth), MealRoamRadius);
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.FeedBegan, A = plant.Id, B = Coil });
        }

        void FeedTick(in TandavaSeveringSwarmState s, IReadOnlyList<TandavaSeveringFood> food)
        {
            int k = -1;
            if (food != null) for (int q = 0; q < food.Count; q++) if (food[q].Id == TargetFood) { k = q; break; }
            EatenHere = s.EatenTotal - _eatenAtMeal;
            float sat = Clock - _feedSince;
            int lostHere = s.Lost - _lostAtMeal;
            if (lostHere >= MealBreakShare * Form.PlanCount)
            {
                EndMeal(TandavaSeveringMealEnd.Broken);
                _fleeUntil = Clock + S.FleeMinSeconds;   // hurt at the table: it bolts
                Threat = MathF.Max(Threat, S.FleeEnter);
                SetMood(TandavaSeveringMood.Fleeing);
                return;
            }
            if (Form.Role == TandavaSeveringFormRole.Final && Progress(s) >= 1f) { End(TandavaSeveringOutcome.Completed); return; }
            if (k < 0) { EndMeal(TandavaSeveringMealEnd.Bare); return; }
            if (EatenHere >= Form.MealVolume || Progress(s) >= 1f) { EndMeal(TandavaSeveringMealEnd.Fed); return; }
            if (s.StomachFill >= S.LeaveWhenStomachFill) { EndMeal(TandavaSeveringMealEnd.Full); return; }
            if (sat > S.FeedSettleSeconds + S.GiveUpSeconds && s.SinceBite > S.GiveUpSeconds) { EndMeal(TandavaSeveringMealEnd.Bare); return; }
            if (sat > S.MaxFeedSeconds) { EndMeal(TandavaSeveringMealEnd.TooLong); return; }
            Goal = ClampInside(food[k].Position - InBody(s, MealMouth), MealRoamRadius);
        }

        void EndMeal(TandavaSeveringMealEnd why)
        {
            int id = TargetFood;
            Phase = TandavaSeveringPhase.Roam; _phaseSince = Clock;
            if (id >= 0) _restUntil[id] = Clock + S.RestSeconds;
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.FeedEnded, A = id, B = (int)why });
            TargetFood = -1;
            _coilFor = -1;   // the next meal, even at this plant again, draws a new formation
        }

        void End(TandavaSeveringOutcome outcome)
        {
            if (Outcome != TandavaSeveringOutcome.Running) return;
            Outcome = outcome;
            Phase = TandavaSeveringPhase.Over;
            HoldLaying = false; CruiseScale = 1f; TurnScale = 1f;
            Events.Add(new TandavaSeveringEvent { Kind = TandavaSeveringEventKind.Ended, A = FormIx, B = (int)outcome });
        }

        // ──────────────────────────────────────────────────────────────── the variants (pure, so every peer agrees)

        /// <summary>One variant per form, drawn from <paramref name="seed"/>: <paramref name="variants"/>[k] choices for form k.</summary>
        public static int[] DrawVariants(int seed, IReadOnlyList<int> variants)
        {
            var rng = new Random(seed);
            var pick = new int[variants.Count];
            for (int k = 0; k < pick.Length; k++) pick[k] = variants[k] > 1 ? rng.Next(variants[k]) : 0;
            return pick;
        }

        /// <summary>The picks packed four bits a form (replicated as one int).</summary>
        public static int Pack(IReadOnlyList<int> picks)
        {
            int m = 0;
            for (int k = 0; k < picks.Count && k < 8; k++) m |= (picks[k] & 15) << (4 * k);
            return m;
        }

        public static int Unpack(int packed, int form) => form is >= 0 and < 8 ? (packed >> (4 * form)) & 15 : 0;
    }
}
