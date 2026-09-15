using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Bakes one genome to a portrait PNG in its OWN render context — a
    /// <see cref="PreviewRenderUtility"/> scene with key, fill and rim lights, a perspective
    /// camera framed on the head, and the bust's URP Lit materials — and caches it under
    /// <c>Library/CharacterPortraits</c> keyed on the genome's content hash. Nothing here
    /// touches the gameplay renderer, its lights or its prism materials.
    ///
    /// Alpha is recovered the way <c>CodexImageBaker</c> does it: two opaque renders, on black
    /// and on white, and <c>a = 1 - (white - black)</c> per pixel — a render target's alpha is
    /// pipeline-dependent, two subtractions are not.
    ///
    /// READER by the tooling contract: it writes only under <c>Library/</c> (gitignored).
    /// </summary>
    public static class CharacterPortraitBaker
    {
        /// <summary>Bump when the generator changes visibly, so stale cache files are never mistaken for the new look.</summary>
        public const string BakeVersion = "v6";
        public const string CacheFolder = "Library/CharacterPortraits";
        public const float FieldOfView = 26f;
        public const float CameraYawDeg = 22f;
        public const float CameraPitchDeg = 4f;
        public const float CameraDistance = 4.7f;
        static readonly Vector3 CameraTarget = new Vector3(0f, -0.26f, 0f);

        public struct BakeResult
        {
            public Texture2D Texture;
            public string CachePath;
            public bool FromCache;
            public string Error;
            public int VertexCount;
        }

        public static string CachePathFor(CharacterGenome genome, int size) =>
            Path.Combine(CacheFolder, $"{genome.ContentHash()}_{size}_{BakeVersion}.png");

        public static bool TryLoadCached(CharacterGenome genome, int size, out Texture2D texture)
        {
            texture = null;
            var path = CachePathFor(genome, size);
            if (!File.Exists(path)) return false;
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, name = "Portrait" };
            if (texture.LoadImage(File.ReadAllBytes(path))) return true;
            Object.DestroyImmediate(texture);
            texture = null;
            return false;
        }

        public static void ClearCache()
        {
            if (Directory.Exists(CacheFolder)) Directory.Delete(CacheFolder, true);
        }

        /// <summary>Bake (or fetch) the portrait. The returned texture is owned by the caller.</summary>
        public static BakeResult Bake(CharacterGenome genome, CladeCatalog catalog, CharacterGenerationConfigSO config,
                                      SO_ColorSet colorSet, int size, bool useCache = true)
        {
            var result = new BakeResult { CachePath = CachePathFor(genome, size) };
            if (useCache && TryLoadCached(genome, size, out var cached))
            {
                result.Texture = cached;
                result.FromCache = true;
                return result;
            }

            CharacterBustBuilder.BuiltBust bust = null;
            PreviewRenderUtility preview = null;
            try
            {
                var blueprint = CharacterResolver.Resolve(genome, catalog, config);
                var head = config.CreateBaseHead(config.PortraitDetail);
                var model = CharacterAssembler.Assemble(blueprint, head);
                model.Textures = CharacterTexturePainter.Paint(model, config, colorSet);
                bust = CharacterBustBuilder.Build(model, config);
                result.VertexCount = bust.VertexCount;

                preview = new PreviewRenderUtility();
                preview.camera.cameraType = CameraType.Preview;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.fieldOfView = FieldOfView;
                preview.camera.orthographic = false;
                preview.camera.nearClipPlane = 0.05f;
                preview.camera.farClipPlane = 20f;

                // Key: warm, upper-left, from the camera side. Fill: cool, right. Rim: behind.
                preview.lights[0].intensity = 1.5f;
                preview.lights[0].color = new Color(1.0f, 0.96f, 0.90f);
                preview.lights[0].transform.rotation = Quaternion.LookRotation(-new Vector3(-0.55f, 0.65f, 0.55f).normalized);
                preview.lights[1].intensity = 0.6f;
                preview.lights[1].color = new Color(0.70f, 0.78f, 1.0f);
                preview.lights[1].transform.rotation = Quaternion.LookRotation(-new Vector3(0.85f, 0.10f, 0.35f).normalized);
                preview.ambientColor = new Color(0.20f, 0.21f, 0.26f, 1f);

                var rimGo = new GameObject("RimLight") { hideFlags = HideFlags.HideAndDontSave };
                var rim = rimGo.AddComponent<Light>();
                rim.type = LightType.Directional;
                rim.intensity = 2.2f;
                rim.color = new Color(0.9f, 0.95f, 1.0f);
                rimGo.transform.rotation = Quaternion.LookRotation(-new Vector3(0.4f, 0.8f, -0.5f).normalized);
                preview.AddSingleGO(rimGo);
                preview.AddSingleGO(bust.Root);

                FrameCamera(preview.camera);
                var onBlack = Capture(preview, size, Color.black);
                var onWhite = Capture(preview, size, Color.white);
                if (onBlack == null || onWhite == null)
                {
                    if (onBlack) Object.DestroyImmediate(onBlack);
                    if (onWhite) Object.DestroyImmediate(onWhite);
                    result.Error = "The preview renderer returned no image.";
                    return result;
                }
                result.Texture = RecoverAlpha(onBlack, onWhite);
                if (config.PaintPortraits)
                {
                    var painted = Stylize(result.Texture, config.PortraitStyle,
                        CharacterPaletteBinding.Resolve(genome.Domain, colorSet).Accent, genome.Seed);
                    Object.DestroyImmediate(result.Texture);
                    result.Texture = painted;
                }
                result.Texture.name = "Portrait";
                Object.DestroyImmediate(onBlack);
                Object.DestroyImmediate(onWhite);

                Directory.CreateDirectory(CacheFolder);
                File.WriteAllBytes(result.CachePath, result.Texture.EncodeToPNG());
                return result;
            }
            catch (Exception e)
            {
                result.Error = e.Message;
                if (result.Texture) Object.DestroyImmediate(result.Texture);
                result.Texture = null;
                return result;
            }
            finally
            {
                bust?.Dispose();
                preview?.Cleanup();
            }
        }

        static void FrameCamera(Camera camera)
        {
            float yaw = CameraYawDeg * Mathf.Deg2Rad, pitch = CameraPitchDeg * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * CameraDistance;
            camera.transform.position = CameraTarget + offset;
            camera.transform.LookAt(CameraTarget, Vector3.up);
        }

        static Texture2D Capture(PreviewRenderUtility preview, int size, Color background)
        {
            preview.camera.backgroundColor = background;
            preview.BeginPreview(new Rect(0f, 0f, size, size), GUIStyle.none);
            preview.Render(true, false);
            var rendered = preview.EndPreview() as RenderTexture;
            if (rendered == null) return null;
            var previous = RenderTexture.active;
            RenderTexture.active = rendered;
            var texture = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, rendered.width, rendered.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        /// <summary>
        /// The painterly pass. The preview render is sRGB-encoded; the stylizer works in linear
        /// and hands back sRGB with the frame composited in, so the cached PNG is the finished
        /// avatar. Same code the offline harness runs.
        /// </summary>
        static Texture2D Stylize(Texture2D lit, in PortraitStyle style, Color accent, int seed)
        {
            int w = lit.width, h = lit.height;
            var px = lit.GetPixels();
            var rgba = new float[w * h * 4];
            for (int i = 0; i < px.Length; i++)
            {
                rgba[i * 4] = PortraitStylizer.FromSrgb(px[i].r);
                rgba[i * 4 + 1] = PortraitStylizer.FromSrgb(px[i].g);
                rgba[i * 4 + 2] = PortraitStylizer.FromSrgb(px[i].b);
                rgba[i * 4 + 3] = px[i].a;
            }
            var outp = PortraitStylizer.Apply(rgba, w, h, style, accent, seed);
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var colors = new Color[px.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(outp[i * 4], outp[i * 4 + 1], outp[i * 4 + 2], outp[i * 4 + 3]);
            result.SetPixels(colors);
            result.Apply(false, false);
            return result;
        }

        static Texture2D RecoverAlpha(Texture2D onBlack, Texture2D onWhite)
        {
            var black = onBlack.GetPixels();
            var white = onWhite.GetPixels();
            var output = new Color[black.Length];
            for (int i = 0; i < black.Length; i++)
            {
                var b = black[i];
                var w = white[i];
                float background = ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f;
                float alpha = Mathf.Clamp01(1f - background);
                output[i] = alpha <= 0.004f
                    ? Color.clear
                    : new Color(Mathf.Clamp01(b.r / alpha), Mathf.Clamp01(b.g / alpha), Mathf.Clamp01(b.b / alpha), alpha);
            }
            var result = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            result.SetPixels(output);
            result.Apply();
            return result;
        }

        /// <summary>Compose baked portraits into one sheet PNG (labels are written to a sidecar .txt, IMGUI draws them live).</summary>
        public static bool ExportSheet(IReadOnlyList<Texture2D> portraits, IReadOnlyList<string> labels, int columns, string path)
        {
            if (portraits == null || portraits.Count == 0 || string.IsNullOrEmpty(path)) return false;
            int cell = portraits[0].width;
            int rows = (portraits.Count + columns - 1) / columns;
            var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGBA32, false);
            var fill = new Color[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.08f, 0.08f, 0.09f, 1f);
            sheet.SetPixels(fill);
            for (int i = 0; i < portraits.Count; i++)
            {
                var p = portraits[i];
                if (!p || p.width != cell || p.height != cell) continue;
                int cx = (i % columns) * cell, cy = (rows - 1 - i / columns) * cell;
                var px = p.GetPixels();
                for (int k = 0; k < px.Length; k++)
                {
                    var c = px[k];
                    px[k] = new Color(Mathf.Lerp(0.08f, c.r, c.a), Mathf.Lerp(0.08f, c.g, c.a), Mathf.Lerp(0.09f, c.b, c.a), 1f);
                }
                sheet.SetPixels(cx, cy, cell, cell, px);
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            var sidecar = new System.Text.StringBuilder();
            for (int i = 0; i < labels.Count; i++) sidecar.Append(i).Append(": ").Append(labels[i]).Append('\n');
            File.WriteAllText(Path.ChangeExtension(path, ".txt"), sidecar.ToString());
            return true;
        }
    }
}
