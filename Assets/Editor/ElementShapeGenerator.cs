using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Generates geometric shape sprites for the Element Pips HUD system.
    /// Run via menu: CosmicShore > Generate Element Shape Sprites
    ///
    /// Shapes (from design spec):
    ///   Mass   = Triangle
    ///   Space  = Pentagon (kite/shield)
    ///   Charge = Hexagon
    ///   Time   = Diamond (rhombus)
    ///
    /// Produces filled (pip) and outline (label) variants for each element,
    /// saves as PNGs, configures sprite import settings, and optionally
    /// assigns them to an ElementPipsConfigSO asset.
    /// </summary>
    public static class ElementShapeGenerator
    {
        const int Size = 64;
        const int OutlineSize = 128;
        const float StrokeWidth = 3f;
        const string OutputFolder = "Assets/_Graphics/ElementShapes";

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
                MakePentagon(),
                MakeHexagon(),
                MakeDiamond(),
            };

            var filledPaths = new Dictionary<Element, string>();
            var outlinePaths = new Dictionary<Element, string>();

            foreach (var shape in shapes)
            {
                // Filled (pip) variant
                var filledTex = CreateFilledTexture(shape.vertices, Size);
                string filledPath = $"{OutputFolder}/{shape.name}_filled.png";
                SaveTexture(filledTex, filledPath);
                filledPaths[shape.element] = filledPath;

                // Outline (label) variant
                var outlineTex = CreateOutlineTexture(shape.vertices, OutlineSize, StrokeWidth);
                string outlinePath = $"{OutputFolder}/{shape.name}_outline.png";
                SaveTexture(outlineTex, outlinePath);
                outlinePaths[shape.element] = outlinePath;
            }

            AssetDatabase.Refresh();

            // Configure sprite import settings
            foreach (var path in filledPaths.Values) ConfigureSpriteImport(path);
            foreach (var path in outlinePaths.Values) ConfigureSpriteImport(path);

            // Try to auto-assign to ElementPipsConfigSO
            TryAssignToConfig(filledPaths, outlinePaths);

            Debug.Log($"[ElementShapeGenerator] Generated {shapes.Length * 2} sprites in {OutputFolder}");
        }

        // --- Shape Definitions ---

        static ShapeDef MakeTriangle() => new()
        {
            name = "mass_triangle",
            element = Element.Mass,
            vertices = RegularPolygon(3, -Mathf.PI / 2f), // point up
        };

        static ShapeDef MakePentagon() => new()
        {
            name = "space_pentagon",
            element = Element.Space,
            vertices = RegularPolygon(5, -Mathf.PI / 2f), // point up
        };

        static ShapeDef MakeHexagon() => new()
        {
            name = "charge_hexagon",
            element = Element.Charge,
            vertices = RegularPolygon(6, 0f), // flat top
        };

        static ShapeDef MakeDiamond() => new()
        {
            name = "time_diamond",
            element = Element.Time,
            vertices = RegularPolygon(4, -Mathf.PI / 2f), // point up (rotated square)
        };

        static Vector2[] RegularPolygon(int sides, float startAngle)
        {
            var verts = new Vector2[sides];
            float step = 2f * Mathf.PI / sides;
            for (int i = 0; i < sides; i++)
            {
                float angle = startAngle + i * step;
                // Map from [-1,1] to [0,1] with padding
                float padding = 0.08f;
                float radius = 0.5f - padding;
                verts[i] = new Vector2(
                    0.5f + radius * Mathf.Cos(angle),
                    0.5f + radius * Mathf.Sin(angle));
            }
            return verts;
        }

        // --- Texture Generation ---

        static Texture2D CreateFilledTexture(Vector2[] verts, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float dist = SignedDistanceToPolygon(uv, verts);

                    // Anti-aliased edge: ~1 pixel feather
                    float feather = 1.5f / size;
                    float alpha = Mathf.Clamp01(-dist / feather);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D CreateOutlineTexture(Vector2[] verts, int size, float strokeWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            float strokeHalf = strokeWidth / size;
            float feather = 1.5f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float dist = SignedDistanceToPolygon(uv, verts);

                    // Outline = ring around boundary
                    float absDist = Mathf.Abs(dist);
                    float alpha = Mathf.Clamp01((strokeHalf - absDist) / feather);

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

            // Negative inside, positive outside
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
            Dictionary<Element, string> filledPaths,
            Dictionary<Element, string> outlinePaths)
        {
            // Find any ElementPipsConfigSO in the project
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

                AssignSprite(so, "chargeSprite",      filledPaths,  Element.Charge);
                AssignSprite(so, "massSprite",         filledPaths,  Element.Mass);
                AssignSprite(so, "spaceSprite",        filledPaths,  Element.Space);
                AssignSprite(so, "timeSprite",         filledPaths,  Element.Time);
                AssignSprite(so, "chargeLabelSprite",  outlinePaths, Element.Charge);
                AssignSprite(so, "massLabelSprite",    outlinePaths, Element.Mass);
                AssignSprite(so, "spaceLabelSprite",   outlinePaths, Element.Space);
                AssignSprite(so, "timeLabelSprite",    outlinePaths, Element.Time);

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
