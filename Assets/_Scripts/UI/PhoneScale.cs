using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The phone rule of Docs/UI_STYLE_GUIDE.md §8 (D6, 2026-11-05): a screen whose short side is under
    /// <see cref="PhoneMaxShortSidePoints"/> points is a phone, and a canvas that has a phone layout lays it out
    /// against <see cref="PhoneReference"/> with the type roles' phone column. PC, console and iPad keep the
    /// 1920 x 1080 reference. Pure functions, so the rule is testable without a device.
    /// </summary>
    public static class PhoneScale
    {
        /// <summary>The short side, in points, below which a screen is a phone (an iPad sideways is 820).</summary>
        public const float PhoneMaxShortSidePoints = 600f;

        /// <summary>One point is 1/160 inch (Android's dp; iOS's 1/163 is within 2 %).</summary>
        public const float PointsPerInch = 160f;

        /// <summary>The phone canvas reference (STYLE_FOUNDATION §4's mobile column is authored at this size).</summary>
        public static readonly Vector2 PhoneReference = new(960f, 540f);

        /// <summary>The PC canvas reference every shipped canvas is authored at.</summary>
        public static readonly Vector2 PcReference = new(1920f, 1080f);

        /// <summary>
        /// Whether a screen of this size and density is a phone. <paramref name="dpi"/> 0 means the platform did
        /// not report one: then only a mobile platform can be a phone, judged at a typical 2x density
        /// (320 dpi), and a desktop never is.
        /// </summary>
        public static bool IsPhone(int widthPx, int heightPx, float dpi, bool mobilePlatform)
        {
            int shortSide = Mathf.Min(widthPx, heightPx);
            if (shortSide <= 0) return false;
            if (dpi <= 0f)
            {
                if (!mobilePlatform) return false;
                dpi = 2f * PointsPerInch;
            }
            return shortSide / (dpi / PointsPerInch) < PhoneMaxShortSidePoints;
        }

        /// <summary><see cref="IsPhone(int,int,float,bool)"/> for the live screen.</summary>
        public static bool IsPhoneScreen() =>
            IsPhone(Screen.width, Screen.height, Screen.dpi, Application.isMobilePlatform);
    }
}
