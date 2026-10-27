using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning, text and sounds for the PLAIN payload (Docs/Pilots/ARTIFACTS.md §2): a parcel of an
    /// Ark's own hull that any vessel grabs with RB beside its squad's Ark, carries on its hull,
    /// and releases with RB to crack super-shielded and shielded prisms around it.
    ///
    /// Place the asset at <c>Resources/PayloadConfig</c> (written by
    /// <c>Tools/Build/author_payload_assets.py</c>, which owns it: <c>--check</c> diffs disk). With no
    /// asset the defaults below apply, the burst carries no effect, and a warning names the
    /// missing asset once.
    /// </summary>
    [CreateAssetMenu(fileName = "PayloadConfig", menuName = "ScriptableObjects/Vessel/Payload Config")]
    public class PayloadConfigSO : ScriptableObject
    {
        [Header("Grab")]
        [Tooltip("How close to its squad's Ark (the Ark's centre) a vessel must be for RB to take a payload.")]
        [Min(1f)]
        [SerializeField] float grabReach = 160f;

        [Tooltip("Hull prisms one payload takes off the Ark. Moved, never minted: every payload is plating " +
                 "the Ark no longer has.")]
        [Min(1)]
        [SerializeField] int parcelPrisms = 6;

        [Tooltip("Hull prisms the hold never gives away. An Ark's only deaths are forces from outside it, so " +
                 "its own crew can never strip it bare.")]
        [Min(1)]
        [SerializeField] int hullReserve = 12;

        [Header("Carry")]
        [Tooltip("Where the parcel rides, in the carrier's local space (+z the bow, +y the dorsal side).")]
        [SerializeField] Vector3 carryOffset = new(0f, 4.5f, -3f);

        [Tooltip("Spacing between the parcel's prisms while carried, world units.")]
        [Min(0.1f)]
        [SerializeField] float carrySpacing = 1.6f;

        [Tooltip("Scale of each carried prism relative to its hull scale - cargo, not a second hull.")]
        [Range(0.05f, 1f)]
        [SerializeField] float carryScale = 0.4f;

        [Tooltip("World units per second a prism closes on its seat in the parcel. It flies from the hull " +
                 "to the carrier rather than popping there.")]
        [Min(1f)]
        [SerializeField] float seatSpeed = 120f;

        [Header("Release")]
        [Tooltip("Radius of the burst around the release point, world units.")]
        [Min(1f)]
        [SerializeField] float burstRadius = 60f;

        [Tooltip("What the plain payload does to every prism in the burst. The crack is " +
                 "PayloadCrackPrismEffect; an artifact replaces this list with its own.")]
        [SerializeField] PayloadPrismEffectSO[] burstEffects = System.Array.Empty<PayloadPrismEffectSO>();

        [Tooltip("Seconds the spent parcel withers over before its prisms return to the pool.")]
        [Min(0f)]
        [SerializeField] float witherSeconds = 0.8f;

        // The lines themselves live in the asset (author_payload_assets.py), never as C# defaults:
        // a missing asset shows nothing rather than a hardcoded sentence (CLAUDE.md, player-facing text).
        [Header("Text (authored; empty shows nothing)")]
        [Tooltip("Banner when the local pilot takes a payload.")]
        [SerializeField] string grabbedLine = "";

        [Tooltip("Banner when the local pilot releases a payload.")]
        [SerializeField] string releasedLine = "";

        [Tooltip("Banner when RB is pressed out of reach of the squad's Ark.")]
        [SerializeField] string outOfReachLine = "";

        [Tooltip("Banner when the hold has no hull left to give.")]
        [SerializeField] string holdEmptyLine = "";

        [Min(0f)]
        [SerializeField] float bannerSeconds = 3f;

        [Header("Audio (shipped empty: an unwired slot is a visible TODO)")]
        [SerializeField] EventReference grabSound;
        [SerializeField] EventReference releaseSound;
        [SerializeField] EventReference refusedSound;

        public float GrabReach => grabReach;
        public int ParcelPrisms => parcelPrisms;
        public int HullReserve => hullReserve;
        public Vector3 CarryOffset => carryOffset;
        public float CarrySpacing => carrySpacing;
        public float CarryScale => carryScale;
        public float SeatSpeed => seatSpeed;
        public float BurstRadius => burstRadius;
        public PayloadPrismEffectSO[] BurstEffects => burstEffects ?? System.Array.Empty<PayloadPrismEffectSO>();
        public float WitherSeconds => witherSeconds;
        public string GrabbedLine => grabbedLine;
        public string ReleasedLine => releasedLine;
        public string OutOfReachLine => outOfReachLine;
        public string HoldEmptyLine => holdEmptyLine;
        public float BannerSeconds => bannerSeconds;
        public EventReference GrabSound => grabSound;
        public EventReference ReleaseSound => releaseSound;
        public EventReference RefusedSound => refusedSound;
    }
}
