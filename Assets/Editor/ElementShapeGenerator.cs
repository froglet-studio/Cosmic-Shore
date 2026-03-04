using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Generates premium UI sprites for the Element Pips HUD system.
    /// Run via menu: CosmicShore > Generate Element Shape Sprites
    ///
    /// Produces:
    ///   - 1 uniform tick sprite (used for all element pip indicators)
    ///   - 4 premium label outlines (one per element shape)
    ///
    /// Shapes (geometry from design spec — unchanged):
    ///   Mass   = Equilateral triangle, point up
    ///   Space  = Kite (4-sided, sharp top, wide shoulders, bottom point)
    ///   Charge = Irregular pentagon (flat top, wide middle, bottom point)
    ///   Time   = Tall diamond/rhombus (~1.4:1 aspect)
    /// </summary>
    public static class ElementShapeGenerator
    {
        const int LabelSize = 256;
        const int TickSize = 128;
        const string OutputFolder = "Assets/_Graphics/ElementShapes";

        // --- Premium Rendering Parameters ---

        // Label outline stroke
        const float LabelStrokePx = 5f;
        const float FeatherPx = 1.5f;

        // Label outer glow (soft halo outside the stroke)
        const float LabelOuterGlowPx = 14f;
        const float LabelOuterGlowIntensity = 0.28f;

        // Label inner edge glow (luminous falloff just inside the stroke)
        const float LabelInnerEdgePx = 10f;
        const float LabelInnerEdgeIntensity = 0.12f;

        // Label subtle inner fill
        const float LabelInnerFillAlpha = 0.04f;

        // Tick outer glow
        const float TickGlowPx = 10f;
        const float TickGlowIntensity = 0.32f;

        struct ShapeDef
        {
            public string name;
            public Element element;
            public Vector2[] vertices; // normalized 0..1 coords, center at 0.5,0.5
        }

        [MenuItem("CosmicShore/Generate Element Shape Sprites")]
        static void Generate()
        {
            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            var shapes = new[]
            {
                MakeTriangle(),
                MakeKite(),
                MakePentagon(),
                MakeDiamond(),
            };

            var pipPaths = new Dictionary<Element, string>();
            var labelPaths = new Dictionary<Element, string>();

            // --- Generate premium tick (single sprite, all elements share it) ---
            var tickTex = CreatePremiumTick(TickSize);
            string tickPath = $"{OutputFolder}/pip_tick.png";
            SaveTexture(tickTex, tickPath);

            foreach (var shape in shapes)
            {
                pipPaths[shape.element] = tickPath;

                // --- Generate premium label outline (unique shape per element) ---
                var labelTex = CreatePremiumLabel(shape.vertices, LabelSize);
                string labelPath = $"{OutputFolder}/{shape.name}_label.png";
                SaveTexture(labelTex, labelPath);
                labelPaths[shape.element] = labelPath;
            }

            AssetDatabase.Refresh();

            // Configure sprite imports
            ConfigureSpriteImport(tickPath);
            foreach (var path in labelPaths.Values)
                ConfigureSpriteImport(path);

            // Auto-assign to config SO
            TryAssignToConfig(pipPaths, labelPaths);

            Debug.Log($"[ElementShapeGenerator] Generated {1 + shapes.Length} premium sprites in {OutputFolder}");
        }

        // --- Shape Definitions (geometry unchanged from design spec) ---

        static ShapeDef MakeTriangle() => new()
        {
            name = "mass_triangle",
            element = Element.Mass,
            vertices = new[]
            {
                new Vector2(0.50f, 0.92f), // top center
                new Vector2(0.92f, 0.08f), // bottom right
                new Vector2(0.08f, 0.08f), // bottom left
            },
        };

        static ShapeDef MakeKite() => new()
        {
            name = "space_kite",
            element = Element.Space,
            vertices = new[]
            {
                new Vector2(0.50f, 0.92f), // top point
                new Vector2(0.92f, 0.58f), // right shoulder
                new Vector2(0.50f, 0.08f), // bottom point
                new Vector2(0.08f, 0.58f), // left shoulder
            },
        };

        static ShapeDef MakePentagon() => new()
        {
            name = "charge_pentagon",
            element = Element.Charge,
            vertices = new[]
            {
                new Vector2(0.30f, 0.92f), // top left
                new Vector2(0.70f, 0.92f), // top right
                new Vector2(0.92f, 0.55f), // right middle
                new Vector2(0.50f, 0.08f), // bottom point
                new Vector2(0.08f, 0.55f), // left middle
            },
        };

        static ShapeDef MakeDiamond() => new()
        {
            name = "time_diamond",
            element = Element.Time,
            vertices = new[]
            {
                new Vector2(0.50f, 0.92f), // top
                new Vector2(0.80f, 0.50f), // right
                new Vector2(0.50f, 0.08f), // bottom
                new Vector2(0.20f, 0.50f), // left
            },
        };

        // --- Premium Tick Pip ---

        static Texture2D CreatePremiumTick(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            // Rounded rectangle (pill shape): wide, short
            var halfExtent = new Vector2(0.34f, 0.09f);
            float cornerRadius = 0.07f;

            float feather = FeatherPx / size;
            float glowR = TickGlowPx / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Center origin
                    var p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    float dist = SdfRoundedRect(p, halfExtent, cornerRadius);

                    // Core fill — solid white, anti-aliased
                    float core = Mathf.Clamp01(-dist / feather);

                    // Outer glow — soft quadratic falloff halo
                    float glow = 0f;
                    if (dist > 0f)
                    {
                        glow = Mathf.Clamp01(1f - dist / glowR);
                        glow *= glow * TickGlowIntensity;
                    }

                    float alpha = Mathf.Max(core, glow);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// SDF for an axis-aligned rounded rectangle centered at origin.
        /// </summary>
        static float SdfRoundedRect(Vector2 p, Vector2 halfExtent, float radius)
        {
            var d = new Vector2(
                Mathf.Abs(p.x) - halfExtent.x + radius,
                Mathf.Abs(p.y) - halfExtent.y + radius);
            float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
            return outside + inside - radius;
        }

        // --- Premium Label Outline ---

        static Texture2D CreatePremiumLabel(Vector2[] verts, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            float strokeHalf = LabelStrokePx * 0.5f / size;
            float feather = FeatherPx / size;
            float outerGlowR = LabelOuterGlowPx / size;
            float innerEdgeR = LabelInnerEdgePx / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float dist = SignedDistanceToPolygon(uv, verts);
                    float absDist = Mathf.Abs(dist);

                    // 1. Crisp anti-aliased stroke
                    float stroke = Mathf.Clamp01((strokeHalf - absDist) / feather);

                    // 2. Outer glow — soft halo bleeding outward from the stroke
                    float outerGlow = 0f;
                    if (dist > strokeHalf)
                    {
                        float d = dist - strokeHalf;
                        outerGlow = Mathf.Clamp01(1f - d / outerGlowR);
                        outerGlow *= outerGlow * LabelOuterGlowIntensity; // quadratic falloff
                    }

                    // 3. Inner edge glow — luminous rim just inside the stroke
                    float innerEdge = 0f;
                    if (dist < -strokeHalf)
                    {
                        float d = -dist - strokeHalf;
                        innerEdge = Mathf.Clamp01(1f - d / innerEdgeR);
                        innerEdge *= innerEdge * LabelInnerEdgeIntensity;
                    }

                    // 4. Subtle inner fill — gives the shape body
                    float fill = dist < 0f ? LabelInnerFillAlpha : 0f;

                    float alpha = Mathf.Max(
                        Mathf.Max(stroke, outerGlow),
                        Mathf.Max(innerEdge, fill));

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

                // Closest point on edge segment
                float t = Mathf.Clamp01(Vector2.Dot(toP, edge) / edge.sqrMagnitude);
                float dist = (toP - edge * t).magnitude;
                minDist = Mathf.Min(minDist, dist);

                // Winding test (cross product)
                float cross = edge.x * toP.y - edge.y * toP.x;
                if (a.y <= p.y && b.y > p.y && cross > 0f) sign = -sign;
                else if (a.y > p.y && b.y <= p.y && cross < 0f) sign = -sign;
            }

            return sign * minDist;
        }

        // --- File I/O ---

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

        // --- Config Assignment ---

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

                Debug.Log($"[ElementShapeGenerator] Assigned premium sprites to {assetPath}");
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
