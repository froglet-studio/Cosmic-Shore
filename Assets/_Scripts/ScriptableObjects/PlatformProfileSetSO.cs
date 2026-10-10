using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The one asset that says which <see cref="PlatformProfileSO"/> each <see cref="DeviceTier"/>
    /// runs, and how a device is sorted into a tier. Loaded by <c>PlatformProfile</c> from
    /// <c>Resources/PlatformProfiles</c> (it must stay at <c>Assets/Resources/PlatformProfiles.asset</c>),
    /// which also pulls the three profiles into every build.
    ///
    /// Inspect what a device resolves to with FrogletTools ▸ Performance ▸ Device Tier, which also
    /// simulates a tier in the Editor. <c>Docs/PLATFORM_UNIFICATION.md</c> §3.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlatformProfiles",
        menuName = "ScriptableObjects/Platform/" + nameof(PlatformProfileSetSO))]
    public class PlatformProfileSetSO : ScriptableObject
    {
        /// <summary>Resources path <c>PlatformProfile</c> loads this from.</summary>
        public const string ResourcePath = "PlatformProfiles";

        [Header("Profile per tier")]
        [Tooltip("Windows / macOS / Linux, and the Editor unless a tier is simulated.")]
        [SerializeField] PlatformProfileSO desktop;

        [Tooltip("Phones and tablets fast enough for the full game - every current iPhone.")]
        [SerializeField] PlatformProfileSO mobileHigh;

        [Tooltip("Phones and tablets that need the reduced-cost profile - e.g. a 4 GB budget Android.")]
        [SerializeField] PlatformProfileSO mobileLow;

        [Header("Classification - iOS")]
        [Tooltip("An iPhone/iPad reporting less RAM than this (MB) is MobileLow. 2 GB devices " +
                 "report ~1,900, 3 GB ones ~2,800.")]
        [SerializeField, Min(0)] int iosLowMemoryMB = 2500;

        [Header("Classification - Android")]
        [Tooltip("An Android device needs at least this much RAM (MB) to be MobileHigh. 4 GB phones " +
                 "report ~3,600-3,800, 6 GB ones ~5,400+.")]
        [SerializeField, Min(0)] int androidHighMinMemoryMB = 5000;

        [Tooltip("Case-insensitive regexes over SystemInfo.graphicsDeviceName. An Android device is " +
                 "MobileHigh only if one matches; an unrecognised GPU is MobileLow. The boot log and " +
                 "the Device Tier window print the name a device reports - add it here.")]
        [SerializeField] string[] androidHighEndGpuPatterns =
            (string[])DeviceTierRules.DefaultAndroidHighEndGpuPatterns.Clone();

        /// <summary>The profile authored for <paramref name="tier"/>, or null if the slot is empty.</summary>
        public PlatformProfileSO For(DeviceTier tier) => tier switch
        {
            DeviceTier.MobileHigh => mobileHigh,
            DeviceTier.MobileLow => mobileLow,
            _ => desktop,
        };

        /// <summary>This asset's classification thresholds, for <see cref="DeviceTierClassifier"/>.</summary>
        public DeviceTierRules Rules => new()
        {
            IosLowMemoryMB = iosLowMemoryMB,
            AndroidHighMinMemoryMB = androidHighMinMemoryMB,
            AndroidHighEndGpuPatterns = androidHighEndGpuPatterns ?? System.Array.Empty<string>(),
        };
    }
}
