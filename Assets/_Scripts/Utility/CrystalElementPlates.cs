using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Names the ELEMENT of every plate of an exploded crystal mesh, per vertex, so the omni crystal's body can wear
    /// each element's shapes in that element's own shade of lime (Garrett, 2026-10-28: "change the omnicrystal to retain
    /// all four elemental colors of lime in all instances"; Docs/PALETTE.md §2.12). The omni body is one mesh of 122
    /// disjoint plates and each family of plates is one element's shape (§2.10):
    /// <list type="bullet">
    /// <item>a pentagonal prism (10 corners) is Charge,</item>
    /// <item>a triangular prism (6 corners) is Mass,</item>
    /// <item>a square slab (8 corners, all at one distance from its centroid) is Space,</item>
    /// <item>a rhombic slab (8 corners at two distances, <see cref="RhombusSkinBaker.IsRhombus"/>) is Time.</item>
    /// </list>
    /// A plate is a connected component of the welded mesh, split the way <see cref="RhombusSkinBaker"/> splits it.
    ///
    /// <para>The element goes into UV channel <see cref="Channel"/> (TEXCOORD4) as x = <c>(int)Element</c>, 0 for a
    /// plate of none of the four shapes. <c>OmniCrystalFresnelShader</c> reads it: with <c>_ElementShades</c> on, a
    /// plate's bright colour is its element's. A mesh without the channel reads 0 there, so it draws in
    /// <c>_BrightColor</c> exactly as before.</para>
    ///
    /// <para>Every twin the omni body draws carries it: <see cref="RhombusSkinBaker"/> writes it into the skin twin it
    /// bakes, and the cut and shaped twins are copies of that one; <c>CrystalSpaceSquaresBaker</c> writes it into a
    /// shaped twin whose mesh did not have it; the vessel morphs build from <see cref="WithElements"/>.</para>
    /// </summary>
    public static class CrystalElementPlates
    {
        /// <summary>The UV channel the element is written to: TEXCOORD4 (channels 2 and 3 are the crystal morph's).</summary>
        public const int Channel = 4;

        const int TriangleCorners = 6, BoxCorners = 8, PentagonCorners = 10;

        /// <summary>Every twin <see cref="WithElements"/> made ends with this.</summary>
        const string Suffix = "(Elements)";

        /// <summary>Element twins: authored mesh -> its copy carrying the channel. Shared by every crystal.</summary>
        static readonly Dictionary<Mesh, Mesh> s_twins = new();

        // Enter Play Mode keeps statics (domain reload is off): a re-exported model is classified afresh.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => s_twins.Clear();

        /// <summary>The element a plate of <paramref name="corners"/> distinct corners stands for, or None.</summary>
        public static Element ElementOf(IReadOnlyList<Vector3> corners) => corners.Count switch
        {
            PentagonCorners => Element.Charge,
            TriangleCorners => Element.Mass,
            BoxCorners => RhombusSkinBaker.IsRhombus(corners, out _, out _) ? Element.Time : Element.Space,
            _ => Element.None,
        };

        /// <summary>True when <paramref name="mesh"/> already names its plates' elements.</summary>
        public static bool Carries(Mesh mesh) => mesh && mesh.HasVertexAttribute(VertexAttribute.TexCoord4);

        /// <summary>Every vertex's element, by its plate. <paramref name="mesh"/> must be CPU-readable.</summary>
        public static Element[] Classify(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var plateOf = RhombusSkinBaker.PlateOfVertices(mesh, vertices, out _, out var weldOf, out var weldPositions);
            // Each plate's distinct corners: one per weld, read off the first vertex of that weld.
            var plateCorners = new Dictionary<int, List<Vector3>>();
            var seen = new HashSet<int>();
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!seen.Add(weldOf[v])) continue;
                if (!plateCorners.TryGetValue(plateOf[v], out var corners))
                    plateCorners[plateOf[v]] = corners = new List<Vector3>(PentagonCorners);
                corners.Add(weldPositions[weldOf[v]]);
            }
            var elementOfPlate = new Dictionary<int, Element>(plateCorners.Count);
            foreach (var plate in plateCorners) elementOfPlate[plate.Key] = ElementOf(plate.Value);

            var elements = new Element[vertices.Length];
            for (int v = 0; v < vertices.Length; v++) elements[v] = elementOfPlate[plateOf[v]];
            return elements;
        }

        /// <summary>
        /// Writes every vertex's element into <paramref name="mesh"/>'s channel <see cref="Channel"/>, measured on
        /// <paramref name="plates"/> (the same vertices in the same order; <paramref name="mesh"/> itself when null).
        /// False, with the reason, when the plates cannot be read or name no element.
        /// </summary>
        public static bool TryWrite(Mesh mesh, Mesh plates, out string problem)
        {
            plates = plates ? plates : mesh;
            if (!mesh || !plates)
            {
                problem = "no mesh to name the elements of";
                return false;
            }
            if (!plates.isReadable)
            {
                problem = $"mesh '{plates.name}' is not Read/Write enabled, so its plates cannot be found";
                return false;
            }
            if (plates.vertexCount != mesh.vertexCount)
            {
                problem = $"mesh '{plates.name}' has {plates.vertexCount} vertices, '{mesh.name}' {mesh.vertexCount}";
                return false;
            }
            var elements = Classify(plates);
            var ids = new Vector4[elements.Length];
            bool any = false;
            for (int v = 0; v < elements.Length; v++)
            {
                ids[v] = new Vector4((int)elements[v], 0f, 0f, 0f);
                any |= elements[v] != Element.None;
            }
            if (!any)
            {
                problem = $"mesh '{plates.name}' has no pentagonal, triangular, square or rhombic plate";
                return false;
            }
            mesh.SetUVs(Channel, ids);
            problem = null;
            return true;
        }

        /// <summary>
        /// <paramref name="source"/> itself when it already names its elements, else a copy of it that does, made once
        /// and shared, and registered with <see cref="RhombusSkinBaker.RegisterDerivedTwin"/> so
        /// <see cref="RhombusSkinBaker.SourceOf"/> still finds the authored asset behind it. <paramref name="source"/>
        /// when it cannot be classified (the caller then draws it as before, in <c>_BrightColor</c>).
        /// </summary>
        public static Mesh WithElements(Mesh source)
        {
            if (!source || Carries(source)) return source;
            if (s_twins.TryGetValue(source, out var twin) && twin) return twin;
            twin = Object.Instantiate(source);
            twin.name = $"{source.name} {Suffix}";
            if (!TryWrite(twin, source, out _))
            {
                if (Application.isPlaying) Object.Destroy(twin);
                else Object.DestroyImmediate(twin);
                return source;
            }
            RhombusSkinBaker.RegisterDerivedTwin(twin, source);
            s_twins[source] = twin;
            return twin;
        }
    }
}
