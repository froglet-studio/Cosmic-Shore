using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using ImGuiNET;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The EDITOR page: Prisma's first editor surfaces, picked for a game whose content already
    /// exists. TOOLS is every FrogletTools tool, run by the agent without Unity; DATA browses and
    /// edits the ScriptableObject data sets; MODELS shows each FBX as Unity imports it, with a
    /// preview. Scene and hierarchy work is the agent's (cs-asset), so there is no hierarchy panel.
    /// Everything is read through the workspace's own cs-asset (<see cref="EditorTool"/>), and every
    /// edit lands in the workspace, where the GIT page commits it.
    /// </summary>
    public sealed partial class LauncherApp
    {
        EditorTool? _edTool;
        EditorTool Ed => _edTool ??= new EditorTool(_tools, _ws, _jobs.Log);
        int _edTab;
        string _edSearch = "";
        string? _edNote;
        bool _edNoteBad;

        string? _edOpen; // a file to open once the page has loaded (--page editor:TAB:PATH)

        void EdNote(string? error, string ok) { _edNote = error ?? ok; _edNoteBad = error != null; }

        void DrawEditor(Vector2 a, Vector2 b)
        {
            PageHeader(a, "EDITOR", _edTab switch
            {
                0 => "Every FrogletTools tool - RUN the native ones, BUILD the rest into Prisma with the agent",
                1 => "The game's ScriptableObject data sets - browse and edit fields; edits land in the workspace (commit on GIT)",
                _ => "Every model (FBX, Blender, Maya) as Unity imports it, in the game's colours - drag to turn, VIEW IN ENGINE",
            });
            ImGui.SetCursorScreenPos(new Vector2(b.X - 470, a.Y + 6));
            Segmented("edtab", new[] { "TOOLS", "DATA", "MODELS" }, _edTab, i => { _edTab = i; _edSearch = ""; }, Neon.Cyan);
            ImGui.SetCursorScreenPos(new Vector2(b.X - 150, a.Y + 6));
            if (SmallButton(Ed.Building ? "BUILDING" : "REBUILD", 140, !Ed.Building))
            {
                Ed.Invalidate();
                _edTools = null; _edTypes = null; _edData = null; _edModel = null; _edModels = null;
            }
            Neon.Tooltip("Rebuild the editor tools from the workspace (after a pull or an agent's script edit) and reload this page.");

            // An update moved the workspace: rebuild the tools from its code and reload the page.
            if (_jobs.SyncCount != _edSyncSeen)
            {
                if (_edSyncSeen >= 0) { Ed.Invalidate(); _edTools = null; _edTypes = null; _edData = null; _edModel = null; _edModels = null; }
                _edSyncSeen = _jobs.SyncCount;
            }
            var bannerH = DrawBehindBanner(ImGui.GetWindowDrawList(), new Vector2(a.X, a.Y + 64), b.X - a.X);
            a.Y += bannerH;

            var ca = new Vector2(a.X, a.Y + 70);
            ImGui.SetCursorScreenPos(ca);
            ImGui.PushItemWidth(320);
            ImGui.InputTextWithHint("##edsearch", _edTab switch { 0 => "search tools", 1 => "search data types and files", _ => "search models" }, ref _edSearch, 120);
            ImGui.PopItemWidth();
            if (_edNote != null)
            {
                ImGui.SameLine(0, 16);
                ImGui.PushFont(Neon.Small);
                ImGui.TextColored(_edNoteBad ? Neon.Red : Neon.Lime, Trim(_edNote, 140));
                ImGui.PopFont();
            }
            var body = new Vector2(a.X, ca.Y + 46);
            if (!_toolsScanned) { ImGui.SetCursorScreenPos(body); ImGui.TextColored(Neon.Dim, "Finding git and .NET..."); return; }
            if (Ed.Building)
            {
                var dl = ImGui.GetWindowDrawList();
                Card(dl, body, new Vector2(b.X, body.Y + 110));
                dl.AddText(Neon.Strong, 16, body + new Vector2(22, 22), Neon.U(Neon.Ink), "Building the editor tools from the workspace");
                dl.AddText(Neon.Small, 14, body + new Vector2(22, 50), Neon.U(Neon.Dim), "cs-asset and the game's scripts compile once per session (a minute or two the first time). CONSOLE shows the build.");
                return;
            }
            switch (_edTab)
            {
                case 0: DrawEdTools(body, b); break;
                case 1: DrawEdData(body, b); break;
                default: DrawEdModels(body, b); break;
            }
        }

        bool EdFailed(Vector2 a, Vector2 b, bool loading)
        {
            if (Ed.Error == null || loading) return false;
            var dl = ImGui.GetWindowDrawList();
            Card(dl, a, new Vector2(b.X, a.Y + 96));
            dl.AddText(Neon.Strong, 16, a + new Vector2(22, 20), Neon.U(Neon.Red), "The editor tools could not answer");
            dl.AddText(Neon.Small, 14, a + new Vector2(22, 48), Neon.U(Neon.Dim), Trim(Ed.Error, 160));
            return true;
        }

        bool Matches(string? s) => _edSearch.Length == 0 || (s ?? "").Contains(_edSearch, StringComparison.OrdinalIgnoreCase);

        /// <summary>The EDITOR rail icon: an isometric cube.</summary>
        static void IconCube(ImDrawListPtr dl, Vector2 c, uint col)
        {
            var top = c + new Vector2(0, -9); var l = c + new Vector2(-8, -4.5f); var r = c + new Vector2(8, -4.5f);
            var mid = c; var bl = c + new Vector2(-8, 5); var br = c + new Vector2(8, 5); var bot = c + new Vector2(0, 9.5f);
            dl.AddLine(top, l, col, 1.6f); dl.AddLine(top, r, col, 1.6f); dl.AddLine(l, mid, col, 1.6f); dl.AddLine(r, mid, col, 1.6f);
            dl.AddLine(l, bl, col, 1.6f); dl.AddLine(r, br, col, 1.6f); dl.AddLine(mid, bot, col, 1.6f); dl.AddLine(bl, bot, col, 1.6f); dl.AddLine(br, bot, col, 1.6f);
        }

        // ------------------------------------------------------------------ TOOLS

        sealed record EdToolInfo(string Menu, string Name, string Group, string Category, int Importance, bool Annotated, string Description,
            string Doc, string File, int Line, string Class, string Method, string Kind, string Writes, string Summary);

        List<EdToolInfo>? _edTools;
        bool _edToolsLoading;
        string _edToolCat = "ALL";

        static readonly JsonSerializerOptions EdJson = new() { PropertyNameCaseInsensitive = true };

        void LoadEdTools()
        {
            if (_edToolsLoading) return;
            _edToolsLoading = true;
            Task.Run(async () =>
            {
                try
                {
                    using var doc = await Ed.Json("tools");
                    if (doc != null) _edTools = JsonSerializer.Deserialize<List<EdToolInfo>>(doc.RootElement.GetProperty("tools").GetRawText(), EdJson);
                }
                finally { _edToolsLoading = false; }
            });
        }

        static Vector4 CategoryColor(string c) => c switch
        {
            "Build" => Neon.Lime, "Vessels" => Neon.Cyan, "Ecology" => new Vector4(0.45f, 0.95f, 0.6f, 1), "GameModes" => Neon.Magenta,
            "Interface" => Neon.Violet, "Performance" => Neon.Amber, "Validation" => new Vector4(0.6f, 0.85f, 1f, 1),
            "Diagnostics" => Neon.Red, "Services" => new Vector4(1f, 0.6f, 0.4f, 1), "SceneSetup" => new Vector4(0.9f, 0.9f, 0.5f, 1),
            "Qa" => new Vector4(0.8f, 0.6f, 1f, 1), _ => Neon.Dim,
        };

        void DrawEdTools(Vector2 a, Vector2 b)
        {
            if (_edTools == null) { if (!_edToolsLoading && Ed.Error == null) LoadEdTools(); }
            if (EdFailed(a, b, _edToolsLoading)) return;
            if (_edTools == null) { ImGui.SetCursorScreenPos(a); ImGui.TextColored(Neon.Dim, "Reading the FrogletTools..."); return; }
            var cats = new[] { "ALL" }.Concat(_edTools.GroupBy(t => t.Category).OrderByDescending(g => g.Count()).Select(g => g.Key)).ToList();
            // Chips laid out by their measured width, wrapping at the page's right edge.
            ImGui.PushFont(Neon.Small);
            var style = ImGui.GetStyle();
            float cx = a.X, cy = a.Y, rowH = ImGui.GetFrameHeight();
            foreach (var c in cats)
            {
                int n = c == "ALL" ? _edTools.Count : _edTools.Count(t => t.Category == c);
                string label = $"{c.ToUpperInvariant()}  {n}";
                float bw = ImGui.CalcTextSize(label).X + style.FramePadding.X * 2;
                if (cx > a.X && cx + bw > b.X) { cx = a.X; cy += rowH + 6; }
                ImGui.SetCursorScreenPos(new Vector2(cx, cy));
                bool on = _edToolCat == c;
                ImGui.PushStyleColor(ImGuiCol.Button, on ? Neon.Mix(Neon.Space0, CategoryColor(c), 0.45f) : Neon.Mix(Neon.Space0, Neon.Ink, 0.06f));
                if (ImGui.Button(label + "##cat" + c)) _edToolCat = c;
                ImGui.PopStyleColor();
                cx += bw + 6;
            }
            ImGui.PopFont();
            var top = cy + rowH + 12;
            ImGui.SetCursorScreenPos(new Vector2(a.X, top));
            ImGui.BeginChild("##edtools", new Vector2(b.X - a.X, b.Y - top));
            var shown = _edTools.Where(t => (_edToolCat == "ALL" || t.Category == _edToolCat) &&
                                            (Matches(t.Menu) || Matches(t.Description) || Matches(t.Summary))).ToList();
            float w = ImGui.GetContentRegionAvail().X;
            int cols = Math.Max(1, (int)(w / 380));
            float cw = (w - (cols - 1) * 12) / cols, ch = 172;
            var origin = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            for (int i = 0; i < shown.Count; i++)
            {
                var t = shown[i];
                var p = origin + new Vector2((i % cols) * (cw + 12), (i / cols) * (ch + 12));
                var q = p + new Vector2(cw, ch);
                Card(dl, p, q);
                var cc = CategoryColor(t.Category);
                dl.AddRectFilled(p + new Vector2(0, 14), p + new Vector2(4, ch - 14), Neon.U(cc), 2);
                dl.AddText(Neon.Strong, 15, p + new Vector2(16, 12), Neon.U(Neon.Ink), Trim(Glyphs(t.Name), (int)(cw / 8.5f) - 6));
                for (int d = 0; d < 5; d++) dl.AddCircleFilled(new Vector2(q.X - 66 + d * 11, p.Y + 22), 3.2f, Neon.U(d < t.Importance ? cc : Neon.Dim, d < t.Importance ? 1f : 0.3f));
                var facts = new[] { t.Category, t.Group != null && t.Group != t.Category ? t.Group : null, t.Kind == "window" ? "window" : "one click",
                                    t.Writes == "writer" ? "writes assets" : t.Writes == "reader" ? "read only" : null };
                dl.AddText(Neon.Small, 12, p + new Vector2(16, 34), Neon.U(cc, 0.9f), string.Join("  ·  ", facts.Where(x => x != null)));
                var text = Glyphs(t.Description ?? t.Summary ?? "No description in its source.");
                dl.AddText(Neon.Small, 13, p + new Vector2(16, 56), Neon.U(Neon.Mix(Neon.Ink, Neon.Dim, 0.35f)), Wrap(text, (int)(cw / 7.2f), 4));
                ImGui.SetCursorScreenPos(new Vector2(p.X + 14, q.Y - 46));
                ImGui.PushID(t.Menu);
                var native = NativeTool(t.Menu);
                if (native != null)
                {
                    if (SmallButton(_edToolRunning == t.Menu ? "RUNNING..." : "RUN", 110, _edToolRunning == null)) RunNativeTool(t, native);
                    Neon.Tooltip("Runs Prisma's native version of this tool (cs-asset " + string.Join(" ", native.Args) + ")" +
                                 (native.Writes ? ".\nIt changes files in Prisma's workspace; review them on GIT." : ". It only reads."));
                }
                else
                {
                    if (SmallButton("BUILD", 110, true)) BuildToolWithClaude(t);
                    Neon.Tooltip("Opens an agent chat that builds this tool natively in Prisma (a cs-asset command, its test and a RUN button here),\n" +
                                 "so it runs without Unity from then on. The chat may change only cs-asset and the native-tools registry.");
                }
                ImGui.SameLine(0, 6);
                if (SmallButton("SOURCE", 84, true)) SourceControl.OpenUrl(Ed.FullPath(t.File));
                Neon.Tooltip($"{t.File}:{t.Line}  ({t.Class}.{t.Method})");
                if (t.Doc != null)
                {
                    ImGui.SameLine(0, 6);
                    if (SmallButton("DOCS", 70, true)) SourceControl.OpenUrl(Ed.FullPath(t.Doc.Split('#')[0]));
                    Neon.Tooltip(t.Doc);
                }
                ImGui.PopID();
            }
            int rows = (shown.Count + cols - 1) / cols;
            ImGui.SetCursorScreenPos(origin + new Vector2(0, rows * (ch + 12)));
            ImGui.Dummy(new Vector2(1, 1));
            ImGui.EndChild();
        }

        /// <summary>Hard-wraps text into at most <paramref name="lines"/> lines of <paramref name="width"/> characters.</summary>
        static string Wrap(string text, int width, int lines)
        {
            var words = text.Replace('\n', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var outp = new List<string>();
            var cur = "";
            foreach (var w in words)
            {
                if ((cur + " " + w).Trim().Length > width) { outp.Add(cur.Trim()); cur = w; if (outp.Count == lines) break; }
                else cur += " " + w;
            }
            if (outp.Count < lines && cur.Trim().Length > 0) outp.Add(cur.Trim());
            else if (outp.Count == lines) outp[^1] = Trim(outp[^1], Math.Max(4, width - 3)) + "...";
            return string.Join("\n", outp);
        }

        // ── Native tools: Port/tools/froglet-tools/tools.json in the workspace (BUILD adds to it) ──

        internal sealed record NativeToolEntry(string Menu, string[] Args, bool Writes, string? Summary);
        List<NativeToolEntry>? _nativeTools;
        DateTime _nativeToolsStamp;
        string? _edToolRunning;

        /// <summary>The native version of a tool, from the workspace's registry (re-read when it changes).</summary>
        NativeToolEntry? NativeTool(string menu)
        {
            var file = Path.Combine(_ws.Dir, "Port", "tools", "froglet-tools", "tools.json");
            var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
            if (_nativeTools == null || stamp != _nativeToolsStamp)
            {
                _nativeToolsStamp = stamp;
                _nativeTools = LoadNativeTools(file);
            }
            return _nativeTools.FirstOrDefault(n => string.Equals(n.Menu, menu, StringComparison.Ordinal));
        }

        internal static List<NativeToolEntry> LoadNativeTools(string file)
        {
            var list = new List<NativeToolEntry>();
            try
            {
                if (!File.Exists(file)) return list;
                using var d = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var e in d.RootElement.GetProperty("tools").EnumerateArray())
                {
                    var menu = e.TryGetProperty("menu", out var m) ? m.GetString() : null;
                    var args = e.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : null;
                    if (string.IsNullOrEmpty(menu) || args is not { Length: > 0 }) continue;
                    list.Add(new NativeToolEntry(menu, args, e.TryGetProperty("writes", out var w) && w.ValueKind == JsonValueKind.True,
                                                 e.TryGetProperty("summary", out var su) ? su.GetString() : null));
                }
            }
            catch (Exception e) when (e is JsonException or IOException or KeyNotFoundException or InvalidOperationException) { }
            return list;
        }

        void RunNativeTool(EdToolInfo t, NativeToolEntry native)
        {
            _edToolRunning = t.Menu;
            Task.Run(async () =>
            {
                try
                {
                    var (ok, stdout, err) = await Ed.Run(native.Args);
                    _jobs.Log.Add(LogKind.Info, $"---- {t.Name} (native) ----");
                    foreach (var line in (stdout + "\n" + err).Split('\n').Where(l => l.Trim().Length > 0)) _jobs.Log.Add(ok ? LogKind.Output : LogKind.Warn, line);
                    var first = stdout.Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
                    EdNote(ok ? null : $"{t.Name} failed: " + (err.Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "see CONSOLE"),
                           $"{t.Name}: {Trim(first, 120)}  (full output on CONSOLE{(native.Writes ? "; changes on GIT" : "")})");
                }
                finally { _edToolRunning = null; }
            });
        }

        /// <summary>BUILD: an agent chat (TOOL scope) that makes this tool native - a cs-asset command, its test, a registry entry.</summary>
        void BuildToolWithClaude(EdToolInfo t)
        {
            var chat = _chats.New(ClaudeChat.Scope.Tool);
            chat.Title = "Build tool: " + t.Name;
            _page = Page.Chat;
            SendChat(
                $"Build the Unity editor tool \"{t.Menu}\" natively for Prisma. Its source is {t.File} (class {t.Class}, method {t.Method}, line {t.Line})" +
                (t.Description != null ? $"; it describes itself as: {t.Description}" : "") + (t.Doc != null ? $". Its docs: {t.Doc}" : "") + ".\n" +
                "Plan it first: what the tool reads and writes, the cs-asset command you will add (its name and arguments), how you will test it, and anything it " +
                "needs from the Unity editor that has no file equivalent.",
                ClaudeChat.Mode.Plan);
        }

        // ------------------------------------------------------------------ DATA

        sealed record EdItem(string Path, string Name);
        sealed record EdType(string Guid, string Type, string Ns, string Script, int Count, List<EdItem> Items);

        public sealed class EdField
        {
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
            public string Path { get; set; } = "";
            public string Kind { get; set; } = "";
            public string? Type { get; set; }
            public string? Value { get; set; }
            public bool Editable { get; set; }
            public string? Header { get; set; }
            public string? Tooltip { get; set; }
            public float[]? Range { get; set; }
            public bool Multiline { get; set; }
            public List<string[]>? Options { get; set; }
            public string? RefPath { get; set; }
            public bool Stale { get; set; }
            public int Count { get; set; }
            public List<EdField>? Children { get; set; }
        }

        sealed class EdObject
        {
            public long FileId { get; set; }
            public string? Name { get; set; }
            public string? Type { get; set; }
            public string? Script { get; set; }
            public bool Resolved { get; set; }
            public List<EdField> Fields { get; set; } = new();
        }

        List<EdType>? _edTypes;
        bool _edTypesLoading, _edDataLoading;
        EdType? _edType;
        string? _edFile;
        List<EdObject>? _edData;
        readonly Dictionary<string, string> _edBuf = new();

        void LoadEdTypes()
        {
            if (_edTypesLoading) return;
            _edTypesLoading = true;
            Task.Run(async () =>
            {
                try
                {
                    using var doc = await Ed.Json("datasets");
                    if (doc != null) _edTypes = JsonSerializer.Deserialize<List<EdType>>(doc.RootElement.GetProperty("types").GetRawText(), EdJson);
                }
                finally { _edTypesLoading = false; }
            });
        }

        void OpenData(string path)
        {
            _edFile = path;
            _edData = null;
            _edBuf.Clear();
            _edDataLoading = true;
            Task.Run(async () =>
            {
                try
                {
                    using var doc = await Ed.Json("dataset", path);
                    if (doc != null && _edFile == path) _edData = JsonSerializer.Deserialize<List<EdObject>>(doc.RootElement.GetProperty("objects").GetRawText(), EdJson);
                }
                finally { _edDataLoading = false; }
            });
        }

        /// <summary>Writes one field through cs-asset set, then reloads the file so the page shows what was saved.</summary>
        void ApplyField(EdObject o, EdField f, string yamlValue)
        {
            var file = _edFile;
            if (file == null) return;
            Task.Run(async () =>
            {
                var (ok, _, err) = await Ed.Run("set", file, "&" + o.FileId, f.Path, yamlValue);
                EdNote(ok ? null : (err.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "set failed"),
                    $"Saved {f.Label} = {Trim(yamlValue, 50)} in {Path.GetFileName(file)}  (in the workspace - commit it on GIT)");
                if (ok) { _ui.Enqueue(() => OpenData(file)); RefreshGit(); }
            });
        }

        void DrawEdData(Vector2 a, Vector2 b)
        {
            if (_edTypes == null) { if (!_edTypesLoading && Ed.Error == null) LoadEdTypes(); }
            if (EdFailed(a, b, _edTypesLoading || _edDataLoading)) return;
            if (_edTypes == null) { ImGui.SetCursorScreenPos(a); ImGui.TextColored(Neon.Dim, "Reading the data sets..."); return; }
            if (_edOpenData != null && _edTypes.Any(t => t.Items.Any(i => i.Path == _edOpenData))) { OpenDataByPath(_edOpenData); _edOpenData = null; }
            if (_edOpen != null && _edOpen.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) { OpenDataByPath(_edOpen); _edOpen = null; }
            var dl = ImGui.GetWindowDrawList();
            float c1 = 280, c2 = 300;

            // types
            var ta = a; var tb = new Vector2(a.X + c1, b.Y);
            Card(dl, ta, tb);
            dl.AddText(Neon.Strong, 14, ta + new Vector2(14, 10), Neon.U(Neon.Ink), $"{_edTypes.Count} TYPES  ·  {_edTypes.Sum(t => t.Count)} FILES");
            ImGui.SetCursorScreenPos(ta + new Vector2(6, 36));
            ImGui.BeginChild("##edtypes", tb - ta - new Vector2(12, 42));
            foreach (var t in _edTypes.Where(t => Matches(t.Type) || t.Items.Any(i => Matches(i.Name))))
            {
                ImGui.PushID(t.Guid);
                bool sel = ReferenceEquals(t, _edType);
                if (ImGui.Selectable($"{t.Type}##t", sel, ImGuiSelectableFlags.None, new Vector2(0, 22))) { _edType = t; }
                ImGui.SameLine(c1 - 70);
                ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, t.Count.ToString()); ImGui.PopFont();
                ImGui.PopID();
            }
            ImGui.EndChild();

            // files of the type
            var fa = new Vector2(tb.X + 10, a.Y); var fb = new Vector2(fa.X + c2, b.Y);
            Card(dl, fa, fb);
            dl.AddText(Neon.Strong, 14, fa + new Vector2(14, 10), Neon.U(Neon.Ink), _edType == null ? "Pick a type" : Trim(_edType.Type, 30));
            if (_edType != null)
            {
                ImGui.SetCursorScreenPos(new Vector2(fb.X - 82, fa.Y + 6));
                if (SmallButton(_edNewFile ? "CANCEL" : "+ NEW", 74, !_edNewBusy)) { _edNewFile = !_edNewFile; _edNewName = "New" + _edType.Type; }
                Neon.Tooltip("A new data file of this type with the script's defaults, as Unity's Create menu writes it.");
                if (_edNewFile) DrawNewDataFile(fa + new Vector2(14, 40), fb - new Vector2(14, 8));
                else
                {
                ImGui.SetCursorScreenPos(fa + new Vector2(6, 36));
                ImGui.BeginChild("##edfiles", fb - fa - new Vector2(12, 42));
                foreach (var it in _edType.Items.Where(i => _edSearch.Length == 0 || Matches(i.Name) || Matches(_edType.Type)).OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
                {
                    ImGui.PushID(it.Path);
                    if (ImGui.Selectable(it.Name + "##f", _edFile == it.Path, ImGuiSelectableFlags.None, new Vector2(0, 22))) OpenData(it.Path);
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(it.Path);
                    ImGui.PopID();
                }
                ImGui.EndChild();
                }
            }

            // fields
            var xa = new Vector2(fb.X + 10, a.Y);
            Card(dl, xa, b);
            if (_edFile == null)
            {
                dl.AddText(Neon.Small, 14, xa + new Vector2(18, 16), Neon.U(Neon.Dim), "Pick a data file to see and edit its fields.");
                return;
            }
            dl.AddText(Neon.Strong, 15, xa + new Vector2(18, 12), Neon.U(Neon.Ink), Trim(Path.GetFileName(_edFile), 60));
            dl.AddText(Neon.Small, 12, xa + new Vector2(18, 34), Neon.U(Neon.Dim), Trim(_edFile, 90));
            ImGui.SetCursorScreenPos(new Vector2(b.X - 280, xa.Y + 10));
            if (SmallButton("ASK CLAUDE", 130, true))
            {
                var chat = _chats.New(); chat.Title = "Data: " + Path.GetFileNameWithoutExtension(_edFile);
                _chatInput = $"About the data file {_edFile} (the prisma tool asset_dataset reads it): ";
                _page = Page.Chat;
            }
            ImGui.SameLine(0, 6);
            if (SmallButton("RELOAD", 120, !_edDataLoading)) OpenData(_edFile);
            ImGui.SetCursorScreenPos(xa + new Vector2(10, 58));
            ImGui.BeginChild("##edfields", b - xa - new Vector2(20, 66));
            if (_edData == null) ImGui.TextColored(Neon.Dim, _edDataLoading ? "Reading..." : "");
            else
                foreach (var o in _edData)
                {
                    ImGui.PushID(o.FileId.ToString());
                    if (_edData.Count > 1 || !o.Resolved)
                    {
                        ImGui.PushFont(Neon.Strong);
                        ImGui.TextColored(Neon.Cyan, $"{o.Name}  ({o.Type ?? "unknown script"})");
                        ImGui.PopFont();
                    }
                    if (!o.Resolved)
                    {
                        ImGui.PushFont(Neon.Small);
                        ImGui.TextColored(Neon.Amber, "Its script is not in the game's compiled code (an editor-only or package script): values are shown as stored, without types.");
                        ImGui.PopFont();
                    }
                    float labelW = Math.Min(300, ImGui.GetContentRegionAvail().X * 0.38f);
                    foreach (var f in o.Fields) DrawEdField(o, f, labelW, 0);
                    ImGui.Dummy(new Vector2(0, 10));
                    ImGui.PopID();
                }
            ImGui.EndChild();
        }

        void DrawEdField(EdObject o, EdField f, float labelW, int depth)
        {
            if (f.Header != null && depth == 0)
            {
                ImGui.Dummy(new Vector2(0, 6));
                ImGui.PushFont(Neon.Strong); ImGui.TextColored(Neon.Cyan, f.Header); ImGui.PopFont();
            }
            ImGui.PushID(f.Path);
            float x0 = ImGui.GetCursorPosX();
            if (f.Kind is "list" or "object")
            {
                bool open = ImGui.TreeNodeEx($"{f.Label}##tn", ImGuiTreeNodeFlags.SpanAvailWidth);
                if (f.Tooltip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(f.Tooltip);
                ImGui.SameLine(x0 + labelW);
                ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, f.Kind == "list" ? $"{f.Count} item{(f.Count == 1 ? "" : "s")}  ·  {f.Type}" : f.Type ?? ""); ImGui.PopFont();
                if (open)
                {
                    foreach (var c in f.Children ?? new()) DrawEdField(o, c, labelW, depth + 1);
                    ImGui.TreePop();
                }
                ImGui.PopID();
                return;
            }
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(f.Stale ? Neon.Amber : Neon.Mix(Neon.Ink, Neon.Dim, 0.3f), Trim(f.Label, 40));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip((f.Tooltip != null ? f.Tooltip + "\n\n" : "") + $"{f.Key}  ·  {f.Type ?? f.Kind}" + (f.Stale ? "\nThis key is in the file but the script no longer has the field: Unity ignores it." : ""));
            ImGui.SameLine(x0 + labelW);
            ImGui.PushItemWidth(Math.Max(120, ImGui.GetContentRegionAvail().X - 10));
            string key = o.FileId + "|" + f.Path;
            switch (f.Kind)
            {
                case "bool":
                {
                    bool v = f.Value is "1" or "true" or "True";
                    if (ImGui.Checkbox("##v", ref v)) ApplyField(o, f, v ? "1" : "0");
                    break;
                }
                case "enum" when f.Options != null:
                {
                    var names = f.Options.Select(x => x[0]).ToArray();
                    int cur = f.Options.FindIndex(x => x[1] == f.Value);
                    int pick = cur;
                    string preview = cur >= 0 ? names[cur] : (f.Value ?? "");
                    if (ImGui.BeginCombo("##v", preview))
                    {
                        for (int i = 0; i < names.Length; i++)
                            if (ImGui.Selectable(names[i], i == cur)) pick = i;
                        ImGui.EndCombo();
                    }
                    if (pick != cur && pick >= 0) ApplyField(o, f, f.Options[pick][1]);
                    break;
                }
                case "number" when f.Range != null:
                {
                    bool isInt = f.Type is "Int32" or "Int64" or "Int16" or "Byte" or "UInt32";
                    if (isInt)
                    {
                        int.TryParse(f.Value, out int v);
                        if (!_edBuf.TryGetValue(key, out var bv)) bv = v.ToString();
                        int iv = int.TryParse(bv, out var parsed) ? parsed : v;
                        if (ImGui.SliderInt("##v", ref iv, (int)f.Range[0], (int)f.Range[1])) _edBuf[key] = iv.ToString();
                        if (ImGui.IsItemDeactivatedAfterEdit()) { ApplyField(o, f, iv.ToString()); _edBuf.Remove(key); }
                    }
                    else
                    {
                        float.TryParse(f.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
                        if (_edBuf.TryGetValue(key, out var bv)) float.TryParse(bv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
                        if (ImGui.SliderFloat("##v", ref v, f.Range[0], f.Range[1])) _edBuf[key] = v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                        if (ImGui.IsItemDeactivatedAfterEdit()) { ApplyField(o, f, v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); _edBuf.Remove(key); }
                    }
                    break;
                }
                case "ref":
                {
                    ImGui.PushFont(Neon.Small);
                    if (f.RefPath == null) ImGui.TextColored(Neon.Dim, "None");
                    else
                    {
                        bool data = f.RefPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
                        bool model = IsModelFile(f.RefPath);
                        ImGui.TextColored(data || model ? Neon.Cyan : Neon.Ink, Trim(f.RefPath, 90));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(f.Value + (data ? "\nClick to open this data file" : model ? "\nClick to open this model" : ""));
                            if (data || model) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        }
                        if (ImGui.IsItemClicked())
                        {
                            if (data) OpenDataByPath(f.RefPath);
                            else if (model) { _edTab = 2; SelectModel(f.RefPath); }
                        }
                    }
                    ImGui.PopFont();
                    break;
                }
                default:
                {
                    if (!_edBuf.TryGetValue(key, out var buf)) buf = UnquoteForEdit(f.Value ?? "");
                    bool enter;
                    if (f.Multiline || (f.Kind == "text" && buf.Contains('\n')))
                        enter = ImGui.InputTextMultiline("##v", ref buf, 8000, new Vector2(0, 64));
                    else
                        enter = ImGui.InputText("##v", ref buf, 4000, ImGuiInputTextFlags.EnterReturnsTrue);
                    _edBuf[key] = buf;
                    if (f.Kind == "color") ColorSwatch(buf);
                    if ((enter || ImGui.IsItemDeactivatedAfterEdit()) && f.Editable && buf != UnquoteForEdit(f.Value ?? ""))
                    {
                        string yaml = f.Kind switch
                        {
                            "number" when double.TryParse(buf, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) => buf.Trim(),
                            "number" => "",
                            "vector" or "color" => buf.Trim(),
                            _ => EditorTool.YamlScalar(buf),
                        };
                        if (yaml.Length == 0) EdNote("Not a number: " + buf, "");
                        else ApplyField(o, f, yaml);
                        _edBuf.Remove(key);
                    }
                    break;
                }
            }
            ImGui.PopItemWidth();
            ImGui.PopID();
        }

        // ── NEW: a data file of the selected type (cs-asset new-asset) ──
        bool _edNewFile, _edNewBusy;
        string _edNewName = "", _edNewFolder = "";

        void DrawNewDataFile(Vector2 a, Vector2 b)
        {
            var type = _edType!;
            if (_edNewFolder.Length == 0 || !_edNewFolder.StartsWith("Assets/", StringComparison.Ordinal))
                _edNewFolder = type.Items.Count > 0 ? Path.GetDirectoryName(type.Items[0].Path)!.Replace('\\', '/') : "Assets/_SO_Assets";
            ImGui.SetCursorScreenPos(a);
            ImGui.BeginChild("##ednew", b - a);
            ImGui.PushFont(Neon.Small);
            ImGui.TextColored(Neon.Dim, $"A new {type.Type} with the script's\ndefault values, ready to edit here.");
            ImGui.Dummy(new Vector2(0, 6));
            ImGui.TextColored(Neon.Ink, "Name");
            ImGui.PushItemWidth(-1);
            ImGui.InputText("##newname", ref _edNewName, 128);
            ImGui.PopItemWidth();
            ImGui.TextColored(Neon.Ink, "Folder");
            ImGui.PushItemWidth(-1);
            ImGui.InputText("##newfolder", ref _edNewFolder, 512);
            ImGui.PopItemWidth();
            ImGui.Dummy(new Vector2(0, 8));
            string name = _edNewName.Trim();
            bool ready = !_edNewBusy && name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            if (SmallButton(_edNewBusy ? "CREATING..." : "CREATE", 140, ready))
            {
                _edNewBusy = true;
                string path = _edNewFolder.TrimEnd('/') + "/" + name + ".asset";
                Task.Run(async () =>
                {
                    try
                    {
                        var (ok, _, err) = await Ed.Run("new-asset", type.Type, path);
                        if (!ok) { EdNote(err.Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "could not create it", ""); return; }
                        EdNote(null, $"Created {path}. Commit it on GIT when it is ready.");
                        _edNewFile = false;
                        _edTypes = null; // re-read the data sets, then open the new file
                        _edOpenData = path;
                    }
                    finally { _edNewBusy = false; }
                });
            }
            ImGui.PopFont();
            ImGui.EndChild();
        }

        string? _edOpenData;

        static string UnquoteForEdit(string v) =>
            v.Length >= 2 && v[0] == '\'' && v[^1] == '\'' ? v[1..^1].Replace("''", "'") :
            v.Length >= 2 && v[0] == '"' && v[^1] == '"' ? v[1..^1].Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\") : v;

        static void ColorSwatch(string flow)
        {
            float Get(string k) => System.Text.RegularExpressions.Regex.Match(flow, k + @":\s*([-0-9.eE]+)") is { Success: true } m &&
                                   float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0;
            var r = ImGui.GetItemRectMax();
            var col = new Vector4(Get("r"), Get("g"), Get("b"), 1);
            ImGui.GetWindowDrawList().AddRectFilled(new Vector2(r.X - 30, r.Y - 20), new Vector2(r.X - 6, r.Y - 4), Neon.U(col), 3);
        }

        void OpenDataByPath(string path)
        {
            _edType = _edTypes?.FirstOrDefault(t => t.Items.Any(i => i.Path == path)) ?? _edType;
            OpenData(path);
        }

        // ------------------------------------------------------------------ MODELS

        List<string>? _edModels;
        int _edSyncSeen = -1;
        string? _edModelPath;
        JsonDocument? _edModel;
        bool _edModelLoading, _edPreviewLoading;
        string? _edModelError;

        /// <summary>A file Unity's ModelImporter takes: FBX, and Blender/Maya files through the installed application.</summary>
        static bool IsModelFile(string path) =>
            path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || DccOf(path) != null;

        static string? DccOf(string? path) =>
            path == null ? null
            : path.EndsWith(".blend", StringComparison.OrdinalIgnoreCase) ? "BLENDER"
            : path.EndsWith(".ma", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mb", StringComparison.OrdinalIgnoreCase) ? "MAYA"
            : null;
        string? _edPreviewFile;
        // The turntable sheet: frames around the model, cols to a row, the yaw of frame 0 and the step.
        (int frames, int cols, double yaw0, double step) _edSheet = (1, 1, 145, 0);
        float _edAngle = 145;
        readonly Dictionary<string, (uint tex, Vector2 size)> _edTex = new();

        // Blend-shape weights for the preview (0-100), by shape name; reset with each model.
        readonly Dictionary<string, float> _edShapes = new(StringComparer.Ordinal);

        string ShapeSpec() => string.Join(";", _edShapes.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key + "=" + kv.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)));

        void SelectModel(string path)
        {
            if (_edModelPath != path) _edShapes.Clear();
            _edModelPath = path;
            _edModel?.Dispose();
            _edModel = null;
            _edModelLoading = true;
            Task.Run(async () =>
            {
                try
                {
                    var doc = await Ed.Json("model", path);
                    if (_edModelPath == path) { _edModel = doc; _edModelError = doc == null ? Ed.Error : null; } else doc?.Dispose();
                }
                finally { _edModelLoading = false; }
            });
            RenderPreview();
        }

        static string PreviewDir => Path.Combine(LauncherSettings.DataDir, "previews");

        /// <summary>
        /// One turntable sheet per model (24 views, game colours) so dragging the preview turns it at
        /// once; a branch whose cs-asset predates --turntable answers one picture, shown as it is.
        /// </summary>
        void RenderPreview()
        {
            var path = _edModelPath;
            if (path == null) return;
            var full = Ed.FullPath(path);
            long stamp = File.Exists(full) ? File.GetLastWriteTimeUtc(full).Ticks : 0;
            string shapes = ShapeSpec();
            var key = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes($"{path}|{stamp}|tt24|{shapes}")))[..16];
            var png = Path.Combine(PreviewDir, key + ".png");
            var meta = png + ".json";
            if (File.Exists(png) && File.Exists(meta)) { _edSheet = ReadSheet(File.ReadAllText(meta)); _edPreviewFile = png; return; }
            _edPreviewFile = null;
            _edPreviewLoading = true;
            Task.Run(async () =>
            {
                try
                {
                    Directory.CreateDirectory(PreviewDir);
                    var args = new List<string> { "model-preview", path, "--out", png, "--size", "320", "--turntable", "24", "--yaw", "145" };
                    if (shapes.Length > 0) { args.Add("--shapes"); args.Add(shapes); }
                    var (ok, stdout, err) = await Ed.Run(args.ToArray());
                    if (ok)
                    {
                        File.WriteAllText(meta, stdout);
                        if (_edModelPath == path && ShapeSpec() == shapes) { _edSheet = ReadSheet(stdout); _edPreviewFile = png; }
                    }
                    else EdNote(err.Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "preview failed", "");
                }
                finally { _edPreviewLoading = false; }
            });
        }

        static (int frames, int cols, double yaw0, double step) ReadSheet(string json)
        {
            try
            {
                using var d = JsonDocument.Parse(EditorTool.RepairConsoleText(json));
                var r = d.RootElement;
                int frames = r.TryGetProperty("frames", out var f) ? f.GetInt32() : 1;
                int cols = r.TryGetProperty("cols", out var c) ? c.GetInt32() : 1;
                double yaw0 = r.TryGetProperty("yaw", out var y) ? y.GetDouble() : 145;
                double step = r.TryGetProperty("step", out var st) ? st.GetDouble() : 0;
                return (Math.Max(1, frames), Math.Max(1, cols), yaw0, step);
            }
            catch (JsonException) { return (1, 1, 145, 0); }
        }

        unsafe (uint tex, Vector2 size) PngTexture(string file)
        {
            if (_edTex.TryGetValue(file, out var t)) return t;
            try
            {
                var img = ImageResult.FromMemory(File.ReadAllBytes(file), ColorComponents.RedGreenBlueAlpha);
                uint tex = _gl.GenTexture();
                _gl.BindTexture(TextureTarget.Texture2D, tex);
                fixed (byte* p = img.Data)
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                t = (tex, new Vector2(img.Width, img.Height));
            }
            catch { t = (0, Vector2.Zero); }
            // Keep a handful: each preview is one texture.
            if (_edTex.Count > 24) { foreach (var old in _edTex.Values) if (old.tex != 0) _gl.DeleteTexture(old.tex); _edTex.Clear(); }
            _edTex[file] = t;
            return t;
        }

        void DrawEdModels(Vector2 a, Vector2 b)
        {
            if (!_ws.Exists) { ImGui.SetCursorScreenPos(a); ImGui.TextColored(Neon.Dim, "No workspace yet: press START on PLAY once."); return; }
            _edModels ??= Directory.EnumerateFiles(Path.Combine(_ws.Dir, "Assets"), "*", SearchOption.AllDirectories)
                .Where(IsModelFile)
                .Select(f => Path.GetRelativePath(_ws.Dir, f).Replace('\\', '/')).Distinct().OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            if (_edOpen != null && IsModelFile(_edOpen)) { SelectModel(_edOpen); _edOpen = null; }
            var dl = ImGui.GetWindowDrawList();
            float c1 = 320;
            var la = a; var lb = new Vector2(a.X + c1, b.Y);
            Card(dl, la, lb);
            dl.AddText(Neon.Strong, 14, la + new Vector2(14, 10), Neon.U(Neon.Ink), $"{_edModels.Count} MODELS");
            ImGui.SetCursorScreenPos(new Vector2(lb.X - 104, la.Y + 6));
            if (SmallButton(_edImporting ? "CANCEL" : "+ IMPORT", 96, !_edImportBusy)) _edImporting = !_edImporting;
            Neon.Tooltip("Bring a new FBX (or .blend/.ma/.mb) into the project with the import settings the other models use.");
            if (_edImporting) { DrawModelImport(la + new Vector2(14, 40), lb - new Vector2(14, 8)); return; }
            ImGui.SetCursorScreenPos(la + new Vector2(6, 36));
            ImGui.BeginChild("##edmodels", lb - la - new Vector2(12, 42));
            foreach (var g in _edModels.Where(m => Matches(m)).GroupBy(m => Path.GetDirectoryName(m)!.Replace('\\', '/')))
            {
                ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, Trim(g.Key.Replace("Assets/", ""), 44)); ImGui.PopFont();
                foreach (var m in g)
                {
                    if (ImGui.Selectable("   " + Path.GetFileNameWithoutExtension(m) + "##" + m, _edModelPath == m)) SelectModel(m);
                    if (DccOf(m) is { } dcc) { ImGui.SameLine(); ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Violet, dcc); ImGui.PopFont(); }
                }
            }
            ImGui.EndChild();

            var ra = new Vector2(lb.X + 10, a.Y);
            Card(dl, ra, b);
            if (_edModelPath == null)
            {
                dl.AddText(Neon.Small, 14, ra + new Vector2(18, 16), Neon.U(Neon.Dim), "Pick a model to see it and what Unity imports from it.");
                return;
            }
            dl.AddText(Neon.Strong, 15, ra + new Vector2(18, 12), Neon.U(Neon.Ink), Path.GetFileName(_edModelPath));
            dl.AddText(Neon.Small, 12, ra + new Vector2(18, 34), Neon.U(Neon.Dim), _edModelPath);
            ImGui.SetCursorScreenPos(new Vector2(b.X - 470, ra.Y + 10));
            if (SmallButton("VIEW IN ENGINE", 174, !_jobs.Busy)) _jobs.ViewModel(_edModelPath);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open it in Prisma's renderer with the game's materials:\ndrag to turn, wheel to zoom, Tab for another prefab's materials.");
            ImGui.SameLine(0, 6);
            if (SmallButton("ASK CLAUDE", 130, true))
            {
                var chat = _chats.New(); chat.Title = "Model: " + Path.GetFileNameWithoutExtension(_edModelPath);
                _chatInput = $"About the model {_edModelPath} (the prisma tools asset_model and asset_model_preview read it): ";
                _page = Page.Chat;
            }
            ImGui.SameLine(0, 6);
            if (SmallButton("FOLDER", 130, true)) SourceControl.OpenUrl(Path.GetDirectoryName(Ed.FullPath(_edModelPath))!);

            // preview
            float pv = Math.Min(460, (b.X - ra.X) * 0.48f);
            var pa = ra + new Vector2(18, 60);
            dl.AddRectFilled(pa, pa + new Vector2(pv, pv), Neon.U(Neon.Space0, 0.9f), 8);
            // Drag across the picture to turn the model (the sheet's nearest view).
            ImGui.SetCursorScreenPos(pa);
            ImGui.InvisibleButton("##edturn", new Vector2(pv, pv));
            if (ImGui.IsItemActive()) _edAngle = Wrap(_edAngle - ImGui.GetIO().MouseDelta.X * 0.6f);
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
            if (_edPreviewFile != null && File.Exists(_edPreviewFile))
            {
                var (tex, _) = PngTexture(_edPreviewFile);
                var (frames, cols, yaw0, step) = _edSheet;
                int rows = (frames + cols - 1) / cols;
                int k = step > 0 ? ((int)Math.Round((_edAngle - yaw0) / step) % frames + frames) % frames : 0;
                var uv0 = new Vector2((float)(k % cols) / cols, (float)(k / cols) / rows);
                var uv1 = uv0 + new Vector2(1f / cols, 1f / rows);
                if (tex != 0) dl.AddImageRounded((IntPtr)tex, pa, pa + new Vector2(pv, pv), uv0, uv1, Neon.U(Neon.Ink), 8);
            }
            if (_edPreviewLoading) dl.AddText(Neon.Small, 13, pa + new Vector2(12, pv - 24), Neon.U(Neon.Amber), "rendering...");
            ImGui.SetCursorScreenPos(pa + new Vector2(0, pv + 10));
            bool turns = _edSheet.step > 0;
            if (SmallButton("<", 50, turns)) _edAngle = Wrap(_edAngle - 45);
            ImGui.SameLine(0, 6);
            if (SmallButton(">", 50, turns)) _edAngle = Wrap(_edAngle + 45);
            ImGui.SameLine(0, 6);
            if (SmallButton("FRONT", 90, turns)) _edAngle = 180;
            ImGui.SameLine(0, 10);
            string source = _edModel != null && _edModel.RootElement.TryGetProperty("materialSource", out var ms) && ms.ValueKind == JsonValueKind.String
                ? "colours from " + Path.GetFileNameWithoutExtension(ms.GetString())
                : "the model's own colours";
            ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, turns ? $"drag to turn  ·  {source}" : source); ImGui.PopFont();

            // facts
            var ia = new Vector2(pa.X + pv + 24, pa.Y);
            ImGui.SetCursorScreenPos(ia);
            ImGui.BeginChild("##edmodelinfo", new Vector2(b.X - ia.X - 16, b.Y - ia.Y - 12));
            if (_edModel == null)
            {
                if (_edModelLoading) ImGui.TextColored(Neon.Dim, DccOf(_edModelPath) is { } dcc ? $"Converting through {dcc}, as Unity does..." : "Importing...");
                else if (_edModelError != null) { ImGui.PushTextWrapPos(); ImGui.TextColored(Neon.Amber, _edModelError); ImGui.PopTextWrapPos(); }
            }
            else
            {
                // The JSON comes from the branch's own cs-asset, which may be older or newer than this
                // Prisma: a shape this page does not expect shows a line, never takes Prisma down.
                try { DrawModelFacts(_edModel.RootElement); }
                catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Amber, "This branch's cs-asset describes models differently: " + e.Message); ImGui.PopFont();
                }
            }
            ImGui.EndChild();
        }

        static float Wrap(float deg) => ((deg % 360) + 360) % 360;

        // ── IMPORT: a new model in, with the project's import settings (cs-asset model-import) ──
        bool _edImporting, _edImportBusy, _edImportPrefab = true;
        string _edImportSrc = "", _edImportTo = "Assets/_Models", _edImportName = "";

        void DrawModelImport(Vector2 a, Vector2 b)
        {
            ImGui.SetCursorScreenPos(a);
            ImGui.BeginChild("##edimport", b - a);
            ImGui.PushFont(Neon.Small);
            ImGui.TextColored(Neon.Dim, "The model is copied into the project with a .meta\nin the settings the other models share, and a new GUID.");
            ImGui.Dummy(new Vector2(0, 6));
            ImGui.TextColored(Neon.Ink, "File");
            ImGui.PushItemWidth(-1);
            ImGui.InputTextWithHint("##impsrc", "C:\\...\\model.fbx", ref _edImportSrc, 1024);
            ImGui.PopItemWidth();
            if (OperatingSystem.IsWindows() && SmallButton("BROWSE", 100, !_edImportBusy))
                Task.Run(() =>
                {
                    var picked = FilePicker.Open("Pick a model", "Models|*.fbx;*.blend;*.ma;*.mb");
                    if (picked != null) { _edImportSrc = picked; if (_edImportName.Length == 0) _edImportName = Path.GetFileNameWithoutExtension(picked); }
                });
            ImGui.Dummy(new Vector2(0, 4));
            ImGui.TextColored(Neon.Ink, "Into folder");
            ImGui.PushItemWidth(-1);
            ImGui.InputText("##impto", ref _edImportTo, 512);
            ImGui.PopItemWidth();
            if (_edModelPath != null && SmallButton("SAME AS SELECTED", 170, true)) _edImportTo = Path.GetDirectoryName(_edModelPath)!.Replace('\\', '/');
            ImGui.Dummy(new Vector2(0, 4));
            ImGui.TextColored(Neon.Ink, "Name");
            ImGui.PushItemWidth(-1);
            ImGui.InputTextWithHint("##impname", "the file's name", ref _edImportName, 128);
            ImGui.PopItemWidth();
            ImGui.Checkbox("Also make a prefab that holds it", ref _edImportPrefab);
            ImGui.Dummy(new Vector2(0, 8));
            bool ready = !_edImportBusy && File.Exists(_edImportSrc.Trim().Trim('"')) && IsModelFile(_edImportSrc.Trim().Trim('"'))
                         && _edImportTo.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal);
            if (SmallButton(_edImportBusy ? "IMPORTING..." : "IMPORT", 160, ready))
            {
                _edImportBusy = true;
                var src = _edImportSrc.Trim().Trim('"');
                var args = new List<string> { "model-import", src, "--to", _edImportTo.Replace('\\', '/').TrimEnd('/') };
                if (_edImportName.Trim().Length > 0) { args.Add("--name"); args.Add(_edImportName.Trim()); }
                if (_edImportPrefab) args.Add("--prefab");
                Task.Run(async () =>
                {
                    try
                    {
                        var (ok, stdout, err) = await Ed.Run(args.ToArray());
                        if (!ok) { EdNote(err.Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "import failed", ""); return; }
                        string? path = null, prefab = null;
                        try
                        {
                            using var d = JsonDocument.Parse(EditorTool.RepairConsoleText(stdout));
                            path = d.RootElement.GetProperty("path").GetString();
                            if (d.RootElement.TryGetProperty("prefab", out var pf) && pf.ValueKind == JsonValueKind.String) prefab = pf.GetString();
                        }
                        catch (Exception e) when (e is JsonException or KeyNotFoundException) { }
                        EdNote(null, $"Imported {path}" + (prefab != null ? $" and {prefab}" : "") + ". Commit it on GIT when it looks right.");
                        _edModels = null; _edImporting = false; _edImportName = ""; _edImportSrc = "";
                        if (path != null) _edOpen = path;
                    }
                    finally { _edImportBusy = false; }
                });
            }
            ImGui.PopFont();
            ImGui.EndChild();
        }

        void DrawModelFacts(JsonElement m)
        {
            string S(string k) => m.TryGetProperty(k, out var v) ? v.ToString() : "";
            void Fact(string label, string value)
            {
                ImGui.PushFont(Neon.Small);
                ImGui.TextColored(Neon.Dim, label); ImGui.SameLine(150); ImGui.TextColored(Neon.Ink, value);
                ImGui.PopFont();
            }
            var size = m.GetProperty("bounds").GetProperty("size").EnumerateArray().Select(x => x.GetDouble().ToString("0.##")).ToArray();
            Fact("Triangles", $"{m.GetProperty("triangles").GetInt32():N0}");
            Fact("Vertices", $"{m.GetProperty("vertices").GetInt32():N0}");
            Fact("Meshes / nodes", $"{S("meshCount")} / {S("nodes")}");
            Fact("Size", string.Join(" x ", size) + " m");
            Fact("Materials", string.Join(", ", m.GetProperty("materials").EnumerateArray().Select(x => x.GetString())));
            Fact("Blend shapes", S("blendShapes"));
            Fact("Skinned", m.GetProperty("skinned").GetBoolean() ? "yes" : "no");
            Fact("Takes", m.GetProperty("takes").GetArrayLength() == 0 ? "none" : string.Join(", ", m.GetProperty("takes").EnumerateArray().Select(x => x.GetString())));
            var st = m.GetProperty("settings");
            Fact("Import scale", $"{st.GetProperty("globalScale")}  ·  file scale {(st.GetProperty("useFileScale").GetBoolean() ? S("fileScale") : "off")}");
            Fact("File", $"{S("sizeKB")} KB  ·  guid {Trim(S("guid"), 12)}");
            if (m.TryGetProperty("discardedPolygons", out var dp) && dp.GetInt32() > 0) Fact("Discarded", $"{dp.GetInt32()} degenerate polygons");
            foreach (var w in m.GetProperty("warnings").EnumerateArray()) { ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Amber, Glyphs(w.GetString() ?? "")); ImGui.PopFont(); }
            DrawShapeSliders(m);
            if (m.TryGetProperty("gameMaterials", out var gm) && gm.ValueKind == JsonValueKind.Array) DrawGameMaterials(m, gm);
            ImGui.Dummy(new Vector2(0, 8));
            ImGui.PushFont(Neon.Strong); ImGui.TextColored(Neon.Cyan, "MESHES"); ImGui.PopFont();
            ImGui.PushFont(Neon.Small);
            foreach (var mesh in m.GetProperty("meshes").EnumerateArray())
            {
                var shapes = mesh.GetProperty("blendShapes").EnumerateArray().Select(x => x.GetString()).ToList();
                ImGui.TextColored(Neon.Ink, Glyphs(mesh.GetProperty("name").GetString() ?? ""));
                ImGui.TextColored(Neon.Dim, $"   {mesh.GetProperty("triangles").GetInt32():N0} tris  ·  {mesh.GetProperty("submeshes")} submesh  ·  " +
                                            (mesh.GetProperty("skinned").GetBoolean() ? $"skinned, {mesh.GetProperty("bones")} bones  ·  " : "") +
                                            (shapes.Count > 0 ? $"shapes: {string.Join(", ", shapes)}" : ""));
            }
            ImGui.PopFont();
        }

        /// <summary>
        /// A slider per blend shape (the elemental hull morphs, the crystals' spins): letting go
        /// re-renders the turntable with those weights. VIEW IN ENGINE has the same sliders live.
        /// </summary>
        void DrawShapeSliders(JsonElement m)
        {
            var names = m.GetProperty("meshes").EnumerateArray()
                .SelectMany(x => x.TryGetProperty("blendShapes", out var b) ? b.EnumerateArray().Select(n => n.GetString() ?? "") : Enumerable.Empty<string>())
                .Where(n => n.Length > 0).Distinct().ToList();
            if (names.Count == 0) return;
            ImGui.Dummy(new Vector2(0, 8));
            ImGui.PushFont(Neon.Strong); ImGui.TextColored(Neon.Cyan, "BLEND SHAPES"); ImGui.PopFont();
            ImGui.SameLine(0, 12);
            if (SmallButton("ZERO", 70, _edShapes.Values.Any(v => v > 0) && !_edPreviewLoading)) { _edShapes.Clear(); RenderPreview(); }
            ImGui.PushFont(Neon.Small);
            foreach (var n in names)
            {
                ImGui.PushID("shape" + n);
                float w = _edShapes.TryGetValue(n, out var cur) ? cur : 0;
                ImGui.TextColored(Neon.Ink, Trim(Glyphs(n), 28)); ImGui.SameLine(200);
                ImGui.PushItemWidth(Math.Max(120, ImGui.GetContentRegionAvail().X - 8));
                if (ImGui.SliderFloat("##w", ref w, 0, 100, "%.0f")) _edShapes[n] = w;
                if (ImGui.IsItemDeactivatedAfterEdit()) RenderPreview();
                ImGui.PopItemWidth();
                ImGui.PopID();
            }
            ImGui.PopFont();
        }

        /// <summary>The materials the game draws the model with (from the prefabs that use it), as swatches.</summary>
        void DrawGameMaterials(JsonElement m, JsonElement gm)
        {
            ImGui.Dummy(new Vector2(0, 8));
            ImGui.PushFont(Neon.Strong); ImGui.TextColored(Neon.Cyan, "IN THE GAME"); ImGui.PopFont();
            ImGui.PushFont(Neon.Small);
            var used = m.TryGetProperty("usedBy", out var u) ? u.EnumerateArray().Select(x => Path.GetFileNameWithoutExtension(x.GetString() ?? "")).ToList() : new List<string>();
            ImGui.TextColored(Neon.Dim, used.Count == 0 ? "No prefab draws it: these are its own materials." : "Drawn by " + string.Join(", ", used.Take(6)) + (used.Count > 6 ? $" +{used.Count - 6}" : ""));
            var dl = ImGui.GetWindowDrawList();
            foreach (var mesh in gm.EnumerateArray())
            {
                ImGui.TextColored(Neon.Ink, Glyphs(mesh.GetProperty("mesh").GetString() ?? ""));
                foreach (var mat in mesh.GetProperty("materials").EnumerateArray())
                {
                    var p = ImGui.GetCursorScreenPos() + new Vector2(14, 3);
                    var hex = mat.TryGetProperty("color", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                    if (hex is { Length: 7 })
                    {
                        var col = new Vector4(Convert.ToInt32(hex[1..3], 16) / 255f, Convert.ToInt32(hex[3..5], 16) / 255f, Convert.ToInt32(hex[5..7], 16) / 255f, 1f);
                        dl.AddRectFilled(p, p + new Vector2(12, 12), Neon.U(col), 3);
                    }
                    dl.AddRect(p, p + new Vector2(12, 12), Neon.U(Neon.Dim, 0.6f), 3);
                    ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(32, 0));
                    string shader = mat.TryGetProperty("shader", out var sh) && sh.ValueKind == JsonValueKind.String ? sh.GetString()!.Replace("Shader Graphs/", "") : "?";
                    ImGui.TextColored(Neon.Ink, Glyphs(mat.GetProperty("name").GetString() ?? "")); ImGui.SameLine(0, 8);
                    ImGui.TextColored(Neon.Dim, shader + (hex != null ? "  " + hex : ""));
                    if (ImGui.IsItemHovered() && mat.TryGetProperty("path", out var mp) && mp.ValueKind == JsonValueKind.String) ImGui.SetTooltip(mp.GetString());
                }
            }
            ImGui.PopFont();
        }
    }
}
