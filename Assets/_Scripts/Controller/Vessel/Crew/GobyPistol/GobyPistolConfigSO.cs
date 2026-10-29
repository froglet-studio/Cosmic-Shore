using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every number of the Goby &amp; Pistol crew vessel (Garrett, 2026-10-22: "it should be a goby with its own kit that
    /// represents a goby and synergizes with the pistol and its kit"; built straight in Unity, the lab is behind it). Read
    /// by <c>GobyPistolDuo</c> on <c>Goby.prefab</c>. The design and its decisions:
    /// <c>.claude/skills/duo-vessel/references/designs/goby-and-pistol.md</c> §11 (2026-10-22).
    ///
    /// <para><b>The pair's four elements</b> (one meter, both seats):</para>
    /// <list type="bullet">
    /// <item><b>Charge</b>: petals a vortex ring takes from a rival hull it hits (<see cref="RingPetals"/>).</item>
    /// <item><b>Mass</b>: den prisms per rest (<see cref="DenMassScale"/>); level 5 adds danger and super-shielded prisms.</item>
    /// <item><b>Space</b>: how far either side of the Pistol's plane mass is pulled into it (<see cref="PullReach"/>); level
    /// 5 pulls shielded prisms too.</item>
    /// <item><b>Time</b>: the Pistol's jump cooldown (<see cref="JumpCooldown"/>); level 5 lets the Goby's darts pass through
    /// prisms.</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(fileName = "GobyPistolConfig", menuName = "ScriptableObjects/Vessel Actions/Goby Pistol Config")]
    public sealed class GobyPistolConfigSO : ScriptableObject
    {
        [Header("Goby: dart and perch (the Shrike's sling: distance / dartSeconds, docks on the first block it touches)")]
        [Tooltip("The farthest a full-charged dart goes, u. A dart is the Goby's burst between cruising; its end is the " +
                 "next perch once it is islandMinLength from the last one.")]
        [SerializeField, Min(10f)] float dartRange = 600f;
        [Tooltip("How far a tap darts, u. Holding the dart button charges it up to dartRange over dartChargeSeconds.")]
        [SerializeField, Min(5f)] float dartMinRange = 140f;
        [Tooltip("Seconds of holding the dart button to reach full range.")]
        [SerializeField, Min(0.05f)] float dartChargeSeconds = 0.8f;
        [Tooltip("Seconds a dart takes, whatever its length (the Shrike's slingSeconds).")]
        [SerializeField, Min(0.1f)] float dartSeconds = 1f;
        [Tooltip("Seconds after a dart lands before the next can start.")]
        [SerializeField, Min(0f)] float dartCooldown = 1.2f;
        [Tooltip("The hull's radius against a block's box: a dart stops this far off the block it reaches (the Shrike's 3).")]
        [SerializeField, Min(0.5f)] float hullRadius = 3f;
        [Tooltip("Extra reach of the spatial query round the dart's line, u.")]
        [SerializeField, Min(0f)] float queryReach = 6f;
        [Tooltip("Blocks in the pad the Goby lays when it darts into open space and makes its own perch.")]
        [SerializeField, Range(1, 12)] int ownPerchBlocks = 5;
        [Tooltip("A perch pad block's edge, u.")]
        [SerializeField, Min(0.5f)] float perchBlockSize = 4f;

        [Header("Goby: cruise, perch and the Pistol's islands (Garrett, 2026-10-23: \"the goby is barely cruising around at all\")")]
        [Tooltip("Seconds the Goby stays perched at the end of an island, aiming, before it lifts off and cruises (longer " +
                 "while a person charges a dart from the perch).")]
        [SerializeField, Min(0f)] float perchSeconds = 1.5f;
        [Tooltip("A dart's end is a perch (the end of the Pistol's next island) only this far from the last perch, u; " +
                 "nearer, the dart is a dash and the Goby cruises on. A Tiny Wings island of about 35 hills.")]
        [SerializeField, Min(50f)] float islandMinLength = 900f;
        [Tooltip("A longer gap from the last perch is laid as islands of at most this, u (about 70 hills), each ending in a " +
                 "perch of its own.")]
        [SerializeField, Min(50f)] float islandMaxLength = 1800f;
        [Tooltip("The Goby lays no new perch while this many islands wait ahead of the Pistol: a dart is a dash until the " +
                 "Pistol catches up, so the two stay together.")]
        [SerializeField, Range(1, 8)] int islandQueue = 3;

        [Header("Goby: lock (vessels first, then fauna: the Shrike's lock order)")]
        [Tooltip("How far the Goby can lock, u.")]
        [SerializeField, Min(10f)] float lockRange = 1400f;
        [Tooltip("The lock cone's half-angle, degrees, round the Goby's nose.")]
        [SerializeField, Range(1f, 45f)] float lockCone = 12f;
        [Tooltip("A held lock is kept to this many times the cone and the range.")]
        [SerializeField, Min(1f)] float lockKeepScale = 1.6f;

        [Header("The Pistol's ground (the dart's own trail on a flat plane)")]
        [Tooltip("Degrees the travel from perch to perch runs downhill on the Pistol's screen, either way it rides. 0: level, " +
                 "as Tiny Wings is (every u/s is the rider's own). Past 33 the hills could fold the ground back on itself.")]
        [SerializeField, Range(0f, 33f)] float slopeDegrees = 0f;
        [Tooltip("Tiny Wings' hills, as the game makes them (Docs/PISTOL_TINY_WINGS_STUDY.md): key points, alternately a crest and " +
                 "a trough, each a random distance on and height up or down from the last, a half-cosine between. The shortest " +
                 "half-hill (crest to the next trough), u. The game: 160-240 pt for a 14 pt bird, 5.7-8.6 bird lengths.")]
        [SerializeField, Min(1f)] float hillHalfLengthMin = 10f;
        [Tooltip("The longest half-hill, u.")]
        [SerializeField, Min(1f)] float hillHalfLengthMax = 15f;
        [Tooltip("The least a half-hill rises or falls, u (the game: 60-120 pt, 2.1-4.3 bird lengths).")]
        [SerializeField, Min(0f)] float hillHalfHeightMin = 3.6f;
        [Tooltip("The most a half-hill rises or falls, u, on the first island.")]
        [SerializeField, Min(0f)] float hillHalfHeightMax = 7.2f;
        [Tooltip("The ground keeps within this far of the dart's line, u, either side.")]
        [SerializeField, Min(0f)] float hillBand = 9f;
        [Tooltip("Each island of a run is this much taller than the last (the game's later islands are taller).")]
        [SerializeField, Range(0f, 0.5f)] float hillGrowthPerIsland = 0.08f;
        [Tooltip("The most an island grows, as a multiple of the first.")]
        [SerializeField, Range(1f, 3f)] float hillGrowthMax = 1.5f;
        [Tooltip("A trail block every this many u along the dart.")]
        [SerializeField, Min(2f)] float trailBlockGap = 6f;
        [Tooltip("A trail block's cross-section: x across the plane (it grows here when flown over), y its thickness in the plane. " +
                 "Its length along the dart is not authored: it is the length whose SHIELD meets the next block's tip to tip " +
                 "(trailBlockGap / 3 x trailShieldFill), so a shielded trail is a chain of diamonds, never crossing spikes.")]
        [SerializeField] Vector2 trailBlockSection = new(1.4f, 0.5f);
        [Tooltip("The share of the shield-flush length a trail block takes (1: shielded tips touch; less leaves a gap between " +
                 "the diamonds). The shipped hills never cross at 1 (GobyPistolEnergyTests.Trail_ShieldedNeighboursMeetTipToTip).")]
        [SerializeField, Range(0.5f, 1f)] float trailShieldFill = 0.9f;

        [Header("Pistol: the ride, Tiny Wings' physics (Docs/PISTOL_TINY_WINGS_STUDY.md): a frictionless body under gravity, a " +
                "dive about seven times gravity, almost no drag, a landing that keeps its speed along the slope and loses only " +
                "the part into it; hold to dive down the slopes, let go and the crest throws it)")]
        [SerializeField, Min(1f)] float pGravity = 19f;
        [SerializeField, Min(1f)] float pDiveMul = 7.5f;
        [SerializeField, Min(0f)] float pMinSpeed = 6f;
        [SerializeField, Min(0f)] float pDrag = 0.05f;
        [Tooltip("A landing within this many degrees of the slope is a PERFECT SLIDE (the game's three-in-a-row fever).")]
        [SerializeField, Min(0f)] float pGoodLand = 30f;
        [Tooltip("Seconds in the air before a landing counts as a landing (smooth: the slide's speed; hard: a thud). A " +
                 "shorter hop off a crest is a skip that keeps its speed.")]
        [SerializeField, Min(0f)] float pMinAir = 0.15f;
        [Tooltip("Gravity in the air while the Pistol lets go, as a share of pGravity. 1 is the game's (its float is the dive's " +
                 "weight by contrast, not lighter air); below 1 it hangs longer.")]
        [SerializeField, Range(0.1f, 1f)] float pAirGravity = 1f;
        [Tooltip("A perfect slide's speed, times this. The game rewards a perfect slide with points, not speed (1); a touch " +
                 "above 1 is ours, so a perfect slide is felt.")]
        [SerializeField, Range(1f, 1.5f)] float pSmoothBoost = 1.05f;
        [Tooltip("The share of its speed along the slope a hard landing keeps. 1 is the game's: a landing loses only the part " +
                 "of its speed INTO the slope (its ground is frictionless and does not bounce), so a steep landing is its own " +
                 "penalty and a flat one costs nothing.")]
        [SerializeField, Range(0f, 1f)] float pHardKeep = 1f;
        [Tooltip("The fastest the Pistol goes, u/s.")]
        [SerializeField, Min(1f)] float pMaxSpeed = 90f;
        [Tooltip("The Pistol's body radius in its plane, u.")]
        [SerializeField, Min(0.1f)] float pBodyR = 0.85f;
        [Tooltip("How far either side of its plane the Pistol's body reaches, u (pulled-in mass lands inside it).")]
        [SerializeField, Min(0.5f)] float pBodyHalfDepth = 10f;
        [Tooltip("The jump's farthest reach in the plane, u.")]
        [SerializeField, Min(10f)] float pLaunchRange = 35f;
        [Tooltip("Off: the Pistol has no aimed jump; it flies only off the hills, by letting go of the dive on an upslope " +
                 "(Garrett, 2026-10-22: \"launching intentionally was wierd\"). On: drag (RT + right stick, F) aims a jump on " +
                 "Time's cooldown, up to pLaunchRange.")]
        [SerializeField] bool aimedJump;
        [SerializeField, Min(10f)] float pLaunchSpeed = 30f;
        [SerializeField, Min(1f)] float pLaunchCharges = 1f;
        [Tooltip("Time: the jump's cooldown, s (9 at rest -> 3 at level 10).")]
        [SerializeField] ElementalFloat jumpCooldown = ElementalFloat.Multiplier(9f, 3f, Element.Time, 1f);
        [SerializeField, Min(0.1f)] float pGhostTime = 2.5f;
        [SerializeField, Min(0.1f)] float pGhostCd = 8f;
        [Tooltip("A slingshot pull this long (a share of the screen's short side) is a full jump.")]
        [SerializeField, Range(0.05f, 0.6f)] float pSlingPull = 0.22f;

        [Header("Pistol: a broken trail (Garrett, 2026-10-28: \"the shrimp should have to clear the destroyed section\"; " +
                "2026-10-29: a fall restores what it got past and drops it beyond the gap)")]
        [Tooltip("Where a trail block was destroyed the ground is a hole. A Pistol this far under the ground's line over a hole has " +
                 "fallen in: the gap's blocks it got past are restored, it loses all its energy, and it reappears on the next " +
                 "standing ground beyond the gap with its speed. Less, it still catches the far lip, u.")]
        [SerializeField, Min(0f)] float pEdgeGrace = 1.2f;
        [Tooltip("Seconds the vortex-ring portals last where the Pistol fell in and where it reappears.")]
        [SerializeField, Min(0.05f)] float fallPopSeconds = 0.5f;
        [Tooltip("How far the Pistol spins end over end as it comes out of the portal, degrees (it ends upright).")]
        [SerializeField, Min(0f)] float fallSpinDegrees = 540f;
        [Tooltip("The portals' radius, u (the Pistol is 2.7 u long).")]
        [SerializeField, Min(0.5f)] float fallRingRadius = 2.4f;

        [Header("Pistol: energy from skipped blocks (Garrett, 2026-10-29: \"this reward the player for making big jumps\")")]
        [Tooltip("The energy one block flown over (not ridden) gives, the first of a flight.")]
        [SerializeField, Min(0f)] float energyPerSkippedBlock = 1f;
        [Tooltip("Each further block of the same flight gives this much more (x the first's): the nth gives 1 + bonus n, so a " +
                 "flight over 10 blocks earns 19, over 20 earns 58.")]
        [SerializeField, Range(0f, 1f)] float energyFlightBonus = 0.2f;
        [Tooltip("The most energy the Pistol holds (its Mass card's gauge is full here): a full den's worth at Mass's rest.")]
        [SerializeField, Min(1f)] float energyMax = 640f;
        [Tooltip("A block flown over grows this much OUT OF THE PLANE (across the trail, its x), u a skip.")]
        [SerializeField, Min(0f)] float skipGrowStep = 0.8f;
        [Tooltip("A block grows across the trail to this width, u; one this wide is shielded when flown over again.")]
        [SerializeField, Min(0.5f)] float skipGrowMax = 8f;
        [Header("Pistol: the juice of a skipped block (Garrett, 2026-10-29: \"there should be some good juice associated with " +
                "growing, restoring, and shielding prisms\")")]
        [Tooltip("A rewarded block throws this many lit, faceted shards in the Pistol's plane (a restored one draws them in).")]
        [SerializeField, Range(0, 16)] int juiceShards = 6;
        [Tooltip("Seconds a block's juice lasts.")]
        [SerializeField, Min(0.05f)] float juiceSeconds = 0.45f;
        [Tooltip("How far the shards fly, u.")]
        [SerializeField, Min(0f)] float juiceReach = 4f;
        [Tooltip("A shard's edge, u.")]
        [SerializeField, Min(0.05f)] float juiceShardSize = 0.55f;
        [Tooltip("The block's flash: a lit shell this much bigger than the block, shrinking onto it.")]
        [SerializeField, Range(1f, 3f)] float juicePulseScale = 1.7f;
        [Tooltip("Grown (wider across the trail): the shards' colour.")]
        [SerializeField] Color juiceGrowColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("Shielded: the shards' colour.")]
        [SerializeField] Color juiceShieldColor = new(159f / 255f, 232f / 255f, 1f, 1f);
        [Tooltip("Restored (a destroyed block back): the shards' colour.")]
        [SerializeField] Color juiceRestoreColor = new(1f, 1f, 1f, 1f);
        [Tooltip("FMOD: a block grown by a jump. Leave empty for silence. Played at most once per juiceSoundSpacing.")]
        [SerializeField] EventReference growEvent;
        [Tooltip("FMOD: a block shielded by a jump. Leave empty for silence.")]
        [SerializeField] EventReference shieldEvent;
        [Tooltip("FMOD: a destroyed block restored (by a jump over it, or a fall that got that far). Leave empty for silence.")]
        [SerializeField] EventReference restoreEvent;
        [Tooltip("FMOD: the Pistol falls through a gap and comes out of the portal beyond it. Leave empty for silence.")]
        [SerializeField] EventReference fallThroughEvent;
        [Tooltip("Seconds between two of one kind's sounds (a long jump rewards a block every few frames).")]
        [SerializeField, Min(0f)] float juiceSoundSpacing = 0.07f;

        [Tooltip("How fast a crystal beside the plane drifts into it, u/s (within the undertow's reach).")]
        [SerializeField, Min(0f)] float crystalPullSpeed = 30f;
        [Tooltip("A crystal this much past the Pistol's body (and its own half size) is taken, u.")]
        [SerializeField, Min(0f)] float crystalTouchPad = 1.5f;

        [Header("Pistol: perches and dens")]
        [Tooltip("The Pistol arrives at a perch within this many u of it.")]
        [SerializeField, Min(1f)] float arriveDistance = 2f;
        [Tooltip("Seconds the Pistol rests at a perch, building its den.")]
        [SerializeField, Min(0.1f)] float restSeconds = 1.2f;
        [Tooltip("One den prism per this much of the Pistol's energy (before Mass). The shipped Tiny Wings rider (the AI) gathers " +
                 "about 185 over a 900 u island and 385 over 1800 u, a sloppy one 20-70, one that never flies none.")]
        [SerializeField, Min(0.1f)] float energyPerDenPrism = 8f;
        [Tooltip("Mass: den prisms are scaled by this (x1 at rest -> x2.5 at level 10).")]
        [SerializeField] ElementalFloat denMassScale = ElementalFloat.Multiplier(1f, 2.5f, Element.Mass, 0.25f);
        [Tooltip("The most prisms one den holds.")]
        [SerializeField, Range(4, 200)] int denMax = 80;
        [Tooltip("Share of a den that is shielded.")]
        [SerializeField, Range(0f, 1f)] float denShieldedShare = 0.4f;
        [Tooltip("With the Mass upgrade: share of a den that is danger prisms.")]
        [SerializeField, Range(0f, 1f)] float denDangerShare = 0.15f;
        [Tooltip("With the Mass upgrade: share of a den that is super-shielded.")]
        [SerializeField, Range(0f, 1f)] float denSuperShare = 0.12f;
        [Tooltip("A den block's edge, u.")]
        [SerializeField, Min(0.5f)] float denBlockSize = 4f;
        [Tooltip("The den's first shell radius round the perch, u.")]
        [SerializeField, Min(1f)] float denFirstRadius = 10f;
        [Tooltip("Den blocks keep their bounding diameter times this apart (no two ever clip).")]
        [SerializeField, Min(1f)] float denClearance = 1.1f;

        [Header("Pistol: the undertow (mass pulled into the plane)")]
        [Tooltip("Space: how far out of the plane mass is pulled in, u (40 at rest -> 200 at level 10), from the half the " +
                 "Pistol plays this way (one side of the trail riding ahead, the other riding back); crystals from either side. " +
                 "Danger, super-shielded and the pair's own prisms never move; shielded ones only with the Space upgrade.")]
        [SerializeField] ElementalFloat pullReach = ElementalFloat.Multiplier(40f, 200f, Element.Space, 10f);
        [Tooltip("How fast pulled mass moves toward the plane, u/s.")]
        [SerializeField, Min(1f)] float pullSpeed = 60f;
        [Tooltip("Mass this far along the plane either side of the Pistol is pulled, u.")]
        [SerializeField, Min(10f)] float pullWindow = 320f;
        [Tooltip("At most this many prisms move each frame.")]
        [SerializeField, Range(8, 512)] int pullMaxPerFrame = 96;

        [Header("Pistol: the Snap (a vortex ring)")]
        [SerializeField, Min(1f)] float pVortexR = 22f;
        [SerializeField, Min(1f)] float pVortexSpeed = 120f;
        [SerializeField, Min(0.1f)] float pVortexLife = 2.6f;
        [SerializeField, Min(0f)] float pVortexCapture = 24f;
        [SerializeField, Min(0f)] float pVortexSpin = 7f;
        [SerializeField, Min(1f)] float pVortexMax = 30f;
        [SerializeField, Min(0.1f)] float pVortexCd = 4f;
        [Tooltip("1: the ring collapses and breaks what it carries. 0: it lets them go.")]
        [SerializeField, Range(0f, 1f)] float pVortexCollapse = 1f;
        [Tooltip("A ring fired at the Goby's locked rival turns toward it at this many degrees a second.")]
        [SerializeField, Min(0f)] float homingTurnRate = 240f;
        [Tooltip("Charge: petals the ring takes from EACH element of a rival hull it hits (1 at rest -> 3 at level 10).")]
        [SerializeField] ElementalFloat ringPetals = ElementalFloat.Multiplier(1f, 3f, Element.Charge, 1f);
        [Tooltip("A ring hits a hull within its radius plus this, u.")]
        [SerializeField, Min(0f)] float ringHitPad = 4f;
        [Tooltip("The scored hit a ring lands (the PvP rule: a hit takes petals and nothing else).")]
        [SerializeField] CombatHitClass ringHitClass = CombatHitClass.MissileDirect;
        [Tooltip("Seconds before the same ring can score on the same hull again (the latch window).")]
        [SerializeField, Min(0f)] float hitCooldown = 0.25f;
        [Tooltip("Raised for every scored ring hit (the fleet's combat scoring).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;
        [Tooltip("The match's players: the Goby locks rival vessels from it, and the rings hit them (the Orca's crew reads the same asset).")]
        [SerializeField] GameDataSO gameData;

        [Header("Pistol: the views (the crew vessels' one layout and look)")]
        [Tooltip("The small view's side, as a fraction of the screen height.")]
        [SerializeField, Range(0.15f, 0.6f)] float pipSize = 0.32f;
        [Tooltip("The small view's gap from the screen's edges, as a fraction of the screen height.")]
        [SerializeField, Range(0f, 0.1f)] float viewMargin = 0.015f;
        [Tooltip("How much of the plane the Pistol's screen shows across at rest, u: the game's 480 pt screen for a 28 pt " +
                 "bird, 17 bird lengths, about 1.2 hills.")]
        [SerializeField, Min(5f)] float viewWidth = 29f;
        [Tooltip("The widest the Pistol's screen zooms out to, u, as it speeds up and flies high.")]
        [SerializeField, Min(5f)] float viewMaxWidth = 150f;
        [Tooltip("Width the screen adds per u/s of the Pistol's speed, u (the game zooms by height alone; a little by speed " +
                 "shows the hill ahead at pace).")]
        [SerializeField, Min(0f)] float viewSpeedWiden = 0.15f;
        [Tooltip("Where the ground under a grounded Pistol sits on its screen, as a share of the height from the bottom.")]
        [SerializeField, Range(0.05f, 0.6f)] float viewGroundedAt = 0.4f;
        [Tooltip("The space kept above a flying Pistol, as a share of the screen's height: fly higher and the screen zooms out " +
                 "(the game keeps the bird under four fifths of the screen).")]
        [SerializeField, Range(0.05f, 0.4f)] float viewHeadroom = 0.2f;
        [Tooltip("How far the Pistol's camera sits from its plane at rest (viewWidth), u; it pulls back in step as the screen " +
                 "zooms out, so the lens never changes. Its lens is off-axis: the camera stays beside the Pistol and the " +
                 "screen looks ahead, so the Pistol is never stretched at the edge of the lens.")]
        [SerializeField, Min(20f)] float viewDistance = 60f;
        [Tooltip("Where the Pistol sits across its screen, as a share of the width from the centre (minus: left of it). The " +
                 "game's bird sits an eighth of the screen from the left (-0.375).")]
        [SerializeField, Range(-0.45f, 0.45f)] float viewPistolAt = -0.35f;
        [Tooltip("For the Pistol's view only, a prism this far out of its plane reads as far as the Goby's view's most distant mass.")]
        [SerializeField, Min(1f)] float depthFullSpreadAt = 60f;

        [Header("Pistol: the path map at a perch (Garrett, 2026-10-25: pick a den and a direction; teleporting grows no den)")]
        [Tooltip("How far above or below an island's ground a prism still counts toward the island's score, u (the vortex " +
                 "ring flies in the plane; the undertow's reach decides how far out of it).")]
        [SerializeField, Min(1f)] float pathScoreBand = 60f;
        [Tooltip("Seconds between two islands' scores while the Pistol rides (one island at a time; every frame while the map " +
                 "is open).")]
        [SerializeField, Min(0.01f)] float pathScoreInterval = 0.1f;
        [Tooltip("Holding the dive this long once the map opens sets off on the highlighted path (so holding the dive through a " +
                 "perch rides on); a fresh press sets off at once.")]
        [SerializeField, Min(0f)] float chooseHoldSeconds = 0.5f;
        [Tooltip("The map's tilt when it first opens, degrees, so an island's two half planes (one side ridden ahead, the other " +
                 "ridden back) part on the screen; the person's spin is kept from there.")]
        [SerializeField, Range(-80f, 80f)] float mapStartTilt = 35f;
        [Tooltip("A half plane's wing reaches this far out of the trail at its fullest (the half with the most opposing prisms), " +
                 "as a share of the map's radius.")]
        [SerializeField, Range(0.005f, 0.2f)] float mapWingReach = 0.05f;
        [Tooltip("The map camera's field of view, degrees.")]
        [SerializeField, Range(20f, 90f)] float mapFov = 50f;
        [Tooltip("Room round the perches on the map: 1 frames them edge to edge.")]
        [SerializeField, Range(1f, 2f)] float mapMargin = 1.2f;
        [Tooltip("The map's scale for its marks, as a share of the map's radius: the pointer and the den mark are a few of " +
                 "these; the trail's own bar is mapPathThinShare of it.")]
        [SerializeField, Range(0.001f, 0.05f)] float mapPathThickness = 0.014f;
        [Tooltip("The trail's bar on the map, as a share of mapPathThickness (each half plane's wing stands out of it).")]
        [SerializeField, Range(0.05f, 1f)] float mapPathThinShare = 0.25f;
        [Tooltip("A tap or click this close to a path's glowing bar, pixels at 160 dpi (scaled up on denser screens), picks " +
                 "it; a tap on the picked path rides it.")]
        [SerializeField, Min(8f)] float mapPickPixels = 70f;
        [Tooltip("How far a drag across the map spins it: 1 turns it 180 degrees for a drag the screen's height.")]
        [SerializeField, Min(0.05f)] float mapSpinDragScale = 1f;
        [Tooltip("How fast the right stick (or Q / E) spins the map, degrees per second.")]
        [SerializeField, Min(1f)] float mapSpinRate = 120f;
        [Tooltip("An empty path's glow (the palette's neutral).")]
        [SerializeField] Color mapPathColdColor = new(0.30f, 0.45f, 0.75f, 1f);
        [Tooltip("The path with the most opposing prisms (the palette's call-to-action lime).")]
        [SerializeField] Color mapPathHotColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("The highlighted path, its start den and the arrow that says which way it rides.")]
        [SerializeField] Color mapPickColor = new(0.96f, 0.96f, 1f, 1f);

        [Header("Pistol: its model (Pistol.fbx, specs/pistol.py: a pistol-shrimp ship, nose +Z, big claw on its right)")]
        [Tooltip("The Pistol's hull, drawn where the ride puts it. Empty: a capsule stands in.")]
        [SerializeField] GameObject pistolModel;
        [Tooltip("The model's length along its travel, claw tip to fan tip, u (the capsule it replaced was 2.72).")]
        [SerializeField, Min(0.1f)] float pistolModelLength = 2.7f;
        [Tooltip("How far the hammer claw shuts on a Snap, degrees (the model rests it cocked 75 degrees open).")]
        [SerializeField, Range(0f, 90f)] float snapClawShut = 75f;
        [Tooltip("Seconds the claw stays shut after a Snap before it re-cocks.")]
        [SerializeField, Min(0f)] float snapClawHold = 0.08f;
        [Tooltip("Seconds the claw takes to re-cock after the hold.")]
        [SerializeField, Min(0.01f)] float snapClawRecock = 0.35f;
        [Tooltip("How far the tail curls under while the Pistol dives, degrees.")]
        [SerializeField, Range(0f, 60f)] float diveTailCurl = 22f;
        [Tooltip("How fast the tail follows the dive, per second.")]
        [SerializeField, Min(0.1f)] float diveTailRate = 10f;

        [Header("AI")]
        [Tooltip("The Goby AI's dart, u: it darts once its island is as long as it planned, so the dart's end is the perch.")]
        [SerializeField, Min(10f)] float aiDartLength = 300f;
        [Tooltip("The Goby AI's cruise between perches, u/s (a person cruises on the hull's own two-thumb flight).")]
        [SerializeField, Min(1f)] float aiCruiseSpeed = 100f;
        [Tooltip("How fast the cruising Goby AI turns, degrees a second.")]
        [SerializeField, Min(1f)] float aiTurnRate = 90f;
        [Tooltip("Seconds between the cruising Goby AI's choices of where to head.")]
        [SerializeField, Min(0.2f)] float aiRethinkSeconds = 2.5f;
        [Tooltip("The Goby AI keeps within this distance of where it started, u.")]
        [SerializeField, Min(100f)] float aiHomeRadius = 4000f;
        [Tooltip("The Pistol AI heads back to an old den after waiting this long at the head perch, s.")]
        [SerializeField, Min(0f)] float aiBackAfter = 4f;

        [Header("Each seat's buttons on a phone (the Orca's gunner buttons are the pattern)")]
        [Tooltip("The Goby's Dart button (held to charge, let go to dart). A pad's RT, the keyboard's Q.")]
        [SerializeField] Sprite dartButtonIcon;
        [Tooltip("Player-facing caption under the Dart button. Empty shows none.")]
        [SerializeField] string dartButtonLabel = "";
        [Tooltip("The Goby's Lock button. A pad's LT, the keyboard's F.")]
        [SerializeField] Sprite lockButtonIcon;
        [Tooltip("Player-facing caption under the Lock button. Empty shows none.")]
        [SerializeField] string lockButtonLabel = "";
        [Tooltip("The Pistol's Ghost button (held).")]
        [SerializeField] Sprite ghostButtonIcon;
        [Tooltip("Player-facing caption under the Ghost button. Empty shows none.")]
        [SerializeField] string ghostButtonLabel = "";
        [Tooltip("The Pistol's Snap button (the vortex ring).")]
        [SerializeField] Sprite snapButtonIcon;
        [Tooltip("Player-facing caption under the Snap button. Empty shows none.")]
        [SerializeField] string snapButtonLabel = "";
        [Tooltip("The Pistol's Back button: at a perch, head back toward the older dens (press again to head on).")]
        [SerializeField] Sprite backButtonIcon;
        [Tooltip("Player-facing caption under the Back button. Empty shows none.")]
        [SerializeField] string backButtonLabel = "";

        public float DartRange => dartRange;
        public float DartMinRange => dartMinRange;
        public float DartChargeSeconds => dartChargeSeconds;
        public float DartSeconds => dartSeconds;
        public float DartCooldown => dartCooldown;
        public float HullRadius => hullRadius;
        public float QueryReach => queryReach;
        public int OwnPerchBlocks => ownPerchBlocks;
        public float PerchBlockSize => perchBlockSize;
        public float LockRange => lockRange;
        public float LockCone => lockCone;
        public float LockKeepScale => lockKeepScale;
        public float SlopeDegrees => slopeDegrees;
        public PistolPlane.HillSpec HillSpec => new(hillHalfLengthMin, hillHalfLengthMax, hillHalfHeightMin, hillHalfHeightMax, hillBand, hillGrowthPerIsland, hillGrowthMax);
        public float TrailBlockGap => trailBlockGap;
        /// <summary>A trail block's size (x across, y thick, z along the dart), its length the one whose shield meets the next
        /// block's (<see cref="PistolGround.ShieldFlushLength"/>).</summary>
        public Vector3 TrailBlockScale => new(trailBlockSection.x, trailBlockSection.y,
            PistolGround.ShieldFlushLength(Mathf.Max(2f, trailBlockGap), OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE, trailShieldFill));
        public float BodyR => pBodyR;
        public float PathScoreBand => pathScoreBand;
        public float PathScoreInterval => pathScoreInterval;
        public float ChooseHoldSeconds => chooseHoldSeconds;
        public float MapFov => mapFov;
        public float MapMargin => mapMargin;
        public float MapPathThickness => mapPathThickness;
        public float MapPathThinShare => mapPathThinShare;
        public float MapPickPixels => mapPickPixels;
        public float MapSpinDragScale => mapSpinDragScale;
        public float MapSpinRate => mapSpinRate;
        public Color MapPathColdColor => mapPathColdColor;
        public Color MapPathHotColor => mapPathHotColor;
        public Color MapPickColor => mapPickColor;
        public GameObject PistolModel => pistolModel;
        public float PistolModelLength => pistolModelLength;
        public float SnapClawShut => snapClawShut;
        public float SnapClawHold => snapClawHold;
        public float SnapClawRecock => snapClawRecock;
        public float DiveTailCurl => diveTailCurl;
        public float DiveTailRate => diveTailRate;
        public float BodyHalfDepth => pBodyHalfDepth;
        public float SlingPull => pSlingPull;
        public bool AimedJump => aimedJump;
        public ElementalFloat JumpCooldown => jumpCooldown;
        public float ArriveDistance => arriveDistance;
        public float FallPopSeconds => fallPopSeconds;
        public float FallSpinDegrees => fallSpinDegrees;
        public float FallRingRadius => fallRingRadius;
        public float RestSeconds => restSeconds;
        public float EnergyPerDenPrism => energyPerDenPrism;
        public float EnergyPerSkippedBlock => energyPerSkippedBlock;
        public float EnergyFlightBonus => energyFlightBonus;
        public float EnergyMax => energyMax;
        public float SkipGrowStep => skipGrowStep;
        public float SkipGrowMax => skipGrowMax;
        public float CrystalPullSpeed => crystalPullSpeed;
        public int JuiceShards => juiceShards;
        public float JuiceSeconds => juiceSeconds;
        public float JuiceReach => juiceReach;
        public float JuiceShardSize => juiceShardSize;
        public float JuicePulseScale => juicePulseScale;
        public Color JuiceGrowColor => juiceGrowColor;
        public Color JuiceShieldColor => juiceShieldColor;
        public Color JuiceRestoreColor => juiceRestoreColor;
        public EventReference GrowEvent => growEvent;
        public EventReference ShieldEvent => shieldEvent;
        public EventReference RestoreEvent => restoreEvent;
        public EventReference FallThroughEvent => fallThroughEvent;
        public float JuiceSoundSpacing => juiceSoundSpacing;
        public float CrystalTouchPad => crystalTouchPad;
        public float MapStartTilt => mapStartTilt;
        public float MapWingReach => mapWingReach;
        public ElementalFloat DenMassScale => denMassScale;
        public int DenMax => denMax;
        public float DenShieldedShare => denShieldedShare;
        public float DenDangerShare => denDangerShare;
        public float DenSuperShare => denSuperShare;
        public float DenBlockSize => denBlockSize;
        public float DenFirstRadius => denFirstRadius;
        public float DenClearance => denClearance;
        public ElementalFloat PullReach => pullReach;
        public float PullSpeed => pullSpeed;
        public float PullWindow => pullWindow;
        public int PullMaxPerFrame => pullMaxPerFrame;
        public float HomingTurnRate => homingTurnRate;
        public ElementalFloat RingPetals => ringPetals;
        public float RingHitPad => ringHitPad;
        public CombatHitClass RingHitClass => ringHitClass;
        public float HitCooldown => hitCooldown;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public GameDataSO GameData => gameData;
        public float PipSize => pipSize;
        public float ViewMargin => viewMargin;
        public float ViewWidth => viewWidth;
        public float ViewDistance => viewDistance;
        public float ViewMaxWidth => viewMaxWidth;
        public float ViewSpeedWiden => viewSpeedWiden;
        public float ViewGroundedAt => viewGroundedAt;
        public float ViewHeadroom => viewHeadroom;
        public float ViewPistolAt => viewPistolAt;
        public float DepthFullSpreadAt => depthFullSpreadAt;
        public float AiDartLength => aiDartLength;
        public float AiCruiseSpeed => aiCruiseSpeed;
        public float AiTurnRate => aiTurnRate;
        public float AiRethinkSeconds => aiRethinkSeconds;
        public float PerchSeconds => perchSeconds;
        public float IslandMinLength => islandMinLength;
        public float IslandMaxLength => islandMaxLength;
        public int IslandQueue => islandQueue;
        public float AiHomeRadius => aiHomeRadius;
        public float AiBackAfter => aiBackAfter;
        public Sprite DartButtonIcon => dartButtonIcon;
        public string DartButtonLabel => dartButtonLabel;
        public Sprite LockButtonIcon => lockButtonIcon;
        public string LockButtonLabel => lockButtonLabel;
        public Sprite GhostButtonIcon => ghostButtonIcon;
        public string GhostButtonLabel => ghostButtonLabel;
        public Sprite SnapButtonIcon => snapButtonIcon;
        public string SnapButtonLabel => snapButtonLabel;
        public Sprite BackButtonIcon => backButtonIcon;
        public string BackButtonLabel => backButtonLabel;

        /// <summary>The ride's numbers; <c>pLaunchCd</c> is filled by the caller from <see cref="JumpCooldown"/>.</summary>
        public PistolBodyMath.Settings Body => new()
        {
            pGravity = pGravity, pDiveMul = pDiveMul, pMinSpeed = pMinSpeed, pDrag = pDrag, pGoodLand = pGoodLand, pMinAir = pMinAir,
            pAirGravity = pAirGravity, pSmoothBoost = pSmoothBoost, pHardKeep = pHardKeep, pMaxSpeed = pMaxSpeed,
            pLaunchRange = pLaunchRange, pLaunchSpeed = pLaunchSpeed, pLaunchCharges = pLaunchCharges, pLaunchCd = jumpCooldown.Value,
            pGhostTime = pGhostTime, pGhostCd = pGhostCd, pVortexCd = pVortexCd, pEdgeGrace = pEdgeGrace,
        };

        public VortexRing.Settings Vortex => new()
        {
            pVortexR = pVortexR, pVortexSpeed = pVortexSpeed, pVortexLife = pVortexLife, pVortexCapture = pVortexCapture,
            pVortexSpin = pVortexSpin, pVortexMax = pVortexMax, pVortexCollapse = pVortexCollapse,
        };

        public PistolAI.Settings AI => new()
        {
            pLaunchRange = pLaunchRange, pVortexR = pVortexR, pVortexCapture = pVortexCapture, bodyHalfDepth = pBodyHalfDepth, body = Body, aimedJump = aimedJump,
        };
    }
}
