using System;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Which build and which kind of device sent an event, for Froglet Analytics' Web build and its per-device views
    /// (Shombith 2026-11-07: "another analytics for webgl in our website as well along with release editor port dev,
    /// ... platform specific views like iOS iPad android"). Stamped into the Froglet sink's envelope only
    /// (<see cref="AnalyticsServiceFacade.BuildSinkEnvelope"/>), so UGS never sees these fields.
    ///
    /// <para><c>Application.platform</c> alone cannot answer it: iOS reports <c>IPhonePlayer</c> on an iPad, Android
    /// reports <c>Android</c> on a tablet, and the web Toybox reports <c>WebGLPlayer</c> on a phone and a desktop alike.</para>
    ///
    /// The classifiers are pure (no Unity calls), so the edit-mode tests drive them; <see cref="Current"/> reads the
    /// running device once and keeps the answer, since it cannot change during a run.
    /// </summary>
    public static class AnalyticsDevice
    {
        /// <summary>An Android screen this many inches across or more is a tablet (the usual 7-inch line).</summary>
        public const float TabletDiagonalInches = 7f;

        /// <summary>The longest <c>os</c> and <c>device_model</c> sent; anything longer is cut.</summary>
        public const int MaxTextLength = 64;

        public readonly struct Info
        {
            public readonly string BuildKind;
            public readonly string Device;
            public readonly string Os;
            public readonly string DeviceModel;

            public Info(string buildKind, string device, string os, string deviceModel)
            {
                BuildKind = buildKind;
                Device = device;
                Os = os;
                DeviceModel = deviceModel;
            }
        }

        static Info? s_current;

        /// <summary>The running build and device, read on first use (main thread) and kept.</summary>
        public static Info Current => s_current ??= Read();

        static Info Read()
        {
            var platform = Application.platform;
            var model = SystemInfo.deviceModel;
            float diagonal = 0f;
            float dpi = Screen.dpi;
            if (dpi > 0f)
            {
                var resolution = Screen.currentResolution;
                diagonal = Mathf.Sqrt(resolution.width * resolution.width + resolution.height * resolution.height) / dpi;
            }

            return new Info(
                BuildKindOf(Application.isEditor, platform, Application.version, Debug.isDebugBuild),
                DeviceOf(platform, model, Application.isMobilePlatform, diagonal),
                Cut(SystemInfo.operatingSystem),
                Cut(model));
        }

        /// <summary>
        /// "editor" | "port" | "web" | "development" | "release". The editor first (a WebGL target played in the editor
        /// is editor traffic), then the Amoebius port (its version ends "-port"), then the web Toybox (every WebGL build,
        /// development or not, is the website's), then a development build.
        /// </summary>
        public static string BuildKindOf(bool isEditor, RuntimePlatform platform, string version, bool isDebugBuild)
        {
            if (isEditor || platform.ToString().EndsWith("Editor", StringComparison.Ordinal))
                return "editor";
            if (!string.IsNullOrEmpty(version) && version.EndsWith("-port", StringComparison.Ordinal))
                return "port";
            if (platform == RuntimePlatform.WebGLPlayer)
                return "web";
            return isDebugBuild ? "development" : "release";
        }

        /// <summary>
        /// "iphone" | "ipad" | "android_phone" | "android_tablet" | "android" (size unknown) | "windows" | "mac" |
        /// "linux" | "web_desktop" | "web_mobile" | "other". An editor reports the machine it runs on.
        /// </summary>
        public static string DeviceOf(RuntimePlatform platform, string deviceModel, bool isMobile, float diagonalInches)
        {
            switch (platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    return !string.IsNullOrEmpty(deviceModel) && deviceModel.StartsWith("iPad", StringComparison.Ordinal)
                        ? "ipad"
                        : "iphone";
                case RuntimePlatform.Android:
                    if (diagonalInches <= 0f)
                        return "android";
                    return diagonalInches >= TabletDiagonalInches ? "android_tablet" : "android_phone";
                case RuntimePlatform.WebGLPlayer:
                    return isMobile ? "web_mobile" : "web_desktop";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "windows";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "mac";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "linux";
                default:
                    return "other";
            }
        }

        /// <summary>Trimmed and cut to <see cref="MaxTextLength"/>; empty stays empty.</summary>
        public static string Cut(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            text = text.Trim();
            return text.Length <= MaxTextLength ? text : text.Substring(0, MaxTextLength);
        }
    }
}
