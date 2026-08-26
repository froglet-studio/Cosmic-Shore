using System.Collections.Generic;
using System.IO;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Editor.Codex
{
    /// <summary>
    /// Renders a codex entry's hero image to a transparent PNG under
    /// <see cref="OutputFolder"/> and imports it as a Sprite.
    ///
    /// <para><b>Alpha is recovered by rendering twice</b> — once on black, once on white — and
    /// solving <c>a = 1 - (white - black)</c> per pixel. That is deliberate rather than clever:
    /// whether a render target ends up carrying usable alpha depends on the pipeline, the URP
    /// asset's settings and the shaders involved, so a bake that trusts the alpha channel works on
    /// one project configuration and silently produces black boxes on another. Two opaque renders
    /// and one subtraction cannot be wrong, and 27 entries × 2 renders is not a cost worth
    /// optimising.</para>
    ///
    /// <para><b>Meshes are harvested off the prefab ASSET, never instantiated.</b> Instantiating a
    /// crystal or a creature runs its Awake — registries, network objects, spawn coroutines — in
    /// the editor, outside a game. Harvesting is the same approach <c>ToyModelBuilder</c> takes for
    /// the toybox's stations, and for the same reason.</para>
    ///
    /// <para>Gameplay prism and crystal shaders read global uniforms that only exist inside a
    /// running frame, so some of them render as a black blob or as nothing at all. The baker does
    /// not pretend otherwise: it measures coverage afterwards and, when a render comes back
    /// essentially empty, retries it as a shaded flat silhouette and says so.</para>
    /// </summary>
    public static class CodexImageBaker
    {
        public const string OutputFolder = "Assets/_Graphics/Codex";

        /// <summary>Below this fraction of visible pixels a render is treated as failed.</summary>
        const float MinimumCoverage = 0.004f;

        const float FieldOfView = 28f;

        public struct BakeResult
        {
            public bool Success;
            public string AssetPath;
            public Sprite Sprite;

            /// <summary>True when the authored materials produced nothing and flat was used.</summary>
            public bool FellBackToFlat;
            public string Error;
        }

        /// <summary>
        /// Bake <paramref name="entry"/>'s image and assign it. Returns the outcome; the caller
        /// records the written path on the tool ledger and saves.
        /// </summary>
        public static BakeResult Bake(CodexEntry entry, int size)
        {
            var result = new BakeResult();

            if (entry == null)
            {
                result.Error = "No entry.";
                return result;
            }
            if (!entry.SourcePrefab)
            {
                result.Error = $"'{entry.DisplayName}' has no source prefab to render.";
                return result;
            }

            var texture = Render(entry, size, entry.FlatSilhouette, out var coverage, out var error);

            // A shader that needs a running frame renders nothing. Say so and fall back, rather
            // than writing an empty PNG that looks like a missing asset.
            if (texture != null && coverage < MinimumCoverage && !entry.FlatSilhouette)
            {
                Object.DestroyImmediate(texture);
                texture = Render(entry, size, true, out coverage, out error);
                result.FellBackToFlat = texture != null && coverage >= MinimumCoverage;
            }

            if (texture == null)
            {
                result.Error = error ?? $"'{entry.DisplayName}' produced no renderable geometry.";
                return result;
            }
            if (coverage < MinimumCoverage)
            {
                Object.DestroyImmediate(texture);
                result.Error = $"'{entry.DisplayName}' rendered empty even as a flat silhouette — " +
                               "check the prefab has visible meshes.";
                return result;
            }

            result.AssetPath = Write(entry, texture);
            Object.DestroyImmediate(texture);

            result.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(result.AssetPath);
            result.Success = result.Sprite;
            if (!result.Success)
                result.Error = $"Wrote {result.AssetPath} but could not load it back as a Sprite.";
            else
                entry.Image = result.Sprite;

            return result;
        }

        // ── Render ───────────────────────────────────────────────────────────────

        static Texture2D Render(CodexEntry entry, int size, bool flat, out float coverage, out string error)
        {
            coverage = 0f;
            error = null;

            var model = HarvestModel(entry.SourcePrefab, flat, out var bounds, out var temporaries);
            if (!model)
            {
                error = $"'{entry.DisplayName}': the prefab has no visible meshes.";
                return null;
            }

            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.cameraType = CameraType.Preview;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.fieldOfView = FieldOfView;
                preview.camera.orthographic = false;

                preview.lights[0].intensity = 1.15f;
                preview.lights[0].transform.rotation = Quaternion.Euler(38f, 140f, 0f);
                preview.lights[0].color = Color.white;
                preview.lights[1].intensity = 0.55f;
                preview.lights[1].transform.rotation = Quaternion.Euler(-18f, -55f, 0f);
                preview.lights[1].color = new Color(0.78f, 0.84f, 1f);
                preview.ambientColor = new Color(0.32f, 0.33f, 0.38f, 1f);

                preview.AddSingleGO(model);
                FrameCamera(preview.camera, bounds, entry);

                var onBlack = Capture(preview, size, Color.black);
                var onWhite = Capture(preview, size, Color.white);
                if (onBlack == null || onWhite == null)
                {
                    if (onBlack) Object.DestroyImmediate(onBlack);
                    if (onWhite) Object.DestroyImmediate(onWhite);
                    error = $"'{entry.DisplayName}': the preview renderer returned no image.";
                    return null;
                }

                var composed = RecoverAlpha(onBlack, onWhite, out coverage);
                Object.DestroyImmediate(onBlack);
                Object.DestroyImmediate(onWhite);
                return composed;
            }
            finally
            {
                // Destroy explicitly rather than leaving it to Cleanup: the harvested objects carry
                // HideFlags.DontSave, which is exactly the flag that makes an object SURVIVE the
                // preview scene being torn down. Leaving it implicit leaks one hierarchy per bake.
                if (model) Object.DestroyImmediate(model);
                foreach (var temporary in temporaries)
                    if (temporary) Object.DestroyImmediate(temporary);
                preview.Cleanup();
            }
        }

        static Texture2D Capture(PreviewRenderUtility preview, int size, Color background)
        {
            preview.camera.backgroundColor = background;

            preview.BeginPreview(new Rect(0f, 0f, size, size), GUIStyle.none);
            preview.Render(true, false); // positional: this overload's 2nd parameter has been
                                            // spelled both updateFOV and updatefov across versions
            var rendered = preview.EndPreview() as RenderTexture;
            if (rendered == null) return null;

            // Read the RT's own dimensions: BeginPreview scales by the editor's pixel density, so
            // the target is not necessarily the size that was asked for.
            var previous = RenderTexture.active;
            RenderTexture.active = rendered;
            var texture = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, rendered.width, rendered.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        /// <summary>
        /// Solve straight-alpha colour from the two backgrounds. Compositing gives
        /// <c>rendered = colour·a + background·(1-a)</c>, so the difference between the white and
        /// black renders is exactly the background's contribution, <c>1-a</c>.
        /// </summary>
        static Texture2D RecoverAlpha(Texture2D onBlack, Texture2D onWhite, out float coverage)
        {
            var black = onBlack.GetPixels();
            var white = onWhite.GetPixels();
            var output = new Color[black.Length];

            int visible = 0;
            for (int i = 0; i < black.Length; i++)
            {
                var b = black[i];
                var w = white[i];

                float background = ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f;
                float alpha = Mathf.Clamp01(1f - background);

                if (alpha <= 0.004f)
                {
                    output[i] = Color.clear;
                    continue;
                }

                visible++;
                output[i] = new Color(
                    Mathf.Clamp01(b.r / alpha),
                    Mathf.Clamp01(b.g / alpha),
                    Mathf.Clamp01(b.b / alpha),
                    alpha);
            }

            coverage = black.Length == 0 ? 0f : visible / (float)black.Length;

            var result = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false);
            result.SetPixels(output);
            result.Apply();
            return result;
        }

        static void FrameCamera(Camera camera, Bounds bounds, CodexEntry entry)
        {
            var rotation = Quaternion.Euler(entry.PreviewPitch, entry.PreviewYaw, 0f);
            float radius = Mathf.Max(0.001f, bounds.extents.magnitude);
            float distance = radius * Mathf.Max(0.2f, entry.PreviewPadding) /
                             Mathf.Sin(Mathf.Deg2Rad * FieldOfView * 0.5f);

            camera.transform.rotation = rotation;
            camera.transform.position = bounds.center - rotation * Vector3.forward * distance;
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 4f);
            camera.farClipPlane = distance + radius * 8f;
        }

        // ── Model harvest ────────────────────────────────────────────────────────

        /// <summary>
        /// Copy the prefab's meshes into a plain GameObject hierarchy, normalised so its largest
        /// dimension is 2 units. Reads the prefab ASSET — nothing is instantiated, so no gameplay
        /// component ever wakes up.
        /// </summary>
        static GameObject HarvestModel(GameObject prefab, bool flat, out Bounds bounds,
            out List<Object> temporaries)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            temporaries = new List<Object>();

            var root = new GameObject("CodexPreviewModel") { hideFlags = HideFlags.HideAndDontSave };
            var flatMaterial = flat ? BuildFlatMaterial() : null;
            if (flatMaterial) temporaries.Add(flatMaterial);
            bool any = false;

            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter || !filter.sharedMesh) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled) continue;
                AddMesh(root.transform, prefab.transform, filter.transform, filter.sharedMesh,
                        renderer.sharedMaterials, ref flatMaterial, temporaries);
                any = true;
            }

            foreach (var skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skinned || !skinned.sharedMesh || !skinned.enabled) continue;
                AddMesh(root.transform, prefab.transform, skinned.transform, skinned.sharedMesh,
                        skinned.sharedMaterials, ref flatMaterial, temporaries);
                any = true;
            }

            if (!any)
            {
                Object.DestroyImmediate(root);
                foreach (var temporary in temporaries) if (temporary) Object.DestroyImmediate(temporary);
                temporaries.Clear();
                return null;
            }

            Normalize(root.transform, out bounds);
            return root;
        }

        static void AddMesh(Transform parent, Transform prefabRoot, Transform node, Mesh mesh,
            Material[] authored, ref Material flatMaterial, List<Object> temporaries)
        {
            var child = new GameObject(node.name) { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(parent, false);

            // The node's pose relative to the prefab root - the pose it would render in.
            var local = prefabRoot.worldToLocalMatrix * node.localToWorldMatrix;
            Vector3 right = local.GetColumn(0), up = local.GetColumn(1), forward = local.GetColumn(2);

            child.transform.localPosition = local.GetColumn(3);
            // A zero-scaled axis makes LookRotation undefined; keep identity rather than warn.
            child.transform.localRotation = forward.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f
                ? Quaternion.LookRotation(forward, up)
                : Quaternion.identity;
            child.transform.localScale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);

            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();

            var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++)
            {
                var authoredSlot = authored != null && i < authored.Length ? authored[i] : null;
                if (!flatMaterial && authoredSlot) { materials[i] = authoredSlot; continue; }

                // Either a flat bake, or an authored slot that is empty. Mint the shared fallback
                // once and hand it to every slot that needs it.
                if (!flatMaterial)
                {
                    flatMaterial = BuildFlatMaterial();
                    temporaries.Add(flatMaterial);
                }
                materials[i] = flatMaterial;
            }
            renderer.sharedMaterials = materials;
        }

        static void Normalize(Transform root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest > 0.0001f)
            {
                float scale = 2f / largest;
                root.localScale = Vector3.one * scale;
                root.position = -bounds.center * scale;
            }

            renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        }

        /// <summary>
        /// A shaded neutral material. Deliberately LIT rather than flat-unlit: a codex icon has to
        /// read as a shape, and an unlit fill of one colour throws away every bit of form the model
        /// has.
        /// </summary>
        static Material BuildFlatMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Universal Render Pipeline/Unlit");

            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.82f, 0.84f, 0.88f));
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.82f, 0.84f, 0.88f));
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return material;
        }

        // ── Write ────────────────────────────────────────────────────────────────

        static string Write(CodexEntry entry, Texture2D texture)
        {
            EnsureFolder(OutputFolder);

            var path = $"{OutputFolder}/{FileNameFor(entry)}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            return path;
        }

        public static string FileNameFor(CodexEntry entry) =>
            string.IsNullOrWhiteSpace(entry.Id)
                ? CodexHarvester.Slug(entry.DisplayName)
                : entry.Id.Replace('.', '_');

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var accumulated = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{accumulated}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(accumulated, parts[i]);
                accumulated = next;
            }
        }

        /// <summary>Every path this baker would write for the supplied entries.</summary>
        public static List<string> PathsFor(IEnumerable<CodexEntry> entries)
        {
            var paths = new List<string>();
            foreach (var entry in entries)
                if (entry != null) paths.Add($"{OutputFolder}/{FileNameFor(entry)}.png");
            return paths;
        }
    }
}
