using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One (vessel, element) crystal fusion SOLVED AT EDIT TIME — written by
    /// <b>FrogletTools > Vessels > Bake Crystal Hull Fusions</b> and referenced from its
    /// <see cref="CrystalHullFusionConfigSO.Entry"/>. With a current bake the game does no
    /// geometry at all: no cut, no hull projection, no worker thread — a pickup reads this asset,
    /// matches faces to patches and clones <see cref="TemplateMesh"/>. Record:
    /// <c>Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md</c> §4.
    ///
    /// The solution is a property of two ASSETS (the hull in its bind pose and the crystal mesh),
    /// so it is fingerprinted against both. A bake whose fingerprint no longer matches is STALE:
    /// the runtime says so once, names the tool, and falls back to solving on a worker thread —
    /// correct, just not free.
    ///
    /// The drawn mesh depends on the CRYSTAL alone, so every vessel's bake for one element points
    /// at the same shared template (<c>&lt;Element&gt;_FusionTemplate.asset</c>); a bake itself
    /// carries only its hull's layout.
    /// </summary>
    [CreateAssetMenu(fileName = "HullFusionBake", menuName = "ScriptableObjects/" + nameof(CrystalHullFusionBakeSO))]
    public class CrystalHullFusionBakeSO : ScriptableObject
    {
        /// <summary>Bump when the solver or this layout changes meaning; every older bake reads stale.</summary>
        /// <remarks>2: static multi-part hulls, the per-face crystal anchors, shared per-element templates.
        /// 3: the hull is solved at unit size (the Manta family's 0.011-unit bind-pose mesh landed 6%).</remarks>
        public const int CurrentSchema = 3;

        [Header("Fingerprint - what this was solved against")]
        [Tooltip("Solver version the bake was made with. Older than the code's = stale.")]
        [SerializeField] int schema;

        [Tooltip("The mesh naming the hull: the vessel's element-shape SkinnedMeshRenderer mesh, or a " +
                 "static hull's body mesh.")]
        [SerializeField] Mesh hullMesh;
        [Tooltip("Vertices across every hull part.")]
        [SerializeField] int hullVertexCount;
        [Tooltip("1 for a skinned hull; a static hull's body plus every mesh part under it.")]
        [SerializeField] int hullPartCount;
        [Tooltip("ContentHash of the hull's bind-pose vertices and triangles, every part placed in the " +
                 "hull's space - catches a re-export that keeps the count, or a part moved on the prefab.")]
        [SerializeField] uint hullHash;

        [Tooltip("The crystal model's SOURCE mesh (the FBX mesh, before the edge-arc twin is baked from it).")]
        [SerializeField] Mesh crystalSourceMesh;
        [SerializeField] int crystalPlateCorners;
        [SerializeField] int crystalVertexCount;
        [SerializeField] uint crystalHash;

        [Tooltip("The entry tuning the layout was solved with. A retune of either reads stale.")]
        [SerializeField] float tileFill;
        [SerializeField] float surfaceLift;
        [SerializeField] int subdivisions;

        [Header("Solution")]
        [Tooltip("The faces as drawn: the crystal's filler plus every face cut into its fan, with the " +
                 "charge discharge channels in UV1-3 and (face, point) packed into UV0. Shared by every " +
                 "vessel's bake for this element.")]
        [SerializeField] Mesh templateMesh;
        [SerializeField] CrystalHullFusionGeometry.FusionSolution solution;

        public int Schema => schema;
        public Mesh HullMesh => hullMesh;
        public Mesh CrystalSourceMesh => crystalSourceMesh;
        public int CrystalPlateCorners => crystalPlateCorners;
        public Mesh TemplateMesh => templateMesh;
        public CrystalHullFusionGeometry.FusionSolution Solution => solution;
        public uint HullHash => hullHash;
        public uint CrystalHash => crystalHash;

        /// <summary>
        /// The cheap runtime check: same assets, same counts, same tuning, same solver. The content
        /// hashes are checked at edit time (the tool and its test), where reading every vertex is
        /// free; at runtime a re-export that keeps the count is the one thing this does not catch.
        /// </summary>
        public bool Matches(Mesh hull, int hullVertices, int hullParts, Mesh crystalSource, int plateCorners,
                            CrystalHullFusionConfigSO.Entry entry, int faceSubdivisions, out string why)
        {
            why = null;
            if (schema != CurrentSchema) why = $"solved by schema {schema}, the code is at {CurrentSchema}";
            else if (!templateMesh || solution == null || solution.Layout == null) why = "it holds no solution";
            else if (hull != hullMesh) why = $"it was solved for hull '{Name(hullMesh)}', the vessel draws '{Name(hull)}'";
            else if (hullParts != hullPartCount) why = $"the hull has {hullParts} mesh part(s), the bake {hullPartCount}";
            else if (hullVertices != hullVertexCount) why = $"the hull has {hullVertices} vertices, the bake {hullVertexCount}";
            else if (crystalSource != crystalSourceMesh || plateCorners != crystalPlateCorners)
                why = $"it was solved for crystal '{Name(crystalSourceMesh)}', the pickup draws '{Name(crystalSource)}'";
            else if (crystalSource.vertexCount != crystalVertexCount)
                why = $"'{Name(crystalSource)}' has {crystalSource.vertexCount} vertices, the bake {crystalVertexCount}";
            else if (entry != null && (!Mathf.Approximately(entry.tileFill, tileFill) || !Mathf.Approximately(entry.surfaceLift, surfaceLift)))
                why = $"the entry's tileFill/surfaceLift ({entry.tileFill}/{entry.surfaceLift}) were retuned since the bake ({tileFill}/{surfaceLift})";
            else if (faceSubdivisions != subdivisions) why = $"faces are cut {faceSubdivisions} deep, the bake {subdivisions}";
            return why == null;
        }

        static string Name(Object o) => o ? o.name : "(none)";

#if UNITY_EDITOR
        /// <summary>Editor-only: written by the bake tool, never at runtime.</summary>
        public void EditorWrite(Mesh hull, int hullVertices, int hullParts, uint hullContentHash, Mesh crystalSource,
                                int plateCorners, uint crystalContentHash, CrystalHullFusionConfigSO.Entry entry,
                                int faceSubdivisions, Mesh template, CrystalHullFusionGeometry.FusionSolution solved)
        {
            schema = CurrentSchema;
            hullMesh = hull;
            hullVertexCount = hullVertices;
            hullPartCount = hullParts;
            hullHash = hullContentHash;
            crystalSourceMesh = crystalSource;
            crystalPlateCorners = plateCorners;
            crystalVertexCount = crystalSource ? crystalSource.vertexCount : 0;
            crystalHash = crystalContentHash;
            tileFill = entry.tileFill;
            surfaceLift = entry.surfaceLift;
            subdivisions = faceSubdivisions;
            templateMesh = template;
            solution = solved;
        }
#endif
    }
}
