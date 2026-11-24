#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A hull's domain colour lands on the same slot everywhere it is drawn (Garrett, 2026-11-10, LOCKED: every colour
    /// comes from the colour set, and a team's colour is its owner's domain). The live ship paints a skinned mesh's slot
    /// 0, the fleet's Domain slot, whatever <c>VesselCustomization._domainMaterialSlot</c> says
    /// (<c>ShipHelper.ApplyShipMaterial</c>); a display hull (the toybox's matrices, the theater's ghosts) must do the
    /// same, or a Goby or Squirrel shows its team colour on the body and the Ruby placeholder on its accent.
    /// </summary>
    public class DomainMaterialSlotTests
    {
        static Material Mat(string name) => new Material(Shader.Find("Hidden/InternalErrorShader")) { name = name };

        [Test]
        public void ASkinnedHull_WearsTheDomainOnSlotZero_LikeTheLiveShip()
        {
            var accent = Mat("RedAccent");
            var body = Mat("Body");
            var window = Mat("Window");
            var domain = Mat("Domain");
            var authored = new[] { accent, body, window };

            var skinned = VesselModelBuilder.ResolveDomainMaterials(authored, null, 1, domain, skinned: true);
            Assert.AreSame(domain, skinned[0], "a skinned mesh's domain slot is 0");
            Assert.AreSame(body, skinned[1], "the body keeps its own material");

            var mesh = VesselModelBuilder.ResolveDomainMaterials(authored, null, 1, domain);
            Assert.AreSame(domain, mesh[1], "a mesh renderer keeps the authored slot index");
            Assert.AreSame(accent, authored[0], "the prefab's own array is never written");
        }

        [Test]
        public void NamedIdentities_WinOverTheSlot_ForEitherRenderer()
        {
            var placeholder = Mat("GreenAccent");
            var body = Mat("Body");
            var domain = Mat("Domain");
            var authored = new[] { body, placeholder };
            var resolved = VesselModelBuilder.ResolveDomainMaterials(authored, new[] { placeholder }, 0, domain, skinned: true);
            Assert.AreSame(body, resolved[0]);
            Assert.AreSame(domain, resolved[1], "the named placeholder is replaced wherever it sits");
        }
    }
}
#endif
