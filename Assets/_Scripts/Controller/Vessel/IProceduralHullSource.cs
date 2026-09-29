using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One piece of a procedurally built hull, as bare geometry a harvester can turn into a
    /// <see cref="Mesh"/> it then OWNS. Bare arrays rather than a Mesh on purpose: a source is
    /// asked off the prefab ASSET, in the editor and at runtime, and a mesh minted by the asset
    /// would belong to nobody.
    /// </summary>
    public sealed class ProceduralHullPiece
    {
        public string Name;
        /// <summary>Where this piece sits, relative to the source component's own transform.</summary>
        public Vector3 LocalPosition;
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] Uvs;
        /// <summary>Triangle lists, one per material slot of the source's renderer.</summary>
        public int[][] Submeshes;
    }

    /// <summary>
    /// A hull that is BUILT at runtime rather than authored as a model, and can rebuild itself
    /// from the settings serialized on its prefab without waking anything. Every mesh harvester
    /// that reads the prefab ASSET (the toybox's mini hulls, the codex's portrait bakes) asks this
    /// for the hull it would otherwise never see — on the asset the procedural hull is an empty
    /// MeshFilter, and the only geometry present is whatever legacy model the source hides at
    /// Awake, so a harvester that only reads renderers photographs the wrong ship (the Scarab
    /// baked as a Sparrow, byte for byte). Pair it with
    /// <see cref="IProceduralElementMorphSource.HiddenLegacyModelRoot"/>, which is how the
    /// harvesters know what NOT to read.
    /// </summary>
    public interface IProceduralHullSource
    {
        /// <summary>Append the hull's pieces, built from the authored settings, at rest (no
        /// element morph, no puppetry). Must not touch the scene or any Unity object.</summary>
        void BuildPreviewPieces(List<ProceduralHullPiece> into);
    }
}
