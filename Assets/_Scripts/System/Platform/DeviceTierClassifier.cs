using System;
using System.Text.RegularExpressions;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>Which mobile operating system a device runs; <see cref="Other"/> for everything else
    /// (desktop players and the Editor). See <see cref="DeviceTierClassifier.OsFromName"/> for the
    /// Device Simulator case.</summary>
    public enum DeviceOs
    {
        Other = 0,
        Android = 1,
        IOS = 2,
    }

    /// <summary>
    /// The hardware facts a <see cref="DeviceTier"/> is decided from. Plain data so the decision can
    /// be tested (and run outside Unity) without a device: <c>PlatformProfile.ReadDeviceFacts</c>
    /// fills it from <c>SystemInfo</c> / <c>Application.platform</c>.
    /// </summary>
    public readonly struct DeviceFacts
    {
        public readonly bool IsHandheld;
        public readonly DeviceOs Os;
        public readonly int SystemMemoryMB;
        public readonly int ProcessorCount;
        public readonly string GraphicsDeviceName;

        public DeviceFacts(bool isHandheld, DeviceOs os, int systemMemoryMB, int processorCount,
                           string graphicsDeviceName)
        {
            IsHandheld = isHandheld;
            Os = os;
            SystemMemoryMB = systemMemoryMB;
            ProcessorCount = processorCount;
            GraphicsDeviceName = graphicsDeviceName ?? string.Empty;
        }

        public override string ToString() =>
            $"{Os}{(IsHandheld ? " handheld" : "")}, {SystemMemoryMB} MB RAM, {ProcessorCount} cores, " +
            $"GPU '{GraphicsDeviceName}'";
    }

    /// <summary>
    /// The thresholds <see cref="DeviceTierClassifier"/> applies. Authored on
    /// <c>PlatformProfileSetSO</c> (Resources/PlatformProfiles); <see cref="Default"/> is only the
    /// fallback when that asset is missing, and mirrors what the asset ships with.
    /// </summary>
    public sealed class DeviceTierRules
    {
        /// <summary>An iPhone/iPad with less RAM than this is <see cref="DeviceTier.MobileLow"/>. 2 GB
        /// devices (iPhone 6s-8, SE 1st gen - still on iOS 15) report ~1,900; 3 GB ones ~2,800.</summary>
        public int IosLowMemoryMB = 2500;

        /// <summary>An Android device needs at least this much RAM to be
        /// <see cref="DeviceTier.MobileHigh"/>. 4 GB phones report ~3,600-3,800, 6 GB ones ~5,400+.</summary>
        public int AndroidHighMinMemoryMB = 5000;

        /// <summary>
        /// Case-insensitive regexes over <c>SystemInfo.graphicsDeviceName</c>; an Android device is
        /// <see cref="DeviceTier.MobileHigh"/> only if one matches. Deliberately conservative: an
        /// unrecognised GPU lands on MobileLow, which plays smoothly, rather than on a profile it
        /// cannot hold. The Device Tier window (FrogletTools ▸ Performance) and the boot log print
        /// the name a device reports, which is what to add here.
        /// </summary>
        public string[] AndroidHighEndGpuPatterns = (string[])DefaultAndroidHighEndGpuPatterns.Clone();

        /// <summary>
        /// Adreno 640 and up (Snapdragon 855 onward; 6x0-63x stays low), Mali-G76/G77/G78 and the
        /// G7xx+ generation, Immortalis, Samsung Xclipse. Mali-G5x/G6x, PowerVR and older Adreno are
        /// the budget and mid-range parts a 4 GB phone ships with.
        /// </summary>
        public static readonly string[] DefaultAndroidHighEndGpuPatterns =
        {
            "Adreno.*(6[4-9][0-9]|[7-9][0-9][0-9])",
            "Mali-G(7[6-9]|[7-9][0-9][0-9])",
            "Immortalis",
            "Xclipse",
        };

        public static DeviceTierRules Default => new();
    }

    /// <summary>
    /// Decides a device's <see cref="DeviceTier"/> from its <see cref="DeviceFacts"/>. Pure: no
    /// Unity API, so it is unit-tested directly and compiled and run outside the Editor.
    ///
    /// <para>Why these inputs and not a score. This title is bound by ONE CPU thread
    /// (<c>Docs/PLATFORM_UNIFICATION.md</c> §1.2), and the facts <c>SystemInfo</c> exposes say very
    /// little about single-core speed: a budget Android phone has MORE cores than an iPhone (8 slow
    /// against 2+4), which is exactly how the desktop capability score
    /// (<c>SettingsAutoDetector.CapabilityScore</c>) ranked a 4 GB Samsung above an iPhone. RAM and
    /// the GPU family are what reliably separate a budget phone from a flagship, so those decide.</para>
    /// </summary>
    public static class DeviceTierClassifier
    {
        public static DeviceTier Classify(in DeviceFacts facts, DeviceTierRules rules, out string reason)
        {
            rules ??= DeviceTierRules.Default;

            if (!facts.IsHandheld)
            {
                reason = "not a handheld device";
                return DeviceTier.Desktop;
            }

            if (facts.Os == DeviceOs.IOS)
            {
                if (facts.SystemMemoryMB < rules.IosLowMemoryMB)
                {
                    reason = $"iOS with {facts.SystemMemoryMB} MB RAM (< {rules.IosLowMemoryMB})";
                    return DeviceTier.MobileLow;
                }
                reason = $"iOS with {facts.SystemMemoryMB} MB RAM";
                return DeviceTier.MobileHigh;
            }

            // Android, and any other handheld (the Editor's Device Simulator reports Os Other).
            if (facts.SystemMemoryMB < rules.AndroidHighMinMemoryMB)
            {
                reason = $"{facts.SystemMemoryMB} MB RAM (< {rules.AndroidHighMinMemoryMB})";
                return DeviceTier.MobileLow;
            }

            string pattern = MatchingPattern(facts.GraphicsDeviceName, rules.AndroidHighEndGpuPatterns);
            if (pattern == null)
            {
                reason = $"GPU '{facts.GraphicsDeviceName}' is not on the high-end list";
                return DeviceTier.MobileLow;
            }

            reason = $"{facts.SystemMemoryMB} MB RAM and high-end GPU '{facts.GraphicsDeviceName}'";
            return DeviceTier.MobileHigh;
        }

        /// <summary>
        /// The OS named by <c>SystemInfo.operatingSystem</c> ("iOS 17.5", "iPadOS 17.5",
        /// "Android OS 14 / API-34 ..."). <c>PlatformProfile</c> uses it only when
        /// <c>Application.platform</c> is not a phone - i.e. the Editor's Device Simulator, which
        /// simulates <c>SystemInfo</c> but not the platform - so a simulated iPhone takes the iOS rule.
        /// </summary>
        public static DeviceOs OsFromName(string operatingSystem)
        {
            if (string.IsNullOrEmpty(operatingSystem)) return DeviceOs.Other;
            if (operatingSystem.StartsWith("iOS", StringComparison.OrdinalIgnoreCase)
                || operatingSystem.StartsWith("iPadOS", StringComparison.OrdinalIgnoreCase))
                return DeviceOs.IOS;
            if (operatingSystem.StartsWith("Android", StringComparison.OrdinalIgnoreCase))
                return DeviceOs.Android;
            return DeviceOs.Other;
        }

        /// <summary>The first pattern that matches <paramref name="gpuName"/>, or null. A malformed
        /// pattern is skipped (it never matches) rather than throwing at boot.</summary>
        public static string MatchingPattern(string gpuName, string[] patterns)
        {
            if (string.IsNullOrEmpty(gpuName) || patterns == null) return null;

            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                try
                {
                    if (Regex.IsMatch(gpuName, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        return pattern;
                }
                catch (ArgumentException)
                {
                    // Malformed regex authored on the asset: treat as no match.
                }
            }
            return null;
        }
    }
}
