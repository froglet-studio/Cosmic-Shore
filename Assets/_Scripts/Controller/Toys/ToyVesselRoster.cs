using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
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
        /// A mini hull wearing the ship's OWN materials, marked so the VESSEL VISION BAND shades it
        /// exactly as it shades a real vessel (Docs/VESSEL_VISION.md).
        ///
        /// <para>This is what a fly-at STATION should use, and the geometry is why it works: a
        /// vessel matrix blooms <c>StationSpacing x MatrixDistanceFactor</c> — 360 units on the
        /// shipped vessel changer — out along the outward radial, which lands the whole grid just
        /// past the band's <c>nearFullStart</c>. So the ships arrive already at FULL mark, read as
        /// domain-coloured cel silhouettes for the entire approach while you are choosing between
        /// them, and resolve into their real hulls over the last 150 units as you commit to one.
        /// Choosing at range, arriving at a ship.</para>
        ///
        /// <para>A GLYPH is a different job and keeps the flat fill: a toy's emblem and the
        /// kingdom icons sit ON the toy, inside the band's near cutoff where the mark is correctly
        /// zero, and at glyph size a real hull is a black blob. Use <see cref="TryBuildHull"/>
        /// there.</para>
        /// </summary>
        public static bool TryBuildLiveHull(ToyContext context, VesselClassType vessel,
            float radius, out GameObject model)
        {
            model = null;
            var container = context?.VesselPrefabContainer;
            if (!container || !container.TryGetShipPrefab(vessel, out Transform prefab)) return false;
            if (!VesselModelBuilder.TryBuildLive(prefab, radius, DomainMaterial(context), out model))
                return false;

            VesselVisionShading.StampDisplayModel(model.transform, DomainSignalColor(context));
            return true;
        }

        /// <summary>
        /// Re-apply the local player's domain to an already-built mini hull, whichever kind it is.
        ///
        /// <para>One list can hold both kinds (the Lifeform Matrix's <c>_hullBodies</c> holds its
        /// kingdom glyph and its hangar stations), and they must be re-tinted in OPPOSITE ways: a
        /// flat model owns a preview material, so it is repainted; a live model draws with shared
        /// PROJECT assets, so repainting would recolour every ship in the game permanently. Hence
        /// one entry point that dispatches on <see cref="ToyLiveHull"/> rather than two the caller
        /// has to choose between correctly.</para>
        /// </summary>
        public static void ApplyDomain(ToyContext context, Transform body, Color flatColor)
        {
            if (!body) return;

            if (body.GetComponentInChildren<ToyLiveHull>(true))
            {
                RepaintLiveHull(context, body);
                return;
            }
            Recolor(body, flatColor);
        }

        /// <summary>
        /// Swap a live hull's domain-role material and re-stamp its vision mark. Never writes to a
        /// material — only to which shared material each slot points at, and to a per-renderer
        /// property block.
        /// </summary>
        static void RepaintLiveHull(ToyContext context, Transform body)
        {
            var domainMaterial = DomainMaterial(context);
            if (domainMaterial)
            {
                // The authored identities are gone by now (the build already swapped them), so the
                // slots to repaint are the ones already wearing A domain ship material. Every
                // domain's material is in the theme's own set, which is the only list that can
                // answer "is this slot the domain one" after the fact.
                foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (!IsAnyDomainShipMaterial(context, materials[i])) continue;
                        materials[i] = domainMaterial;
                        changed = true;
                    }
                    if (changed) renderer.sharedMaterials = materials;
                }
            }

            VesselVisionShading.StampDisplayModel(body, DomainSignalColor(context));
        }

        static bool IsAnyDomainShipMaterial(ToyContext context, Material candidate)
        {
            if (!candidate) return false;
            var themeData = context?.GameData ? context.GameData.ThemeManagerData : null;
            var sets = themeData ? themeData.TeamMaterialSets : null;
            if (sets == null) return false;

            foreach (var pair in sets)
                if (pair.Value && pair.Value.ShipMaterial == candidate) return true;
            return false;
        }

        /// <summary>
        /// The domain SHIP material for the local player's domain — what the live ship wears, so a
        /// mini hull wears it too. Null when the theme has not populated its sets yet, which leaves
        /// the prefab's authored (jade placeholder) accent in place rather than painting nothing.
        /// </summary>
        static Material DomainMaterial(ToyContext context)
        {
            var themeData = context?.GameData ? context.GameData.ThemeManagerData : null;
            var sets = themeData ? themeData.TeamMaterialSets : null;
            if (sets == null) return null;
            return sets.TryGetValue(PlayerDomain(context), out var set) && set ? set.ShipMaterial : null;
        }

        /// <summary>
        /// The colour the vision band marks a mini hull with: the palette's DOMAIN SIGNAL colour,
        /// the same accessor <c>VesselHelper.SetShipProperties</c> stamps a real vessel with — not
        /// the toy's accent, so a previewed ship is marked in exactly the colour the real one would
        /// be. White when no theme is resolvable, so a mark can never silently become invisible.
        /// </summary>
        static Color DomainSignalColor(ToyContext context)
        {
            var themeData = context?.GameData ? context.GameData.ThemeManagerData : null;
            var colorSet = themeData ? themeData.ColorSet : null;
            return colorSet ? colorSet.GetDomainSignalColor(PlayerDomain(context)) : Color.white;
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
        /// Re-tint a FLAT (preview-material) mini hull in place — no rebuild, so the recolour is
        /// instant and pop-free. Each such model owns its own preview material, so this only
        /// affects that station; mirrors the property writes in
        /// <see cref="ToyModelBuilder.BuildPreviewMaterial"/>.
        ///
        /// <para><b>Never call this on a live hull.</b> It writes THROUGH the material, and a live
        /// hull's materials are shared project assets — it would recolour every ship in the game.
        /// Go through <see cref="ApplyDomain"/>, which dispatches.</para>
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
