using UnityEngine;
using UnityEditor;

namespace CosmicShore
{
    /// <summary>
    /// One-click builder that creates all membrane assets:
    /// 1. Icosphere mesh (subdivision 4, normals inward)
    /// 2. Material using MembraneSkybox shader
    /// 3. MembraneIcosphere prefab with MeshFilter + MeshRenderer + MembraneIcosphereSetup
    /// 4. BigMembraneIcosphere variant (1600 scale)
    ///
    /// Run via Tools > Build Membrane Assets.
    /// </summary>
    public static class MembraneAssetBuilder
    {
        [MenuItem("Tools/Build Membrane Assets")]
        public static void Build()
        {
            // --- Shader ---
            var shader = Shader.Find("CosmicShore/MembraneSkybox");
            if (shader == null)
            {
                Debug.LogError("Shader 'CosmicShore/MembraneSkybox' not found. Make sure MembraneSkyboxShader.shader exists.");
                return;
            }

            // --- Mesh ---
            string meshPath = "Assets/_Models/MembraneIcosphere.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = GenerateIcosphere(4);
                mesh.name = "MembraneIcosphere";
                AssetDatabase.CreateAsset(mesh, meshPath);
                Debug.Log($"Created mesh at {meshPath}");
            }

            // --- Material ---
            string matPath = "Assets/_Graphics/Materials/MembraneSkyboxMaterial.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(shader);
                material.name = "MembraneSkyboxMaterial";
                SetDefaultMaterialProperties(material);
                AssetDatabase.CreateAsset(material, matPath);
                Debug.Log($"Created material at {matPath}");
            }

            // --- Prefab: MembraneIcosphere ---
            string prefabPath = "Assets/_Prefabs/Environment/MembraneIcosphere.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var go = CreateMembraneGameObject("MembraneIcosphere", mesh, material, 1000f);
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);
                Debug.Log($"Created prefab at {prefabPath}");
            }

            // --- Prefab: BigMembraneIcosphere ---
            string bigPrefabPath = "Assets/_Prefabs/Environment/BigMembraneIcosphere.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(bigPrefabPath) == null)
            {
                var go = CreateMembraneGameObject("BigMembraneIcosphere", mesh, material, 1600f);
                PrefabUtility.SaveAsPrefabAsset(go, bigPrefabPath);
                Object.DestroyImmediate(go);
                Debug.Log($"Created prefab at {bigPrefabPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Wire into CellConfigDataSO assets
            WireMembraneToCellConfigs(prefabPath, bigPrefabPath);

            Debug.Log("Membrane assets built and wired to cell configs.");
        }

        static void WireMembraneToCellConfigs(string standardPrefabPath, string bigPrefabPath)
        {
            var standardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(standardPrefabPath);
            var bigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bigPrefabPath);

            if (standardPrefab == null)
            {
                Debug.LogWarning("Standard membrane prefab not found, skipping config wiring.");
                return;
            }

            // Find all CellConfigDataSO assets
            string[] guids = AssetDatabase.FindAssets("t:CellConfigDataSO", new[] { "Assets/_SO_Assets/Cell Configs" });
            int updated = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<CosmicShore.Game.CellConfigDataSO>(path);
                if (config == null || config.MembranePrefab == null) continue;

                // Determine which prefab to use based on current membrane scale
                bool isBig = config.MembranePrefab.transform.localScale.x > 1200f;
                var newPrefab = isBig && bigPrefab != null ? bigPrefab : standardPrefab;

                Undo.RecordObject(config, "Update membrane prefab");
                config.MembranePrefab = newPrefab;
                EditorUtility.SetDirty(config);
                updated++;
                Debug.Log($"Updated {path} → {newPrefab.name}");
            }

            if (updated > 0)
                AssetDatabase.SaveAssets();
        }

        static GameObject CreateMembraneGameObject(string name, Mesh mesh, Material material, float scale)
        {
            var go = new GameObject(name);
            go.transform.localScale = Vector3.one * scale;

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var setup = go.AddComponent<CosmicShore.Game.Environment.MembraneIcosphereSetup>();

            return go;
        }

        static void SetDefaultMaterialProperties(Material mat)
        {
            // Match original SkyboxModelGraphMaterial values
            mat.SetColor("_GradientColor", new Color(0.14391243f, 0.20994417f, 0.6226415f, 1f));
            mat.SetFloat("_EdgeBlend", 0.9f);
            mat.SetFloat("_GradientEdge", 0.16f);
            mat.SetFloat("_FresnelPower", 3.0f);

            mat.SetFloat("_Amplitude", 0.02f);
            mat.SetFloat("_Frequency", 0.58f);
            mat.SetFloat("_DisplacementSpeed", 0.3f);

            mat.SetFloat("_RippleDensity", 5.69f);
            mat.SetVector("_RippleOrigin", Vector4.zero);
            mat.SetFloat("_EffectRadius", 8.66f);

            // Pore defaults — ~40% membrane visible, 60% skybox
            mat.SetFloat("_PoreNoiseScale", 4.0f);
            mat.SetFloat("_PoreSpeed", 0.08f);
            mat.SetFloat("_PoreThreshold", 0.42f);
            mat.SetFloat("_PoreEdgeSoftness", 0.08f);
            mat.SetFloat("_FresnelOpacityPower", 2.5f);
            mat.SetFloat("_FresnelOpacityStrength", 0.85f);

            mat.SetColor("_HDREmission", Color.clear);

            // Rendering settings for transparent cutout
            mat.SetFloat("_ZWrite", 1);
            mat.renderQueue = 2450; // AlphaTest queue
        }

        static Mesh GenerateIcosphere(int subdivisionLevel)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            float a = 1f;
            float b = 1f / phi;

            var vertices = new System.Collections.Generic.List<Vector3>
            {
                new( 0,  b, -a),
                new( b,  a,  0),
                new(-b,  a,  0),
                new( 0,  b,  a),
                new( 0, -b,  a),
                new(-a,  0,  b),
                new( 0, -b, -a),
                new( a,  0, -b),
                new( a,  0,  b),
                new(-a,  0, -b),
                new( b, -a,  0),
                new(-b, -a,  0),
            };

            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            var triangles = new System.Collections.Generic.List<int>
            {
                2, 1, 0,   1, 2, 3,   5, 2, 3,   3, 8, 1,   3, 4, 8,
                3, 5, 4,   0, 1, 7,   7, 1, 8,   6, 7, 8,   6, 8,10,
                8, 4,10,   4,11,10,   4, 5,11,  11, 5, 9,   9, 5, 2,
                9, 2, 0,   6, 0, 7,   9, 0, 6,  11, 9, 6,  10,11, 6,
            };

            var midpointCache = new System.Collections.Generic.Dictionary<(int, int), int>();

            for (int level = 0; level < subdivisionLevel; level++)
            {
                var newTriangles = new System.Collections.Generic.List<int>();
                midpointCache.Clear();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int v0 = triangles[i];
                    int v1 = triangles[i + 1];
                    int v2 = triangles[i + 2];

                    int m01 = GetMidpoint(v0, v1, vertices, midpointCache);
                    int m12 = GetMidpoint(v1, v2, vertices, midpointCache);
                    int m20 = GetMidpoint(v2, v0, vertices, midpointCache);

                    newTriangles.AddRange(new[] { v0, m01, m20 });
                    newTriangles.AddRange(new[] { v1, m12, m01 });
                    newTriangles.AddRange(new[] { v2, m20, m12 });
                    newTriangles.AddRange(new[] { m01, m12, m20 });
                }

                triangles = newTriangles;
            }

            // Inward-facing normals
            for (int i = 0; i < triangles.Count; i += 3)
                (triangles[i], triangles[i + 1]) = (triangles[i + 1], triangles[i]);

            var mesh = new Mesh();
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        static int GetMidpoint(int a, int b,
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.Dictionary<(int, int), int> cache)
        {
            var key = a < b ? (a, b) : (b, a);
            if (cache.TryGetValue(key, out int index))
                return index;

            Vector3 midpoint = ((vertices[a] + vertices[b]) * 0.5f).normalized;
            index = vertices.Count;
            vertices.Add(midpoint);
            cache[key] = index;
            return index;
        }
    }
}
