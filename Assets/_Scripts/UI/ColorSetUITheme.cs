using System;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The UI colours a colour set gives the game: the same theme the analytics console and the Fleet Codex wear for that
    /// set (Shombith, 2026-11-09: "I want the theme to also affect the UI how it does in the analytics or fleet codex
    /// artifacts with all UI"). A line-for-line port of <c>Tools/Analytics/console/make_themes.py</c> <c>theme_tokens</c>,
    /// held to its output by <c>ColorSetUIThemeTests</c> (the shipped <c>themes.css</c> is the golden):
    /// <list type="bullet">
    /// <item>the ground, panels, lines and text keep the authored Deep Sea lightness and take the hue of the set's
    /// signature face (its Jade face), each with a chroma cap;</item>
    /// <item>the call to action and danger are the set's own (EnvironmentColors BrightCTA, Danger);</item>
    /// <item>selection and the control colour are its Jade rim, purchase its Gold rim;</item>
    /// <item>a mark or text too faint on a panel moves its OKLab lightness until it reaches 3:1 (marks) or 4.5:1 (text).</item>
    /// </list>
    /// Every token and the sea lines' four colours are the set's own (TracedColors <c>ui-token</c> ids; Garrett,
    /// 2026-11-10: every colour comes from the colour set, LOCKED): Photic, the set the game was authored in, carries
    /// the authored theme (UITheme.asset, its selection Jade as the style guide decided, D3) and the sea material's own
    /// colours; every other set carries what <c>Tools/Build/colour_trace.py</c> computes for it with this derivation
    /// (and its family rims for the tooltip and the element colours). What a set carries wins; the derivation is what a
    /// set that carries nothing gets. A set's colours are linear HDR; they reach the screen with a soft shoulder above 1
    /// and the sRGB curve (Docs/PALETTE.md section 3).
    /// </summary>
    public static class ColorSetUITheme
    {
        /// <summary>A whole UI theme: one colour per <see cref="UIColorToken"/>, and the sea lines' four colours.</summary>
        public sealed class Palette
        {
            public readonly Color[] Tokens = new Color[TokenCount];
            public Color SeaLine, SeaMid, SeaEnd, SeaGlow;

            public Color this[UIColorToken token] => Tokens[(int)token];
        }

        public static readonly int TokenCount = Enum.GetValues(typeof(UIColorToken)).Length;

        const double Mark = 3.0, Text = 4.5;   // WCAG 2.2: graphics and focus rings, text

        /// <summary>The authored theme as a palette (no colour set): what the UI is before a set is worn.</summary>
        public static Palette Authored(UIThemeSO theme)
        {
            var p = new Palette();
            for (int i = 0; i < TokenCount; i++) p.Tokens[i] = UITheme.ResolveAuthored(theme, (UIColorToken)i);
            p.SeaLine = p.SeaMid = p.SeaEnd = p.SeaGlow = Color.clear;
            return p;
        }

        /// <summary>The UI theme <paramref name="set"/> gives, built on the authored <paramref name="theme"/>.</summary>
        public static Palette Build(UIThemeSO theme, SO_ColorSet set)
        {
            var p = Authored(theme);
            if (!set) return p;

            var face = Screen(Face(set.JadeColors));
            var rimJade = Screen(Rim(set.JadeColors));
            var rimRuby = Screen(Rim(set.RubyColors));
            var rimGold = Screen(Rim(set.GoldColors));
            var rimBlue = Screen(Rim(set.BlueColors));
            var env = set.EnvironmentColors;

            Color Base(UIColorToken t) => p.Tokens[(int)t];
            var ground = Tint(Base(UIColorToken.SurfaceVeryDark), face, 0.05);
            var groundDeep = Tint(Base(UIColorToken.SurfaceBlack), face, 0.035);
            var surface = Tint(Base(UIColorToken.SurfaceDark), face, 0.06);
            var raised = Tint(Base(UIColorToken.SurfaceLight), face, 0.08);
            var text = Tint(Base(UIColorToken.TextLight), face, 0.02);
            var textSoft = Fit(Tint(Base(UIColorToken.NeutralLightest), face, 0.05), surface, Text);
            var textOff = Tint(Base(UIColorToken.InactiveLight), face, 0.03);
            // Text on an inactive button: not on the console, so the darkest text's cap.
            var textInactive = Tint(Base(UIColorToken.TextInactive), face, 0.02);

            Color MarkOf(Color rim) => Fit(Fit(rim, ground, Mark), surface, Mark);
            var jade = MarkOf(rimJade);
            var gold = MarkOf(rimGold);

            Set(p, UIColorToken.SurfaceVeryDark, ground);
            Set(p, UIColorToken.SurfaceBlack, groundDeep);
            Set(p, UIColorToken.SurfaceDark, surface);
            Set(p, UIColorToken.SurfaceLight, raised);
            Set(p, UIColorToken.TextLight, text);
            Set(p, UIColorToken.NeutralLightest, textSoft);
            Set(p, UIColorToken.InactiveLight, textOff);
            Set(p, UIColorToken.TextInactive, textInactive);
            if (env != null)
            {
                Set(p, UIColorToken.Cta, Fit(Screen(env.BrightCTA), surface, Mark));
                Set(p, UIColorToken.Danger, Fit(Screen(env.Danger), surface, Text));
            }
            Set(p, UIColorToken.Selection, jade);
            Set(p, UIColorToken.ControlActive, jade);
            Set(p, UIColorToken.Purchase, gold);

            // The sea: the shader's cyan / periwinkle / violet become the set's Jade, Blue and Ruby rims.
            p.SeaLine = rimJade;
            p.SeaMid = rimBlue;
            p.SeaEnd = rimRuby;
            p.SeaGlow = rimJade;

            // What the set gives (TracedColors ui-token:<Token>, Docs/VesselModels/COLOR_TRACE.md): every token and the
            // sea's four colours, Photic's the authored ones.
            for (int i = 0; i < TokenCount; i++)
                if (set.TryGetTracedColor(TokenIds[i], out var given))
                    p.Tokens[i] = new Color(given.r, given.g, given.b, 1f);
            if (set.TryGetTracedColor(SeaLineId, out var line)) p.SeaLine = new Color(line.r, line.g, line.b, 1f);
            if (set.TryGetTracedColor(SeaMidId, out var mid)) p.SeaMid = new Color(mid.r, mid.g, mid.b, 1f);
            if (set.TryGetTracedColor(SeaEndId, out var end)) p.SeaEnd = new Color(end.r, end.g, end.b, 1f);
            if (set.TryGetTracedColor(SeaGlowId, out var glow)) p.SeaGlow = new Color(glow.r, glow.g, glow.b, 1f);
            return p;
        }

        static readonly string[] TokenIds = BuildTokenIds();

        /// <summary>The TracedColors ids a colour set gives the sea lines' four colours by.</summary>
        public const string SeaLineId = "ui-token:SeaLine", SeaMidId = "ui-token:SeaMid", SeaEndId = "ui-token:SeaEnd",
                            SeaGlowId = "ui-token:SeaGlow";

        static string[] BuildTokenIds()
        {
            var ids = new string[TokenCount];
            for (int i = 0; i < TokenCount; i++) ids[i] = "ui-token:" + (UIColorToken)i;
            return ids;
        }

        /// <summary>The TracedColors id a colour set gives <paramref name="token"/> by: <c>ui-token:&lt;Token&gt;</c>.</summary>
        public static string TokenId(UIColorToken token) => TokenIds[(int)token];

        static void Set(Palette p, UIColorToken token, Color c) => p.Tokens[(int)token] = new Color(c.r, c.g, c.b, 1f);

        static Color Rim(DomainColorSet d) => d != null ? d.TrailHighlightColor : Color.gray;
        static Color Face(DomainColorSet d) => d != null ? d.OutsideBlockColor : Color.black;

        // ------------------------------------------------------------ colour (make_themes.py, in doubles)

        /// <summary>A shipped (linear HDR) colour on screen: a soft shoulder above 1, then the sRGB curve.</summary>
        public static Color Screen(Color linear)
        {
            static float One(float x)
            {
                double v = Math.Max(0.0, x);
                if (v > 1) v /= 1 + 0.25 * (v - 1);
                return (float)Gam(Math.Min(1.0, v));
            }
            return new Color(One(linear.r), One(linear.g), One(linear.b), 1f);
        }

        static double Lin(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

        static double Gam(double v)
        {
            v = Math.Max(0.0, Math.Min(1.0, v));
            return v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
        }

        public static double Luminance(Color c) => 0.2126 * Lin(c.r) + 0.7152 * Lin(c.g) + 0.0722 * Lin(c.b);

        public static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        static (double L, double a, double b) ToOklab(Color c)
        {
            double r = Lin(c.r), g = Lin(c.g), bl = Lin(c.b);
            double l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * bl);
            double m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * bl);
            double s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * bl);
            return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                    1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                    0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
        }

        static Color FromOklab(double L, double a, double b)
        {
            double l = Math.Pow(L + 0.3963377774 * a + 0.2158037573 * b, 3);
            double m = Math.Pow(L - 0.1055613458 * a - 0.0638541728 * b, 3);
            double s = Math.Pow(L - 0.0894841775 * a - 1.2914855480 * b, 3);
            return new Color((float)Gam(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
                             (float)Gam(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
                             (float)Gam(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s), 1f);
        }

        /// <summary>The token's lightness with the face's hue, at the token's own chroma (never more than the cap).</summary>
        public static Color Tint(Color token, Color face, double cap)
        {
            var (L, a, b) = ToOklab(token);
            var (_, fa, fb) = ToOklab(face);
            double c = Math.Min(Math.Sqrt(a * a + b * b), cap), h = Math.Atan2(fb, fa);
            return FromOklab(L, c * Math.Cos(h), c * Math.Sin(h));
        }

        /// <summary>
        /// The colour with its hue kept, made lighter (on a dark ground) or darker (on a light one) until it reaches
        /// <paramref name="ratio"/> against the ground. Already there: unchanged.
        /// </summary>
        public static Color Fit(Color c, Color ground, double ratio)
        {
            if (Contrast(c, ground) >= ratio) return c;
            var (L, a, b) = ToOklab(c);
            double step = Luminance(ground) < 0.18 ? 0.01 : -0.01;
            var output = c;
            for (int i = 0; i < 100; i++)
            {
                L = Math.Max(0.0, Math.Min(1.0, L + step));
                output = FromOklab(L, a, b);
                if (Contrast(output, ground) >= ratio || L == 0.0 || L == 1.0) return output;
            }
            return output;
        }
    }
}
