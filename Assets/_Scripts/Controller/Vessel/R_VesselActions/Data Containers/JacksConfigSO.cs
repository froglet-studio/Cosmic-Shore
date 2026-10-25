using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number the Jacks read (design: <c>R_VesselActions/JACKS.md</c>). The lab's keys keep the lab's
    /// names and SHIPPED values (<c>Docs/Studios/SwarmVesselLab/src/shepherd.js</c>, <c>SHEP_SHIPPED</c>), with the lab's label as
    /// the tooltip. Lengths and speeds are authored in LAB units and multiplied by <see cref="LabScale"/>: the lab's creatures
    /// are about 2.5x the game's (its sheep cruise 70 u/s and keep 58 u apart, a Quadfish 28 u/s and 30 u), so one number
    /// carries the whole lab into the game and the lab's values stay verbatim.
    /// </summary>
    [CreateAssetMenu(fileName = "JacksConfig", menuName = "ScriptableObjects/Vessel Actions/Jacks Config")]
    public sealed class JacksConfigSO : ScriptableObject
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
        [Tooltip("viewSpan: the LAB's view, world units across the screen's SHORT side (lab units). It sizes the smallest " +
                 "ring in the world (a big thumb on the lab's nominal phone at this span); the jacks' view itself is framed " +
                 "by phoneShortMm and thumbCover.")]
        [SerializeField, Min(10f)] float viewSpan = 1800f;
        [Tooltip("phoneShortMm: a FULL-SIZE phone's screen, short side (mm; a 6.7-inch 19.5:9 phone is about 71). The jacks' " +
                 "view is framed for it.")]
        [SerializeField, Min(10f)] float phoneShortMm = 71f;
        [Tooltip("thumbCover: on that phone, the smallest ring's diameter as a share of a big thumb's occlusion (thumbMm). " +
                 "Just under 1 = the thumb barely covers it (Garrett, 2026-10-21: \"zoomed out enough that on a full size " +
                 "mobile device an above average sized thumb barely covers the smallest jack repulsion radius\"). Lower " +
                 "zooms the view out further; the ring's size in the world does not change.")]
        [SerializeField, Range(0.2f, 2f)] float thumbCover = 0.9f;
        [Tooltip("camSlack: Steady view, share of the half-screen the herd may wander before the view pans.")]
        [SerializeField, Range(0f, 1f)] float camSlack = 0.45f;

        [Header("The herd")]
        [Tooltip("planePull: how hard the herd is drawn to the hull's plane, the board (1/s^2).")]
        [SerializeField, Min(0f)] float planePull = 1.6f;
        [Tooltip("The herd's own flocking: separation distance (lab units; the lab's 58).")]
        [SerializeField, Min(0f)] float separation = 58f;
        [Tooltip("The herd's own flocking: neighbour radius (lab units; the lab's 160).")]
        [SerializeField, Min(1f)] float neighbourRadius = 160f;
        [Tooltip("How far a calm creature looks for its next meal (lab units; the lab's 130).")]
        [SerializeField, Min(0f)] float grazeSense = 130f;
        [Tooltip("The minimum herd: it starves only down to this many (Garrett, 2026-10-21: \"should only starve down to a " +
                 "minimum size\"). An empty herd has this many born to it, as the Angler's empty school has its keeper.")]
        [SerializeField, Min(0)] int minHerd = 30;
        [Tooltip("The most creatures the herd holds. Wild herbivores that come inside a herder's ring join it until it is full.")]
        [SerializeField, Min(1)] int herdCap = 150;
        [Tooltip("Seconds between births while an empty herd is being given its floor.")]
        [SerializeField, Min(0.05f)] float floorBirthSeconds = 0.12f;
        [Tooltip("The species an empty herd is given when its cell grows no leashable herbivore of its own.")]
        [SerializeField] FaunaConfigurationSO companionFauna;
        [Tooltip("The herd's mark: how far (0..1 of the way round the hue circle) a herded creature's spindles lean toward " +
                 "our domain's hue, so the herd reads as ours (Garrett, 2026-10-21: \"a slight hue shift in your domain\"). " +
                 "0 = no mark.")]
        [SerializeField, Range(0f, 1f)] float herdHueLean = 0.35f;
        [Tooltip("The herd lights up (Garrett, 2026-10-23: \"fauna that get herded should light up when in herding mode. " +
                 "fauna that get called should light up when flying\"): while the jacks herd, and while a call lasts in " +
                 "flight, a herded creature's spindle colours are multiplied by 1 + this (HDR, so the bloom catches them). " +
                 "0 = they never light up.")]
        [SerializeField, Range(0f, 7.9f)] float herdGlow = 1.5f;

        [Header("The jacks")]
        [Tooltip("dogSpeed: how fast a jack runs to its thumb (lab units/s).")]
        [SerializeField, Min(0f)] float dogSpeed = 520f;
        [Tooltip("padDogSpeed: a full stick runs a jack at this share of dogSpeed.")]
        [SerializeField, Min(0f)] float padDogSpeed = 1f;
        [Tooltip("aiNoise: the AI jacks' sloppiness (0 expert, 1 rookie).")]
        [SerializeField, Range(0f, 1f)] float aiNoise = 0.15f;

        [Header("Flying (Garrett, 2026-10-21: \"move one fish up and the other down to roll, or move both together for pitch " +
                "and yaw, and bring them apart to speed up, bring them together to slow down\")")]
        [Tooltip("Flying: how far each jack sits from the hull's centre line with its stick at rest (game units).")]
        [SerializeField, Min(0f)] float flightGap = 14f;
        [Tooltip("Flying: how far a full stick moves its jack, sideways and up or down, in the hull's frame (game units). " +
                 "Keep it under flightGap so the two never cross.")]
        [SerializeField, Min(0f)] float flightReach = 10f;
        [Tooltip("Flying: how fast a jack eases to the place under its thumb (1/s), and swims into formation on take-off.")]
        [SerializeField, Min(0.1f)] float flightFormationEase = 8f;

        [Header("Netting (Garrett, 2026-10-24: \"lets make it a net that opens wider with xdiff\")")]
        [Tooltip("Netting: how much further apart the fish swim with the sticks fully apart (u per side, on top of " +
                 "flightGap and the sticks' own reach), so the net opens wider.")]
        [SerializeField, Min(0f)] float netSpread = 30f;
        [Tooltip("Netting: the mouth's height with the net closed (sticks at rest or together; u).")]
        [SerializeField, Min(0.5f)] float netMinHeight = 6f;
        [Tooltip("Netting: the mouth's height with the net fully open (sticks fully apart; u).")]
        [SerializeField, Min(0.5f)] float netMaxHeight = 36f;
        [Tooltip("Netting: how deep the bag hangs behind the mouth, in its middle (u).")]
        [SerializeField, Min(0.5f)] float netDepth = 26f;
        [Tooltip("Netting: how fast the bag's swing follows the hull's turning (1/s; the turn rate it swings against is eased " +
                 "at this). Lower swings later and longer.")]
        [SerializeField, Min(0.1f)] float netTrailEase = 5f;
        [Tooltip("Netting: how far the bag swings out against a turn, in seconds of the nose's turn (Garrett, 2026-10-25: \"i " +
                 "would expect this to work in any direction ... whether changing or maintaining directions\"): turning up " +
                 "it swings down, turning left it swings right, for as long as the turn lasts.")]
        [SerializeField, Min(0f)] float netSwingSeconds = 0.6f;
        [Tooltip("Netting: the most the bag leans from straight back (the tangent of the angle: 1.2 is about 50 degrees).")]
        [SerializeField, Min(0f)] float netSwingMax = 1.2f;
        [Tooltip("Netting: a crystal settles this far into the bag (0 the mouth, 1 the bottom).")]
        [SerializeField, Range(0f, 1f)] float netSettle = 0.75f;
        [Tooltip("Netting: how fast a netted crystal sinks to netSettle (1/s).")]
        [SerializeField, Min(0.1f)] float netSettleEase = 3f;
        [Tooltip("Netting: the most prisms the net drags at once (Garrett, 2026-10-25: \"lets have prisms move and be dragged " +
                 "by the net\"). Each is a mover of the prism-motion rule: one that runs into another prism destroys both.")]
        [SerializeField, Range(0, 256)] int netPrismCap = 48;
        [Tooltip("Netting: our own domain's prisms the net crosses are lit, not moved (Garrett, 2026-10-25: \"when prisms of " +
                 "the same domain are netted then they should be briefly lit\"): how far toward white (Prism.SetColorLift).")]
        [SerializeField, Range(0f, 1f)] float netLitLift = 0.6f;
        [Tooltip("Netting: how long an own-domain prism stays lit after the net leaves it (s).")]
        [SerializeField, Min(0f)] float netLitSeconds = 0.6f;
        [Tooltip("Netting: how long a lit prism takes to light up and to fade back (s): nothing pops.")]
        [SerializeField, Min(0f)] float netLitFadeSeconds = 0.25f;
        [Tooltip("Netting: a netted rival hull loses this much of each element (normalized; 0.1 = one crystal each), " +
                 "knocked loose into the net.")]
        [SerializeField, Range(0f, 1f)] float netRivalTake = 0.1f;
        [Tooltip("Netting: seconds before the same rival hull can lose crystals to the net again.")]
        [SerializeField, Min(0.1f)] float netRivalCooldown = 2f;
        [Tooltip("Herding: how close a jack must come to a crystal to collect it, across the board as the herding view sees " +
                 "it (u; Garrett, 2026-10-24: \"i had a hard time collecting crystals\").")]
        [SerializeField, Min(0f)] float jackCrystalReach = 32f;
        [Tooltip("Netting: how close a flying jack must come to a crystal to collect it (u, round the fish; Garrett, " +
                 "2026-10-25: \"if either jack hits the omnicrystal it should still produce the serpent\"). The net between " +
                 "the jacks carries the crystals the fish do not touch.")]
        [SerializeField, Min(0f)] float jackNetCrystalReach = 10f;
        [Tooltip("Herding: how far above or below the board a crystal can sit and still be collected by a jack under it in " +
                 "the top-down view (u).")]
        [SerializeField, Min(0f)] float jackCrystalHeight = 200f;
        [Tooltip("Netting: the chase camera backs off until the jacks and the net's catch fit inside this share of the " +
                 "view from its centre (Garrett, 2026-10-24: \"the jacks and anything in their net should be in the camera " +
                 "view while netting\").")]
        [SerializeField, Range(0.2f, 1f)] float netCameraFill = 0.8f;
        [Tooltip("Netting: how fast the chase camera eases to the distance that keeps the net in view (1/s).")]
        [SerializeField, Min(0.1f)] float netCameraEase = 3f;

        [Header("The AI")]
        [Tooltip("An AI hull herds this long (s), then flies.")]
        [SerializeField, Min(1f)] float aiHerdSeconds = 14f;
        [Tooltip("An AI hull flies this long (s), calling the herd as it takes off, then herds again.")]
        [SerializeField, Min(1f)] float aiFlightSeconds = 6f;

        [Header("The call and the decoy")]
        [Tooltip("callTime: herding, seconds the jacks' call lasts (the herd pulled to the point between the jacks).")]
        [SerializeField, Min(0f)] float callTime = 2.5f;
        [Tooltip("callCooldown: herding, seconds before the call is ready again.")]
        [SerializeField, Min(0f)] float callCooldown = 20f;
        [Tooltip("decoyCooldown: Time L5, seconds between the decoys the flying jacks drop.")]
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

        [Header("The herding view")]
        [Tooltip("The small view's height, as a share of the screen's height (the Wrasse's pipSize).")]
        [SerializeField, Range(0.1f, 0.6f)] float pipSize = 0.32f;
        [Tooltip("The small view's margin from the screen's corner, as a share of the screen's height.")]
        [SerializeField, Range(0f, 0.2f)] float viewMargin = 0.02f;
        [Tooltip("How far above and below the plane the herding camera sees (game units).")]
        [SerializeField, Min(10f)] float viewDepth = 600f;

        [Header("Stability while herding (Garrett, 2026-10-21: \"we need to make things as stable for the jack player as we " +
                "can\")")]
        [Tooltip("While the jacks herd: how fast the board (the plane they run on, and the herding view looks down) turns " +
                 "toward the hull's own plane (1/s). The hull holds still while they herd, so this only settles a take-off's " +
                 "last turn; 0 holds the plane still until they fly again.")]
        [FormerlySerializedAs("jackSeatPlaneEase")]
        [SerializeField, Min(0f)] float herdPlaneEase = 0.25f;
        [Tooltip("While the jacks fly: how fast the board follows the hull's plane (1/s).")]
        [FormerlySerializedAs("pilotSeatPlaneEase")]
        [SerializeField, Min(0f)] float flightPlaneEase = 3f;
        [Tooltip("While the jacks herd: how much the view's framing leans toward the hull (0 = the herd alone). With no " +
                 "herd the view frames the jacks.")]
        [FormerlySerializedAs("jackSeatMarlinWeight")]
        [SerializeField, Range(0f, 1f)] float herdHullWeight = 0f;
        [Tooltip("While the jacks fly: how much the small herding view's framing leans toward the hull.")]
        [FormerlySerializedAs("pilotSeatMarlinWeight")]
        [SerializeField, Range(0f, 1f)] float flightHullWeight = 0.45f;
        [Tooltip("How fast the view pans once its goal leaves the slack box (1/s; the lab's 1.6). Lower is calmer.")]
        [SerializeField, Min(0.01f)] float viewPanEase = 1.2f;
        [Tooltip("Herding: the view's span is multiplied by this (Garrett, 2026-10-24: \"the herding view should have a view " +
                 "that is zoomed in a bit more and follows the jacks\"). 1 = the thumb-framed span.")]
        [SerializeField, Range(0.2f, 2f)] float herdViewZoom = 0.75f;
        [Tooltip("Herding: how much the view's framing follows the jacks rather than the herd (1 = the jacks alone).")]
        [SerializeField, Range(0f, 1f)] float herdFollowJacks = 1f;
        [Tooltip("Herding, zoomed out to the whole cell (the zoom button, gamepad Y, F): the cell's diameter times this " +
                 "fills the view's short side.")]
        [SerializeField, Min(0.5f)] float cellViewMargin = 1.1f;
        [Tooltip("Herding: the view zooms out so both jacks stay inside this share of it from its centre (Garrett, " +
                 "2026-10-24: \"as the sharks fly apart in herding mode the camera should zoom out so they never collide with " +
                 "the screen perimeter\").")]
        [SerializeField, Range(0.3f, 0.95f)] float herdJacksFill = 0.8f;
        [Tooltip("Herding: room kept round each jack inside that share (u; a herding jack is about 40 long).")]
        [SerializeField, Min(0f)] float herdJacksMargin = 40f;
        [Tooltip("How fast the herding view eases between the jacks' zoom and the whole cell (1/s).")]
        [SerializeField, Min(0.1f)] float viewZoomEase = 3f;
        [Tooltip("The zoom button's size, as a share of the screen's height (top right of the herding view).")]
        [SerializeField, Range(0.04f, 0.3f)] float zoomButtonSize = 0.09f;
        [Tooltip("Optional icon drawn inside the zoom button (empty: the button is the style guide's frame alone).")]
        [SerializeField] Sprite zoomButtonIcon;
        [Tooltip("Optional icon drawn inside the herding view's call button, below the zoom button (empty: the frame alone).")]
        [SerializeField] Sprite callButtonIcon;

        [Header("Audio (FMOD; empty until sound design wires them)")]
        [Tooltip("The jacks call the herd.")]
        [SerializeField] EventReference callSfx;
        [Tooltip("A decoy is dropped.")]
        [SerializeField] EventReference decoySfx;
        [Tooltip("The flying jacks jump to the decoy.")]
        [SerializeField] EventReference teleportSfx;

        public float LabScale => labScale;
        public float ThumbMm => thumbMm;
        public float ThumbMargin => thumbMargin;
        /// <summary>The lab's view span in GAME units: what the ring's floor is measured against.</summary>
        public float LabViewSpan => viewSpan * labScale;
        /// <summary>The herding view in GAME units across the screen's short side: zoomed out until the smallest ring is
        /// <c>thumbCover</c> of a big thumb on a full-size phone (<see cref="JacksMath.ThumbViewSpan"/>).</summary>
        public float ViewSpan => JacksMath.ThumbViewSpan(RingFloor, thumbMm, thumbCover, phoneShortMm);
        public float PhoneShortMm => phoneShortMm;
        public float ThumbCover => thumbCover;
        public float CamSlack => camSlack;
        public float PlanePull => planePull;
        public float Separation => separation * labScale;
        public float NeighbourRadius => neighbourRadius * labScale;
        public float GrazeSense => grazeSense * labScale;
        public int MinHerd => minHerd;
        public int HerdCap => Mathf.Max(minHerd, herdCap);
        public float FloorBirthSeconds => floorBirthSeconds;
        public FaunaConfigurationSO CompanionFauna => companionFauna;
        public float HerdHueLean => herdHueLean;
        public float HerdGlow => herdGlow;
        public float DogSpeed => dogSpeed * labScale;
        public float PadDogSpeed => padDogSpeed;
        public float AINoise => aiNoise;
        public float FlightGap => flightGap;
        public float FlightReach => flightReach;
        public float FlightFormationEase => flightFormationEase;
        public float NetSpread => netSpread;
        public float NetMinHeight => netMinHeight;
        public float NetMaxHeight => Mathf.Max(netMinHeight, netMaxHeight);
        public float NetDepth => netDepth;
        public float NetTrailEase => netTrailEase;
        public float NetSwingSeconds => netSwingSeconds;
        public float NetSwingMax => netSwingMax;
        public float NetSettle => netSettle;
        public float NetSettleEase => netSettleEase;
        public int NetPrismCap => netPrismCap;
        public float NetLitLift => netLitLift;
        public float NetLitSeconds => netLitSeconds;
        public float NetLitFadeSeconds => netLitFadeSeconds;
        public float NetRivalTake => netRivalTake;
        public float NetRivalCooldown => netRivalCooldown;
        public float JackCrystalReach => jackCrystalReach;
        public float JackCrystalHeight => jackCrystalHeight;
        public float JackNetCrystalReach => jackNetCrystalReach;
        public float NetCameraFill => netCameraFill;
        public float NetCameraEase => netCameraEase;
        /// <summary>The fish's distance apart with the net closed, and fully open: what <see cref="JacksMath.NetOpenFromGap"/>
        /// reads the opening back from.</summary>
        public float NetClosedGap => 2f * flightGap;
        public float NetOpenGap => 2f * (flightGap + netSpread + flightReach);
        public float HerdViewZoom => herdViewZoom;
        public float HerdFollowJacks => herdFollowJacks;
        public float CellViewMargin => cellViewMargin;
        public float ViewZoomEase => viewZoomEase;
        public float HerdJacksFill => herdJacksFill;
        public float HerdJacksMargin => herdJacksMargin;
        public float ZoomButtonSize => zoomButtonSize;
        public Sprite ZoomButtonIcon => zoomButtonIcon;
        public Sprite CallButtonIcon => callButtonIcon;
        public float AIHerdSeconds => aiHerdSeconds;
        public float AIFlightSeconds => aiFlightSeconds;
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
        public float HerdPlaneEase => herdPlaneEase;
        public float FlightPlaneEase => flightPlaneEase;
        public float HerdHullWeight => herdHullWeight;
        public float FlightHullWeight => flightHullWeight;
        public float ViewPanEase => viewPanEase;
        public EventReference CallSfx => callSfx;
        public EventReference DecoySfx => decoySfx;
        public EventReference TeleportSfx => teleportSfx;

        /// <summary>The ring's floor in GAME units: a big thumb on the lab's nominal phone, at the lab's view span.</summary>
        public float RingFloor => JacksMath.FloorUnits(thumbMm, thumbMargin, LabViewSpan);
    }
}
