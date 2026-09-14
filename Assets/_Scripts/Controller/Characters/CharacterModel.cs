using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The assembled, still Unity-free character: the head part plus every placed feature in
    /// HEAD space, the landmarks the painter needs, and (once painted) the textures.
    /// <see cref="CharacterBustBuilder"/> turns it into a GameObject.
    /// </summary>
    public sealed class CharacterModel
    {
        public CharacterBlueprint Blueprint;
        public IBaseHead BaseHead;
        public HeadShape Shape;
        public MeshPart Head;
        public readonly List<MeshPart> Parts = new();
        public FaceLandmarks Landmarks;
        public CharacterTextures Textures;

        public IEnumerable<MeshPart> AllParts()
        {
            yield return Head;
            foreach (var p in Parts) yield return p;
        }

        public Bounds ComputeBounds()
        {
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var part in AllParts())
                foreach (var v in part.Verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            if (float.IsInfinity(min.x)) return new Bounds(Vector3.zero, Vector3.one);
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        public int VertexCount()
        {
            int n = 0;
            foreach (var p in AllParts()) n += p.Verts.Count;
            return n;
        }
    }
}
