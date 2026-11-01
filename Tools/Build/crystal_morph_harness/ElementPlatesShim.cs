// Stands in for Assets/_Scripts/Utility/CrystalElementPlates.cs, which CrystalMorphMeshBuilder reads
// to carry the omni body's per-plate element shade (TEXCOORD4) through the morph. The real file pulls
// in CosmicShore.Data and UnityEngine.Rendering, which UnityStub.cs does not model. The suites' test
// cages carry no element channel, so the shim answers exactly as the real file would for them: the
// channel number, and "this mesh does not carry it".
using UnityEngine;

namespace CosmicShore.Utility
{
    public static class CrystalElementPlates
    {
        public const int Channel = 4;   // must match CrystalElementPlates.Channel
        public static bool Carries(Mesh mesh) => false;
    }
}
