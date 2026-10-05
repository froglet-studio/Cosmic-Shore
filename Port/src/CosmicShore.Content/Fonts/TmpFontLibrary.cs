using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.Content.Fonts
{
    /// <summary>
    /// Imports TextMeshPro font assets, their SDF atlases and TMP materials out of the Unity
    /// project's YAML. One library per <see cref="AssetDatabase"/>; everything is cached by
    /// (guid, fileID) so a scene's 346 references to one font resolve to one instance.
    /// Read-only: never writes into the Unity project and never mutates the global
    /// <see cref="TMP_Settings"/> unless <see cref="ApplyToGlobalSettings"/> is called.
    /// </summary>
    public sealed class TmpFontLibrary : ITmpFontResolver
    {
        /// <summary>TMP_FontAsset MonoScript guid.</summary>
        public const string FontAssetScriptGuid = "71c1514a6bd24e1e882cebbe1904ce04";
        /// <summary>TMP Settings asset MonoScript guid.</summary>
        public const string SettingsScriptGuid = "2705215ac5b84b70bacc50632be6e391";
        public const string AldrichGuid = "6ab8eca0e6e2b7c4a8a495d9afae2053";
        public const string LiberationSansGuid = "8f586378b4e144a9851e7b34d9b748ee";

        /// <summary>TMP shader guid → shader name (the two SDF shaders the project's fonts use, plus siblings).</summary>
        static readonly Dictionary<string, string> KnownShaders = new(StringComparer.Ordinal)
        {
            ["68e6db2ebdc24f95958faec2be5558d6"] = TmpSdfShader.DistanceFieldShaderName,
            ["fe393ace9b354375a9cb14cdbbc28be4"] = TmpSdfShader.MobileDistanceFieldShaderName,
        };

        public readonly AssetDatabase Db;

        readonly Dictionary<(string, long), TMP_FontAsset> _fonts = new();
        readonly Dictionary<(string, long), Material> _materials = new();
        bool _settingsLoaded;
        TMP_FontAsset _defaultFont;
        readonly List<TMP_FontAsset> _globalFallbacks = new();
        int _missingGlyphCharacter;

        public TmpFontLibrary(AssetDatabase db) { Db = db ?? throw new ArgumentNullException(nameof(db)); }

        // ── settings ──────────────────────────────────────────────────────────────

        public TMP_FontAsset DefaultFont { get { EnsureSettings(); return _defaultFont; } }
        public IReadOnlyList<TMP_FontAsset> GlobalFallbackFonts { get { EnsureSettings(); return _globalFallbacks; } }
        public int MissingGlyphCharacter { get { EnsureSettings(); return _missingGlyphCharacter; } }

        TMP_FontAsset ITmpFontResolver.DefaultFontAsset => DefaultFont;
        IReadOnlyList<TMP_FontAsset> ITmpFontResolver.GlobalFallbackFontAssets => GlobalFallbackFonts;
        int ITmpFontResolver.MissingGlyphCharacter => MissingGlyphCharacter;

        void EnsureSettings()
        {
            if (_settingsLoaded) return;
            _settingsLoaded = true;
            string guid = null;
            foreach (var p in Db.AllAssetPaths)
            {
                if (p.EndsWith("TMP Settings.asset", StringComparison.OrdinalIgnoreCase)) { guid = Db.GuidOf(p); break; }
            }
            var file = guid != null ? Db.Load(guid) : null;
            var body = file?.Get(11400000)?.Body;
            if (body == null)
            {
                _defaultFont = Load(LiberationSansGuid);
                return;
            }
            _defaultFont = LoadRef(ObjRef.From(body["m_defaultFontAsset"]), file);
            foreach (var item in body["m_fallbackFontAssets"]?.Items ?? Array.Empty<YNode>())
            {
                var f = LoadRef(ObjRef.From(item), file);
                if (f != null) _globalFallbacks.Add(f);
            }
            _missingGlyphCharacter = body.Int("m_missingGlyphCharacter");
        }

        /// <summary>Publishes the loaded project settings onto the engine-wide <see cref="TMP_Settings"/>.</summary>
        public void ApplyToGlobalSettings()
        {
            TMP_Settings.defaultFontAsset = DefaultFont;
            TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>(GlobalFallbackFonts);
            TMP_Settings.missingGlyphCharacter = MissingGlyphCharacter;
        }

        // ── fonts ─────────────────────────────────────────────────────────────────

        /// <summary>Loads the font asset in a file (the MonoBehaviour at fileID 11400000).</summary>
        public TMP_FontAsset Load(string guid) => LoadRef(new ObjRef(11400000, guid, 2), null);

        /// <summary>Loads a font asset from a serialized reference (e.g. a text component's <c>m_fontAsset</c>).</summary>
        public TMP_FontAsset LoadRef(ObjRef r, AssetFile context = null)
        {
            if (r.IsNull) return null;
            string guid = r.IsLocal ? context?.Guid : r.Guid;
            if (guid == null) return null;
            var key = (guid, r.FileId);
            if (_fonts.TryGetValue(key, out var cached)) return cached;
            var file = Db.Load(guid);
            var doc = file?.Get(r.FileId);
            if (doc == null || doc.TypeName != "MonoBehaviour") { _fonts[key] = null; return null; }
            var font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            _fonts[key] = font; // register before recursing into fallbacks (cycles)
            Populate(font, doc.Body, file);
            return font;
        }

        void Populate(TMP_FontAsset font, YMap b, AssetFile file)
        {
            font.name = b.Str("m_Name") ?? "TMP_FontAsset";
            font.sourceFontFileGuid = b.Str("m_SourceFontFileGUID");
            font.atlasPopulationMode = b.Int("m_AtlasPopulationMode");
            font.faceInfo = ReadFaceInfo(b["m_FaceInfo"] as YMap);
            font.atlasWidth = b.Int("m_AtlasWidth");
            font.atlasHeight = b.Int("m_AtlasHeight");
            font.atlasPadding = b.Int("m_AtlasPadding");
            font.atlasRenderMode = b.Int("m_AtlasRenderMode");
            font.normalStyle = b.Float("normalStyle");
            font.normalSpacingOffset = b.Float("normalSpacingOffset");
            font.boldStyle = b.Float("boldStyle", 0.75f);
            font.boldSpacing = b.Float("boldSpacing", 7f);
            font.italicStyle = (byte)Math.Clamp(b.Int("italicStyle", 35), 0, 255);
            font.tabSize = (byte)Math.Clamp(b.Int("tabSize", 10), 0, 255);

            foreach (var n in b["m_GlyphTable"]?.Items ?? Array.Empty<YNode>())
            {
                var m = n["m_Metrics"];
                var r = n["m_GlyphRect"];
                font.glyphTable.Add(new Glyph
                {
                    index = (uint)n.Long("m_Index"),
                    metrics = m == null ? default : new GlyphMetrics(m.Float("m_Width"), m.Float("m_Height"),
                        m.Float("m_HorizontalBearingX"), m.Float("m_HorizontalBearingY"), m.Float("m_HorizontalAdvance")),
                    glyphRect = r == null ? default : new GlyphRect(r.Int("m_X"), r.Int("m_Y"), r.Int("m_Width"), r.Int("m_Height")),
                    scale = n.Float("m_Scale", 1f),
                    atlasIndex = n.Int("m_AtlasIndex"),
                });
            }
            foreach (var n in b["m_CharacterTable"]?.Items ?? Array.Empty<YNode>())
            {
                font.characterTable.Add(new TMP_Character
                {
                    unicode = (uint)n.Long("m_Unicode"),
                    glyphIndex = (uint)n.Long("m_GlyphIndex"),
                    scale = n.Float("m_Scale", 1f),
                });
            }
            font.ReadFontAssetDefinition();

            foreach (var n in b["m_FontFeatureTable"]?["m_GlyphPairAdjustmentRecords"]?.Items ?? Array.Empty<YNode>())
            {
                var first = n["m_FirstAdjustmentRecord"];
                var second = n["m_SecondAdjustmentRecord"];
                if (first == null || second == null) continue;
                font.AddPairAdjustment(new GlyphPairAdjustmentRecord
                {
                    firstGlyphIndex = (uint)first.Long("m_GlyphIndex"),
                    firstAdjustment = ReadValueRecord(first["m_GlyphValueRecord"]),
                    secondGlyphIndex = (uint)second.Long("m_GlyphIndex"),
                    secondAdjustment = ReadValueRecord(second["m_GlyphValueRecord"]),
                });
            }

            // Atlas textures (usually sub-assets of this same file).
            var atlasRefs = b["m_AtlasTextures"]?.Items ?? Array.Empty<YNode>();
            var textures = new List<Texture2D>();
            var pixels = new List<byte[]>();
            foreach (var item in atlasRefs)
            {
                var r = ObjRef.From(item);
                var texDoc = r.IsNull ? null : Db.Resolve(r, file);
                if (texDoc == null || texDoc.ClassId != 28) continue;
                var (tex, px) = ReadAlphaTexture(texDoc.Body, font.atlasWidth, font.atlasHeight);
                textures.Add(tex);
                pixels.Add(px);
            }
            font.atlasTextures = textures.ToArray();
            font.atlasPixels = pixels.ToArray();
            if (font.atlasWidth == 0 && textures.Count > 0) { font.atlasWidth = textures[0].width; font.atlasHeight = textures[0].height; }

            // Default material.
            var matRef = ObjRef.From(b["material"]);
            font.material = LoadMaterial(matRef, file);

            // Fallbacks (after registration, so cycles terminate).
            foreach (var item in b["m_FallbackFontAssetTable"]?.Items ?? Array.Empty<YNode>())
            {
                var f = LoadRef(ObjRef.From(item), file);
                if (f != null && !ReferenceEquals(f, font)) font.fallbackFontAssetTable.Add(f);
            }
        }

        static GlyphValueRecord ReadValueRecord(YNode n)
            => n == null ? default : new GlyphValueRecord
            {
                xPlacement = n.Float("m_XPlacement"),
                yPlacement = n.Float("m_YPlacement"),
                xAdvance = n.Float("m_XAdvance"),
                yAdvance = n.Float("m_YAdvance"),
            };

        internal static FaceInfo ReadFaceInfo(YMap f)
        {
            if (f == null) return default;
            return new FaceInfo
            {
                faceIndex = f.Int("m_FaceIndex"),
                familyName = f.Str("m_FamilyName"),
                styleName = f.Str("m_StyleName"),
                pointSize = f.Float("m_PointSize"),
                scale = f.Float("m_Scale", 1f),
                unitsPerEM = f.Int("m_UnitsPerEM"),
                lineHeight = f.Float("m_LineHeight"),
                ascentLine = f.Float("m_AscentLine"),
                capLine = f.Float("m_CapLine"),
                meanLine = f.Float("m_MeanLine"),
                baseline = f.Float("m_Baseline"),
                descentLine = f.Float("m_DescentLine"),
                superscriptOffset = f.Float("m_SuperscriptOffset"),
                superscriptSize = f.Float("m_SuperscriptSize", 0.5f),
                subscriptOffset = f.Float("m_SubscriptOffset"),
                subscriptSize = f.Float("m_SubscriptSize", 0.5f),
                underlineOffset = f.Float("m_UnderlineOffset"),
                underlineThickness = f.Float("m_UnderlineThickness"),
                strikethroughOffset = f.Float("m_StrikethroughOffset"),
                strikethroughThickness = f.Float("m_StrikethroughThickness"),
                tabWidth = f.Float("m_TabWidth"),
            };
        }

        /// <summary>
        /// Reads a Texture2D document's raw pixels into Alpha8 (rows bottom-up, as Unity
        /// stores them). Supports Alpha8 (1), RGBA32 (4), ARGB32 (5), R8 (63); other formats
        /// and empty/streamed data yield an all-zero atlas of the declared size.
        /// </summary>
        internal static (Texture2D tex, byte[] alpha) ReadAlphaTexture(YMap t, int fallbackW, int fallbackH)
        {
            int w = t.Int("m_Width", fallbackW), h = t.Int("m_Height", fallbackH);
            int format = t.Int("m_TextureFormat");
            var tex = new Texture2D(w, h) { name = t.Str("m_Name") ?? "Atlas" };
            var alpha = new byte[Math.Max(0, w * h)];
            string hex = t.Str("_typelessdata");
            if (!string.IsNullOrEmpty(hex) && w > 0 && h > 0)
            {
                int bpp = format switch { 1 => 1, 63 => 1, 4 => 4, 5 => 4, _ => 0 };
                int alphaOffset = format switch { 4 => 3, 5 => 0, _ => 0 };
                if (bpp > 0)
                {
                    int count = Math.Min(w * h, hex.Length / 2 / bpp);
                    for (int i = 0; i < count; i++)
                    {
                        int pos = (i * bpp + alphaOffset) * 2;
                        alpha[i] = (byte)((Hex(hex[pos]) << 4) | Hex(hex[pos + 1]));
                    }
                }
            }
            return (tex, alpha);
        }

        static int Hex(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;

        // ── materials ─────────────────────────────────────────────────────────────

        /// <summary>Loads a TMP material (class 21) from a reference, e.g. a component's <c>m_sharedMaterial</c>.</summary>
        public Material LoadMaterial(ObjRef r, AssetFile context = null)
        {
            if (r.IsNull) return null;
            string guid = r.IsLocal ? context?.Guid : r.Guid;
            AssetFile file = r.IsLocal ? context : Db.Load(r.Guid);
            if (file == null) return null;
            var key = (guid ?? file.Path, r.FileId);
            if (_materials.TryGetValue(key, out var cached)) return cached;
            var doc = file.Get(r.FileId);
            // A .mat file's main object: when the reference fileID is 2100000 but the doc id differs, take the only material.
            if (doc == null && r.FileId == 2100000)
                foreach (var d in file.Documents) if (d.ClassId == 21) { doc = d; break; }
            if (doc == null || doc.ClassId != 21) { _materials[key] = null; return null; }
            var mat = ReadMaterial(doc.Body);
            _materials[key] = mat;
            return mat;
        }

        internal static Material ReadMaterial(YMap m)
        {
            var shaderRef = ObjRef.From(m["m_Shader"]);
            string shaderName = shaderRef.Guid != null && KnownShaders.TryGetValue(shaderRef.Guid, out var s)
                ? s : TmpSdfShader.DistanceFieldShaderName;
            var mat = new Material(Shader.Find(shaderName)) { name = m.Str("m_Name") ?? "TMP Material" };

            // Keywords: newer Unity writes m_ValidKeywords (list), older m_ShaderKeywords (space separated).
            foreach (var k in m["m_ValidKeywords"]?.Items ?? Array.Empty<YNode>())
                if (!string.IsNullOrEmpty(k.Scalar)) mat.EnableKeyword(k.Scalar);
            var legacy = m.Str("m_ShaderKeywords");
            if (!string.IsNullOrWhiteSpace(legacy))
                foreach (var k in legacy.Split(' ', StringSplitOptions.RemoveEmptyEntries)) mat.EnableKeyword(k);

            var props = m["m_SavedProperties"];
            foreach (var entry in props?["m_Floats"]?.Items ?? Array.Empty<YNode>())
                if (entry is YMap em)
                    foreach (var kv in em.Entries)
                        if (YScalar.TryFloat(kv.Value?.Scalar, out float f)) mat.SetFloat(kv.Key, f);
            foreach (var entry in props?["m_Ints"]?.Items ?? Array.Empty<YNode>())
                if (entry is YMap em)
                    foreach (var kv in em.Entries)
                        if (YScalar.TryLong(kv.Value?.Scalar, out long v)) mat.SetInt(kv.Key, (int)v);
            foreach (var entry in props?["m_Colors"]?.Items ?? Array.Empty<YNode>())
                if (entry is YMap em)
                    foreach (var kv in em.Entries)
                        if (kv.Value is YMap c)
                            mat.SetColor(kv.Key, new Color(c.Float("r"), c.Float("g"), c.Float("b"), c.Float("a", 1f)));
            return mat;
        }

        // ── ITmpFontResolver extras ──────────────────────────────────────────────

        internal static string F(float v) => v.ToString(CultureInfo.InvariantCulture);
    }
}
