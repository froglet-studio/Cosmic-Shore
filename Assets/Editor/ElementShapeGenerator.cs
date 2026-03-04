using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Generates element shape sprites for the Element Pips HUD.
    /// Run via menu: CosmicShore > Generate Element Shape Sprites
    ///
    /// Tuned for small-scale display (pipSize 14x5, labelIconSize 20x20).
    /// Uses 4x oversampling — enough for clean AA without downscale artifacts.
    ///
    /// Produces:
    ///   - 1 uniform tick sprite (shared by all pip indicators)
    ///   - 4 label outlines (one per element shape)
    /// </summary>
    public static class ElementShapeGenerator
    {
        // 4x oversample of display sizes for clean bilinear downsampling.
        const int LabelSize = 80;       // 4x of labelIconSize 20x20
        const int TickWidth = 56;       // 4x of pipSize 14x5
        const int TickHeight = 20;
        const string OutputFolder = "Assets/_Graphics/ElementShapes";

        struct ShapeDef
        {
            public string name;
            public Element element;
            public Vector2[] vertices;
        }

        [MenuItem("CosmicShore/Generate Element Shape Sprites")]
        static void Generate()
        {
            // Clean previous output to remove stale sprites
            if (Directory.Exists(OutputFolder))
            {
                foreach (var file in Directory.GetFiles(OutputFolder, "*.png"))
                    File.Delete(file);
            }
            else
            {
                Directory.CreateDirectory(OutputFolder);
            }

            var shapes = new[]
            {
                MakeTriangle(),
                MakeKite(),
                MakePentagon(),
                MakeDiamond(),
            };

            var pipPaths = new Dictionary<Element, string>();
            var labelPaths = new Dictionary<Element, string>();

            // --- Uniform tick (single sprite, all elements share it) ---
            var tickTex = CreateTick(TickWidth, TickHeight);
            string tickPath = $"{OutputFolder}/pip_tick.png";
            SaveTexture(tickTex, tickPath);

            foreach (var shape in shapes)
            {
                pipPaths[shape.element] = tickPath;

                // --- Per-element label outline ---
                var labelTex = CreateLabel(shape.vertices, LabelSize);
                string labelPath = $"{OutputFolder}/{shape.name}_label.png";
                SaveTexture(labelTex, labelPath);
                labelPaths[shape.element] = labelPath;
            }

            AssetDatabase.Refresh();

            ConfigureSpriteImport(tickPath);
            foreach (var path in labelPaths.Values)
                ConfigureSpriteImport(path);

            TryAssignToConfig(pipPaths, labelPaths);

            Debug.Log($"[ElementShapeGenerator] Generated {1 + shapes.Length} sprites in {OutputFolder}");
        }

        // =====================================================================
        // Shape Definitions (geometry unchanged from design spec)
        // =====================================================================

        static ShapeDef MakeTriangle() => new()
        {
            name = "mass_triangle",
            element = Element.Mass,
            vertices = new[]
            {
                new Vector2(0.50f, 0.92f),
                new Vector2(0.92f, 0.08f),
                new Vector2(0.08f, 0.08f),
            },
        };

        static ShapeDef MakeKite() => new()
        {
            name = "space_kite",
            element = Element.Space,
            vertices = new[]
            {
                new Vector2(0.50f, 0.92f),
                new Vector2(0.92f, 0.58f),
                new Vector2(0.50f, 0.08f),
                new Vector2(0.08f, 0.58f),
            },
        };

        static ShapeDef MakePentagon() => new()
        {
            name = "charge_pentagon",
            element = Element.Charge,
            vertices = new[]
            {
                new Vector2(0.30f, 0.92f),
                new Vector2(0.70f, 0.92f),
                new Vector2(0.92f, 0.55f),
                new Vector2(0.50f, 0.08f),
                new Vector2(0.08f, 0.55f),
            },
        };

        static ShapeDef MakeDiamond() => new()
        {
            name = "time_diamond",
            element = Element.Time,
            vertices = new[]
            {
                new Vector2(0.50f, 0.92f),
                new Vector2(0.80f, 0.50f),
                new Vector2(0.50f, 0.08f),
                new Vector2(0.20f, 0.50f),
            },
        };

        // =====================================================================
        // Tick Pip — solid filled pill, aspect-matched to pipSize (14x5)
        // =====================================================================

        static Texture2D CreateTick(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            // Pill shape in pixel-space (centered at origin)
            float padPx = 3f;
            float halfW = width * 0.5f - padPx;
            float halfH = height * 0.5f - padPx;
            float radius = halfH; // full pill — completely rounded short ends

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f - height * 0.5f);
                    float dist = SdfRoundedRectPx(p, halfW, halfH, radius);

                    // Solid fill, 1.2px AA feather
                    float alpha = Mathf.Clamp01(-dist / 1.2f);

                    // Subtle top-lit gradient (bottom 92% → top 100%)
                    float vt = (y + 0.5f) / height;
                    alpha *= Mathf.Lerp(0.92f, 1f, vt);

                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// SDF for a rounded rectangle in pixel-space, centered at origin.
        /// </summary>
        static float SdfRoundedRectPx(Vector2 p, float halfW, float halfH, float radius)
        {
            var d = new Vector2(
                Mathf.Abs(p.x) - halfW + radius,
                Mathf.Abs(p.y) - halfH + radius);
            float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
            return outside + inside - radius;
        }

        // =====================================================================
        // Label Outline — thick stroke + subtle inner fill, sized for 20x20
        // =====================================================================

        static Texture2D CreateLabel(Vector2[] verts, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            // 8px total stroke at 80px texture → 2px at 20px display (clean and readable)
            float strokeHalf = 4f / size;
            float feather = 1.2f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float dist = SignedDistanceToPolygon(uv, verts);
                    float absDist = Mathf.Abs(dist);

                    // Thick anti-aliased stroke
                    float stroke = Mathf.Clamp01((strokeHalf - absDist) / feather);

                    // Subtle inner fill (6% alpha) — gives the shape body
                    float fill = dist < 0f ? 0.06f : 0f;

                    float alpha = Mathf.Max(stroke, fill);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Signed distance from point p to the boundary of a convex polygon.
        /// Negative = inside, positive = outside.
        /// </summary>
        static float SignedDistanceToPolygon(Vector2 p, Vector2[] verts)
        {
            int n = verts.Length;
            float minDist = float.MaxValue;
            float sign = 1f;

            for (int i = 0; i < n; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % n];
                var edge = b - a;
                var toP = p - a;

                float t = Mathf.Clamp01(Vector2.Dot(toP, edge) / edge.sqrMagnitude);
                float dist = (toP - edge * t).magnitude;
                minDist = Mathf.Min(minDist, dist);

                float cross = edge.x * toP.y - edge.y * toP.x;
                if (a.y <= p.y && b.y > p.y && cross > 0f) sign = -sign;
                else if (a.y > p.y && b.y <= p.y && cross < 0f) sign = -sign;
            }

            return sign * minDist;
        }

        // =====================================================================
        // File I/O & Import
        // =====================================================================

        static void SaveTexture(Texture2D tex, string path)
        {
            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);
            Object.DestroyImmediate(tex);
        }

        static void ConfigureSpriteImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100;
            importer.SaveAndReimport();
        }

        // =====================================================================
        // Config Assignment
        // =====================================================================

        static void TryAssignToConfig(
            Dictionary<Element, string> pipPaths,
            Dictionary<Element, string> labelPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:ElementPipsConfigSO");
            if (guids.Length == 0)
            {
                Debug.Log("[ElementShapeGenerator] No ElementPipsConfigSO found — assign sprites manually.");
                return;
            }

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<ElementPipsConfigSO>(assetPath);
                if (!config) continue;

                var so = new SerializedObject(config);

                AssignSprite(so, "chargeSprite",      pipPaths,   Element.Charge);
                AssignSprite(so, "massSprite",         pipPaths,   Element.Mass);
                AssignSprite(so, "spaceSprite",        pipPaths,   Element.Space);
                AssignSprite(so, "timeSprite",         pipPaths,   Element.Time);
                AssignSprite(so, "chargeLabelSprite",  labelPaths, Element.Charge);
                AssignSprite(so, "massLabelSprite",    labelPaths, Element.Mass);
                AssignSprite(so, "spaceLabelSprite",   labelPaths, Element.Space);
                AssignSprite(so, "timeLabelSprite",    labelPaths, Element.Time);

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);

                Debug.Log($"[ElementShapeGenerator] Assigned sprites to {assetPath}");
            }

            AssetDatabase.SaveAssets();
        }

        static void AssignSprite(SerializedObject so, string fieldName,
            Dictionary<Element, string> paths, Element element)
        {
            if (!paths.TryGetValue(element, out string path)) return;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) return;

            var prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.objectReferenceValue = sprite;
        }
    }
}
