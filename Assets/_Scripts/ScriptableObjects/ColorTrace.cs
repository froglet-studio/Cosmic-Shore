using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The worn colour set's answer for every colour the game draws outside the set's domain and environment blocks
    /// (Garrett, 2026-11-10: "no pixel anywhere should ever receive a color that can be traced somewhere but a
    /// colorset"). Each such colour is a <see cref="TracedColor"/> on every <see cref="SO_ColorSet"/>: Photic, the set
    /// the game was authored in, holds the baked value, and every other set its own, computed from its pigments by
    /// <c>Tools/Build/colour_trace.py</c> (Docs/VesselModels/COLOR_TRACE.md).
    ///
    /// <para>A traced colour is found on screen by its value. Its id is <c>role:RRGGBB</c>: the role (<c>ui</c> for
    /// canvas graphics, text, selectables and UI materials; <c>world</c> for materials, particles, trails, lights,
    /// cameras and the sky) and the colour as authored in 8-bit channels, with <c>xN.NN</c> (its brightest channel)
    /// when it is HDR. So the id names the authored colour itself, and a colour on screen is found as authored (the id),
    /// or as any set worn this session painted it (its values). The tool keeps every value of every set naming one
    /// colour, in either role, so whichever set painted a colour it is found again. A painter that finds a colour asks
    /// <see cref="TryRetrace"/>, and the answer keeps the found colour's alpha.
    /// <c>ui-token:&lt;Token&gt;</c> ids carry UI theme tokens a set gives directly (<see cref="Get"/>).</para>
    /// <see cref="Changed"/> is raised after each <see cref="Wear"/>; painters repaint then.
    /// </summary>
    public static class ColorTrace
    {
        /// <summary>The catalog id of the set the game's prefabs, scenes and materials were authored in.</summary>
        public const string AuthoredSetId = "photic";

        /// <summary>The roles a value id names (Tools/Build/colour_trace.py role_of).</summary>
        public const string Ui = "ui", World = "world";

        /// <summary>Raised after a colour set is worn (or the trace goes back to the authored colours).</summary>
        public static event Action Changed;

        /// <summary>Counts every <see cref="Wear"/>: a painter that caches a retraced colour refreshes it when this moves.
        /// Never reset, so a cache from an earlier play session can never match.</summary>
        public static int Version { get; private set; }

        static SO_ColorSet _worn, _authored;
        // every set worn since the authored set was named: what can have painted a colour on screen
        static readonly List<SO_ColorSet> Seen = new();
        // value key -> id: each id's own (authored) colour, then the authored set's values, then every seen set's
        static readonly Dictionary<long, string> Names = new(1024);
        static bool _named;

        const long UiBit = 1L << 52;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _worn = _authored = null;
            Seen.Clear();
            Names.Clear();
            _named = false;
            Changed = null;
        }

        /// <summary>The set being worn, or null before any set is worn.</summary>
        public static SO_ColorSet Worn => _worn;

        /// <summary>The set the game was authored in (Photic), or null when none is known.</summary>
        public static SO_ColorSet Authored => _authored;

        /// <summary>
        /// Wears <paramref name="worn"/> (null: back to the authored colours). <paramref name="authored"/> is the set the
        /// assets were authored in. A colour found on screen is matched against the ids (the authored colours) and
        /// against every set worn since (what they painted); a new authored set starts that history again.
        /// </summary>
        public static void Wear(SO_ColorSet worn, SO_ColorSet authored)
        {
            if (authored != _authored)
            {
                _authored = authored;
                Seen.Clear();
                _named = false;
            }
            _worn = worn ? worn : authored;
            if (_worn && _worn != _authored && !Seen.Contains(_worn))
            {
                Seen.Add(_worn);
                _named = false;
            }
            Version++;
            Changed?.Invoke();
        }

        /// <summary>The set the catalog names as the authored one (<see cref="AuthoredSetId"/>), or null.</summary>
        public static SO_ColorSet FindAuthored(ColorSetCatalogSO catalog)
        {
            if (!catalog) return null;
            var entry = catalog.Get(catalog.IndexOfId(AuthoredSetId));
            return entry != null ? entry.colorSet : null;
        }

        /// <summary>The worn set's colour for <paramref name="id"/>, or <paramref name="fallback"/> when no set is worn
        /// or the set does not trace it.</summary>
        public static Color Get(string id, Color fallback) =>
            _worn && _worn.TryGetTracedColor(id, out var c) ? c : fallback;

        /// <summary>
        /// A colour found on screen in <paramref name="role"/>, as authored or as a set worn before painted it: the worn
        /// set's colour for it, with <paramref name="current"/>'s alpha. The other role is asked when this one does not
        /// know the colour (a config colour can be painted on a canvas or in the world). A colour that already is the
        /// worn set's (to its 8-bit id) comes back exactly as it is, so a repaint changes nothing twice and wearing
        /// Photic leaves every baked colour alone. False, and <paramref name="worn"/> unchanged, when the colour is not
        /// traced.
        /// </summary>
        public static bool TryRetrace(string role, Color current, out Color worn)
        {
            worn = current;
            if (!_worn) return false;
            if (!_named) Name();
            bool ui = role == Ui;
            long key = Key(ui, current);
            if (!Names.TryGetValue(key, out var id))
            {
                key = Key(!ui, current);
                if (!Names.TryGetValue(key, out id)) return false;
            }
            if (!_worn.TryGetTracedColor(id, out var c)) return false;
            if (Key((key & UiBit) != 0, c) == key) return true;      // already the worn colour
            worn = new Color(c.r, c.g, c.b, current.a);
            return true;
        }

        /// <summary><paramref name="authored"/> as the worn set paints it (itself when it is not traced). The one call a
        /// painter with a colour literal or a config colour makes before it paints.</summary>
        public static Color Retrace(string role, Color authored) =>
            TryRetrace(role, authored, out var worn) ? worn : authored;

        /// <summary>
        /// The value id of a colour in a role: <c>role:RRGGBB</c>, each channel as Mathf.RoundToInt(v * 255) of the
        /// colour with its brightest channel at most 1, and <c>xN.NN</c> (that channel in hundredths, half up) when it
        /// was above 1.
        /// colour_trace.py value_id computes the same string from the same floats.
        /// </summary>
        public static string ValueId(string role, Color c)
        {
            long k = Key(role == Ui, c);
            int rgb = (int)(k & 0xFFFFFF);
            long n = (k >> 24) & 0xFFFFFFF;
            string id = role + ":" + rgb.ToString("X6", CultureInfo.InvariantCulture);
            return n == 0 ? id : id + "x" + (n / 100).ToString(CultureInfo.InvariantCulture) + "." +
                                 (n % 100).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The colour a value id names (its authored colour, to 8 bits; HDR at its brightest channel): what
        /// <see cref="ValueId"/> packs back to the same id. False for a <c>ui-token:</c> id or anything that is no value id.
        /// </summary>
        public static bool TryColorOf(string id, out Color color)
        {
            color = default;
            if (!TryKeyOf(id, out long k)) return false;
            float n = ((k >> 24) & 0xFFFFFFF) / 100f, peak = n > 0f ? n : 1f;
            color = new Color(((k >> 16) & 0xFF) / 255f * peak, ((k >> 8) & 0xFF) / 255f * peak, (k & 0xFF) / 255f * peak, 1f);
            return true;
        }

        /// <summary>
        /// The value id as a number, built without a string (the lookup a painter makes every time it paints): the
        /// role in bit 52, the HDR peak in hundredths in bits 24-51 (0 when the colour is not HDR), the 8-bit RGB below.
        /// </summary>
        static long Key(bool ui, Color c)
        {
            float r = Mathf.Max(0f, c.r), g = Mathf.Max(0f, c.g), b = Mathf.Max(0f, c.b);
            float peak = Mathf.Max(r, Mathf.Max(g, b));
            long hundredths = 0;
            if (peak > 1f)
            {
                hundredths = (long)Math.Floor(peak * 100.0 + 0.5);   // rounded half up, in double, as the tool does
                r /= peak;
                g /= peak;
                b /= peak;
            }
            long rgb = ((long)Byte(r) << 16) | ((long)Byte(g) << 8) | (long)Byte(b);
            return (ui ? UiBit : 0L) | (Math.Min(hundredths, 0xFFFFFFFL) << 24) | rgb;
        }

        static int Byte(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        // The key a value id names, read from the id itself: role:RRGGBB or role:RRGGBBxN.NN.
        static bool TryKeyOf(string id, out long key)
        {
            key = 0;
            if (string.IsNullOrEmpty(id)) return false;
            int at;
            bool ui;
            if (id.StartsWith(Ui + ":", StringComparison.Ordinal)) { ui = true; at = Ui.Length + 1; }
            else if (id.StartsWith(World + ":", StringComparison.Ordinal)) { ui = false; at = World.Length + 1; }
            else return false;
            if (id.Length < at + 6) return false;
            long rgb = 0;
            for (int i = at; i < at + 6; i++)
            {
                int h = Hex(id[i]);
                if (h < 0) return false;
                rgb = (rgb << 4) | (uint)h;
            }
            long hundredths = 0;
            int p = at + 6;
            if (p < id.Length)
            {
                // xN.NN: the peak in hundredths
                if (id[p] != 'x') return false;
                bool dot = false;
                int decimals = 0;
                for (int i = p + 1; i < id.Length; i++)
                {
                    char ch = id[i];
                    if (ch == '.' && !dot) { dot = true; continue; }
                    if (ch < '0' || ch > '9') return false;
                    hundredths = hundredths * 10 + (ch - '0');
                    if (dot) decimals++;
                }
                if (decimals != 2) return false;
            }
            key = (ui ? UiBit : 0L) | (Math.Min(hundredths, 0xFFFFFFFL) << 24) | rgb;
            return true;
        }

        static int Hex(char c) =>
            c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : c >= 'a' && c <= 'f' ? c - 'a' + 10 : -1;

        // Every colour a traced id can show on screen, named: its id (the authored colour), then what the authored set
        // and every set worn since paint for it. The first claim of a value keeps it (the tool keeps them apart).
        static void Name()
        {
            Names.Clear();
            if (_authored && _authored.TracedColors != null)
                foreach (var t in _authored.TracedColors)
                    if (TryKeyOf(t.Id, out long k)) Names.TryAdd(k, t.Id);
            NameValuesOf(_authored);
            foreach (var set in Seen) NameValuesOf(set);
            _named = true;
        }

        static void NameValuesOf(SO_ColorSet set)
        {
            if (!set || set.TracedColors == null) return;
            foreach (var t in set.TracedColors)
            {
                if (string.IsNullOrEmpty(t.Id)) continue;
                bool ui = t.Id.StartsWith(Ui + ":", StringComparison.Ordinal);
                if (!ui && !t.Id.StartsWith(World + ":", StringComparison.Ordinal)) continue;
                Names.TryAdd(Key(ui, t.Color), t.Id);
            }
        }

        /// <summary>Forget the names (a set's traced colours were edited while playing); they are built again on the
        /// next lookup.</summary>
        public static void Invalidate() => _named = false;
    }
}
