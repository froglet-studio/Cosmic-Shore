#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The two kit pieces Rev 145 H's Toy Box is built from (Docs/MENU_REDESIGN_B.md section 235.3,
    /// Docs/UI/specs/toybox-rev145h.md): the grid that centres its short last row (four lens cards over
    /// three), and the glyph depth drawn as extra copies of the glyph's own quads.
    /// Tools/Build/author_menu_toybox.py --check guards the prefab side.
    /// </summary>
    public class ToyBoxRev145Tests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        [Test]
        public void TheShortLastRowIsCentred()
        {
            _root = new GameObject("Grid", typeof(RectTransform));
            var rt = (RectTransform)_root.transform;
            rt.sizeDelta = new Vector2(1110, 700);
            var grid = _root.AddComponent<KitCenteredGrid>();
            var kids = new RectTransform[7];
            for (int i = 0; i < kids.Length; i++)
            {
                var c = new GameObject("Card" + i, typeof(RectTransform));
                c.transform.SetParent(_root.transform, false);
                kids[i] = (RectTransform)c.transform;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            float cell = grid.CellSize.x;
            float LeftEdge(RectTransform k) => k.anchoredPosition.x - k.sizeDelta.x * k.pivot.x;
            Assert.AreEqual(4, grid.Columns);
            Assert.AreEqual(0f, LeftEdge(kids[0]), 0.01f, "a full row spans the grid");
            // Three cards and two gaps, centred in 1110: (1110 - 3 * 252 - 2 * 34) / 2 = 143.
            Assert.AreEqual(143f, LeftEdge(kids[4]), 0.01f, "the last row of three sits in the middle");
            Assert.AreEqual(cell, kids[4].sizeDelta.x, 0.01f);
            Assert.Less(kids[4].anchoredPosition.y, kids[0].anchoredPosition.y, "the second row is below the first");
        }

        [Test]
        public void GlyphDepthDrawsShadowExtrusionAndFace()
        {
            _root = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(128, 128);
            var depth = _root.AddComponent<KitGlyphDepth>();

            using var vh = new VertexHelper();
            var v = UIVertex.simpleVert;
            v.position = new Vector3(-64, -64); vh.AddVert(v);
            v.position = new Vector3(-64, 64); vh.AddVert(v);
            v.position = new Vector3(64, 64); vh.AddVert(v);
            v.position = new Vector3(64, -64); vh.AddVert(v);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);

            depth.ModifyMesh(vh);

            // One copy for the shadow, one per extrusion layer, one for the face; six stream vertices a quad.
            Assert.AreEqual(6 * (depth.Layers + 2), vh.currentVertCount);
        }
    }
}
#endif
