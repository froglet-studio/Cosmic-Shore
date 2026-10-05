using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// uGUI's mesh builder (UnityEngine.UI.VertexHelper): a vertex stream + triangle
    /// indices in the graphic's RectTransform local space. Filled by
    /// <see cref="Graphic.PopulateMeshForRendering"/>; read by the renderer.
    /// </summary>
    public class VertexHelper : IDisposable
    {
        readonly List<UIVertex> _verts = new();
        readonly List<int> _indices = new();

        public VertexHelper() { }

        public VertexHelper(Mesh m)
        {
            if (m == null) return;
            var p = m.vertices; var c = m.colors32; var uv = m.uv; var tris = m.triangles;
            for (int i = 0; i < p.Length; i++)
            {
                var v = UIVertex.simpleVert;
                v.position = p[i];
                if (c != null && i < c.Length) v.color = c[i];
                if (uv != null && i < uv.Length) v.uv0 = new Vector4(uv[i].x, uv[i].y, 0f, 0f);
                _verts.Add(v);
            }
            if (tris != null) _indices.AddRange(tris);
        }

        public int currentVertCount => _verts.Count;
        public int currentIndexCount => _indices.Count;

        /// <summary>Read-only views for the renderer.</summary>
        public IReadOnlyList<UIVertex> Vertices => _verts;
        public IReadOnlyList<int> Indices => _indices;

        public void Clear() { _verts.Clear(); _indices.Clear(); }

        public void PopulateUIVertex(ref UIVertex vertex, int i) => vertex = _verts[i];
        public void SetUIVertex(UIVertex vertex, int i) => _verts[i] = vertex;

        public void AddVert(Vector3 position, Color32 color, Vector4 uv0, Vector4 uv1, Vector4 uv2, Vector4 uv3, Vector3 normal, Vector4 tangent)
            => _verts.Add(new UIVertex { position = position, color = color, uv0 = uv0, uv1 = uv1, uv2 = uv2, uv3 = uv3, normal = normal, tangent = tangent });

        public void AddVert(Vector3 position, Color32 color, Vector4 uv0, Vector4 uv1, Vector3 normal, Vector4 tangent)
            => AddVert(position, color, uv0, uv1, Vector4.zero, Vector4.zero, normal, tangent);

        public void AddVert(Vector3 position, Color32 color, Vector4 uv0)
            => AddVert(position, color, uv0, Vector4.zero, new Vector3(0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f));

        public void AddVert(Vector3 position, Color32 color, Vector2 uv0)
            => AddVert(position, color, new Vector4(uv0.x, uv0.y, 0f, 0f));

        public void AddVert(UIVertex v) => _verts.Add(v);

        public void AddTriangle(int idx0, int idx1, int idx2) { _indices.Add(idx0); _indices.Add(idx1); _indices.Add(idx2); }

        public void AddUIVertexQuad(UIVertex[] verts)
        {
            int start = _verts.Count;
            for (int i = 0; i < 4; i++) _verts.Add(verts[i]);
            AddTriangle(start, start + 1, start + 2);
            AddTriangle(start + 2, start + 3, start);
        }

        public void AddUIVertexStream(List<UIVertex> verts, List<int> indices)
        {
            int start = _verts.Count;
            if (verts != null) _verts.AddRange(verts);
            if (indices != null) foreach (var i in indices) _indices.Add(start + i);
        }

        public void AddUIVertexTriangleStream(List<UIVertex> verts)
        {
            if (verts == null) return;
            int start = _verts.Count;
            _verts.AddRange(verts);
            for (int i = 0; i < verts.Count; i++) _indices.Add(start + i);
        }

        /// <summary>Expands to a triangle list (three vertices per triangle), like Unity's GetUIVertexStream.</summary>
        public void GetUIVertexStream(List<UIVertex> stream)
        {
            if (stream == null) return;
            stream.Clear();
            foreach (var i in _indices) stream.Add(_verts[i]);
        }

        public void FillMesh(Mesh mesh)
        {
            if (mesh == null) return;
            mesh.Clear();
            var p = new Vector3[_verts.Count];
            var c = new Color32[_verts.Count];
            var uv = new Vector2[_verts.Count];
            for (int i = 0; i < _verts.Count; i++) { p[i] = _verts[i].position; c[i] = _verts[i].color; uv[i] = new Vector2(_verts[i].uv0.x, _verts[i].uv0.y); }
            mesh.vertices = p;
            mesh.colors32 = c;
            mesh.uv = uv;
            mesh.triangles = _indices.ToArray();
        }

        public void Dispose() => Clear();
    }

    /// <summary>A component that post-processes its graphic's mesh (Shadow/Outline/custom effects).</summary>
    public interface IMeshModifier
    {
        void ModifyMesh(VertexHelper verts);
    }

    /// <summary>Base for mesh effects on a graphic (UnityEngine.UI.BaseMeshEffect).</summary>
    public abstract class BaseMeshEffect : MonoBehaviour, IMeshModifier
    {
        Graphic m_Graphic;
        protected Graphic graphic => m_Graphic ??= GetComponent<Graphic>();
        public abstract void ModifyMesh(VertexHelper vh);
        protected virtual void OnEnable() => graphic?.SetVerticesDirty();
        protected virtual void OnDisable() => graphic?.SetVerticesDirty();
    }

    /// <summary>Canvas rebuild phases (UnityEngine.UI.CanvasUpdate).</summary>
    public enum CanvasUpdate
    {
        Prelayout = 0,
        Layout = 1,
        PostLayout = 2,
        PreRender = 3,
        LatePreRender = 4,
        MaxUpdateValue = 5,
    }
}

namespace CosmicShore.Engine
{
    /// <summary>
    /// Per-graphic render state (UnityEngine.CanvasRenderer): a tint multiplied into the
    /// graphic's vertices at draw time, culling, and the material/texture binding. The
    /// port's renderer reads <see cref="GetColor"/> and <see cref="cull"/>.
    /// </summary>
    public sealed class CanvasRenderer : Component
    {
        Color _color = Color.white;
        readonly List<Material> _materials = new();
        Texture _texture;
        Mesh _mesh;

        public bool cull { get; set; }
        public bool cullTransparentMesh { get; set; }
        public bool hasPopInstruction { get; set; }
        public bool hasMoved => false;
        public int absoluteDepth => 0;
        public int relativeDepth => 0;
        public int materialCount { get => _materials.Count; set { while (_materials.Count < value) _materials.Add(null); while (_materials.Count > value) _materials.RemoveAt(_materials.Count - 1); } }
        public int popMaterialCount { get; set; }
        public bool hasRectClipping { get; private set; }
        public Rect clipRect { get; private set; }

        public void SetColor(Color color) => _color = color;
        public Color GetColor() => _color;
        public void SetAlpha(float alpha) => _color = new Color(_color.r, _color.g, _color.b, alpha);
        public float GetAlpha() => _color.a;
        public float GetInheritedAlpha() => _color.a;

        public void SetMaterial(Material material, int index) { materialCount = Math.Max(materialCount, index + 1); _materials[index] = material; }
        public void SetMaterial(Material material, Texture texture) { SetMaterial(material, 0); _texture = texture; }
        public Material GetMaterial(int index = 0) => index < _materials.Count ? _materials[index] : null;
        public void SetPopMaterial(Material material, int index) { }
        public Material GetPopMaterial(int index) => null;
        public void SetTexture(Texture texture) => _texture = texture;
        public void SetAlphaTexture(Texture texture) { }
        public void SetMesh(Mesh mesh) => _mesh = mesh;
        public Mesh GetMesh() => _mesh;
        public void Clear() { _mesh = null; _materials.Clear(); _texture = null; }
        public void EnableRectClipping(Rect rect) { hasRectClipping = true; clipRect = rect; }
        public void DisableRectClipping() => hasRectClipping = false;
        public void SetClippingSoftness(Vector2 softness) { }
    }
}
