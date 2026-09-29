using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;
using StbImageSharp;
using EngineObject = CosmicShore.Engine.Object;

namespace CosmicShore.Content.Textures
{
    /// <summary>
    /// Imports the project's image files the way Unity's TextureImporter does for the
    /// settings this project uses: decode (PNG/JPG/TGA/PSD/BMP/GIF), clamp to the
    /// effective <c>maxTextureSize</c> (Standalone override when set, else Default) by
    /// Unity's aspect-preserving downscale, and expose sprites — single (fileID
    /// 21300000) or sheet entries by <c>internalID</c> — with rect, pivot, border and
    /// pixels-per-unit scaled consistently with the texture. Also synthesizes Unity's
    /// built-in UI sprites (UISprite, Background, Knob…) referenced via the built-in guid.
    /// </summary>
    public sealed class TextureImporter
    {
        public const long SingleSpriteFileId = 21300000;
        public const long TextureFileId = 2800000;

        readonly AssetDatabase _db;
        readonly ConcurrentDictionary<string, Imported> _textures = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<(string, long), Sprite> _sprites = new();

        /// <summary>Upper bound on decoded size regardless of import settings (memory guard).</summary>
        public int GlobalMaxSize = 4096;

        public TextureImporter(AssetDatabase db) { _db = db; }

        sealed class Imported
        {
            public Texture2D Texture;
            public float Scale; // imported size / source size
            public YMap Settings;
        }

        public static void Register(AssetLoader loader, TextureImporter importer)
        {
            loader.Importers[typeof(Sprite)] = r => importer.LoadSprite(r);
            loader.Importers[typeof(Texture2D)] = r => importer.LoadTexture(r);
        }

        static bool IsImage(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".tga" or ".psd" or ".bmp" or ".gif";
        }

        public Texture2D LoadTexture(ObjRef r)
        {
            if (r.Guid == AssetLoader.BuiltinExtraGuid || r.Guid == AssetLoader.BuiltinDefaultGuid)
                return BuiltinSprites.Get(r.FileId)?.texture;
            return Import(r.Guid)?.Texture;
        }

        Imported Import(string guid)
        {
            var path = _db.PathOf(guid);
            if (path == null || !IsImage(path) || !File.Exists(path)) return null;
            return _textures.GetOrAdd(guid, g =>
            {
                var meta = _db.Meta(g);
                var ti = meta?["TextureImporter"] as YMap ?? new YMap();
                ImageResult img;
                try
                {
                    using var stream = File.OpenRead(path);
                    img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Content] could not decode {path}: {e.Message}");
                    return null;
                }

                int maxSize = Math.Min(GlobalMaxSize, EffectiveMaxSize(ti));
                int w = img.Width, h = img.Height;
                byte[] data = img.Data; // top-down RGBA
                float scale = 1f;
                if (w > maxSize || h > maxSize)
                {
                    scale = (float)maxSize / Math.Max(w, h);
                    int nw = Math.Max(1, (int)Math.Round(w * scale));
                    int nh = Math.Max(1, (int)Math.Round(h * scale));
                    data = Resample(data, w, h, nw, nh);
                    scale = (float)nw / w;
                    w = nw; h = nh;
                }

                FlipRowsInPlace(data, w, h); // engine convention: rows bottom-up
                bool mips = (ti["mipmaps"]?.Int("enableMipMap") ?? 0) == 1;
                var tex = new Texture2D(w, h, data, mips)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    filterMode = (FilterMode)(ti["textureSettings"]?.Int("filterMode", 1) ?? 1),
                    wrapModeU = (TextureWrapMode)(ti["textureSettings"]?.Int("wrapU", 0) ?? 0),
                    wrapModeV = (TextureWrapMode)(ti["textureSettings"]?.Int("wrapV", 0) ?? 0),
                    isDataSRGB = (ti["mipmaps"]?.Int("sRGBTexture", 1) ?? 1) == 1,
                };
                if (ti["textureSettings"]?.Int("filterMode", 1) == -1) tex.filterMode = FilterMode.Bilinear;
                return new Imported { Texture = tex, Scale = scale, Settings = ti };
            });
        }

        static int EffectiveMaxSize(YMap ti)
        {
            int size = ti.Int("maxTextureSize", 2048);
            foreach (var p in ti["platformSettings"]?.Items ?? Array.Empty<YNode>())
            {
                string target = p.Str("buildTarget");
                if (target == "DefaultTexturePlatform") size = p.Int("maxTextureSize", size);
            }
            foreach (var p in ti["platformSettings"]?.Items ?? Array.Empty<YNode>())
                if (p.Str("buildTarget") == "Standalone" && p.Int("overridden") == 1)
                    size = p.Int("maxTextureSize", size);
            return size <= 0 ? 2048 : size;
        }

        public Sprite LoadSprite(ObjRef r)
        {
            if (r.Guid == AssetLoader.BuiltinExtraGuid || r.Guid == AssetLoader.BuiltinDefaultGuid)
                return BuiltinSprites.Get(r.FileId);
            return _sprites.GetOrAdd((r.Guid, r.FileId), key => BuildSprite(key.Item1, key.Item2));
        }

        Sprite BuildSprite(string guid, long fileId)
        {
            var imp = Import(guid);
            if (imp == null) return null;
            var ti = imp.Settings;
            float ppu = ti.Float("spritePixelsToUnits", 100f);
            float s = imp.Scale;
            var tex = imp.Texture;
            float srcW = tex.width / s, srcH = tex.height / s;

            Rect rect;
            Vector2 pivot;
            Vector4 border;
            int mode = ti.Int("spriteMode", 1);
            if (fileId == SingleSpriteFileId || mode != 2)
            {
                rect = new Rect(0, 0, srcW, srcH);
                pivot = PivotFor(ti.Int("alignment"), ti["spritePivot"]);
                border = V4(ti["spriteBorder"]);
            }
            else
            {
                YNode found = null;
                foreach (var sp in ti["spriteSheet"]?["sprites"]?.Items ?? Array.Empty<YNode>())
                    if (sp.Long("internalID") == fileId) { found = sp; break; }
                if (found == null)
                {
                    // Legacy metas: fileIDToRecycleName / internalIDToNameTable map the id to a sprite name.
                    string name = NameForId(ti, _db.Meta(guid), fileId);
                    if (name != null)
                        foreach (var sp in ti["spriteSheet"]?["sprites"]?.Items ?? Array.Empty<YNode>())
                            if (sp.Str("name") == name) { found = sp; break; }
                }
                if (found == null) return null;
                var rr = found["rect"];
                rect = new Rect(rr.Float("x"), rr.Float("y"), rr.Float("width"), rr.Float("height"));
                pivot = PivotFor(found.Int("alignment"), found["pivot"]);
                border = V4(found["border"]);
            }

            var sprite = Sprite.Create(tex,
                new Rect(rect.x * s, rect.y * s, rect.width * s, rect.height * s),
                pivot, ppu * s, 0, border * s);
            sprite.name = tex.name;
            return sprite;
        }

        static string NameForId(YMap ti, YMap meta, long fileId)
        {
            foreach (var e in meta?["TextureImporter"]?["internalIDToNameTable"]?.Items ?? Array.Empty<YNode>())
            {
                var first = e["first"] as YMap;
                if (first != null && first.Entries.Count == 1 && YScalar.TryLong(first.Entries[0].Value.Scalar, out var id) && id == fileId)
                    return e.Str("second");
            }
            foreach (var e in meta?["TextureImporter"]?["fileIDToRecycleName"] is YMap m ? m.Entries : new List<KeyValuePair<string, YNode>>())
                if (YScalar.TryLong(e.Key, out var id) && id == fileId) return e.Value.Scalar;
            return null;
        }

        static Vector4 V4(YNode n) => n == null ? Vector4.zero : new Vector4(n.Float("x"), n.Float("y"), n.Float("z"), n.Float("w"));

        // Unity SpriteAlignment: 0 Center, 1 TopLeft, 2 TopCenter, 3 TopRight, 4 LeftCenter,
        // 5 RightCenter, 6 BottomLeft, 7 BottomCenter, 8 BottomRight, 9 Custom.
        static Vector2 PivotFor(int alignment, YNode custom) => alignment switch
        {
            1 => new Vector2(0f, 1f), 2 => new Vector2(0.5f, 1f), 3 => new Vector2(1f, 1f),
            4 => new Vector2(0f, 0.5f), 5 => new Vector2(1f, 0.5f),
            6 => new Vector2(0f, 0f), 7 => new Vector2(0.5f, 0f), 8 => new Vector2(1f, 0f),
            9 => custom != null ? new Vector2(custom.Float("x", 0.5f), custom.Float("y", 0.5f)) : new Vector2(0.5f, 0.5f),
            _ => new Vector2(0.5f, 0.5f),
        };

        internal static void FlipRowsInPlace(byte[] data, int w, int h)
        {
            int stride = w * 4;
            var tmp = new byte[stride];
            for (int y = 0; y < h / 2; y++)
            {
                int a = y * stride, b = (h - 1 - y) * stride;
                Buffer.BlockCopy(data, a, tmp, 0, stride);
                Buffer.BlockCopy(data, b, data, a, stride);
                Buffer.BlockCopy(tmp, 0, data, b, stride);
            }
        }

        /// <summary>Box-filtered downscale in linear-ish space (area average) — the quality Unity's import resize gives.</summary>
        internal static byte[] Resample(byte[] src, int sw, int sh, int dw, int dh)
        {
            var dst = new byte[dw * dh * 4];
            float sx = (float)sw / dw, sy = (float)sh / dh;
            for (int y = 0; y < dh; y++)
            {
                int y0 = (int)(y * sy), y1 = Math.Max(y0 + 1, (int)((y + 1) * sy));
                for (int x = 0; x < dw; x++)
                {
                    int x0 = (int)(x * sx), x1 = Math.Max(x0 + 1, (int)((x + 1) * sx));
                    double r = 0, g = 0, b = 0, a = 0; int n = 0;
                    for (int yy = y0; yy < y1 && yy < sh; yy++)
                        for (int xx = x0; xx < x1 && xx < sw; xx++)
                        {
                            int i = (yy * sw + xx) * 4;
                            double al = src[i + 3] / 255.0;
                            // premultiplied accumulation keeps transparent edges from darkening
                            r += src[i] * al; g += src[i + 1] * al; b += src[i + 2] * al; a += al; n++;
                        }
                    int o = (y * dw + x) * 4;
                    if (a > 0)
                    {
                        dst[o] = (byte)Math.Round(r / a); dst[o + 1] = (byte)Math.Round(g / a); dst[o + 2] = (byte)Math.Round(b / a);
                    }
                    dst[o + 3] = (byte)Math.Round(255.0 * a / Math.Max(1, n));
                }
            }
            return dst;
        }
    }

    /// <summary>
    /// Unity's built-in uGUI sprites (unity_builtin_extra), synthesized: white shapes
    /// with antialiased edges, tinted by Image.color exactly like the originals.
    /// </summary>
    public static class BuiltinSprites
    {
        static readonly Dictionary<long, Sprite> Cache = new();

        public static Sprite Get(long fileId)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(fileId, out var s)) return s;
                s = fileId switch
                {
                    10905 => RoundedRect("UISprite", 32, 6f, 10, rimShade: 0.86f),
                    10907 => RoundedRect("Background", 32, 6f, 10, rimShade: 0.75f),
                    10911 => RoundedRect("InputFieldBackground", 32, 5f, 10, rimShade: 0.72f),
                    10913 => Circle("Knob", 16),
                    10915 => RoundedRect("UIMask", 32, 6f, 10, rimShade: 1f),
                    10917 => Arrow("DropdownArrow", 32),
                    10909 => Check("Checkmark", 64),
                    _ => null,
                };
                Cache[fileId] = s;
                return s;
            }
        }

        static Sprite Make(string name, int w, int h, byte[] rgba, float border)
        {
            var tex = new Texture2D(w, h, rgba) { name = name };
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, new Vector4(border, border, border, border));
            sp.name = name;
            return sp;
        }

        static float Coverage(float sdf) => Math.Clamp(0.5f - sdf, 0f, 1f);

        static float RoundedBoxSdf(float px, float py, float hw, float hh, float r)
        {
            float qx = Math.Abs(px) - (hw - r), qy = Math.Abs(py) - (hh - r);
            float ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            return MathF.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - r;
        }

        static Sprite RoundedRect(string name, int size, float radius, float border, float rimShade)
        {
            var d = new byte[size * size * 4];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sdf = RoundedBoxSdf(x + 0.5f - c, y + 0.5f - c, c - 0.5f, c - 0.5f, radius);
                    float a = Coverage(sdf);
                    // a one-pixel darker rim, as the original's bevel reads when tinted
                    float shade = sdf > -1.5f ? rimShade : 1f;
                    int i = (y * size + x) * 4;
                    byte v = (byte)Math.Round(255 * shade);
                    d[i] = v; d[i + 1] = v; d[i + 2] = v; d[i + 3] = (byte)Math.Round(255 * a);
                }
            return Make(name, size, size, d, border);
        }

        static Sprite Circle(string name, int size)
        {
            var d = new byte[size * size * 4];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float a = Coverage(MathF.Sqrt(dx * dx + dy * dy) - (c - 0.5f));
                    int i = (y * size + x) * 4;
                    d[i] = d[i + 1] = d[i + 2] = 255; d[i + 3] = (byte)Math.Round(255 * a);
                }
            return Make(name, size, size, d, 0);
        }

        static Sprite Arrow(string name, int size)
        {
            var d = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // downward triangle, centred
                    float fx = (x + 0.5f) / size - 0.5f, fy = (y + 0.5f) / size - 0.5f;
                    float inside = (fy < 0.12f && fy > -0.18f && Math.Abs(fx) < (fy + 0.18f) * 1.0f) ? 1f : 0f;
                    int i = (y * size + x) * 4;
                    d[i] = d[i + 1] = d[i + 2] = 255; d[i + 3] = (byte)(255 * inside);
                }
            return Make(name, size, size, d, 0);
        }

        static Sprite Check(string name, int size)
        {
            var d = new byte[size * size * 4];
            // two thick strokes of a tick
            (float ax, float ay, float bx, float by)[] segs = { (0.18f, 0.52f, 0.42f, 0.28f), (0.42f, 0.28f, 0.84f, 0.74f) };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size, py = (y + 0.5f) / size, best = 9f;
                    foreach (var (ax, ay, bx, by) in segs)
                    {
                        float vx = bx - ax, vy = by - ay, t = Math.Clamp(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy), 0, 1);
                        float ddx = px - ax - vx * t, ddy = py - ay - vy * t;
                        best = Math.Min(best, MathF.Sqrt(ddx * ddx + ddy * ddy));
                    }
                    float a = Coverage((best - 0.07f) * size);
                    int i = (y * size + x) * 4;
                    d[i] = d[i + 1] = d[i + 2] = 255; d[i + 3] = (byte)Math.Round(255 * a);
                }
            return Make(name, size, size, d, 0);
        }
    }
}
