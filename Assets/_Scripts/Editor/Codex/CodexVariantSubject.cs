using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Editor.Codex
{
    /// <summary>
    /// Decides what a VARIANT's icon is, and builds it.
    ///
    /// <para>The governing question is <b>"is this variant a distinct object?"</b>, and for most
    /// variants the answer is no — which is why most of them bake nothing at all:</para>
    /// <list type="bullet">
    /// <item>A species' four <b>elements</b> resolve to that element's own <b>ethirion</b> image,
    /// already baked once on its own page (<see cref="CodexSO.VariantImage"/>). Baking 123 more
    /// PNGs of four crystals would be 123 copies of four pictures.</item>
    /// <item>A <b>domain</b> variant is a colour, and a PNG of a flat colour is silly — it draws
    /// its <see cref="CodexVariant.AccentColor"/>.</item>
    /// <item>A <b>kingdom</b> variant (the Lifeform Matrix's Fauna / Flora / Vessels) is a
    /// heading, not a thing. It falls back to the entry's own portrait.</item>
    /// </list>
    ///
    /// <para>What is left really is distinct, and is what this builds: a <b>painting</b> (sixteen
    /// genuinely different pictures) and a <b>hull</b> (a real ship prefab). ~24 images across the
    /// whole codex rather than ~150, and every one of them earns its file.</para>
    ///
    /// <para>Nothing is instantiated, exactly as in <see cref="CodexImageBaker"/>: a hull is
    /// copied out of the prefab ASSET by the baker's own <c>HarvestModel</c>, and a painting's
    /// strokes are built as plain meshes rather than through
    /// <see cref="MiniaturePaintingBuilder"/>, which attaches a live <c>ToyIdleSpin</c> and draws
    /// with <c>LineRenderer</c>s whose view-aligned billboarding is not something to bet a bake
    /// on.</para>
    /// </summary>
    public static class CodexVariantSubject
    {
        /// <summary>Whether this variant has art of its own worth baking.</summary>
        public static bool CanDraw(CodexEntry entry, CodexVariant variant)
        {
            if (entry == null || variant == null) return false;

            // An ecology variant carries the SPECIES prefab - true wiring, and the same prefab the
            // entry photographed. Baking it would write the entry's picture again under a new name.
            if (entry.Kingdom != CodexKingdom.Tool) return false;

            return variant.SourcePrefab || variant.SourceConfig is PaintingDefinitionSO;
        }

        /// <summary>
        /// The subject, NORMALISED and ready to frame - the same contract the baker's other
        /// subject builders honour, so <c>Render</c> treats all three identically and nothing
        /// normalises twice (which would silently undo the first pass). Null when there is
        /// nothing to draw.
        /// </summary>
        public static GameObject Build(CodexEntry entry, CodexVariant variant, bool flat,
            List<Object> temporaries, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (!CanDraw(entry, variant)) return null;

            if (variant.SourceConfig is PaintingDefinitionSO painting)
                return BuildPainting(painting, flat, temporaries, out bounds);

            return BuildHull(variant.SourcePrefab, temporaries, out bounds);
        }

        // ── A hull ───────────────────────────────────────────────────────────────

        /// <summary>
        /// A ship, through the baker's own model harvest — which matters for a reason that is easy
        /// to miss: <b>five of the eight hulls are SKINNED</b> (one armature, one skinned hull),
        /// and a walk over <c>MeshFilter</c>s alone finds nothing on any of them.
        /// <c>HarvestModel</c> covers both families.
        ///
        /// <para>Always FLAT, never the ship's own materials. A vessel draws with the shared
        /// vessel graph, which is domain-tinted and reads per-frame globals that do not exist
        /// outside a running frame — so the authored pass would render black, fall back to flat
        /// anyway, and cost a second render to arrive at the same picture. Neutral is also the
        /// right answer on its own terms: colour means domain, and an encyclopedia page is
        /// nobody's.</para>
        /// </summary>
        static GameObject BuildHull(GameObject prefab, List<Object> temporaries, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (!prefab) return null;

            // HarvestModel flags its own root and children HideAndDontSave, so there is nothing
            // to mark here - it never leaves a live object in the open scene.
            return CodexImageBaker.HarvestModel(prefab, flat: true, temporaries, out bounds);
        }

        // ── A painting ───────────────────────────────────────────────────────────

        /// <summary>Strokes drawn per icon. Past this a monument reads as a scribble.</summary>
        const int MaxStrokes = 40;

        /// <summary>Points kept per stroke — enough to hold a curve at icon size.</summary>
        const int MaxPointsPerStroke = 24;

        /// <summary>Line half-thickness, as a fraction of the painting's largest dimension.</summary>
        const float LineHalfWidth = 0.006f;

        /// <summary>
        /// A painting is its strokes, so the icon is the strokes — drawn as thin crossed ribbons
        /// (two perpendicular quads per segment), which read as a line from any angle without
        /// depending on a billboarding renderer.
        ///
        /// <para><b>Coloured by DOMAIN, which is the one place this codex does that.</b> Elsewhere
        /// a page is painted neutral because colour means ownership and an encyclopedia page is
        /// nobody's. Here the domains ARE the subject: a painting is authored as a multi-domain
        /// object and the toy recolours your trail stroke by stroke, so a monochrome icon would be
        /// hiding the content rather than staying impartial.</para>
        /// </summary>
        static GameObject BuildPainting(PaintingDefinitionSO painting, bool flat,
            List<Object> temporaries, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            painting.EnsureStrokes();
            var strokes = painting.Strokes;
            if (strokes == null || strokes.Count == 0) return null;

            var chosen = SelectStrokes(strokes, MaxStrokes);
            // The extent of what is DRAWN, in the painting's own units. Distinct from the out
            // bounds, which is the normalised subject's and is set at the very end.
            if (!TryMeasure(strokes, chosen, out var extent)) return null;

            float half = Mathf.Max(extent.size.x, Mathf.Max(extent.size.y, extent.size.z)) *
                         LineHalfWidth;
            if (half <= 0f) return null;

            var root = new GameObject("CodexPaintingIcon") { hideFlags = HideFlags.HideAndDontSave };

            // One child per domain: three draw calls at most, and the mesh stays simple.
            var perDomain = new Dictionary<Domains, PaintingMesh>();

            foreach (int index in chosen)
            {
                var stroke = strokes[index];
                var points = Decimate(stroke.points, MaxPointsPerStroke);
                if (points.Count < 2) continue;

                if (!perDomain.TryGetValue(stroke.domain, out var target))
                    perDomain[stroke.domain] = target = new PaintingMesh();

                for (int i = 0; i + 1 < points.Count; i++)
                    target.AddSegment(points[i] - extent.center, points[i + 1] - extent.center, half);
            }

            bool any = false;
            foreach (var pair in perDomain)
            {
                var mesh = pair.Value.ToMesh($"CodexPainting_{pair.Key}");
                if (!mesh) continue;
                temporaries.Add(mesh);

                var tint = flat
                    ? Color.white
                    : ToyFactory.DomainAccentColor(null, pair.Key);
                var material = flat
                    ? CodexImageBaker.BuildFlatMaterial()
                    : CodexImageBaker.BuildTintedMaterial(tint);
                temporaries.Add(material);

                var child = new GameObject(pair.Key.ToString())
                    { hideFlags = HideFlags.HideAndDontSave };
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = material;
                any = true;
            }

            if (!any)
            {
                Object.DestroyImmediate(root);
                return null;
            }

            CodexImageBaker.Normalize(root.transform, out bounds);
            return root;
        }

        /// <summary>
        /// One domain's ribbon geometry. Each segment is two perpendicular quads through the
        /// segment axis, so the stroke has thickness from every viewing angle.
        /// </summary>
        sealed class PaintingMesh
        {
            readonly List<Vector3> _vertices = new();
            readonly List<int> _triangles = new();

            public void AddSegment(Vector3 a, Vector3 b, float half)
            {
                var axis = b - a;
                if (axis.sqrMagnitude < 1e-10f) return;
                axis.Normalize();

                // Any vector not parallel to the axis gives a stable perpendicular basis.
                var seed = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
                var u = Vector3.Normalize(Vector3.Cross(axis, seed)) * half;
                var v = Vector3.Normalize(Vector3.Cross(axis, u)) * half;

                AddQuad(a - u, a + u, b + u, b - u);
                AddQuad(a - v, a + v, b + v, b - v);
            }

            void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int b = _vertices.Count;
                _vertices.Add(p0); _vertices.Add(p1); _vertices.Add(p2); _vertices.Add(p3);

                // Both windings: a ribbon has no meaningful outside, and a one-sided one
                // disappears from half the preview angles.
                _triangles.Add(b); _triangles.Add(b + 1); _triangles.Add(b + 2);
                _triangles.Add(b); _triangles.Add(b + 2); _triangles.Add(b + 3);
                _triangles.Add(b); _triangles.Add(b + 2); _triangles.Add(b + 1);
                _triangles.Add(b); _triangles.Add(b + 3); _triangles.Add(b + 2);
            }

            public Mesh ToMesh(string name)
            {
                if (_vertices.Count == 0) return null;

                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
                if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_vertices);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// Up to <paramref name="budget"/> stroke indices, sampled evenly across author order.
        /// Even rather than longest-first: a painting is built up in passes, so an even sample
        /// spans the whole construction where the longest strokes cluster in one of them.
        /// </summary>
        static List<int> SelectStrokes(IReadOnlyList<PaintingStroke> strokes, int budget)
        {
            var chosen = new List<int>(Mathf.Min(budget, strokes.Count));
            if (strokes.Count <= budget)
            {
                for (int i = 0; i < strokes.Count; i++) chosen.Add(i);
                return chosen;
            }

            for (int i = 0; i < budget; i++)
                chosen.Add(Mathf.Min(strokes.Count - 1,
                    Mathf.RoundToInt(i * (strokes.Count - 1) / (float)(budget - 1))));
            return chosen;
        }

        /// <summary>Evenly thinned to <paramref name="budget"/>, always keeping both ends.</summary>
        static List<Vector3> Decimate(List<Vector3> points, int budget)
        {
            if (points == null) return new List<Vector3>();
            if (points.Count <= budget) return new List<Vector3>(points);

            var result = new List<Vector3>(budget);
            for (int i = 0; i < budget; i++)
                result.Add(points[Mathf.Min(points.Count - 1,
                    Mathf.RoundToInt(i * (points.Count - 1) / (float)(budget - 1)))]);
            return result;
        }

        /// <summary>Bounds of what will actually be DRAWN, not of the whole painting.</summary>
        static bool TryMeasure(IReadOnlyList<PaintingStroke> strokes, List<int> chosen,
            out Bounds bounds)
        {
            bounds = default;
            bool started = false;

            foreach (int index in chosen)
            {
                var points = strokes[index]?.points;
                if (points == null) continue;

                foreach (var point in points)
                {
                    if (!started) { bounds = new Bounds(point, Vector3.zero); started = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return started;
        }
    }
}
