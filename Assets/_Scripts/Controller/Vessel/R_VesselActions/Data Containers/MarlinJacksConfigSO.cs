using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number Marlin and Jacks reads (design: <c>R_VesselActions/MARLIN_JACKS.md</c>). The lab's keys keep the lab's
    /// names and SHIPPED values (<c>Docs/Studios/SwarmVesselLab/src/shepherd.js</c>, <c>SHEP_SHIPPED</c>), with the lab's label as
    /// the tooltip. Lengths and speeds are authored in LAB units and multiplied by <see cref="LabScale"/>: the lab's creatures
    /// are about 2.5x the game's (its sheep cruise 70 u/s and keep 58 u apart, a Quadfish 28 u/s and 30 u), so one number
    /// carries the whole lab into the game and the lab's values stay verbatim.
    /// </summary>
    [CreateAssetMenu(fileName = "MarlinJacksConfig", menuName = "ScriptableObjects/Vessel Actions/Marlin and Jacks Config")]
    public sealed class MarlinJacksConfigSO : ScriptableObject
    {
        [Header("Scale (the port's, not the lab's)")]
        [Tooltip("Game units per lab unit. The lab's creatures are about 2.5x the game's (sheep 70 u/s, 58 u apart; a Quadfish " +
                 "28 u/s, 30 u), so every lab length and speed below is multiplied by this.")]
        [SerializeField, Min(0.01f)] float labScale = 0.4f;

        [Header("The thumb ring (Settings)")]
        [Tooltip("thumbMm: a BIG thumb's occlusion, in mm (the disc of screen a thumb hides).")]
        [SerializeField, Min(1f)] float thumbMm = 22f;
        [Tooltip("thumbMargin: the smallest ring is this many thumb radii.")]
        [SerializeField, Min(0.1f)] float thumbMargin = 1.25f;
        [Tooltip("viewSpan: world units across the screen's SHORT side (lab units).")]
        [SerializeField, Min(10f)] float viewSpan = 1800f;
        [Tooltip("camSlack: Steady view, share of the half-screen the herd may wander before the view pans.")]
        [SerializeField, Range(0f, 1f)] float camSlack = 0.45f;

        [Header("The herd")]
        [Tooltip("planePull: how hard the herd is drawn to the marlin's plane (1/s^2).")]
        [SerializeField, Min(0f)] float planePull = 1.6f;
        [Tooltip("The herd's own flocking: separation distance (lab units; the lab's 58).")]
        [SerializeField, Min(0f)] float separation = 58f;
        [Tooltip("The herd's own flocking: neighbour radius (lab units; the lab's 160).")]
        [SerializeField, Min(1f)] float neighbourRadius = 160f;
        [Tooltip("How far a calm creature looks for its next meal (lab units; the lab's 130).")]
        [SerializeField, Min(0f)] float grazeSense = 130f;
        [Tooltip("The minimum herd: it starves only down to this many (Garrett, 2026-10-21: \"should only starve down to a " +
                 "minimum size\"). An empty herd has this many born to it, as the Angler's empty school has its keeper.")]
        [SerializeField, Min(0)] int minHerd = 12;
        [Tooltip("The most creatures the herd holds. Wild herbivores that come inside a herder's ring join it until it is full.")]
        [SerializeField, Min(1)] int herdCap = 60;
        [Tooltip("Seconds between births while an empty herd is being given its floor.")]
        [SerializeField, Min(0.05f)] float floorBirthSeconds = 0.25f;
        [Tooltip("The species an empty herd is given when its cell grows no leashable herbivore of its own.")]
        [SerializeField] FaunaConfigurationSO companionFauna;

        [Header("The jacks")]
        [Tooltip("dogSpeed: how fast a jack runs to its thumb (lab units/s).")]
        [SerializeField, Min(0f)] float dogSpeed = 520f;
        [Tooltip("padDogSpeed: a full stick runs a jack at this share of dogSpeed.")]
        [SerializeField, Min(0f)] float padDogSpeed = 1f;
        [Tooltip("aiNoise: AI marlin and AI jacks sloppiness (0 expert, 1 rookie).")]
        [SerializeField, Range(0f, 1f)] float aiNoise = 0.15f;

        [Header("The marlin")]
        [Tooltip("callTime: seconds the marlin's call lasts.")]
        [SerializeField, Min(0f)] float callTime = 2.5f;
        [Tooltip("callCooldown: seconds before the call is ready again.")]
        [SerializeField, Min(0f)] float callCooldown = 20f;
        [Tooltip("decoyCooldown: Time L5, seconds between the marlin's own decoys.")]
        [SerializeField, Min(0f)] float decoyCooldown = 4f;
        [Tooltip("Seconds between teleports (the lab's 0.6 s guard).")]
        [SerializeField, Min(0f)] float teleportGuard = 0.6f;

        [Header("Level 5")]
        [Tooltip("pooEvery: Mass L5, seconds between clusters.")]
        [SerializeField, Min(0.1f)] float pooEvery = 6f;
        [Tooltip("Mass L5: prisms in one cluster (the lab's 7).")]
        [SerializeField, Min(1)] int pooCount = 7;
        [Tooltip("Mass L5: the cluster's spread round the creature (lab units; the lab's 22).")]
        [SerializeField, Min(0f)] float pooSpread = 22f;
        [Tooltip("Mass L5: the cluster's prism size (u), the Rhino's trail prism.")]
        [SerializeField] Vector3 pooPrismScale = new(2f, 2f, 5f);
        [Tooltip("attackRange: Charge L5, how close a rival must be for the herd to go for it (lab units).")]
        [SerializeField, Min(0f)] float attackRange = 200f;

        [Header("Contacts (PvP is petals only)")]
        [Tooltip("How close a creature must come to a rival hull to touch it (u, game units).")]
        [SerializeField, Min(0f)] float contactReach = 8f;
        [Tooltip("Seconds before the same hull can be scored on again (CombatHitDrain's per-pair latch).")]
        [SerializeField, Min(0f)] float strikeCooldown = 1.2f;

        [Header("The jacks' view")]
        [Tooltip("The small view's height, as a share of the screen's height (the Wrasse's pipSize).")]
        [SerializeField, Range(0.1f, 0.6f)] float pipSize = 0.32f;
        [Tooltip("The small view's margin from the screen's corner, as a share of the screen's height.")]
        [SerializeField, Range(0f, 0.2f)] float viewMargin = 0.02f;
        [Tooltip("How far above and below the plane the jacks' camera sees (game units).")]
        [SerializeField, Min(10f)] float viewDepth = 600f;

        [Header("Stability for the jacks' player (Garrett, 2026-10-21: \"the marlin makes the jacks player sick with its " +
                "movement. we need to make things as stable for the jack player as we can\")")]
        [Tooltip("While a human runs the jacks: how fast the board (the plane the jacks run on, and the view looks down) " +
                 "turns toward the marlin's own plane (1/s). Low = the marlin's roll and pitch barely move the view; 0 holds " +
                 "the plane still until the human flies the marlin again.")]
        [SerializeField, Min(0f)] float jackSeatPlaneEase = 0.25f;
        [Tooltip("While the human flies the marlin (the jacks are the AI): how fast the board follows the marlin's plane (1/s).")]
        [SerializeField, Min(0f)] float pilotSeatPlaneEase = 3f;
        [Tooltip("While a human runs the jacks: how much the view's framing leans toward the marlin (0 = the herd alone, " +
                 "so the marlin's flight never pans the view). With no herd the view frames the jacks.")]
        [SerializeField, Range(0f, 1f)] float jackSeatMarlinWeight = 0f;
        [Tooltip("While the human flies the marlin: how much the small view's framing leans toward the marlin.")]
        [SerializeField, Range(0f, 1f)] float pilotSeatMarlinWeight = 0.45f;
        [Tooltip("How fast the view pans once its goal leaves the slack box (1/s; the lab's 1.6). Lower is calmer.")]
        [SerializeField, Min(0.01f)] float viewPanEase = 1.2f;

        [Header("Audio (FMOD; empty until sound design wires them)")]
        [Tooltip("The marlin calls the herd.")]
        [SerializeField] EventReference callSfx;
        [Tooltip("A decoy is dropped.")]
        [SerializeField] EventReference decoySfx;
        [Tooltip("The marlin teleports to the decoy.")]
        [SerializeField] EventReference teleportSfx;

        public float LabScale => labScale;
        public float ThumbMm => thumbMm;
        public float ThumbMargin => thumbMargin;
        /// <summary>The view span in GAME units.</summary>
        public float ViewSpan => viewSpan * labScale;
        public float CamSlack => camSlack;
        public float PlanePull => planePull;
        public float Separation => separation * labScale;
        public float NeighbourRadius => neighbourRadius * labScale;
        public float GrazeSense => grazeSense * labScale;
        public int MinHerd => minHerd;
        public int HerdCap => Mathf.Max(minHerd, herdCap);
        public float FloorBirthSeconds => floorBirthSeconds;
        public FaunaConfigurationSO CompanionFauna => companionFauna;
        public float DogSpeed => dogSpeed * labScale;
        public float PadDogSpeed => padDogSpeed;
        public float AINoise => aiNoise;
        public float CallTime => callTime;
        public float CallCooldown => callCooldown;
        public float DecoyCooldown => decoyCooldown;
        public float TeleportGuard => teleportGuard;
        public float PooEvery => pooEvery;
        public int PooCount => pooCount;
        public float PooSpread => pooSpread * labScale;
        public Vector3 PooPrismScale => pooPrismScale;
        public float AttackRange => attackRange * labScale;
        public float ContactReach => contactReach;
        public float StrikeCooldown => strikeCooldown;
        public float PipSize => pipSize;
        public float ViewMargin => viewMargin;
        public float ViewDepth => viewDepth;
        public float JackSeatPlaneEase => jackSeatPlaneEase;
        public float PilotSeatPlaneEase => pilotSeatPlaneEase;
        public float JackSeatMarlinWeight => jackSeatMarlinWeight;
        public float PilotSeatMarlinWeight => pilotSeatMarlinWeight;
        public float ViewPanEase => viewPanEase;
        public EventReference CallSfx => callSfx;
        public EventReference DecoySfx => decoySfx;
        public EventReference TeleportSfx => teleportSfx;

        /// <summary>The ring's floor in GAME units: a big thumb on the nominal phone, at this view span.</summary>
        public float RingFloor => MarlinJacksMath.FloorUnits(thumbMm, thumbMargin, ViewSpan);
    }
}
