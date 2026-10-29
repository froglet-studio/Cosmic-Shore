using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tiger Moth's <b>Drop</b> — TIME. A tiger moth escapes a bat by shutting its wings and dropping. Hold the
    /// trigger and the hull shuts its wings and falls belly-first along its own down (the way its dust rains),
    /// without turning, passing through everything; the chase camera swings up over it so the fall reads as flying
    /// forward. Let go and it lands: the hull pitches 90 degrees nose-down into the frame the camera is already in
    /// and flies straight on along the line it fell. Design record: <c>R_VesselActions/TIGER_MOTH.md</c>.
    ///
    /// <para><b>The recharge is paid by the distance</b> (Garrett, 2026-10-29): a little hop costs a little, a
    /// medium drop (<see cref="FullCooldownDistance"/>) costs the full 20 s, and anything past that is free — it
    /// extends the recharge no further and the drop has no limit. Only a drop that pays the FULL recharge leaves a
    /// wormhole pair standing (the Fold's mouths, with the Fold's rival toll).</para>
    ///
    /// <para><b>Time's dials.</b> The continuous dial is the recharge multiplier (×0.5 at Time 10, never worse than
    /// ×2, the Fold's). Time 5 — "Afterburn" (Garrett, 2026-10-29: "keep your invulnerability and a speed boost for 3
    /// seconds after the drop ends") — the hull stays incorporeal for <see cref="AfterburnSeconds"/> after it lands,
    /// and flies them on a throttle boost that eases from <see cref="AfterburnThrottle"/> back to cruise.</para>
    ///
    /// <para><b>Charge 5 — "Toll Gate"</b> (Garrett, 2026-10-29: "the portal strips the elements of opposing
    /// players"). The wormhole is NEUTRAL by default: the material's own rim, everyone rides free. With the upgrade it
    /// wears the pilot's domain hue and strips rivals who thread it (<see cref="RivalTollPetalsPerElement"/>). A
    /// standing pair switches the moment the upgrade unlocks or relocks (<see cref="TollsRivals"/>).</para>
    ///
    /// <para>The asset is SHARED by every Tiger Moth in a match and holds no per-vessel state; the executor owns
    /// the drop.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "TigerMothDropAction",
        menuName = "ScriptableObjects/Vessel Actions/Tiger Moth Drop")]
    public class TigerMothDropActionSO : ShipActionSO
    {
        [Header("Recharge (paid by distance)")]
        [Tooltip("The FULL recharge, seconds at the resting Time level: what a medium drop costs, and the most any drop " +
                 "costs. Garrett, 2026-10-29: 20.")]
        [SerializeField, Min(0f)] float fullCooldownSeconds = 20f;

        [Tooltip("The 'medium' drop, world units: a drop this long or longer pays the full recharge and leaves a " +
                 "wormhole; a shorter one pays its share (half the distance, half the wait). Past it a drop is free. " +
                 "900 is one held second at the drop speed.")]
        [SerializeField, Min(1f)] float fullCooldownDistance = 900f;

        [Tooltip("TIME -> recharge: the cooldown MULTIPLIER at Time level 10. 0.5 halves the wait.")]
        [SerializeField] float cooldownMultiplierAtFullTime = 0.5f;

        [Tooltip("Ceiling on the cooldown multiplier: the worst a Time DEFICIT can make the recharge, so a debuffed " +
                 "Tiger Moth is slowed down rather than losing the ability.")]
        [SerializeField, Min(1f)] float maxCooldownMultiplier = 2f;

        [Header("Drop")]
        [Tooltip("How fast the hull falls belly-first while the trigger is held, world units per second. It falls as " +
                 "long as the trigger is held: there is no longest drop.")]
        [SerializeField, Min(1f)] float dropSpeed = 900f;

        [Tooltip("Seconds from the press to full drop speed.")]
        [SerializeField, Min(0f)] float dropEaseSeconds = 0.12f;

        [Header("Landing")]
        [Tooltip("Seconds from letting go to landed. Over this time the hull pitches 90 degrees nose-down into the " +
                 "line it fell on, and its speed along that line eases from the fall speed back to the cruise it " +
                 "had before the press.")]
        [SerializeField, Min(0.01f)] float landSeconds = 0.25f;

        [Header("Afterburn (TIME level 5)")]
        [Tooltip("TIME level-5 'Afterburn': seconds the hull stays incorporeal after the landing (passes through " +
                 "prisms, crystals, hulls and mouths; its dust still works). Garrett, 2026-10-29: 3.")]
        [SerializeField, Min(0f)] float afterburnSeconds = 3f;

        [Tooltip("TIME level-5 'Afterburn': the throttle multiplier at the landing, easing back to 1 over the " +
                 "afterburn. 2 = twice the cruise at the landing. A starting number, not Garrett's.")]
        [SerializeField, Min(1f)] float afterburnThrottle = 2f;

        [Header("Camera")]
        [Tooltip("Seconds for the chase camera to swing up over the hull after the press, so down reads as forward. " +
                 "There is no swing back: the landing turns the hull into this frame instead.")]
        [SerializeField, Min(0f)] float cameraEnterSeconds = 0.35f;

        [Header("Wormhole (only a drop that pays the full recharge)")]
        [Tooltip("Radius of each mouth, world units: how big it looks AND how precisely you fly into it.")]
        [SerializeField, Min(1f)] float gateRadius = 33f;

        [Tooltip("Furthest the camera may be from a mouth to show the EXACT view through it (a second render of the " +
                 "world); beyond it plus the fade band, the mouth shows its partner's panorama.")]
        [SerializeField, Min(0f)] float portalWindowRange = 2500f;

        [Tooltip("Distance past the exact range over which the exact view crossfades into the panorama.")]
        [SerializeField, Min(1f)] float portalWindowFadeBand = 600f;

        [Tooltip("Resolution of the exact view as a fraction of the gameplay camera's.")]
        [SerializeField, Range(0.25f, 1f)] float portalWindowRenderScale = 0.75f;

        [Tooltip("Texels per side of each of the six panorama faces a mouth's camera captures.")]
        [SerializeField, Range(64, 1024)] int panoramaFaceSize = 256;

        [Tooltip("Surface material for the mouths (CosmicShore/Wormhole). The rim is tinted to the placer's DOMAIN. " +
                 "Empty = the mouths carry pilots but draw nothing.")]
        [SerializeField] Material wormholeMaterial;

        [Tooltip("Seconds a mouth takes to bloom in, and to wither when the next full drop replaces it.")]
        [SerializeField, Min(0.01f)] float gateBloomSeconds = 0.45f;

        [Tooltip("Longest a PEER waits for the replicated landing before it gives up on laying the pair. The owner " +
                 "never waits (its pose is local); a peer still late at the deadline lays none.")]
        [SerializeField, Min(0f)] float gateSettleSeconds = 1f;

        [Header("Toll (CHARGE level 5: pilots of other domains)")]
        [Tooltip("CHARGE level-5 'Toll Gate': whole petals stripped from EACH of a rival's four elements every time " +
                 "they thread this Tiger Moth's wormhole while its Charge upgrade is active, left on the mouth they " +
                 "went in by as crystals. Without the upgrade the mouths are neutral and everyone rides free. 0 = " +
                 "free even with it. Its own domain never pays.")]
        [SerializeField, Range(0, 15)] int rivalTollPetalsPerElement = 15;

        [Tooltip("World units/second the stripped crystals are thrown off the mouth's surface.")]
        [SerializeField, Min(0f)] float rivalTollShedSpeed = 25f;

        [Tooltip("The combat-hit class a toll scores as against the rival who paid it (PvP is petals only).")]
        [SerializeField] CombatHitClass tollHitClass = CombatHitClass.Debuff;

        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager listens on. Fail-loud.")]
        [SerializeField] ScriptableEventCombatHitStats onTollHitLanded;

        public float FullCooldownSeconds => Mathf.Max(0f, fullCooldownSeconds);
        public float DropSpeed => dropSpeed;
        public float DropEaseSeconds => dropEaseSeconds;
        public float LandSeconds => landSeconds;
        public float CameraEnterSeconds => cameraEnterSeconds;

        public float GateRadius => gateRadius;
        public float PortalWindowRange => portalWindowRange;
        public float PortalWindowFadeBand => portalWindowFadeBand;
        public float PortalWindowRenderScale => portalWindowRenderScale;
        public int PanoramaFaceSize => panoramaFaceSize;
        public Material WormholeMaterial => wormholeMaterial;
        public float GateBloomSeconds => gateBloomSeconds;
        public float GateSettleSeconds => gateSettleSeconds;
        public int RivalTollPetalsPerElement => Mathf.Max(0, rivalTollPetalsPerElement);
        public float RivalTollShedSpeed => rivalTollShedSpeed;
        public CombatHitClass TollHitClass => tollHitClass;
        public ScriptableEventCombatHitStats OnTollHitLanded => onTollHitLanded;

        /// <summary>The medium drop: the full recharge, and the wormhole, at this distance.</summary>
        public float FullCooldownDistance => Mathf.Max(1f, fullCooldownDistance);

        public float AfterburnSeconds => Mathf.Max(0f, afterburnSeconds);
        public float AfterburnThrottle => Mathf.Max(1f, afterburnThrottle);

        /// <summary>TIME level 5, "Afterburn". Gated on the REPLICATED unlock bit, so every peer keeps the landed hull
        /// incorporeal for the same window.</summary>
        public static bool HasAfterburn(IVesselStatus status)
        {
            var abilities = status?.ElementalAbilityHandler;
            return abilities != null && abilities.IsUpgradeActive(Element.Time);
        }

        /// <summary>CHARGE level 5, "Toll Gate": does this pilot's wormhole strip rivals (and wear the domain hue)?
        /// Gated on the REPLICATED unlock bit, so every peer charges the same transits.</summary>
        public static bool TollsRivals(IVesselStatus status)
        {
            var abilities = status?.ElementalAbilityHandler;
            return abilities != null && abilities.IsUpgradeActive(Element.Charge);
        }

        /// <summary>The recharge a drop of <paramref name="distance"/> pays, at the live TIME level (read at use time).</summary>
        public float ResolveCooldown(IVesselStatus status, float distance)
        {
            float multiplier = ElementalScaling.Multiplier(
                status, Element.Time, cooldownMultiplierAtFullTime, cooldownMultiplierAtFullTime);
            return TigerMothDropMath.Cooldown(distance, FullCooldownDistance, FullCooldownSeconds)
                   * Mathf.Min(multiplier, maxCooldownMultiplier);
        }

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<TigerMothDropExecutor>()?.Engage(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<TigerMothDropExecutor>()?.Release(this, vesselStatus);
    }
}
