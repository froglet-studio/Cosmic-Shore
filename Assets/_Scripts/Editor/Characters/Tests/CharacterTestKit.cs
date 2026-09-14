using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>Shared loaders for the character tests: the shipped clade assets and config, never fixtures.</summary>
    public static class CharacterTestKit
    {
        public static CladeCatalog Catalog()
        {
            var clades = Resources.LoadAll<CladeSO>(CladeCatalog.ResourcesFolder);
            Assert.GreaterOrEqual(clades.Length, 7, "expected the human source + six clade assets under Resources/Characters/Clades");
            var catalog = new CladeCatalog(clades);
            Assert.IsNotNull(catalog.Human, "exactly one clade asset must set IsHuman");
            return catalog;
        }

        public static CharacterGenerationConfigSO Config()
        {
            var config = CharacterGenerationConfigSO.LoadDefault();
            Assert.IsNotNull(config, "Resources/Characters/CharacterGenerationConfig.asset missing");
            return config;
        }

        public static CharacterModel Build(CharacterGenome genome, CladeCatalog catalog, CharacterGenerationConfigSO config, HeadDetail detail)
        {
            var bp = CharacterResolver.Resolve(genome, catalog, config);
            var head = new ProceduralBaseHead(detail, config.ProceduralForm);
            return CharacterAssembler.Assemble(bp, head);
        }

        public static bool VerticesEqual(CharacterModel a, CharacterModel b)
        {
            var pa = new List<MeshPart>(a.AllParts());
            var pb = new List<MeshPart>(b.AllParts());
            if (pa.Count != pb.Count) return false;
            for (int p = 0; p < pa.Count; p++)
            {
                if (pa[p].Verts.Count != pb[p].Verts.Count || pa[p].Tris.Count != pb[p].Tris.Count) return false;
                for (int i = 0; i < pa[p].Verts.Count; i++) if (pa[p].Verts[i] != pb[p].Verts[i]) return false;
                for (int i = 0; i < pa[p].Tris.Count; i++) if (pa[p].Tris[i] != pb[p].Tris[i]) return false;
            }
            return true;
        }
    }
}
