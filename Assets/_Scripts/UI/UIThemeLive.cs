using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
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
    /// its colour moves to the same token in the new theme, alpha kept;</item>
    /// <item>every other colour a canvas draws that the colour trace knows (<see cref="ColorTrace"/>, role ui: a
    /// Graphic's colour, a TextMesh Pro vertex gradient, a ColorTint selectable's colours, a shadow or outline, an
    /// input field's caret and selection): the worn set's value for it, alpha kept (Garrett, 2026-11-10: "no pixel
    /// anywhere should ever receive a color that can be traced somewhere but a colorset"). Pure white and black are
    /// the same in every set and never traced, so white art tints are never dyed;</item>
    /// <item>the sea lines (CosmicShore/UI/SeaLines): drawn with a run-time copy of their material in the set's Jade,
    /// Blue and Ruby rims. The material assets are never written.</item>
    /// </list>
    /// UI that appears later is born in the theme: the UI prefab assets in memory are painted where they live (as the
    /// world colour trace paints world prefabs; in the editor each change is put back when play ends), and the kit
    /// reads the live tokens on enable. For what comes from elsewhere, a root canvas whose hierarchy changed is swept
    /// again within a quarter of a second (<see cref="Tick"/>; the list of root canvases is refreshed once a second),
    /// and every canvas of a newly loaded scene. A view that paints from data takes the new theme at its next refresh,
    /// or listens to <see cref="Changed"/>.
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
        static readonly Dictionary<int, float> CanvasSwept = new();          // root canvas id -> when it was last swept
        static readonly HashSet<int> CanvasDirty = new();                    // root canvases changed since their last sweep
        const float SweepEvery = 0.25f;   // a canvas whose hierarchy keeps changing is swept at most this often
        static readonly List<Canvas> RootCanvases = new(16);
        static readonly List<Graphic> Scratch = new(256);
        static readonly List<Selectable> Selectables = new(64);
        static readonly List<Shadow> Shadows = new(16);
        static readonly List<TMP_InputField> Inputs = new(8);
        static float _nextTick;
        static bool _sceneHooked;

        static readonly int LineColorId = Shader.PropertyToID("_LineColor");
        static readonly int LineColorMidId = Shader.PropertyToID("_LineColorMid");
        static readonly int LineColorEndId = Shader.PropertyToID("_LineColorEnd");
        static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

        // The tokens a raw colour is matched against: every token, each the worn set's own (TracedColors ui-token ids).
        static readonly UIColorToken[] Swapped =
        {
            UIColorToken.TextLight, UIColorToken.TextInactive, UIColorToken.InactiveLight, UIColorToken.SurfaceBlack,
            UIColorToken.SurfaceVeryDark, UIColorToken.SurfaceDark, UIColorToken.SurfaceLight, UIColorToken.NeutralLightest,
            UIColorToken.Cta, UIColorToken.Danger, UIColorToken.ControlActive, UIColorToken.Purchase, UIColorToken.Selection,
            UIColorToken.Tooltip, UIColorToken.ElementCharge, UIColorToken.ElementMass, UIColorToken.ElementSpace,
            UIColorToken.ElementTime,
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
            CanvasSwept.Clear();
            CanvasDirty.Clear();
            RootCanvases.Clear();
            _nextTick = 0f;
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
        /// Every frame (ThemeManager.Update), sweeps again each root canvas whose hierarchy grew or shrank since the last
        /// sweep: rows, cards and windows instantiated from prefabs authored in the base theme, repainted within a quarter
        /// of a second (a canvas is swept at most that often, so a HUD that changes every frame costs four sweeps a
        /// second). Cheap otherwise: an int compare per root canvas; the list of root canvases is refreshed once a
        /// second (unscaled), so a brand-new root canvas waits at most that long.
        /// </summary>
        public static void Tick(float unscaledTime)
        {
            if (_live == null || !Application.isPlaying) return;
            if (unscaledTime >= _nextTick)
            {
                _nextTick = unscaledTime + 1f;
                RootCanvases.Clear();
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (canvas.isRootCanvas) RootCanvases.Add(canvas);
            }
            foreach (var canvas in RootCanvases)
            {
                if (!canvas) continue;
                int id = canvas.GetInstanceID(), count = canvas.transform.hierarchyCount;
                // Every change of the count marks the canvas, so a change seen while it waits out its SweepEvery is
                // still swept when the wait ends, even if the count has come back to the last one by then (a toast
                // gone and another come).
                if (!CanvasCounts.TryGetValue(id, out var was) || was != count)
                {
                    CanvasCounts[id] = count;
                    CanvasDirty.Add(id);
                }
                if (!CanvasDirty.Contains(id)) continue;
                // A HUD whose rows come and go every frame is swept at most SweepEvery seconds apart.
                if (CanvasSwept.TryGetValue(id, out var at) && unscaledTime - at < SweepEvery) continue;
                CanvasDirty.Remove(id);
                CanvasSwept[id] = unscaledTime;
                SweepCanvas(canvas.gameObject, _live);
                RepaintKit(canvas.gameObject);
            }
        }

        // Every colour under `root` into the current theme: graphics, then the colours that are not Graphics.
        static void SweepCanvas(GameObject root, ColorSetUITheme.Palette from)
        {
            Scratch.Clear();
            root.GetComponentsInChildren(true, Scratch);
            foreach (var g in Scratch) Swap(g, from);
            Selectables.Clear();
            root.GetComponentsInChildren(true, Selectables);
            foreach (var sel in Selectables) RetraceTint(sel, from);
            Shadows.Clear();
            root.GetComponentsInChildren(true, Shadows);
            foreach (var sh in Shadows) RetraceShadow(sh, from);
            Inputs.Clear();
            root.GetComponentsInChildren(true, Inputs);
            foreach (var f in Inputs) RetraceInput(f, from);
        }

        static void RepaintAll(ColorSetUITheme.Palette previous)
        {
            foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Swap(g, previous);
            foreach (var sel in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                RetraceTint(sel, previous);
            foreach (var sh in Object.FindObjectsByType<Shadow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                RetraceShadow(sh, previous);
            foreach (var f in Object.FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                RetraceInput(f, previous);
            PaintPrefabAssets(previous);
            RepaintKit(null);
            CanvasCounts.Clear();
            CanvasDirty.Clear();
            RootCanvases.Clear();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                RootCanvases.Add(canvas);
                CanvasCounts[canvas.GetInstanceID()] = canvas.transform.hierarchyCount;
            }
        }

        // ------------------------------------------------------------------ UI prefab assets in memory
        // Painted where they live, so the rows, cards and windows instantiated from them are born in the theme (a list
        // rebuilt with as many rows as it had never changes its canvas's count, so the sweep alone would miss it). The
        // same colours as on screen: a Graphic's, a vertex gradient, a ColorTint selectable's, a shadow's, an input
        // field's. Never the sea lines' material: a prefab asset never takes a run-time copy. In the editor every change
        // is recorded and put back when play ends (WorldColorTrace.RecordAssetChange).

        static void PaintPrefabAssets(ColorSetUITheme.Palette from)
        {
            if (WorldColorTrace.IsStopped) return;   // play is ending: the editor has put the assets back
            foreach (var g in Resources.FindObjectsOfTypeAll<Graphic>())
            {
                if (!IsPrefabAsset(g)) continue;
                var was = g.color;
                var to = Retraced(was, from);
                if (!Same(was, to))
                {
                    bool records = WorldColorTrace.RecordsAsset(g);
                    g.color = to;
                    if (records) RecordGraphic(g, was, to);
                }
                if (g is TMP_Text text && text.enableVertexGradient)
                {
                    var v = text.colorGradient;
                    var w = new VertexGradient(Retraced(v.topLeft, from), Retraced(v.topRight, from),
                                               Retraced(v.bottomLeft, from), Retraced(v.bottomRight, from));
                    if (!SameGradient(v, w))
                    {
                        bool records = WorldColorTrace.RecordsAsset(text);
                        text.colorGradient = w;
                        if (records) RecordGradient(text, v, w);
                    }
                }
            }
            foreach (var sel in Resources.FindObjectsOfTypeAll<Selectable>())
            {
                if (sel is KitSelectableBase || sel.transition != Selectable.Transition.ColorTint || !IsPrefabAsset(sel)) continue;
                var b = sel.colors;
                var to = b;
                to.normalColor = Retraced(b.normalColor, from);
                to.highlightedColor = Retraced(b.highlightedColor, from);
                to.pressedColor = Retraced(b.pressedColor, from);
                to.selectedColor = Retraced(b.selectedColor, from);
                to.disabledColor = Retraced(b.disabledColor, from);
                if (to == b) continue;
                bool records = WorldColorTrace.RecordsAsset(sel);
                sel.colors = to;
                if (records) RecordTint(sel, b, to);
            }
            foreach (var sh in Resources.FindObjectsOfTypeAll<Shadow>())
            {
                if (!IsPrefabAsset(sh)) continue;
                var was = sh.effectColor;
                var to = Retraced(was, from);
                if (Same(was, to)) continue;
                bool records = WorldColorTrace.RecordsAsset(sh);
                sh.effectColor = to;
                if (records) RecordShadow(sh, was, to);
            }
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_InputField>())
            {
                if (!IsPrefabAsset(f)) continue;
                var caret = f.caretColor;
                var caretTo = Retraced(caret, from);
                var sel = f.selectionColor;
                var selTo = Retraced(sel, from);
                if (Same(caret, caretTo) && Same(sel, selTo)) continue;
                bool records = WorldColorTrace.RecordsAsset(f);
                f.caretColor = caretTo;
                f.selectionColor = selTo;
                if (records) RecordInput(f, caret, caretTo, sel, selTo);
            }
        }

        // A component of a prefab asset (not in any scene): in the editor, what lives in an asset file.
        static bool IsPrefabAsset(Component c) =>
            c && (WorldColorTrace.Editor != null ? WorldColorTrace.RecordsAsset(c) : !c.gameObject.scene.IsValid());

        static bool SameGradient(VertexGradient a, VertexGradient b) =>
            a.topLeft == b.topLeft && a.topRight == b.topRight && a.bottomLeft == b.bottomLeft && a.bottomRight == b.bottomRight;

        static bool Exact(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 1e-6f && Mathf.Abs(a.g - b.g) < 1e-6f && Mathf.Abs(a.b - b.b) < 1e-6f && Mathf.Abs(a.a - b.a) < 1e-6f;

        // The closures live here, made only when the editor records a change (never in a player).
        static void RecordGraphic(Graphic g, Color was, Color painted) =>
            WorldColorTrace.RecordAssetChange(g, 1, was, painted, () => g.color, v => g.color = v, Exact);

        static void RecordGradient(TMP_Text t, VertexGradient was, VertexGradient painted) =>
            WorldColorTrace.RecordAssetChange(t, 2, was, painted, () => t.colorGradient, v => t.colorGradient = v, SameGradient);

        static void RecordTint(Selectable s, ColorBlock was, ColorBlock painted) =>
            WorldColorTrace.RecordAssetChange(s, 1, was, painted, () => s.colors, v => s.colors = v, (a, b) => a == b);

        static void RecordShadow(Shadow sh, Color was, Color painted) =>
            WorldColorTrace.RecordAssetChange(sh, 1, was, painted, () => sh.effectColor, v => sh.effectColor = v, Exact);

        static void RecordInput(TMP_InputField f, Color caret, Color caretTo, Color sel, Color selTo)
        {
            WorldColorTrace.RecordAssetChange(f, 1, caret, caretTo, () => f.caretColor, v => f.caretColor = v, Exact);
            WorldColorTrace.RecordAssetChange(f, 2, sel, selTo, () => f.selectionColor, v => f.selectionColor = v, Exact);
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
            else if (ColorTrace.TryRetrace(ColorTrace.Ui, c, out var traced) && !Same(c, traced))
            {
                g.color = traced;
            }
            if (g is TMP_Text text && text.enableVertexGradient) RetraceGradient(text, from);
            SwapSea(g);
        }

        // A colour into the current theme: a token of `from` or of the authored theme becomes that token now, any other
        // colour the trace knows becomes the worn set's value for it, alpha kept; anything else is itself.
        static Color Retraced(Color c, ColorSetUITheme.Palette from)
        {
            if (TokenOf(c, from, out var token) || TokenOf(c, _authored, out token))
            {
                var to = (_live ?? _authored).Tokens[(int)token];
                return new Color(to.r, to.g, to.b, c.a);
            }
            return ColorTrace.Retrace(ColorTrace.Ui, c);
        }

        static void RetraceGradient(TMP_Text text, ColorSetUITheme.Palette from)
        {
            var v = text.colorGradient;
            var to = new VertexGradient(Retraced(v.topLeft, from), Retraced(v.topRight, from),
                                        Retraced(v.bottomLeft, from), Retraced(v.bottomRight, from));
            if (to.topLeft != v.topLeft || to.topRight != v.topRight || to.bottomLeft != v.bottomLeft || to.bottomRight != v.bottomRight)
                text.colorGradient = to;
        }

        // A ColorTint selectable's five colours (the kit's selectables paint their own graphics and use no tint).
        static void RetraceTint(Selectable sel, ColorSetUITheme.Palette from)
        {
            if (!sel || sel is KitSelectableBase || sel.transition != Selectable.Transition.ColorTint) return;
            var b = sel.colors;
            var to = b;
            to.normalColor = Retraced(b.normalColor, from);
            to.highlightedColor = Retraced(b.highlightedColor, from);
            to.pressedColor = Retraced(b.pressedColor, from);
            to.selectedColor = Retraced(b.selectedColor, from);
            to.disabledColor = Retraced(b.disabledColor, from);
            if (to != b) sel.colors = to;
        }

        static void RetraceShadow(Shadow sh, ColorSetUITheme.Palette from)
        {
            if (!sh) return;
            var to = Retraced(sh.effectColor, from);
            if (!Same(to, sh.effectColor)) sh.effectColor = to;
        }

        static void RetraceInput(TMP_InputField f, ColorSetUITheme.Palette from)
        {
            if (!f) return;
            var caret = Retraced(f.caretColor, from);
            if (!Same(caret, f.caretColor)) f.caretColor = caret;
            var sel = Retraced(f.selectionColor, from);
            if (!Same(sel, f.selectionColor)) f.selectionColor = sel;
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
