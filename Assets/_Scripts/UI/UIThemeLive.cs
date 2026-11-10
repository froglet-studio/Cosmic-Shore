using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CosmicShore.UI
{
    /// <summary>
    /// The UI theme the worn colour set gives (<see cref="ColorSetUITheme"/>), live in play: every
    /// <see cref="UITheme.Resolve"/> answers from it, and <see cref="Wear"/> repaints the UI already on screen
    /// (Shombith, 2026-11-09: the theme affects all UI, as on the analytics console and the Fleet Codex).
    ///
    /// <para>What a repaint reaches:</para>
    /// <list type="bullet">
    /// <item>the kit, which paints from tokens (<see cref="KitThemedGraphic"/>, <see cref="KitText"/>, every
    /// <see cref="KitSelectableBase"/>): asked to paint again;</item>
    /// <item>any other Graphic whose colour IS a theme token (a colour a generator or a person typed from the theme):
    /// its colour moves to the same token in the new theme, alpha kept. A colour that is no token (white icons, art,
    /// domain colours) is left alone;</item>
    /// <item>the sea lines (CosmicShore/UI/SeaLines): drawn with a run-time copy of their material in the set's Jade,
    /// Blue and Ruby rims. The material assets are never written.</item>
    /// </list>
    /// UI that appears later paints itself (the kit reads the live tokens on enable); for the rest, a root canvas whose
    /// hierarchy changed, and every canvas of a newly loaded scene, is swept again (<see cref="Tick"/>, once a second).
    /// A view that resolves a token only when its data changes takes the new theme at its next refresh, or listens to
    /// <see cref="Changed"/>.
    /// </summary>
    public static class UIThemeLive
    {
        /// <summary>Raised after the UI on screen is repainted in a new theme (or back in the authored one).</summary>
        public static event Action Changed;

        static ColorSetUITheme.Palette _live;       // null: the authored theme
        static ColorSetUITheme.Palette _authored;   // what prefabs and scenes were authored in

        static readonly Dictionary<Material, Material> SeaCopies = new();   // asset -> run-time copy
        static readonly HashSet<Material> SeaCopySet = new();
        static readonly Dictionary<int, int> CanvasCounts = new();           // root canvas id -> hierarchyCount
        static readonly List<Graphic> Scratch = new(256);
        static float _nextTick;
        static bool _sceneHooked;

        static readonly int LineColorId = Shader.PropertyToID("_LineColor");
        static readonly int LineColorMidId = Shader.PropertyToID("_LineColorMid");
        static readonly int LineColorEndId = Shader.PropertyToID("_LineColorEnd");
        static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

        // The tokens a raw colour is matched against: every role a colour set re-hues.
        static readonly UIColorToken[] Swapped =
        {
            UIColorToken.TextLight, UIColorToken.TextInactive, UIColorToken.InactiveLight, UIColorToken.SurfaceBlack,
            UIColorToken.SurfaceVeryDark, UIColorToken.SurfaceDark, UIColorToken.SurfaceLight, UIColorToken.NeutralLightest,
            UIColorToken.Cta, UIColorToken.Danger, UIColorToken.ControlActive, UIColorToken.Purchase, UIColorToken.Selection,
        };

        /// <summary>Half a step of an 8-bit channel: a typed token and its float in a YAML file are the same colour.</summary>
        const float Tolerance = 0.6f / 255f;

        public static bool IsLive => _live != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _live = null;
            _authored = null;
            SeaCopies.Clear();
            SeaCopySet.Clear();
            CanvasCounts.Clear();
            Changed = null;
        }

        /// <summary>The live colour of <paramref name="token"/>, if a colour set's theme is worn.</summary>
        public static bool TryGet(UIColorToken token, out Color color)
        {
            int i = (int)token;
            if (_live != null && i >= 0 && i < _live.Tokens.Length)
            {
                color = _live.Tokens[i];
                return true;
            }
            color = default;
            return false;
        }

        /// <summary>
        /// Wears <paramref name="palette"/> (null: back to the authored theme) and repaints the UI on screen.
        /// <paramref name="authoredTheme"/> is the theme the prefabs were authored in, what a raw colour is matched to.
        /// </summary>
        public static void Wear(ColorSetUITheme.Palette palette, UIThemeSO authoredTheme)
        {
            _authored ??= ColorSetUITheme.Authored(authoredTheme);
            var previous = _live ?? _authored;
            _live = palette;
            if (!_sceneHooked && palette != null)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                _sceneHooked = true;
            }
            // Only in play: in the editor's edit mode (a test) a repaint would write into the open scenes.
            if (Application.isPlaying) RepaintAll(previous);
            Changed?.Invoke();
        }

        /// <summary>Back to the authored theme (the theme manager going). Repaints what is still on screen.</summary>
        public static void Clear()
        {
            if (_live == null) return;
            Wear(null, null);
            if (_sceneHooked) SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneHooked = false;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_live == null || !Application.isPlaying) return;
            RepaintAll(_live);
        }

        /// <summary>
        /// Once a second (unscaled), sweeps again each root canvas whose hierarchy grew or shrank since the last sweep:
        /// rows, cards and windows instantiated from prefabs authored in the base theme. Cheap: one canvas lookup and an
        /// int compare per root canvas.
        /// </summary>
        public static void Tick(float unscaledTime)
        {
            if (_live == null || unscaledTime < _nextTick || !Application.isPlaying) return;
            _nextTick = unscaledTime + 1f;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                int id = canvas.GetInstanceID(), count = canvas.transform.hierarchyCount;
                if (CanvasCounts.TryGetValue(id, out var was) && was == count) continue;
                CanvasCounts[id] = count;
                Scratch.Clear();
                canvas.GetComponentsInChildren(true, Scratch);
                foreach (var g in Scratch) Swap(g, _live);
                RepaintKit(canvas.gameObject);
            }
        }

        static void RepaintAll(ColorSetUITheme.Palette previous)
        {
            foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Swap(g, previous);
            RepaintKit(null);
            CanvasCounts.Clear();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (canvas.isRootCanvas) CanvasCounts[canvas.GetInstanceID()] = canvas.transform.hierarchyCount;
        }

        // The kit paints from tokens: ask it to paint again (null root: everything loaded).
        static void RepaintKit(GameObject root)
        {
            if (root)
            {
                foreach (var t in root.GetComponentsInChildren<KitThemedGraphic>(true)) t.Apply();
                foreach (var t in root.GetComponentsInChildren<KitText>(true)) t.Apply();
                foreach (var s in root.GetComponentsInChildren<KitSelectableBase>(true)) s.RepaintTheme();
                return;
            }
            foreach (var t in Object.FindObjectsByType<KitThemedGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.Apply();
            foreach (var t in Object.FindObjectsByType<KitText>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.Apply();
            foreach (var s in Object.FindObjectsByType<KitSelectableBase>(FindObjectsInactive.Include, FindObjectsSortMode.None)) s.RepaintTheme();
        }

        /// <summary>
        /// One graphic into the current theme: its colour, when it is a token of <paramref name="from"/> or of the
        /// authored theme, becomes that token now (alpha kept); a sea-lines material becomes its painted copy.
        /// </summary>
        static void Swap(Graphic g, ColorSetUITheme.Palette from)
        {
            if (!g) return;
            var now = _live ?? _authored;
            var c = g.color;
            if (TokenOf(c, from, out var token) || TokenOf(c, _authored, out token))
            {
                var to = now.Tokens[(int)token];
                if (!Same(c, to)) g.color = new Color(to.r, to.g, to.b, c.a);
            }
            SwapSea(g);
        }

        public static bool TokenOf(Color c, ColorSetUITheme.Palette palette, out UIColorToken token)
        {
            token = default;
            if (palette == null) return false;
            foreach (var t in Swapped)
                if (Same(c, palette.Tokens[(int)t]))
                {
                    token = t;
                    return true;
                }
            return false;
        }

        static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) <= Tolerance && Mathf.Abs(a.g - b.g) <= Tolerance && Mathf.Abs(a.b - b.b) <= Tolerance;

        static void SwapSea(Graphic g)
        {
            var m = g.material;
            if (!m || !m.HasProperty(LineColorId) || !m.HasProperty(LineColorMidId) || !m.HasProperty(LineColorEndId)) return;
            bool isCopy = SeaCopySet.Contains(m);
            if (_live == null)
            {
                // Back to the authored theme: the asset again.
                if (!isCopy) return;
                foreach (var pair in SeaCopies)
                    if (pair.Value == m) { g.material = pair.Key; break; }
                return;
            }
            if (!isCopy)
            {
                if (!SeaCopies.TryGetValue(m, out var copy) || !copy)
                {
                    copy = new Material(m) { name = m.name + " (colour set)" };
                    SeaCopies[m] = copy;
                    SeaCopySet.Add(copy);
                }
                g.material = copy;
                m = copy;
            }
            m.SetColor(LineColorId, Opaque(_live.SeaLine, m.GetColor(LineColorId).a));
            m.SetColor(LineColorMidId, Opaque(_live.SeaMid, m.GetColor(LineColorMidId).a));
            m.SetColor(LineColorEndId, Opaque(_live.SeaEnd, m.GetColor(LineColorEndId).a));
            if (m.HasProperty(GlowColorId)) m.SetColor(GlowColorId, Opaque(_live.SeaGlow, m.GetColor(GlowColorId).a));
        }

        static Color Opaque(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
    }
}
