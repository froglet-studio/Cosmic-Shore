using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Player
{
    /// <summary>
    /// The control port's <c>ui_sweep DIR[;WxH,WxH...][;VIEWS]</c> (C5, UI parity): every screen of
    /// the loaded scene at each resolution, written in the parity golden layout
    /// (<c>Port/parity/README.md</c>):
    ///
    ///   DIR/frames/ui/&lt;view&gt;_&lt;WxH&gt;.png    the presented frame at that resolution
    ///   DIR/ui/&lt;view&gt;_&lt;WxH&gt;.jsonl          one line per active RectTransform under a screen-space canvas
    ///
    /// Views: in Menu_Main (a <c>ScreenSwitcher</c> is live) each <c>MenuScreens</c> value, then each
    /// registered modal over HOME; anywhere else the scene itself (<c>hud</c>). VIEWS narrows the
    /// list (comma-separated names). Sizes default to the C5 three: 1920x1080, 2560x1080, 1024x768.
    ///
    /// A rect line is <c>{"path","x0","y0","x1","y1","kind","alpha"[,"text","fontSize","overflow","lines"]}</c>:
    /// world corners in screen pixels, bottom-left origin (Unity's <c>GetWorldCorners</c> on an
    /// overlay canvas), rounded to 0.1 px. <c>path</c> is the hierarchy path with <c>#n</c> on the
    /// n-th repeat of a sibling name, the same key the Unity capture writes.
    /// </summary>
    public sealed class UiSweep
    {
        public static readonly (int w, int h)[] DefaultSizes = { (1920, 1080), (2560, 1080), (1024, 768) };

        /// <summary>Frames after a resolution change, and after entering a view (the slide/modal animations run ~0.5 s).</summary>
        const int SettleAfterResize = 20, SettleAfterView = 60, SettleAfterExit = 20;

        readonly string _dir;
        readonly List<(int w, int h)> _sizes;
        readonly List<View> _views;
        readonly ControlServer _control;
        readonly List<string> _written = new();
        int _size, _view = -1, _until = -1;
        Phase _phase = Phase.Resize;

        enum Phase { Resize, Enter, Shoot, Dump, Exit, Done }

        sealed class View
        {
            public string Name;
            public Action Enter, Exit;
        }

        public bool Ok { get; private set; } = true;
        /// <summary>Non-null while a frame should be captured to this path.</summary>
        public string ShotPath { get; private set; }

        public UiSweep(string arg, ControlServer control)
        {
            _control = control;
            var parts = (arg ?? "").Split(';');
            _dir = Path.GetFullPath(parts[0].Trim().Length > 0 ? parts[0].Trim() : Path.Combine(Path.GetTempPath(), "prisma-ui-sweep"));
            _sizes = parts.Length > 1 && parts[1].Trim().Length > 0
                ? parts[1].Split(',').Select(s => ParseSize(s) ?? throw new ArgumentException($"bad size '{s}' (WxH)")).ToList()
                : DefaultSizes.ToList();
            _views = DiscoverViews();
            if (parts.Length > 2 && parts[2].Trim().Length > 0)
            {
                var want = parts[2].Split(',').Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                _views = _views.Where(v => want.Contains(v.Name)).ToList();
            }
            if (_views.Count == 0) throw new ArgumentException("no views to sweep");
            // The development build's diagnostics overlay is not a screen: hidden for the sweep (and in the Unity capture).
            _diagnostics = CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "DiagnosticsHUD");
            SetDiagnostics(false);
            Directory.CreateDirectory(Path.Combine(_dir, "frames", "ui"));
            Directory.CreateDirectory(Path.Combine(_dir, "ui"));
            Console.WriteLine($"[ui_sweep] {_views.Count} view(s) x {_sizes.Count} size(s) -> {_dir}: {string.Join(", ", _views.Select(v => v.Name))}");
        }

        public static (int w, int h)? ParseSize(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0 || s.Equals("off", StringComparison.OrdinalIgnoreCase)) return null;
            var wh = s.Split('x', 'X');
            return wh.Length == 2 && int.TryParse(wh[0], out var w) && int.TryParse(wh[1], out var h) && w > 0 && h > 0
                ? (w, h) : throw new ArgumentException($"bad size '{s}' (WxH)");
        }

        /// <summary>One step on the main thread; true when the sweep is finished.</summary>
        public bool Step(int frame)
        {
            if (frame < _until || ShotPath != null) return false;
            var (w, h) = _sizes[_size];
            switch (_phase)
            {
                case Phase.Resize:
                    _control.VirtualSize = (w, h);
                    _view = 0;
                    _phase = Phase.Enter;
                    _until = frame + SettleAfterResize;
                    return false;
                case Phase.Enter:
                    Try(_views[_view].Enter, "enter " + _views[_view].Name);
                    _phase = Phase.Shoot;
                    _until = frame + SettleAfterView;
                    return false;
                case Phase.Shoot:
                    Canvas.ForceUpdateCanvases();
                    ShotPath = Path.Combine(_dir, "frames", "ui", $"{_views[_view].Name}_{w}x{h}.png");
                    _phase = Phase.Dump;
                    return false;
                case Phase.Dump:
                {
                    var file = Path.Combine(_dir, "ui", $"{_views[_view].Name}_{w}x{h}.jsonl");
                    int n = WriteRects(file);
                    _written.Add(file);
                    Console.WriteLine($"[ui_sweep] {_views[_view].Name} {w}x{h}: {n} rects");
                    Try(_views[_view].Exit, "exit " + _views[_view].Name);
                    _phase = Phase.Exit;
                    _until = frame + SettleAfterExit;
                    return false;
                }
                case Phase.Exit:
                    if (++_view < _views.Count) { _phase = Phase.Enter; return false; }
                    if (++_size < _sizes.Count) { _phase = Phase.Resize; return false; }
                    _phase = Phase.Done;
                    SetDiagnostics(true);
                    return true;
                default:
                    return true;
            }
        }

        readonly MonoBehaviour _diagnostics;

        void SetDiagnostics(bool visible) =>
            _diagnostics?.GetType().GetMethod("SetVisible", Any, null, new[] { typeof(bool) }, null)?.Invoke(_diagnostics, new object[] { visible });

        public void ShotTaken(int w, int h)
        {
            _written.Add(ShotPath);
            ShotPath = null;
        }

        public Dictionary<string, object> Result() => new()
        {
            ["dir"] = _dir,
            ["views"] = _views.Select(v => v.Name).ToArray(),
            ["sizes"] = _sizes.Select(s => $"{s.w}x{s.h}").ToArray(),
            ["files"] = _written.Count,
        };

        void Try(Action a, string what)
        {
            if (a == null) return;
            try { a(); }
            catch (Exception e) { Ok = false; Console.WriteLine($"[ui_sweep] {what} failed: {(e.InnerException ?? e).Message}"); }
        }

        // ── views ─────────────────────────────────────────────────────────────

        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static List<View> DiscoverViews()
        {
            var views = new List<View>();
            var switcher = CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.GetType().Name == "ScreenSwitcher");
            if (switcher == null)
            {
                views.Add(new View { Name = Safe(SceneManager.GetActiveScene()?.name ?? "hud") });
                return views;
            }

            var type = switcher.GetType();
            var screensEnum = type.GetNestedType("MenuScreens");
            var modalsEnum = type.GetNestedType("ModalWindows");
            var navigate = type.GetMethods(Any).FirstOrDefault(m => m.Name == "NavigateTo" && m.GetParameters() is { Length: 2 } p && p[0].ParameterType == screensEnum);
            var openModal = type.GetMethod("OpenModal", Any, null, new[] { modalsEnum }, null);
            if (screensEnum == null || navigate == null) throw new InvalidOperationException("ScreenSwitcher has no NavigateTo(MenuScreens, bool)");

            foreach (var value in Enum.GetValues(screensEnum))
            {
                var v = value;
                views.Add(new View { Name = "menu-" + Safe(v.ToString()), Enter = () => navigate.Invoke(switcher, new[] { v, (object)false }) });
            }

            // Each registered modal, opened over HOME and closed straight after its capture.
            var home = Enum.Parse(screensEnum, "HOME");
            if (openModal != null && type.GetField("Modals", Any)?.GetValue(switcher) is System.Collections.IEnumerable modals)
            {
                var seen = new HashSet<string>();
                foreach (var m in modals.Cast<object>().Where(m => m is Component c && c != null).Cast<Component>())
                {
                    var modalType = m.GetType().GetProperty("ModalType", Any)?.GetValue(m) ?? m.GetType().GetField("ModalType", Any)?.GetValue(m);
                    if (modalType == null || !seen.Add(modalType.ToString())) continue;
                    var mt = modalType;
                    var modal = m;
                    views.Add(new View
                    {
                        Name = "modal-" + Safe(mt.ToString()),
                        Enter = () => { navigate.Invoke(switcher, new[] { home, (object)false }); openModal.Invoke(switcher, new[] { mt }); },
                        Exit = () => modal.GetType().GetMethod("ForceCloseImmediate", Any, null, Type.EmptyTypes, null)?.Invoke(modal, null),
                    });
                }
            }
            return views;
        }

        static string Safe(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? char.ToLowerInvariant(c) : '-').ToArray());

        // ── rect dump ─────────────────────────────────────────────────────────

        /// <summary>Every active RectTransform under a screen-space root canvas, one JSON line each.</summary>
        public static int WriteRects(string file)
        {
            var sb = new StringBuilder();
            int n = 0;
            var roots = CosmicShore.Engine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(c => c.isActiveAndEnabled && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
                .OrderBy(c => PathKey(c.transform), StringComparer.Ordinal);
            foreach (var canvas in roots) n += Walk(canvas.transform, PathKey(canvas.transform), sb);
            File.WriteAllText(file, sb.ToString());
            return n;
        }

        static int Walk(Transform t, string path, StringBuilder sb)
        {
            if (!t.gameObject.activeInHierarchy) return 0;
            int n = 0;
            if (t is RectTransform rt) { sb.Append(RectLine(rt, path)).Append('\n'); n++; }
            var names = new Dictionary<string, int>();
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                int k = names.TryGetValue(c.name, out var seen) ? seen + 1 : 0;
                names[c.name] = k;
                n += Walk(c, path + "/" + c.name + (k > 0 ? "#" + k : ""), sb);
            }
            return n;
        }

        /// <summary>The root-to-node path with #n on repeated sibling names.</summary>
        public static string PathKey(Transform t)
        {
            if (t.parent == null) return t.name;
            int k = 0;
            for (int i = 0; i < t.GetSiblingIndex(); i++) if (t.parent.GetChild(i).name == t.name) k++;
            return PathKey(t.parent) + "/" + t.name + (k > 0 ? "#" + k : "");
        }

        static string F(float v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);

        static string RectLine(RectTransform rt, string path)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var sb = new StringBuilder();
            sb.Append("{\"path\":").Append(Json(path))
              .Append(",\"x0\":").Append(F(c[0].x)).Append(",\"y0\":").Append(F(c[0].y))
              .Append(",\"x1\":").Append(F(c[2].x)).Append(",\"y1\":").Append(F(c[2].y));
            var g = rt.GetComponent<CosmicShore.Engine.UI.Graphic>();
            string kind = g switch
            {
                null => "none",
                CosmicShore.Engine.UI.TMP_Text => "text",
                CosmicShore.Engine.UI.Image => "image",
                CosmicShore.Engine.UI.RawImage => "rawimage",
                _ => g.GetType().Name,
            };
            sb.Append(",\"kind\":\"").Append(kind).Append('"');
            if (g != null && g.enabled)
            {
                float alpha = g.color.a * g.canvasRenderer.GetAlpha();
                for (var t = (Transform)rt; t != null; t = t.parent)
                    foreach (var cg in t.gameObject.GetComponents<CanvasGroup>()) alpha *= cg.alpha;
                sb.Append(",\"alpha\":").Append(F(alpha));
            }
            if (g is CosmicShore.Engine.UI.TMP_Text tmp && tmp.enabled)
            {
                var text = tmp.text ?? "";
                bool overflow = tmp.isTextOverflowing;
                sb.Append(",\"text\":").Append(Json(text.Length > 80 ? text[..80] : text))
                  .Append(",\"fontSize\":").Append(F(tmp.fontSize))
                  .Append(",\"overflow\":").Append(overflow ? "true" : "false")
                  .Append(",\"lines\":").Append(tmp.textInfo?.lineCount ?? 0);
            }
            return sb.Append('}').ToString();
        }

        static string Json(string s) => System.Text.Json.JsonSerializer.Serialize(s);
    }
}
