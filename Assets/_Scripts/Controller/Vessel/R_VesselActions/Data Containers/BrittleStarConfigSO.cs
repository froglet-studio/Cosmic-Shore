using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number the Brittle Star's executor reads (design: <c>R_VesselActions/BRITTLE_STAR.md</c>; the class
    /// document is the Notion "Class - Brittle Star" page). One asset, wired directly on the executor (vessel rule 20),
    /// shared by every Brittle Star and never written at runtime (rule 1).
    ///
    /// <para><b>One element per mode</b> (the four-icon row, charge → mass → space → time; Garrett, 2026-10-16):
    /// <list type="bullet">
    /// <item><b>Charge — Charge Mode</b>, the five whip cords (Rolling Thunder: "larger projectile size and larger AOE on
    /// support ring detonation"): the cords' cut width, the rounds' size (<see cref="ProjectileScaleMultiplier"/>) and the
    /// ring explosion's thickness (<see cref="BlastScale"/>).</item>
    /// <item><b>Mass — every trail block's girth</b> (Fabricator: "increased block size in creation mode"; Garrett,
    /// 2026-10-16: "mass can control the x and y"): <see cref="PrismWidthScale"/> widens and thickens the base trail's
    /// block and Creation Mode's five corner trails alike, never their length.</item>
    /// <item><b>Space — Destruction (Space) Mode</b>, the five guns (Hunter: "increased shot range"):
    /// <see cref="RangeMultiplier"/>, ×1 → ×2 on every gun's range, and <see cref="HeatCapacity"/>, ×1 → ×3.33 on
    /// the heat the guns hold (Garrett: "the maximum heat scale with space", 2026-10-16; a full burst "from 6 to 20
    /// seconds", 2026-10-17). The
    /// document's "nudge intensity" is retired with knockback itself — see below.</item>
    /// <item><b>Time — Speed Mode</b> (Zephyr: "increased movement speed bonus in speed mode"):
    /// <see cref="SpeedModeMultiplier"/>.</item>
    /// </list></para>
    ///
    /// <para><b>PvP is petals only (LOCKED, 2026-10-10).</b> The class document predates that rule and asks for shots
    /// that knock enemies back, cut their momentum and disable their trail. None of that may touch another pilot's
    /// vessel any more: every hit on an enemy hull is a SCORED combat hit that moves petals, through the fleet's
    /// reporters (the bullet's <c>VesselCombatHitByBullet</c>) and, for the cords and the ring explosion,
    /// <see cref="CombatHitDrain.TryAdmit"/> directly.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BrittleStarConfig", menuName = "ScriptableObjects/Vessel Actions/Brittle Star Config")]
    public sealed class BrittleStarConfigSO : ScriptableObject
    {
        // ------------------------------------------------------------------ modes
        [Header("Modes")]
        [Tooltip("Seconds a mode takes to come fully on or off (thrusters, trail, the HUD's switch, the cords growing out).")]
        [SerializeField, Min(0.01f)] float modeBlendSeconds = 0.35f;
        [Tooltip("TIME: Speed Mode's multiplier on the hull's cruise speed (x6 at rest - Garrett, 2026-10-14). The Zephyr's element.")]
        [SerializeField] ElementalFloat speedModeMultiplier = ElementalFloat.Multiplier(6f, 8f, Element.Time, 4f);
        [Tooltip("1 = Speed Mode stretches the base trail's spacing by exactly the speed it adds, so the hull lays the " +
                 "same prisms per second whatever its speed (the document: 'so as not to increase production output').")]
        [SerializeField, Range(0f, 1f)] float speedModeSpacingFollow = 1f;
        [Tooltip("1 = while the spacing is stretched, every prism is stretched along the path by the same factor, so the " +
                 "fast trail joins up as well as the cruise trail does (Garrett, 2026-10-16: 'stretch its prisms so its trail " +
                 "connects better'). 0 = blocks keep their length and the gaps grow with the speed.")]
        [SerializeField, Range(0f, 1f)] float speedModeStretch = 1f;

        [Header("Trail blocks")]
        [Tooltip("MASS: multiplier on the x and y of EVERY block the hull lays - the base trail's and Creation Mode's " +
                 "corner trails' - never its length (Garrett, 2026-10-16: 'stretch the prisms in z and decrease them in x " +
                 "and y at base. mass can control the x and y'). The block itself is BrittleStar.prefab's spawner BaseScale " +
                 "(1, 1, 6). The Fabricator's element.")]
        [SerializeField] ElementalFloat prismWidthScale = ElementalFloat.Multiplier(1f, 2f, Element.Mass, 1f);

        [Header("Creation Mode (a trail behind each ring corner)")]
        [Tooltip("World units between blocks along each corner's trail (the base trail's wavelength).")]
        [SerializeField, Min(0.5f)] float creationSpacing = 6f;
        [Tooltip("How far out from the hull's axis the corner trails are laid, as a multiple of the ring's radius (1 = on the corners).")]
        [SerializeField, Min(0.2f)] float creationRadiusScale = 1f;
        [Tooltip("How far behind the ring the corner trails are laid, world units (the base trail's offset).")]
        [SerializeField, Min(0f)] float creationOffset = 2f;
        [Tooltip("Most blocks one corner may lay in a frame; excess owed distance is DROPPED so a hitch never dumps a burst.")]
        [SerializeField, Range(1, 8)] int creationMaxPerFrame = 3;
        [Tooltip("Creation Mode's multiplier on cruise speed: the SLOWEST of the modes (Garrett, 2026-10-14). Building " +
                 "is slow work; the corner trails keep their spacing, so production per second falls with it.")]
        [SerializeField, Range(0.1f, 1f)] float creationSpeedMultiplier = 0.5f;
        [Tooltip("Seconds a freshly laid block's collider stays off, so the hull clears it.")]
        [SerializeField, Min(0f)] float creationWaitTime = 0.6f;

        // ------------------------------------------------------------------ the guns
        [Header("Guns (RT, analog: Space Mode's five, the aimed turret in Cruise / Speed / Creation)")]
        [Tooltip("Rounds per second from EACH firing gun at a FULL trigger pull; the pull scales it down to nothing " +
                 "(Garrett, 2026-10-16: 'the trigger should control the firing rate in an analog fashion'). 90 = the " +
                 "Sparrow's full-auto cadence, the rate the five arms fired at.")]
        [SerializeField, Min(1f)] float fireRate = 90f;
        [Tooltip("Muzzle speed, u/s - the Sparrow's round at a tenth of its speed (Garrett, 2026-10-14). Range = speed x " +
                 "2T/pi (Projectile's ease-out, vessel rule 34).")]
        [SerializeField, Min(10f)] float projectileSpeed = 135f;
        [Tooltip("Flight time at Space 0, seconds. 4.5 s: 150% of the 3 s the arms flew (Garrett, 2026-10-16: 'increase " +
                 "base gun range to 150%'), so 135 u/s reaches 387 u.")]
        [SerializeField, Min(0.05f)] float projectileTime = 4.5f;
        [Tooltip("SPACE: multiplier on the flight time, and so on every gun's range - x2 at Space 10 ('double that " +
                 "through the space scaling'): 387 u -> 774 u. The Hunter's element.")]
        [SerializeField] ElementalFloat rangeMultiplier = ElementalFloat.Multiplier(1f, 2f, Element.Space, 0.7f);
        [Tooltip("Round size multiplier on the projectile prefab's own size, at Charge 0 (the Sparrow's own: 1).")]
        [SerializeField, Min(0.1f)] float projectileScale = 1f;
        [Tooltip("CHARGE: the rounds' size multiplier, and the cords' cut width. Rolling Thunder's element.")]
        [SerializeField] ElementalFloat projectileScaleMultiplier = ElementalFloat.Multiplier(1f, 1.8f, Element.Charge, 0.6f);
        [Tooltip("In-flight growth of every round: each leaves the muzzle at its size and swells to this multiple over its " +
                 "flight, its hit radius with it - the Sparrow's look (FullAutoAction.growthFactorAtRestingMass 3). A fixed " +
                 "factor here: Mass is Creation Mode's dial on this hull, so it does not also scale the rounds.")]
        [SerializeField, Min(0.01f)] float roundGrowthFactor = 3f;
        [Tooltip("Space Mode: with the right stick IN (centred), the five guns angle inward to meet the nose axis this far " +
                 "ahead, world units - the vessel as a crosshair. (Charge Mode's cords hang on the same lines.)")]
        [SerializeField, Min(1f)] float armFocusDistance = 120f;
        [Tooltip("With the right stick pushed OUT to the rim, each gun (and each cord) splays this many degrees outward. " +
                 "Between in and out the angle moves linearly (BrittleStarRules.ArmDirection).")]
        [SerializeField, Range(0f, 60f)] float armSplayDegrees = 25f;
        [Tooltip("Right-stick deflection below this aims nowhere: the ring holds its rotation.")]
        [SerializeField, Range(0f, 0.9f)] float aimDeadzone = 0.25f;
        [Tooltip("Degrees per second the support ring turns to bring the aimed turret round (the 'firing rotation'). " +
                 "A stick spun faster than this turns the ring flat out, forward (BrittleStarRules.ForwardTurn). 1080 " +
                 "since 2026-11-03 (the ring spin lab's pick, with the motion smear below).")]
        [SerializeField, Min(0f)] float ringTurnRate = 1080f;

        [Header("Ring spin: a spinning stick (Garrett, 2026-11-03, Docs/Studios/BrittleStarRingLab.html)")]
        [Tooltip("The stick counts as SWEEPING above this many degrees per second: the ring then hands over only to " +
                 "the turret ahead and never turns back against the sweep (the wagon-wheel fix).")]
        [SerializeField, Min(0f)] float aimSweepThreshold = 90f;
        [Tooltip("Seconds the stick's sweep rate is eased over (an exponential), so one noisy frame is not a sweep.")]
        [SerializeField, Min(0f)] float aimSweepSmoothing = 0.06f;
        [Tooltip("A stick angle that jumps more than this in one frame is a FLICK to a new aim, not a sweep: the ring " +
                 "takes the nearest turret, as it always has.")]
        [SerializeField, Range(45f, 180f)] float aimFlickDegrees = 120f;

        [Header("Ring motion smear (normalized: it adds no brightness)")]
        [Tooltip("How much of the angle the ring turned this frame it is smeared over (1 = the whole frame, a box " +
                 "shutter). The copies are AVERAGED, weights summing to one, so a smeared ring spreads its light and " +
                 "never brightens. 0 = off. Drawn by BrittleStarRingSmearPass.")]
        [SerializeField, Range(0f, 1.5f)] float ringSmear = 1f;
        [Tooltip("The ring must turn more than this many degrees in a frame to smear; slower, it is drawn as it stands.")]
        [SerializeField, Min(0f)] float ringSmearMinDegrees = 1.5f;
        [Tooltip("One copy per this many degrees of the smeared arc (at least 2).")]
        [SerializeField, Min(0.1f)] float ringSmearStepDegrees = 2.5f;
        [Tooltip("The most copies one smear averages (each is one texture read per pixel of the ring).")]
        [SerializeField, Range(2, 32)] int ringSmearMaxCopies = 24;
        [Tooltip("BrittleStarRingSmear.shader: averages the ring's copies over the swept arc. Empty = no smear (the " +
                 "ring is drawn as it stands).")]
        [SerializeField] Shader ringSmearShader;

        [Header("Heat (Garrett, 2026-10-16: 'shooting should over heat')")]
        [Tooltip("Heat each round adds (1 = the guns' heat at Space 0), so heat per second = rounds/s x guns x this. " +
                 "0.000637: the five guns at a full pull overheat in ~6 s at Space 0 (Garrett, 2026-10-17: 'from 6 to 20 seconds'); one turret at a full " +
                 "pull (0.057/s) " +
                 "runs below passive cooling.")]
        [SerializeField, Min(0f)] float heatPerRound = 0.000637f;
        [Tooltip("SPACE: the heat the guns hold before they overheat, x the Space 0 capacity (1). x3.33 at Space 10, so " +
                 "the five guns' full-pull burst runs ~20 s instead of ~6 s (Garrett: 'have the maximum heat scale with " +
                 "space', 2026-10-16; 'from 6 to 20 seconds', 2026-10-17). The Hunter's element.")]
        [SerializeField] ElementalFloat heatCapacity = ElementalFloat.Multiplier(1f, 3.3333f, Element.Space, 1f);
        [Tooltip("Heat lost per second, always (in Space 0 capacities: a bigger capacity does not cool faster).")]
        [SerializeField, Min(0f)] float coolRate = 0.12f;
        [Tooltip("Extra heat lost per second while Charge Mode's cords are out ('using charge lasers cools the guns'), " +
                 "x how far they are grown out.")]
        [SerializeField, Min(0f)] float chargeCoolRate = 0.6f;
        [Tooltip("Overheated guns fire again once they have shed (1 - this) of the Space 0 capacity: heat 0.35 at Space 0, " +
                 "the same 0.65 of heat (and so the same wait) at any Space. Meanwhile, in Space Mode, the trail lays DANGER " +
                 "prisms.")]
        [SerializeField, Range(0f, 0.95f)] float overheatRecoverBelow = 0.35f;

        // ------------------------------------------------------------------ Charge Mode: the five whip cords
        [Header("Charge Mode (the five cords: whips released, sabers pulled)")]
        [Tooltip("Each cord's length at a FULL trigger pull (the saber), world units. Released, it is cordLooseLengthScale " +
                 "x this (the whip); the pull moves between the two.")]
        [SerializeField, Min(1f)] float cordLength = 80f;
        [Tooltip("The whip's length as a multiple of the saber's (Garrett, 2026-10-16: 'extends to 150%').")]
        [SerializeField, Min(1f)] float cordLooseLengthScale = 1.5f;
        [Tooltip("Links per cord (BrittleStarCord). Each is swept against prisms and hulls every frame: 5 x this queries. " +
                 "More links = a finer wave.")]
        [SerializeField, Range(2, 32)] int cordLinks = 16;
        [Tooltip("Half-width of what a cord cuts, world units, at Charge 0 (the Thresher's chainCutRadius 0.6). " +
                 "x projectileScaleMultiplier (CHARGE). Drawn at exactly this width.")]
        [SerializeField, Min(0.05f)] float cordCutRadius = 0.6f;
        [Tooltip("1/s^2: the WHIP's wave stiffness (trigger released). A pulse at the root travels about sqrt(this) links " +
                 "a second - 144: ~12/s, so a flick takes over a second to reach the tip of a 16-link cord. (80% of the " +
                 "first whip's 180 - Garrett, 2026-10-16: 'even less stiff'.)")]
        [SerializeField, Min(0f)] float cordLooseWave = 144f;
        [Tooltip("1/s^2: the fully rigid cord's wave stiffness. A full pull reaches only cordMaxRigidity of the way here.")]
        [SerializeField, Min(0f)] float cordRigidWave = 2500f;
        [Tooltip("1/s: how fast the whip's waves die. Low = high inertia: a flick rings on for seconds.")]
        [SerializeField, Min(0f)] float cordLooseDamping = 0.4f;
        [Tooltip("1/s: the fully rigid cord's damping (a full pull reaches cordMaxRigidity of the way here).")]
        [SerializeField, Min(0f)] float cordRigidDamping = 14f;
        [Tooltip("1/s^2: the pull of every link onto the straight arm's line at FULL rigidity (x rigidity squared).")]
        [SerializeField, Min(0f)] float cordPose = 1600f;
        [Tooltip("How rigid a FULL pull makes the cords, 0 (still a whip) to 1 (the rigid dials exactly). 0.4: the saber " +
                 "keeps 60% of the whip's elasticity (Garrett: 'not completely rigid at full press', 0.7 on 2026-10-16, " +
                 "then 'further reduce… to 40%' on 2026-10-17). The pull " +
                 "still shortens the cords all the way to cordLength.")]
        [SerializeField, Range(0f, 1f)] float cordMaxRigidity = 0.4f;
        [Tooltip("1/s: how fast the cords follow the trigger between whip and saber (an ease, so a digital press does not " +
                 "snap them).")]
        [SerializeField, Min(0.1f)] float cordRigidityRate = 12f;
        [Tooltip("Debris of a cut prism leaves at this share of the cutting link's velocity (the Thresher's 0.5).")]
        [SerializeField, Min(0f)] float cordDebrisRestitution = 0.5f;
        [Tooltip("Cap on that debris speed, u/s (0 = none). A whipping tip moves several hundred u/s.")]
        [SerializeField, Min(0f)] float cordDebrisSpeedLimit = 200f;
        [Tooltip("Drawn segments per gameplay link: the cord is drawn as a smooth curve through its links " +
                 "(ThresherChainLinks.Smooth), passing through every one of them exactly.")]
        [SerializeField, Range(1, 12)] int cordRenderSubdivisions = 4;
        [Tooltip("The cord's body (the fresnel tube's dark colour), under the bloom threshold.")]
        [SerializeField] Color cordColor = new(0.07f, 0.065f, 0.065f);
        [Tooltip("The rim's brightness: the pilot's domain colour normalised to this max channel (the Thresher's taut 0.75).")]
        [SerializeField, Range(0f, 1f)] float cordRimBrightness = 0.75f;
        [Tooltip("The rim's colour when no palette is loaded (and for Domains.Blue, which the palette does not author).")]
        [SerializeField] Color cordFallbackRim = new(0.3f, 0.9f, 0.85f);

        // ------------------------------------------------------------------ ring detonation
        [Header("Ring Detonation")]
        [Tooltip("Launch speed of the ejected ring, u/s, on top of the hull's own velocity.")]
        [SerializeField, Min(1f)] float ringLaunchSpeed = 240f;
        [Tooltip("Seconds the ring flies before it detonates on its own.")]
        [SerializeField, Min(0.05f)] float ringFuseSeconds = 1.1f;
        [Tooltip("The ring detonates on touching any prism or enemy hull within this distance of its centre line, " +
                 "as a multiple of the ring's radius.")]
        [SerializeField, Min(0.1f)] float ringContactRadiusScale = 1f;
        [Tooltip("Spin of the flying ring about its axis, degrees per second.")]
        [SerializeField] float ringFlightSpin = 900f;
        [Tooltip("The RING EXPLOSION (Garrett, 2026-10-14): a flat disk the diameter of the cell's NUCLEUS and as thick as " +
                 "the ring, centred where the ring blows and lying in its plane. It expands from the ring's own radius out to the " +
                 "nucleus's over this many seconds, destroying every prism it passes (any domain - friendly fire) and " +
                 "stripping petals from every rival hull it passes.")]
        [SerializeField, Min(0.05f)] float ringBlastSeconds = 0.7f;
        [Tooltip("The disk's radius when the ring blows outside any cell with a nucleus, world units.")]
        [SerializeField, Min(1f)] float ringBlastFallbackRadius = 200f;
        [Tooltip("The disk's THICKNESS before Charge, a multiple of the ring's own (Garrett, 2026-10-18: 'quadruple the " +
                 "ring explosion thickness'): x4, so 4 x the ring's 0.44 u = 1.76 u at Charge 0. What it reaches AND what is drawn.")]
        [SerializeField, Min(0.1f)] float ringBlastThicknessScale = 4f;
        [Tooltip("CHARGE: multiplier on the disk's THICKNESS, on top of ringBlastThicknessScale (x4 at Charge 10). Rolling " +
                 "Thunder's element: 'larger AOE on support ring detonation'.")]
        [SerializeField] ElementalFloat blastScale = ElementalFloat.Multiplier(1f, 4f, Element.Charge, 1f);

        [Tooltip("The ring explosion's CORE (Shombith, 2026-11-03: the ring 'should perform an explosion which blows up " +
                 "nearby' mass): every prism within this many world units of where the ring blows is destroyed at once " +
                 "(any domain - friendly fire; your own fresh trail is spared; a shield pops first, super-shielded mass " +
                 "refuses), and every rival hull inside it is scored as the disk scores it. The flat disk alone reached " +
                 "only the prisms lying in the ring's plane. 0 = no core.")]
        [SerializeField, Min(0f)] float ringBlastCoreRadius = 40f;

        [Header("Ring Detonation: the look (BrittleStarBlastForm - five-fold faceted volumes, from the ring that blew)")]
        [Tooltip("The front's radial width between crests, x its radius (Garrett, 2026-10-18: 'nothing in the environment " +
                 "should ever look flat' - every thickness of the look scales with the blast, so it reads as a volume at " +
                 "any size). Its outer ridge is always the blast's true edge.")]
        [SerializeField, Range(0.005f, 0.2f)] float ringBlastFrontWidth = 0.05f;
        [Tooltip("The front's height, x its radius (never thinner than the disk itself). The disk that does the damage " +
                 "is only a couple of units thick; this is the look's volume, standing above and below it.")]
        [SerializeField, Range(0.005f, 0.2f)] float ringBlastFrontHeight = 0.04f;
        [Tooltip("How far the arms, the ring's pentagon and their crystals may stand out of the plane, x the front's " +
                 "radius: the arms coil within it.")]
        [SerializeField, Range(0f, 0.3f)] float ringBlastRelief = 0.12f;
        [Tooltip("Degrees the explosion's five-fold pattern turns on after the blast, keeping the thrown ring's spin " +
                 "(its direction) and braking to a stop over ringBlastSeconds.")]
        [SerializeField, Range(0f, 720f)] float ringBlastSpinDegrees = 108f;
        [Tooltip("Each of the front's five crests (where the arms meet it) is this much wider than the front, " +
                 "x ringBlastFrontWidth. 0 = a plain torus.")]
        [SerializeField, Range(0f, 10f)] float ringBlastCrestDepth = 2.5f;
        [Tooltip("The thrown ring's own pentagon grows with the blast, its corners at this share of the front's radius.")]
        [SerializeField, Range(0f, 1f)] float ringBlastPentagonLag = 0.8f;
        [Tooltip("Each of the five arms' thickness at its root (the ring's point), x the front's radius; they taper to " +
                 "30% at their crystal head and thin as the blast spends itself. The ring's pentagon is a quarter of it.")]
        [SerializeField, Range(0.005f, 0.3f)] float ringBlastArmWidth = 0.08f;
        [Tooltip("How far each arm wriggles sideways, x its length (a wave running from root to tip, the animal's); a " +
                 "quarter-wave on, it coils out of the plane within ringBlastRelief.")]
        [SerializeField, Range(0f, 0.3f)] float ringBlastArmWiggle = 0.06f;
        [Tooltip("Degrees each arm's tip trails its root, curling back against the spin (a pinwheel).")]
        [SerializeField, Range(0f, 90f)] float ringBlastArmCurlDegrees = 30f;

        // ------------------------------------------------------------------ level-5 upgrades (Shombith, 2026-11-03)
        [Header("Level-5 upgrades (each gated on IsUpgradeActive for its element)")]
        [Tooltip("SPACE L5 (Explosive Rounds): a round a prism stops bursts, destroying every prism within this many " +
                 "world units of where it struck (any domain - friendly fire, as the rounds have it; your own fresh trail " +
                 "is spared; a shield pops first). Prisms only: the round's own hit on a hull is already the scored hit.")]
        [SerializeField, Min(0.5f)] float explosiveRoundRadius = 10f;
        [Tooltip("SPACE L5: the most round bursts resolved in one frame; the rest of that frame's bursts are dropped " +
                 "(five guns at 90 rounds/s each can stop hundreds of rounds a second).")]
        [SerializeField, Range(1, 64)] int explosiveRoundMaxPerFrame = 12;
        [Tooltip("CHARGE L5 (Thrown Sabers): with Charge Mode's cords held RIGID (RT held, the cords fully out), LT cuts " +
                 "all five off and throws them along the lines they point - the right stick aims them as it aims the " +
                 "cords (centred they converge ahead, pushed they fan out). The ring is thrown only when the sabers " +
                 "cannot be. Each saber's speed, world units per second, on top of the hull's own velocity.")]
        [SerializeField, Min(1f)] float thrownSaberSpeed = 220f;
        [Tooltip("CHARGE L5: seconds a thrown saber flies, cutting every prism it crosses and striking rival hulls, " +
                 "before it is gone.")]
        [SerializeField, Min(0.05f)] float thrownSaberSeconds = 1.2f;
        [Tooltip("CHARGE L5: a thrown saber's spin about its middle, degrees per second (it tumbles end over end in " +
                 "the plane of its throw).")]
        [SerializeField] float thrownSaberSpin = 720f;
        [Tooltip("CHARGE L5: seconds before new cords can grow back out of the muzzles after a throw.")]
        [SerializeField, Min(0f)] float thrownSaberRegrowSeconds = 1.5f;
        [Tooltip("TIME L5 (Speed Roll): in Speed Mode the right stick ROLLS the hull instead of turning the ring - its " +
                 "x is the roll, degrees per second at full deflection.")]
        [SerializeField, Min(0f)] float speedRollDegreesPerSecond = 220f;
        [Tooltip("TIME L5: the right stick's y PULLS the nose (pitch) at up to this many degrees per second, so a roll " +
                 "then a pull turns the hull at Speed Mode's speed. 0 = roll only.")]
        [SerializeField, Min(0f)] float speedRollPullDegreesPerSecond = 90f;

        // ------------------------------------------------------------------ the ring stock
        [Header("Ring stock (Garrett, 2026-10-16)")]
        [Tooltip("Spare rings the hull can store. A crystal adds one; the ring can be thrown only with a spare, and the " +
                 "mounted ring then regrows on its own.")]
        [SerializeField, Range(1, 5)] int maxSpareRings = 2;
        [Tooltip("Seconds the mounted ring takes to regrow after a throw (Garrett, 2026-10-16: 3 s). The modes are off " +
                 "until it has.")]
        [SerializeField, Min(0.05f)] float rebuildSeconds = 3f;

        [Header("Scored hits (PvP is petals only)")]
        [Tooltip("The ring explosion's hit on an enemy hull: its class (and so its price in petals).")]
        [SerializeField] CombatHitClass ringBlastHitClass = CombatHitClass.MissileBlast;
        [Tooltip("Seconds before the same victim can be scored by this pilot's ring explosion again.")]
        [SerializeField, Min(0f)] float ringBlastHitCooldown = 0.5f;
        [Tooltip("A cord across an enemy hull is a scored STRIKE (a blade: CombatHitClass's contact verb) at the fleet " +
                 "price. Seconds before the same victim can be struck by this pilot's cords again.")]
        [SerializeField, Min(0.05f)] float cordHitCooldown = 0.5f;
        [Tooltip("Shared combat-hit channel (StatsManager scores from it).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        // ------------------------------------------------------------------ autopilot
        [Header("Autopilot (input-only: the AI presses the same replicated controls a human does)")]
        [Tooltip("Seconds between the AI's mode decisions.")]
        [SerializeField, Min(0.2f)] float aiModeIntervalSeconds = 3f;
        [Tooltip("Rival prisms on the line ahead that make the AI hold the trigger.")]
        [SerializeField, Min(1)] int aiFireMinTargets = 2;
        [Tooltip("Rival prisms on the line ahead that make the AI switch to Space (Destruction) Mode's five guns; within " +
                 "cord reach it takes Charge Mode's sabers instead.")]
        [SerializeField, Min(1)] int aiDestructionMinTargets = 8;
        [Tooltip("Rival prisms near the line ahead that make the AI throw its ring.")]
        [SerializeField, Min(1)] int aiDetonateMinTargets = 18;
        [Tooltip("Degrees off the nose beyond which the AI's target is far enough round to want Speed Mode.")]
        [SerializeField, Range(0f, 180f)] float aiSpeedModeDegrees = 50f;
        [Tooltip("Shortest time the AI holds or releases the trigger.")]
        [SerializeField, Min(0.05f)] float aiTriggerMinSeconds = 0.4f;

        // ------------------------------------------------------------------ audio (exposed, shipped EMPTY)
        [Header("Audio (FMOD — every slot ships empty until audio authors it)")]
        [SerializeField] EventReference speedModeEvent;
        [SerializeField] EventReference creationModeEvent;
        [SerializeField] EventReference destructionModeEvent;
        [SerializeField] EventReference chargeModeEvent;
        [SerializeField] EventReference modeOffEvent;
        [SerializeField] EventReference fireEvent;
        [Tooltip("A cord cuts a prism (one per cut, at the cut).")]
        [SerializeField] EventReference cordSliceEvent;
        [SerializeField] EventReference ringLaunchEvent;
        [SerializeField] EventReference ringBlastEvent;
        [SerializeField] EventReference ringRebuildEvent;
        [Tooltip("A crystal adds a spare ring.")]
        [SerializeField] EventReference chargeStoredEvent;
        [Tooltip("The guns overheat.")]
        [SerializeField] EventReference overheatEvent;
        [Tooltip("SPACE L5: an explosive round bursts (one per burst, at the burst).")]
        [SerializeField] EventReference explosiveRoundEvent;
        [Tooltip("CHARGE L5: the five sabers are cut off and thrown.")]
        [SerializeField] EventReference saberThrowEvent;

        // ------------------------------------------------------------------ accessors
        public float ModeBlendSeconds => modeBlendSeconds;
        public ElementalFloat SpeedModeMultiplier => speedModeMultiplier;
        public float SpeedModeSpacingFollow => speedModeSpacingFollow;
        public float SpeedModeStretch => speedModeStretch;
        public ElementalFloat PrismWidthScale => prismWidthScale;
        public float CreationSpacing => creationSpacing;
        public float CreationRadiusScale => creationRadiusScale;
        public float CreationOffset => creationOffset;
        public int CreationMaxPerFrame => creationMaxPerFrame;
        public float CreationSpeedMultiplier => creationSpeedMultiplier;
        public float CreationWaitTime => creationWaitTime;
        public float FireRate => fireRate;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileTime => projectileTime;
        public ElementalFloat RangeMultiplier => rangeMultiplier;
        public float ProjectileScale => projectileScale;
        public ElementalFloat ProjectileScaleMultiplier => projectileScaleMultiplier;
        public float RoundGrowthFactor => roundGrowthFactor;
        public float ArmFocusDistance => armFocusDistance;
        public float ArmSplayDegrees => armSplayDegrees;
        public float AimDeadzone => aimDeadzone;
        public float RingTurnRate => ringTurnRate;
        public float AimSweepThreshold => aimSweepThreshold;
        public float AimSweepSmoothing => aimSweepSmoothing;
        public float AimFlickDegrees => aimFlickDegrees;
        public float RingSmear => ringSmear;
        public float RingSmearMinDegrees => ringSmearMinDegrees;
        public float RingSmearStepDegrees => ringSmearStepDegrees;
        public int RingSmearMaxCopies => ringSmearMaxCopies;
        public Shader RingSmearShader => ringSmearShader;
        public float HeatPerRound => heatPerRound;
        public ElementalFloat HeatCapacity => heatCapacity;
        public float CoolRate => coolRate;
        public float ChargeCoolRate => chargeCoolRate;
        public float OverheatRecoverBelow => overheatRecoverBelow;
        public float CordLength => cordLength;
        public float CordLooseLengthScale => cordLooseLengthScale;
        public int CordLinks => cordLinks;
        public float CordCutRadius => cordCutRadius;
        public float CordLooseWave => cordLooseWave;
        public float CordRigidWave => cordRigidWave;
        public float CordLooseDamping => cordLooseDamping;
        public float CordRigidDamping => cordRigidDamping;
        public float CordPose => cordPose;
        public float CordMaxRigidity => cordMaxRigidity;
        public float CordRigidityRate => cordRigidityRate;
        public float CordDebrisRestitution => cordDebrisRestitution;
        public float CordDebrisSpeedLimit => cordDebrisSpeedLimit;
        public int CordRenderSubdivisions => cordRenderSubdivisions;
        public Color CordColor => ColorTrace.Retrace(ColorTrace.World, cordColor);
        public float CordRimBrightness => cordRimBrightness;
        public Color CordFallbackRim => ColorTrace.Retrace(ColorTrace.World, cordFallbackRim);
        public float RingLaunchSpeed => ringLaunchSpeed;
        public float RingFuseSeconds => ringFuseSeconds;
        public float RingContactRadiusScale => ringContactRadiusScale;
        public float RingFlightSpin => ringFlightSpin;
        public float RingBlastSeconds => ringBlastSeconds;
        public float RingBlastFallbackRadius => ringBlastFallbackRadius;
        public float RingBlastThicknessScale => ringBlastThicknessScale;
        public ElementalFloat BlastScale => blastScale;
        public float RingBlastFrontWidth => ringBlastFrontWidth;
        public float RingBlastFrontHeight => ringBlastFrontHeight;
        public float RingBlastRelief => ringBlastRelief;
        public float RingBlastSpinDegrees => ringBlastSpinDegrees;
        public float RingBlastCrestDepth => ringBlastCrestDepth;
        public float RingBlastPentagonLag => ringBlastPentagonLag;
        public float RingBlastArmWidth => ringBlastArmWidth;
        public float RingBlastArmWiggle => ringBlastArmWiggle;
        public float RingBlastArmCurlDegrees => ringBlastArmCurlDegrees;
        public int MaxSpareRings => maxSpareRings;
        public float RebuildSeconds => rebuildSeconds;
        public CombatHitClass RingBlastHitClass => ringBlastHitClass;
        public float RingBlastHitCooldown => ringBlastHitCooldown;
        public float CordHitCooldown => cordHitCooldown;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public float AiModeIntervalSeconds => aiModeIntervalSeconds;
        public int AiFireMinTargets => aiFireMinTargets;
        public int AiDestructionMinTargets => aiDestructionMinTargets;
        public int AiDetonateMinTargets => aiDetonateMinTargets;
        public float AiSpeedModeDegrees => aiSpeedModeDegrees;
        public float AiTriggerMinSeconds => aiTriggerMinSeconds;
        public EventReference SpeedModeEvent => speedModeEvent;
        public EventReference CreationModeEvent => creationModeEvent;
        public EventReference DestructionModeEvent => destructionModeEvent;
        public EventReference ChargeModeEvent => chargeModeEvent;
        public EventReference ModeOffEvent => modeOffEvent;
        public EventReference FireEvent => fireEvent;
        public EventReference CordSliceEvent => cordSliceEvent;
        public EventReference RingLaunchEvent => ringLaunchEvent;
        public EventReference RingBlastEvent => ringBlastEvent;
        public EventReference RingRebuildEvent => ringRebuildEvent;
        public EventReference ChargeStoredEvent => chargeStoredEvent;
        public EventReference OverheatEvent => overheatEvent;
        public float RingBlastCoreRadius => ringBlastCoreRadius;
        public float ExplosiveRoundRadius => explosiveRoundRadius;
        public int ExplosiveRoundMaxPerFrame => explosiveRoundMaxPerFrame;
        public float ThrownSaberSpeed => thrownSaberSpeed;
        public float ThrownSaberSeconds => thrownSaberSeconds;
        public float ThrownSaberSpin => thrownSaberSpin;
        public float ThrownSaberRegrowSeconds => thrownSaberRegrowSeconds;
        public float SpeedRollDegreesPerSecond => speedRollDegreesPerSecond;
        public float SpeedRollPullDegreesPerSecond => speedRollPullDegreesPerSecond;
        public EventReference ExplosiveRoundEvent => explosiveRoundEvent;
        public EventReference SaberThrowEvent => saberThrowEvent;
    }
}
