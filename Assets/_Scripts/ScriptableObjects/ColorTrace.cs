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
    /// when it is HDR. A painter that finds a colour asks <see cref="TryRetrace"/>: the colour may be the authored
    /// (Photic) value or the value the set worn before painted, and the answer keeps the found colour's alpha.
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

        static SO_ColorSet _worn, _authored, _previous;
        // set -> (value key of the set's colour for an id -> that id): how a colour found on screen is named
        static readonly Dictionary<SO_ColorSet, Dictionary<long, string>> Reverse = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _worn = _authored = _previous = null;
            Reverse.Clear();
            Changed = null;
        }

        /// <summary>The set being worn, or null before any set is worn.</summary>
        public static SO_ColorSet Worn => _worn;

        /// <summary>The set the game was authored in (Photic), or null when none is known.</summary>
        public static SO_ColorSet Authored => _authored;

        /// <summary>True when a set other than the authored one is worn: something on screen may need repainting.</summary>
        public static bool IsRepainting => _worn && _authored && _worn != _authored;

        /// <summary>
        /// Wears <paramref name="worn"/> (null: back to the authored colours). <paramref name="authored"/> is the set the
        /// assets were authored in: a value found on screen is matched against it, and against the set worn before.
        /// </summary>
        public static void Wear(SO_ColorSet worn, SO_ColorSet authored)
        {
            var next = worn ? worn : authored;
            _previous = _worn ? _worn : authored;
            _worn = next;
            _authored = authored;
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
        /// A colour found on screen in <paramref name="role"/>, as authored or as the previous set painted it: the worn
        /// set's colour for it, with <paramref name="current"/>'s alpha. The other role is asked when this one does not
        /// know the colour (a config colour can be painted on a canvas or in the world). False, and
        /// <paramref name="worn"/> unchanged, when the colour is not traced.
        /// </summary>
        public static bool TryRetrace(string role, Color current, out Color worn)
        {
            worn = current;
            if (!_worn) return false;
            bool ui = role == Ui;
            if (!TryIdOf(Key(ui, current), out var id) && !TryIdOf(Key(!ui, current), out id)) return false;
            if (!_worn.TryGetTracedColor(id, out var c)) return false;
            worn = new Color(c.r, c.g, c.b, current.a);
            return true;
        }

        /// <summary><paramref name="authored"/> as the worn set paints it (itself when it is not traced). The one call a
        /// painter with a colour literal or a config colour makes before it paints.</summary>
        public static Color Retrace(string role, Color authored) =>
            TryRetrace(role, authored, out var worn) ? worn : authored;

        static bool TryIdOf(long key, out string id)
        {
            if (TryIdIn(_previous, key, out id)) return true;
            if (_authored != _previous && TryIdIn(_authored, key, out id)) return true;
            return false;
        }

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
            return (ui ? 1L << 52 : 0L) | (Math.Min(hundredths, 0xFFFFFFFL) << 24) | rgb;
        }

        static int Byte(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        // The id whose value in `set` has value id `key` (built once per set).
        static bool TryIdIn(SO_ColorSet set, long key, out string id)
        {
            id = null;
            if (!set || set.TracedColors == null) return false;
            if (!Reverse.TryGetValue(set, out var map))
            {
                map = new Dictionary<long, string>(set.TracedColors.Length);
                foreach (var t in set.TracedColors)
                {
                    if (string.IsNullOrEmpty(t.Id)) continue;
                    bool ui = t.Id.StartsWith(Ui + ":", StringComparison.Ordinal);
                    if (!ui && !t.Id.StartsWith(World + ":", StringComparison.Ordinal)) continue;
                    map[Key(ui, t.Color)] = t.Id;
                }
                Reverse[set] = map;
            }
            return map.TryGetValue(key, out id);
        }

        /// <summary>Forget the reverse maps (a set's traced colours were edited while playing).</summary>
        public static void Invalidate() => Reverse.Clear();
    }
}
