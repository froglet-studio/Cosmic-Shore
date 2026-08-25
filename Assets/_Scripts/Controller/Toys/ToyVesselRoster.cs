using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one place a toy answers <b>"which hulls do I offer, and what does one look like?"</b>
    ///
    /// Two toys ask it and they ask it for opposite reasons - the <see cref="VesselChangerToy"/>
    /// offers hulls to BECOME (so it excludes the one you are flying), the Lifeform Matrix's
    /// VESSELS branch offers hulls to RELEASE as AI companions (so it excludes nothing). The
    /// roster, the meta-value filtering, the de-duplication and the mini-hull build are identical
    /// either way, and a second copy of the curated list is a second list to forget to update
    /// when a vessel ships.
    /// </summary>
    public static class ToyVesselRoster
    {
        /// <summary>
        /// Curated default so a matrix isn't all eleven ships (four of which are unimplemented
        /// planned classes). Override per-asset wherever a toy authors its own list.
        /// </summary>
        public static readonly VesselClassType[] Default =
        {
            VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino,
            VesselClassType.Squirrel, VesselClassType.Serpent, VesselClassType.Sparrow,
            VesselClassType.Urchin, VesselClassType.Scarab,
        };

        /// <summary>
        /// Fill <paramref name="into"/> with the hulls to offer: <paramref name="authored"/> when a
        /// definition supplies one, else <see cref="Default"/>, minus the meta values
        /// (<see cref="VesselClassType.Any"/> / <see cref="VesselClassType.Random"/> are not hulls),
        /// minus <paramref name="exclude"/>, de-duplicated in authored order.
        /// </summary>
        public static void Resolve(VesselClassType[] authored, List<VesselClassType> into,
            VesselClassType? exclude = null)
        {
            into.Clear();
            var collection = authored is { Length: > 0 } ? authored : Default;

            foreach (var vessel in collection)
            {
                if (vessel is VesselClassType.Any or VesselClassType.Random) continue;
                if (exclude.HasValue && vessel == exclude.Value) continue;
                if (!into.Contains(vessel)) into.Add(vessel);
            }
        }

        /// <summary>
        /// A display-only mini hull for <paramref name="vessel"/>, built straight from the ship
        /// PREFAB ASSET (never instantiated, so no NetworkObject / VesselStatus / controller ever
        /// runs). Returns false when the context has no prefab registry, the class has no prefab,
        /// or the prefab carries no hull geometry - callers keep their anonymous sphere.
        /// </summary>
        public static bool TryBuildHull(ToyContext context, VesselClassType vessel,
            float radius, Color previewColor, out GameObject model)
        {
            model = null;
            var container = context?.VesselPrefabContainer;
            if (!container || !container.TryGetShipPrefab(vessel, out Transform prefab)) return false;
            return VesselModelBuilder.TryBuild(prefab, radius, previewColor, out model);
        }

        /// <summary>As above, painted with a material the caller owns (an emblem's shared material).</summary>
        public static bool TryBuildHull(ToyContext context, VesselClassType vessel,
            float radius, Material shared, out GameObject model)
        {
            model = null;
            var container = context?.VesselPrefabContainer;
            if (!container || !container.TryGetShipPrefab(vessel, out Transform prefab)) return false;
            return VesselModelBuilder.TryBuild(prefab, radius, shared, out model);
        }

        /// <summary>
        /// The colour a mini hull reads as: the LOCAL player's domain colour, so every ship in a
        /// matrix previews "you, different hull". Falls back to <paramref name="fallback"/> (the
        /// toy's accent) when no player or theme is resolvable yet.
        /// </summary>
        public static Color PreviewColor(ToyContext context, Color fallback)
        {
            var player = context?.GameData ? context.GameData.LocalPlayer : null;
            if (player == null) return fallback;
            return ToyFactory.DomainAccentColor(context, player.Domain);
        }

        /// <summary>
        /// The domain an AI companion is released in - the local player's, so the toy grows YOUR
        /// side rather than seeding an opponent. Jade (the menu domain) is the neutral fallback.
        /// </summary>
        public static Domains PlayerDomain(ToyContext context)
        {
            var player = context?.GameData ? context.GameData.LocalPlayer : null;
            return player?.Domain ?? Domains.Jade;
        }

        /// <summary>
        /// Re-tint a built mini hull in place (no rebuild, so the recolour is instant and pop-free).
        /// Each model owns its own preview material, so this only affects that station; mirrors the
        /// property writes in <see cref="ToyModelBuilder.BuildPreviewMaterial"/>.
        /// </summary>
        public static void Recolor(Transform body, Color color)
        {
            if (!body) return;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (!m) continue;
                m.color = color;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 0.6f);
            }
        }
    }
}
