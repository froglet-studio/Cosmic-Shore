using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// This device's <see cref="DeviceTier"/> and the <see cref="PlatformProfileSO"/> it runs.
    /// Resolved once per session on first read, from <c>Resources/PlatformProfiles</c>
    /// (<see cref="PlatformProfileSetSO"/>) and the device's <c>SystemInfo</c>, and fixed for the
    /// rest of it: the systems that read it latch their choice at startup, so a tier that changed
    /// mid-session would leave them disagreeing.
    ///
    /// Runtime detection, never a compile guard (<c>Docs/CONDITIONAL_COMPILATION.md</c>): one build
    /// serves every platform, and the Editor can simulate any tier through
    /// <see cref="TierOverride"/> (FrogletTools ▸ Performance ▸ Device Tier).
    /// <c>Docs/PLATFORM_UNIFICATION.md</c> §3.
    ///
    /// Input is deliberately NOT routed through here: <c>InputController</c> already picks the touch
    /// strategy from <c>SystemInfo.deviceType</c>, the same signal <see cref="ReadDeviceFacts"/>
    /// reads, so the controls and the tier cannot disagree about whether this is a phone.
    /// </summary>
    public static class PlatformProfile
    {
        /// <summary>PlayerPrefs key holding a forced tier (its int value), absent for auto-detect.</summary>
        public const string TierOverridePrefKey = "platform.tierOverride";

        static bool s_resolved;
        static DeviceTier s_tier;
        static DeviceTier s_detectedTier;
        static PlatformProfileSO s_profile;
        static DeviceFacts s_facts;
        static string s_reason;

        /// <summary>Enter Play Mode runs with domain reload off, so the latched tier must be dropped
        /// by hand or the first session's answer would outlive a changed override.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_resolved = false;
            s_profile = null;
            s_reason = null;
        }

        /// <summary>The tier this session runs as (the override when one is set).</summary>
        public static DeviceTier Tier { get { EnsureResolved(); return s_tier; } }

        /// <summary>The tier the device itself classified as, ignoring any override.</summary>
        public static DeviceTier DetectedTier { get { EnsureResolved(); return s_detectedTier; } }

        /// <summary>
        /// The profile for <see cref="Tier"/>. Null only when the profile set asset or its slot is
        /// missing (an error is logged naming it); callers then keep their pre-tier behaviour,
        /// which is what the Desktop profile describes.
        /// </summary>
        public static PlatformProfileSO Current { get { EnsureResolved(); return s_profile; } }

        /// <summary>Why <see cref="Tier"/> was chosen, for logs and the diagnostics overlay.</summary>
        public static string Reason { get { EnsureResolved(); return s_reason; } }

        /// <summary>The facts the tier was decided from.</summary>
        public static DeviceFacts Facts { get { EnsureResolved(); return s_facts; } }

        public static bool IsMobile => Tier != DeviceTier.Desktop;

        /// <summary>
        /// A forced tier, or null for auto-detect. Persisted in PlayerPrefs on this machine, and read
        /// once per session: setting it takes effect from the next launch / Play.
        /// </summary>
        public static DeviceTier? TierOverride
        {
            get
            {
                if (!PlayerPrefs.HasKey(TierOverridePrefKey)) return null;
                int value = PlayerPrefs.GetInt(TierOverridePrefKey);
                return System.Enum.IsDefined(typeof(DeviceTier), value) ? (DeviceTier?)value : null;
            }
            set
            {
                if (value.HasValue) PlayerPrefs.SetInt(TierOverridePrefKey, (int)value.Value);
                else PlayerPrefs.DeleteKey(TierOverridePrefKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The device facts <see cref="DeviceTierClassifier"/> decides from, read live.</summary>
        public static DeviceFacts ReadDeviceFacts()
        {
            bool handheld = SystemInfo.deviceType == DeviceType.Handheld;
            DeviceOs os = Application.platform switch
            {
                RuntimePlatform.Android => DeviceOs.Android,
                RuntimePlatform.IPhonePlayer => DeviceOs.IOS,
                // The Device Simulator simulates SystemInfo but not the platform.
                _ => handheld ? DeviceTierClassifier.OsFromName(SystemInfo.operatingSystem) : DeviceOs.Other,
            };
            return new DeviceFacts(handheld, os, SystemInfo.systemMemorySize, SystemInfo.processorCount,
                SystemInfo.graphicsDeviceName);
        }

        /// <summary>The profile set asset, or null if it is missing.</summary>
        public static PlatformProfileSetSO LoadSet() =>
            Resources.Load<PlatformProfileSetSO>(PlatformProfileSetSO.ResourcePath);

        static void EnsureResolved()
        {
            if (s_resolved) return;
            s_resolved = true;

            var set = LoadSet();
            if (!set)
                CSDebug.LogError($"[Platform] Resources/{PlatformProfileSetSO.ResourcePath}.asset is missing: " +
                               "every device runs its pre-tier behaviour. Restore " +
                               $"Assets/Resources/{PlatformProfileSetSO.ResourcePath}.asset.");

            s_facts = ReadDeviceFacts();
            s_detectedTier = DeviceTierClassifier.Classify(s_facts, set ? set.Rules : DeviceTierRules.Default,
                out string detectedReason);

            var forced = TierOverride;
            s_tier = forced ?? s_detectedTier;
            s_reason = forced.HasValue
                ? $"override (device classifies as {s_detectedTier}: {detectedReason})"
                : detectedReason;

            s_profile = set ? set.For(s_tier) : null;
            if (set && !s_profile)
                CSDebug.LogError($"[Platform] {set.name} has no profile assigned for {s_tier}: this device runs " +
                               "its pre-tier behaviour. Assign one on the asset.");

            CSDebug.LogVerbose(CSLogChannel.Boot,
                $"[Platform] Device tier {s_tier} ({s_reason}). {s_facts}");
            DiagnosticsHUD.SetStat("Platform", "tier", forced.HasValue ? $"{s_tier} (override)" : s_tier.ToString());
            DiagnosticsHUD.SetStat("Platform", "device", s_facts.ToString());
        }
    }
}
