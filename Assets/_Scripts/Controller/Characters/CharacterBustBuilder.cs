using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The only Unity-facing step: a painted <see cref="CharacterModel"/> becomes a GameObject —
    /// one child per material slot (all parts of a slot merged into one mesh), URP Lit
    /// materials with the painted textures. Smooth-shaded, lit, textured: the portrait stage is
    /// its own render context and nothing here touches the gameplay renderer or its prism
    /// materials. The caller owns the returned hierarchy and its meshes/materials/textures
    /// (<see cref="BuiltBust.Dispose"/>).
    /// </summary>
    public static class CharacterBustBuilder
    {
        public sealed class BuiltBust
        {
            public GameObject Root;
            public Bounds Bounds;
            public readonly List<Object> Owned = new();
            public int VertexCount;

            public void Dispose()
            {
                foreach (var o in Owned) if (o) Object.DestroyImmediate(o);
                Owned.Clear();
                if (Root) Object.DestroyImmediate(Root);
                Root = null;
            }
        }

        public static BuiltBust Build(CharacterModel model, CharacterGenerationConfigSO config, HideFlags hideFlags = HideFlags.HideAndDontSave)
        {
            var bust = new BuiltBust { Root = new GameObject("CharacterBust") { hideFlags = hideFlags } };
            var textures = model.Textures;
            var bySlot = new Dictionary<CharacterMaterialSlot, List<MeshPart>>();
            foreach (var part in model.AllParts())
            {
                if (!bySlot.TryGetValue(part.Slot, out var list)) bySlot[part.Slot] = list = new List<MeshPart>();
                list.Add(part);
            }

            foreach (var kv in bySlot)
            {
                var mesh = Merge(kv.Value, kv.Key.ToString());
                bust.Owned.Add(mesh);
                var material = MakeMaterial(kv.Key, textures, config, bust.Owned);
                var go = new GameObject(kv.Key.ToString()) { hideFlags = hideFlags };
                go.transform.SetParent(bust.Root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = ShadowCastingMode.On;
                bust.VertexCount += mesh.vertexCount;
            }
            bust.Bounds = model.ComputeBounds();
            return bust;
        }

        static Mesh Merge(List<MeshPart> parts, string name)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            foreach (var p in parts)
            {
                int offset = verts.Count;
                verts.AddRange(p.Verts);
                normals.AddRange(p.Normals);
                uvs.AddRange(p.Uvs);
                for (int i = 0; i < p.Tris.Count; i++) tris.Add(p.Tris[i] + offset);
            }
            var mesh = new Mesh { name = "Character_" + name, hideFlags = HideFlags.DontSave };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static Material MakeMaterial(CharacterMaterialSlot slot, CharacterTextures t, CharacterGenerationConfigSO config, List<Object> owned)
        {
            Material template = slot switch
            {
                CharacterMaterialSlot.Skin => config.SkinMaterialTemplate,
                CharacterMaterialSlot.Eye => config.EyeMaterialTemplate,
                CharacterMaterialSlot.Keratin => config.KeratinMaterialTemplate,
                _ => config.HairMaterialTemplate,
            };
            Material m;
            if (template != null) m = new Material(template);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
            }
            m.hideFlags = HideFlags.DontSave;
            m.name = "Character_" + slot;
            owned.Add(m);

            TextureCanvas canvas; float smooth; bool metallicIsh = false;
            switch (slot)
            {
                case CharacterMaterialSlot.Skin: canvas = t.Skin; smooth = 0.4f; break;
                case CharacterMaterialSlot.Eye: canvas = t.Eye; smooth = t.EyeSmoothness; break;
                case CharacterMaterialSlot.Keratin: canvas = t.Keratin; smooth = t.KeratinSmoothness; metallicIsh = true; break;
                default: canvas = t.Hair; smooth = t.HairSmoothness; break;
            }
            var albedo = Upload(canvas, true, owned);
            m.SetTexture("_BaseMap", albedo);
            m.SetTexture("_MainTex", albedo);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_Color", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallicIsh ? 0.05f : 0f);
            if (slot == CharacterMaterialSlot.Skin)
            {
                // Smoothness lives in the albedo alpha (painted per covering).
                if (m.HasProperty("_SmoothnessTextureChannel")) m.SetFloat("_SmoothnessTextureChannel", 1f);
                m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                if (t.SkinNormal != null && config.DetailNormalStrength > 0f)
                {
                    var normal = Upload(t.SkinNormal, false, owned);
                    m.SetTexture("_BumpMap", normal);
                    if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
            return m;
        }

        static Texture2D Upload(TextureCanvas canvas, bool srgb, List<Object> owned)
        {
            var tex = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, true, !srgb)
            {
                hideFlags = HideFlags.DontSave, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            tex.SetPixels32(canvas.ToColor32());
            tex.Apply(true, false);
            owned.Add(tex);
            return tex;
        }
    }
}
