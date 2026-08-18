using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Urchin's <b>Track Projector</b> — press the trigger and a straight stretch of
    /// single-track trail forms out in front of the nose, an on-ramp the vessel can immediately
    /// fly into and grind.
    ///
    /// It exists because the Urchin's whole loop runs on someone's prismscape and the map has
    /// long empty stretches with nothing to latch onto: without a rail the vessel is an ordinary
    /// ship with a shotgun. The track is not a wall, a weapon or a trap — it is ONE lane of
    /// ordinary conserved trail mass in the pilot's own domain, which means the ride reads it as
    /// friendly terrain (150 u/s), the ride GROWS it as it passes, and the food web can graze it
    /// like any other trail. Nothing is created that the rest of the game does not already
    /// understand.
    ///
    /// SPACE — reach — is its element: the dial is how LONG the stretch is, and the level-5
    /// upgrade ("Long Haul") adds a further authored stretch on top. The cooldown is deliberately
    /// NOT elemental: TIME belongs to Slip on this vessel, and a second Time consumer would be
    /// the double-dip the fleet convention exists to prevent. It is the Squirrel boost ring's
    /// 20-second cooldown, matched on purpose so the two "place a structure" abilities share one
    /// cadence.
    ///
    /// The asset is SHARED by every Urchin in a match and holds no per-vessel state.
    /// See <c>URCHIN_TRACK_PROJECTOR.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "UrchinTrackAction",
        menuName = "ScriptableObjects/Vessel Actions/Urchin Track")]
    public class UrchinTrackActionSO : ShipActionSO
    {
        [Header("Track")]
        [Tooltip("Length of the stretch in world units, before the SPACE multiplier. 100 is the " +
                 "authored default - long enough to be worth launching off, short enough that a " +
                 "deploy is a decision about WHERE rather than free terrain everywhere.")]
        [SerializeField] float trackLength = 100f;

        [Tooltip("World units between prism centres along the track. The vessel's own wake lays " +
                 "at its wavelength (10 on the Urchin) and the ride bridges gaps happily, so " +
                 "this is a look-and-catchability dial, not a correctness one: tighter is easier " +
                 "to fly into and costs more prisms.")]
        [SerializeField] float prismSpacing = 8f;

        [Tooltip("World scale of each track prism. Authored a little heavier than one of the " +
                 "vessel's own wake ribbons (2 x 2.5 x 4): this is a rail you meant to place, " +
                 "and it has to be catchable at grind speed. Z is the length ALONG the track.")]
        [SerializeField] Vector3 prismScale = new(3f, 3f, 6f);

        [Header("Placement")]
        [Tooltip("Minimum distance ahead of the vessel the track's mouth forms, world units. " +
                 "Far enough that it never materialises inside the hull when slow or stopped.")]
        [SerializeField] float forwardOffset = 40f;

        [Tooltip("Seconds of travel ahead the mouth forms - the offset is speed * leadSeconds, " +
                 "floored at forwardOffset, so a fast Urchin gets room to line up instead of " +
                 "arriving on top of its own ramp.")]
        [SerializeField] float leadSeconds = 0.35f;

        [Header("Spawn / Cooldown")]
        [Tooltip("Prisms laid per frame, to spread the spawn spike.")]
        [SerializeField] int spawnPerFrame = 8;

        [Tooltip("Seconds before the track can be projected again. 20 - the same cooldown the " +
                 "Squirrel's boost-ring trigger carries.")]
        [SerializeField] float cooldown = 20f;

        [Header("Elemental (Space)")]
        [Tooltip("SPACE -> length: multiplier on trackLength at Space level 10 (1 at the resting " +
                 "level, extrapolating into the deficit band so debuffed Space SHORTENS the " +
                 "ramp). Authored here rather than taken from the map's generic multiplier so " +
                 "the trade is visible in the asset.")]
        [SerializeField] float lengthMultiplierAtFullSpace = 2f;

        [Tooltip("Floor for the Space length multiplier, so a deep deficit can never project a " +
                 "track too short to ride.")]
        [SerializeField] float minLengthMultiplier = 0.4f;

        [Tooltip("SPACE level-5 'Long Haul': extra world units of track added while the Space " +
                 "upgrade is active (per-deploy snapshot - a track already laid keeps the " +
                 "length it was laid with).")]
        [SerializeField] float upgradeExtraLength = 100f;

        public float TrackLength => Mathf.Max(1f, trackLength);
        public float PrismSpacing => Mathf.Max(0.5f, prismSpacing);
        public Vector3 PrismScale => prismScale;
        public float ForwardOffset => Mathf.Max(0f, forwardOffset);
        public float LeadSeconds => Mathf.Max(0f, leadSeconds);
        public int SpawnPerFrame => Mathf.Max(1, spawnPerFrame);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public float LengthMultiplierAtFullSpace => lengthMultiplierAtFullSpace;
        public float MinLengthMultiplier => Mathf.Max(0.01f, minLengthMultiplier);
        public float UpgradeExtraLength => Mathf.Max(0f, upgradeExtraLength);

        /// <summary>
        /// The length this deploy actually lays: the authored stretch scaled by the vessel's
        /// live SPACE level, plus the level-5 bonus stretch when "Long Haul" is unlocked.
        ///
        /// The upgrade gates on <c>IsUpgradeActive</c> — the REPLICATED unlock bit — never on a
        /// raw local level read: the track is prismscape, and two peers laying different-length
        /// rails is a divergent world. The continuous SPACE dial underneath it is a local level
        /// read and shares the vessel's standing multiplayer gap (see <c>URCHIN_BACKLOG.md</c>
        /// U1); it is the same trade every element dial on this vessel already makes.
        /// </summary>
        public float ResolveLength(IVesselStatus status)
        {
            float length = TrackLength * ElementalScaling.Multiplier(
                status, Element.Space, lengthMultiplierAtFullSpace, MinLengthMultiplier);

            var abilities = status?.ElementalAbilityHandler;
            if (abilities != null && abilities.IsUpgradeActive(Element.Space))
                length += UpgradeExtraLength;

            return length;
        }

        /// <summary>Prisms a stretch of <paramref name="length"/> takes, ends included. Pure, so
        /// the collider-budget claim in the docs is checkable without play mode.</summary>
        public int PrismCountForLength(float length)
            => Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(0f, length) / PrismSpacing) + 1);

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<UrchinTrackActionExecutor>()?.Begin(this, vesselStatus);

        /// <summary>Release: nothing. The track is placed on the press — there is no preview to
        /// commit and no charge to release.</summary>
        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}
