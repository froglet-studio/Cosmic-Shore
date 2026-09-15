using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Which material a generated part draws with. The head's skin texture is shared by every
    /// part that is skin (lids, ears, nose leaf) — their UVs are re-projected through the head's
    /// own mapping at assembly so a seam is colour-continuous. The other slots carry their own
    /// procedural texture.
    /// </summary>
    public enum CharacterMaterialSlot
    {
        Skin = 0,
        Eye = 1,
        Keratin = 2,   // beak, mandibles, antennae, fangs, claws
        Hair = 3,      // hair cap, crest feathers, whiskers
        Gear = 4,      // jacket, collar, goggle frames and lenses
    }

    /// <summary>
    /// One generated piece of a character — plain lists, no Unity Mesh, no components — so the
    /// generator is pure and runs in edit-mode tests and in an offline harness byte for byte.
    /// Verts are in HEAD space once assembled; a feature generator emits them in SITE space and
    /// <see cref="CharacterAssembler"/> places them.
    /// </summary>
    public sealed class MeshPart
    {
        public string Name;
        public CharacterMaterialSlot Slot;
        public readonly List<Vector3> Verts = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<int> Tris = new();

        /// <summary>
        /// Seam-attached features: the number of leading vertices that form the base ring. 0 =
        /// embedded (no seam contract). See <see cref="AttachmentContract"/>.
        /// </summary>
        public int SeamRingCount;

        /// <summary>True when the part's UVs should be re-projected through the head's mapping.</summary>
        public bool ProjectUvsOntoHead;

        /// <summary>
        /// True when the generator emitted HEAD-space vertices (it sampled the surface itself,
        /// like the hair cap) — the assembler then places nothing and only mirrors/reprojects.
        /// </summary>
        public bool IsHeadSpace;

        public MeshPart(string name, CharacterMaterialSlot slot)
        {
            Name = name;
            Slot = slot;
        }

        public int AddVertex(Vector3 p, Vector2 uv)
        {
            Verts.Add(p);
            Uvs.Add(uv);
            Normals.Add(Vector3.zero);
            return Verts.Count - 1;
        }

        public void AddTri(int a, int b, int c)
        {
            Tris.Add(a); Tris.Add(b); Tris.Add(c);
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTri(a, b, c);
            AddTri(a, c, d);
        }

        /// <summary>Duplicate every triangle with the opposite winding and mirrored normals, so a thin part draws from both sides.</summary>
        public void MakeDoubleSided()
        {
            int n = Verts.Count;
            for (int i = 0; i < n; i++) AddVertex(Verts[i], Uvs[i]);
            for (int i = 0; i < n; i++) Normals[n + i] = -Normals[i];
            int tris = Tris.Count;
            for (int i = 0; i < tris; i += 3)
            {
                Tris.Add(Tris[i] + n);
                Tris.Add(Tris[i + 2] + n);
                Tris.Add(Tris[i + 1] + n);
            }
        }

        public void FlipWinding()
        {
            for (int i = 0; i < Tris.Count; i += 3)
            {
                int t = Tris[i + 1];
                Tris[i + 1] = Tris[i + 2];
                Tris[i + 2] = t;
            }
        }
    }
}
