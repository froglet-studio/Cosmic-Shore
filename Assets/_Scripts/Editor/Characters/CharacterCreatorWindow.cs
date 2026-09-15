using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The chimeric character creator: a contact sheet of baked portraits (18 chimeras + 6 pure
    /// human controls from one seed, with a BLIND toggle that hides the labels so a viewer can
    /// try to name the two clades before revealing them), and a single-face inspector with live
    /// clade pickers, the three weight sliders, a re-roll, and genome save/load.
    ///
    /// READER by the tooling contract (`Docs/TOOLING.md` §6): every write goes under
    /// <c>Library/</c> or to a path the user picks in a file panel; nothing lands in
    /// <c>Assets/</c>, nothing is ledgered, no ship panel.
    /// </summary>
    public sealed class CharacterCreatorWindow : EditorWindow
    {
        const string ColorSetPath = "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset";
        const int SheetChimeras = 18, SheetHumans = 6, SheetColumns = 6;
        const int SheetPortraitSize = 256, InspectorPortraitSize = 512;

        [MenuItem("FrogletTools/Characters/Chimera Character Creator", false, 300)]
        [FrogletTool(FrogletToolCategory.Misc, Importance = 3,
            Description = "Spike: bake a contact sheet of chimeric faces (human × two animal clades) and inspect one with live sliders.",
            DocPath = "Assets/_Scripts/Controller/Characters/CHARACTERS.md")]
        public static void Open()
        {
            var w = GetWindow<CharacterCreatorWindow>("Chimera Characters");
            w.minSize = new Vector2(900f, 640f);
        }

        enum Tab { Sheet, Inspector, Avatars }

        Tab _tab;
        int _sheetSeed = 1;
        bool _blind;
        readonly List<CharacterGenome> _sheet = new();
        readonly List<Texture2D> _sheetPortraits = new();
        readonly List<string> _sheetErrors = new();
        Vector2 _scroll;
        int _selected = -1;

        CharacterGenome _current;
        Texture2D _currentPortrait;
        string _currentError;
        int _currentSeed = 7;
        bool _currentDirty = true;

        CladeCatalog _catalog;
        CharacterGenerationConfigSO _config;
        SO_ColorSet _colorSet;
        string[] _animalKeys = Array.Empty<string>();
        readonly List<Action> _deferred = new();

        // Avatars tab: the shipped profile icons beside their recreations.
        const string ProfileIconsPath = "Assets/_SO_Assets/SO_DefaultProfileIcons.asset";
        readonly List<AvatarPresets.Preset> _presets = new();
        readonly List<Texture2D> _presetPortraits = new();
        readonly List<string> _presetErrors = new();
        readonly Dictionary<string, Sprite> _icons = new();
        Vector2 _avatarScroll;

        void OnEnable()
        {
            Refresh();
            if (_current.Weights.Human == 0f && _current.IsPureHuman && string.IsNullOrEmpty(_current.CladeA))
                _current = SafeRoll(_currentSeed, false);
        }

        void OnDisable()
        {
            ClearSheetTextures();
            ClearPresetTextures();
            if (_currentPortrait) Object.DestroyImmediate(_currentPortrait);
        }

        void Refresh()
        {
            var guids = AssetDatabase.FindAssets("t:CladeSO");
            var clades = new List<CladeSO>();
            foreach (var g in guids)
            {
                var c = AssetDatabase.LoadAssetAtPath<CladeSO>(AssetDatabase.GUIDToAssetPath(g));
                if (c) clades.Add(c);
            }
            _catalog = new CladeCatalog(clades);
            _config = CharacterGenerationConfigSO.LoadDefault();
            if (!_config)
            {
                var cfgGuids = AssetDatabase.FindAssets("t:CharacterGenerationConfigSO");
                if (cfgGuids.Length > 0) _config = AssetDatabase.LoadAssetAtPath<CharacterGenerationConfigSO>(AssetDatabase.GUIDToAssetPath(cfgGuids[0]));
            }
            _colorSet = AssetDatabase.LoadAssetAtPath<SO_ColorSet>(ColorSetPath);
            var animals = _catalog.Animals();
            _animalKeys = new string[animals.Count];
            for (int i = 0; i < animals.Count; i++) _animalKeys[i] = animals[i].Key;
        }

        bool Ready => _catalog != null && _config != null && _catalog.Human != null && _animalKeys.Length >= 2;

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Chimera Character Creator",
                "Do these read as people you could care about? Bake a sheet, hide the labels, try to name the clades.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Misc));

            if (!Ready)
            {
                EditorGUILayout.HelpBox("Needs the clade assets under Resources/Characters/Clades (one IsHuman + at least two animals) and CharacterGenerationConfig.asset.", MessageType.Warning);
                if (GUILayout.Button("Refresh")) Refresh();
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Toggle(_tab == Tab.Sheet, "Contact Sheet", EditorStyles.toolbarButton)) _tab = Tab.Sheet;
                if (GUILayout.Toggle(_tab == Tab.Inspector, "Inspector", EditorStyles.toolbarButton)) _tab = Tab.Inspector;
                if (GUILayout.Toggle(_tab == Tab.Avatars, "Avatars", EditorStyles.toolbarButton)) _tab = Tab.Avatars;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear portrait cache", EditorStyles.toolbarButton))
                    _deferred.Add(() => { CharacterPortraitBaker.ClearCache(); ClearSheetTextures(); _currentDirty = true; });
                if (GUILayout.Button("Refresh assets", EditorStyles.toolbarButton)) _deferred.Add(Refresh);
            }

            if (_tab == Tab.Sheet) DrawSheet(); else if (_tab == Tab.Inspector) DrawInspector(); else DrawAvatars();

            if (_deferred.Count > 0 && Event.current.type == EventType.Repaint)
            {
                var actions = _deferred.ToArray();
                _deferred.Clear();
                foreach (var a in actions) a();
                Repaint();
            }
        }

        void DrawSheet()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _sheetSeed = EditorGUILayout.IntField("Sheet seed", _sheetSeed, GUILayout.Width(220f));
                if (FrogletEditorPalette.ColorButton($"Bake sheet ({SheetChimeras} chimeras + {SheetHumans} humans)", FrogletEditorPalette.Jade, 300f))
                    _deferred.Add(BakeSheet);
                GUILayout.Space(12f);
                bool blind = GUILayout.Toggle(_blind, _blind ? "BLIND — labels hidden (click to reveal)" : "Blind toggle (hide labels)", "Button", GUILayout.Width(260f));
                if (blind != _blind) _blind = blind;
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_sheetPortraits.Count == 0))
                    if (GUILayout.Button("Export sheet PNG", GUILayout.Width(140f))) _deferred.Add(ExportSheet);
            }
            EditorGUILayout.LabelField(_sheet.Count == 0
                ? "No sheet yet. Baking renders each bust twice (alpha recovery) in its own preview scene; expect about a minute."
                : $"{_sheet.Count} portraits · seed {_sheetSeed} · the last {SheetHumans} are the pure-human CONTROL (same head, same blend spaces, no clade).", EditorStyles.miniLabel);

            if (_sheet.Count == 0) return;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            float cell = Mathf.Max(140f, (position.width - 40f) / SheetColumns);
            float labelH = _blind ? 18f : 34f;
            int rows = (_sheet.Count + SheetColumns - 1) / SheetColumns;
            var area = GUILayoutUtility.GetRect(cell * SheetColumns, rows * (cell + labelH));
            for (int i = 0; i < _sheet.Count; i++)
            {
                var r = new Rect(area.x + (i % SheetColumns) * cell, area.y + (i / SheetColumns) * (cell + labelH), cell - 4f, cell - 4f);
                bool isControl = _sheet[i].IsPureHuman;
                FrogletEditorPalette.DrawCard(r, FrogletEditorPalette.Surface,
                    i == _selected ? FrogletEditorPalette.Jade : (isControl ? FrogletEditorPalette.Gold.WithAlpha(0.6f) : FrogletEditorPalette.Muted.WithAlpha(0.3f)));
                if (i < _sheetPortraits.Count && _sheetPortraits[i])
                    GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), _sheetPortraits[i], ScaleMode.ScaleToFit, true);
                else if (i < _sheetErrors.Count && !string.IsNullOrEmpty(_sheetErrors[i]))
                    GUI.Label(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f), _sheetErrors[i], FrogletEditorPalette.CardBodyWrapped);
                var lr = new Rect(r.x, r.yMax + 2f, r.width, labelH);
                if (_blind)
                {
                    GUI.Label(lr, $"#{i + 1}", FrogletEditorPalette.Pill);
                }
                else
                {
                    GUI.Label(new Rect(lr.x, lr.y, lr.width, 16f), isControl ? $"#{i + 1}  {_sheet[i].Label()}  (control)" : $"#{i + 1}  {_sheet[i].Label()}", FrogletEditorPalette.CardTitle);
                    GUI.Label(new Rect(lr.x, lr.y + 16f, lr.width, 16f), string.Join(", ", _sheet[i].ExpressedTraits ?? Array.Empty<string>()).Replace("Human.", "H."), FrogletEditorPalette.CardBody);
                }
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    _selected = i;
                    _current = _sheet[i];
                    _currentSeed = _current.Seed;
                    _currentDirty = true;
                    if (Event.current.clickCount == 2) _tab = Tab.Inspector;
                    Event.current.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawAvatars()
        {
            if (_presets.Count == 0) LoadPresets();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton($"Bake all {_presets.Count} avatar recreations", FrogletEditorPalette.Jade, 300f))
                    _deferred.Add(BakePresets);
                if (GUILayout.Button("Reload presets", GUILayout.Width(120f))) _deferred.Add(() => { LoadPresets(); ClearPresetTextures(); });
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.LabelField("Left: the shipped profile icon. Right: the generator's nearest genome, baked in the painterly style. Click a pair to open it in the Inspector.", EditorStyles.miniLabel);
            if (_presets.Count == 0)
            {
                EditorGUILayout.HelpBox($"No presets under Resources/{AvatarPresets.ResourcesFolder}. Run author_character_assets.py.", MessageType.Info);
                return;
            }
            _avatarScroll = EditorGUILayout.BeginScrollView(_avatarScroll);
            const int pairsPerRow = 3;
            float pair = Mathf.Max(260f, (position.width - 40f) / pairsPerRow);
            float cell = pair * 0.5f - 6f;
            int rows = (_presets.Count + pairsPerRow - 1) / pairsPerRow;
            var area = GUILayoutUtility.GetRect(pair * pairsPerRow, rows * (cell + 24f));
            for (int i = 0; i < _presets.Count; i++)
            {
                var pr = _presets[i];
                float x = area.x + (i % pairsPerRow) * pair, y = area.y + (i / pairsPerRow) * (cell + 24f);
                var left = new Rect(x, y, cell, cell);
                var right = new Rect(x + cell + 8f, y, cell, cell);
                FrogletEditorPalette.DrawCard(left, FrogletEditorPalette.Surface, FrogletEditorPalette.Muted.WithAlpha(0.3f));
                FrogletEditorPalette.DrawCard(right, FrogletEditorPalette.Surface, i == _selected ? FrogletEditorPalette.Jade : FrogletEditorPalette.Muted.WithAlpha(0.3f));
                if (_icons.TryGetValue(pr.IconName, out var sprite) && sprite)
                    GUI.DrawTexture(new Rect(left.x + 2f, left.y + 2f, left.width - 4f, left.height - 4f), sprite.texture, ScaleMode.ScaleToFit, true);
                if (i < _presetPortraits.Count && _presetPortraits[i])
                    GUI.DrawTexture(new Rect(right.x + 2f, right.y + 2f, right.width - 4f, right.height - 4f), _presetPortraits[i], ScaleMode.ScaleToFit, true);
                else if (i < _presetErrors.Count && !string.IsNullOrEmpty(_presetErrors[i]))
                    GUI.Label(new Rect(right.x + 4f, right.y + 4f, right.width - 8f, right.height - 8f), _presetErrors[i], FrogletEditorPalette.CardBodyWrapped);
                GUI.Label(new Rect(x, y + cell + 2f, pair - 8f, 18f), $"{pr.Name}   ·   {pr.Genome.Label()}", FrogletEditorPalette.CardTitle);
                var both = new Rect(x, y, pair - 8f, cell);
                if (Event.current.type == EventType.MouseDown && both.Contains(Event.current.mousePosition))
                {
                    _selected = i;
                    _current = pr.Genome;
                    _currentSeed = _current.Seed;
                    _currentDirty = true;
                    if (Event.current.clickCount == 2) _tab = Tab.Inspector;
                    Event.current.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void LoadPresets()
        {
            _presets.Clear();
            _presets.AddRange(AvatarPresets.FromResources());
            _icons.Clear();
            var list = AssetDatabase.LoadAssetAtPath<SO_ProfileIconList>(ProfileIconsPath);
            if (list != null && list.profileIcons != null)
                foreach (var icon in list.profileIcons)
                    if (icon.IconSprite) _icons[icon.IconSprite.name] = icon.IconSprite;
        }

        void BakePresets()
        {
            ClearPresetTextures();
            try
            {
                for (int i = 0; i < _presets.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Baking avatar recreations", $"{i + 1}/{_presets.Count}  {_presets[i].Name}", i / (float)_presets.Count);
                    var result = CharacterPortraitBaker.Bake(_presets[i].Genome, _catalog, _config, _colorSet, SheetPortraitSize);
                    _presetPortraits.Add(result.Texture);
                    _presetErrors.Add(result.Error);
                    if (!string.IsNullOrEmpty(result.Error)) Debug.LogWarning($"[Characters] avatar {_presets[i].Name}: {result.Error}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void ClearPresetTextures()
        {
            foreach (var t in _presetPortraits) if (t) Object.DestroyImmediate(t);
            _presetPortraits.Clear();
            _presetErrors.Clear();
        }

        void DrawInspector()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // ---- controls ---------------------------------------------------------
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(380f)))
                {
                    EditorGUILayout.LabelField("Genome", FrogletEditorPalette.SectionHeader);
                    var g = _current;
                    bool pureHuman = EditorGUILayout.Toggle("Pure human (control)", g.IsPureHuman);
                    if (pureHuman != g.IsPureHuman)
                    {
                        if (pureHuman) { g.CladeA = string.Empty; g.CladeB = string.Empty; g.Weights = new CharacterWeights { Human = 1f }; }
                        else { g.CladeA = _animalKeys[0]; g.CladeB = _animalKeys[1]; g.Weights = CharacterWeights.EqualThirds; }
                        _currentDirty = true;
                    }
                    if (!g.IsPureHuman)
                    {
                        int ia = Mathf.Max(0, Array.IndexOf(_animalKeys, g.CladeA));
                        int ib = Mathf.Max(0, Array.IndexOf(_animalKeys, g.CladeB));
                        int na = EditorGUILayout.Popup("Clade A", ia, _animalKeys);
                        int nb = EditorGUILayout.Popup("Clade B", ib, _animalKeys);
                        if (na != ia || nb != ib) { g.CladeA = _animalKeys[na]; g.CladeB = _animalKeys[nb]; _currentDirty = true; }

                        EditorGUILayout.LabelField($"Weights (each in [{CharacterWeights.Min:0.00}, {CharacterWeights.Max:0.00}], sum 1)", EditorStyles.miniLabel);
                        var w = g.Weights;
                        float h = EditorGUILayout.Slider("Human", w.Human, CharacterWeights.Min, CharacterWeights.Max);
                        if (Math.Abs(h - w.Human) > 1e-5f) { w = CharacterWeights.ConstrainHolding(w, 0, h); _currentDirty = true; }
                        float a = EditorGUILayout.Slider(g.CladeA, w.CladeA, CharacterWeights.Min, CharacterWeights.Max);
                        if (Math.Abs(a - w.CladeA) > 1e-5f) { w = CharacterWeights.ConstrainHolding(w, 1, a); _currentDirty = true; }
                        float b = EditorGUILayout.Slider(g.CladeB, w.CladeB, CharacterWeights.Min, CharacterWeights.Max);
                        if (Math.Abs(b - w.CladeB) > 1e-5f) { w = CharacterWeights.ConstrainHolding(w, 2, b); _currentDirty = true; }
                        g.Weights = w;
                    }

                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Individual", FrogletEditorPalette.SectionHeader);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _currentSeed = EditorGUILayout.IntField("Seed", _currentSeed);
                        if (GUILayout.Button("Re-roll", GUILayout.Width(70f)))
                        {
                            _currentSeed = unchecked(_currentSeed * 1103515245 + 12345) & 0x7FFFFFFF;
                            g = GenomeRoller.RerollIndividual(g, _currentSeed, _config);
                            _currentDirty = true;
                        }
                    }
                    g.Age = EditorGUILayout.Slider("Age", g.Age, 0f, 1f);
                    g.Fleshiness = EditorGUILayout.Slider("Fleshiness", g.Fleshiness, 0f, 1f);
                    g.HairVolume = EditorGUILayout.Slider("Hair volume", g.HairVolume, 0f, 1f);
                    g.SkinTone = EditorGUILayout.Slider("Skin tone", g.SkinTone, 0f, 1f);
                    g.SkinWarmth = EditorGUILayout.Slider("Skin warmth", g.SkinWarmth, 0f, 1f);
                    g.HairShade = EditorGUILayout.Slider("Hair shade", g.HairShade, 0f, 1f);
                    g.IrisKey = EditorGUILayout.Slider("Iris key", g.IrisKey, 0f, 1f);
                    g.MarkingKey = EditorGUILayout.Slider("Marking key", g.MarkingKey, 0f, 1f);
                    g.Domain = (CosmicShore.Data.Domains)EditorGUILayout.EnumPopup("Domain (accent)", g.Domain);
                    if (GUI.changed) _currentDirty = true;
                    _current = g;

                    EditorGUILayout.Space(8f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (FrogletEditorPalette.ColorButton(_currentDirty ? "Bake portrait" : "Portrait up to date", FrogletEditorPalette.Jade, 150f, enabled: _currentDirty))
                            _deferred.Add(BakeCurrent);
                        if (GUILayout.Button("Save genome…", GUILayout.Width(110f)))
                        {
                            var path = CharacterGenomeFile.PromptSavePath(_current);
                            if (!string.IsNullOrEmpty(path) && CharacterGenomeFile.Save(_current, path)) Debug.Log($"[Characters] genome saved: {path}");
                        }
                        if (GUILayout.Button("Load genome…", GUILayout.Width(110f)))
                        {
                            var path = CharacterGenomeFile.PromptLoadPath();
                            if (!string.IsNullOrEmpty(path) && CharacterGenomeFile.TryLoad(path, out var loaded)) { _current = loaded; _currentSeed = loaded.Seed; _currentDirty = true; }
                        }
                    }
                    if (GUILayout.Button("Copy genome JSON to clipboard")) EditorGUIUtility.systemCopyBuffer = _current.ToJson(true);

                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("Expressed traits", FrogletEditorPalette.SectionHeader);
                    var traits = _current.ExpressedTraits ?? Array.Empty<string>();
                    EditorGUILayout.LabelField(traits.Length == 0 ? "(bake to resolve)" : string.Join("\n", traits), FrogletEditorPalette.CardBodyWrapped);
                    if (!string.IsNullOrEmpty(_currentError)) EditorGUILayout.HelpBox(_currentError, MessageType.Error);
                }

                // ---- portrait ---------------------------------------------------------
                using (new EditorGUILayout.VerticalScope())
                {
                    float side = Mathf.Min(position.width - 420f, position.height - 200f);
                    var r = GUILayoutUtility.GetRect(side, side);
                    FrogletEditorPalette.DrawCard(r, FrogletEditorPalette.Surface, FrogletEditorPalette.Muted.WithAlpha(0.3f));
                    if (_currentPortrait) GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), _currentPortrait, ScaleMode.ScaleToFit, true);
                    else GUI.Label(r, "Bake to see the portrait.", FrogletEditorPalette.CardBody);
                    EditorGUILayout.LabelField(_current.Label(), FrogletEditorPalette.CardTitle);
                }
            }
        }

        // ------------------------------------------------------------------ actions

        CharacterGenome SafeRoll(int seed, bool human) =>
            !Ready ? CharacterGenome.Empty : (human ? GenomeRoller.RollHuman(seed, _config) : GenomeRoller.RollChimera(seed, _catalog, _config));

        void BakeSheet()
        {
            ClearSheetTextures();
            _sheet.Clear();
            _sheet.AddRange(GenomeRoller.RollSheet(_sheetSeed, SheetChimeras, SheetHumans, _catalog, _config));
            try
            {
                for (int i = 0; i < _sheet.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Baking portraits", $"{i + 1}/{_sheet.Count}  {_sheet[i].Label()}", i / (float)_sheet.Count);
                    var result = CharacterPortraitBaker.Bake(_sheet[i], _catalog, _config, _colorSet, SheetPortraitSize);
                    // The resolver stamps the expression list; keep it on the sheet's copy for the label.
                    var g = _sheet[i];
                    try { g = CharacterResolver.Resolve(g, _catalog, _config).Genome; } catch (Exception) { }
                    _sheet[i] = g;
                    _sheetPortraits.Add(result.Texture);
                    _sheetErrors.Add(result.Error);
                    if (!string.IsNullOrEmpty(result.Error)) Debug.LogWarning($"[Characters] portrait {i} ({g.Label()}): {result.Error}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log($"[Characters] baked sheet seed {_sheetSeed}: {_sheet.Count} portraits.");
        }

        void BakeCurrent()
        {
            if (_currentPortrait) Object.DestroyImmediate(_currentPortrait);
            _currentPortrait = null;
            _currentError = null;
            var result = CharacterPortraitBaker.Bake(_current, _catalog, _config, _colorSet, InspectorPortraitSize);
            _currentPortrait = result.Texture;
            _currentError = result.Error;
            try { _current = CharacterResolver.Resolve(_current, _catalog, _config).Genome; } catch (Exception e) { _currentError = e.Message; }
            _currentDirty = false;
        }

        void ExportSheet()
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "Library", "CharacterPortraits");
            Directory.CreateDirectory(folder);
            var path = EditorUtility.SaveFilePanel("Export contact sheet", folder, $"ChimeraSheet_seed{_sheetSeed}.png", "png");
            if (string.IsNullOrEmpty(path)) return;
            var labels = new List<string>();
            foreach (var g in _sheet) labels.Add($"{g.Label()} | {string.Join(", ", g.ExpressedTraits ?? Array.Empty<string>())}");
            if (CharacterPortraitBaker.ExportSheet(_sheetPortraits, labels, SheetColumns, path))
                Debug.Log($"[Characters] sheet exported: {path} (+ .txt labels)");
        }

        void ClearSheetTextures()
        {
            foreach (var t in _sheetPortraits) if (t) Object.DestroyImmediate(t);
            _sheetPortraits.Clear();
            _sheetErrors.Clear();
        }
    }
}
