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
    /// offers hulls to BECOME (so it excludes the one you are flying), the Spawn Matrix's
    /// VESSELS branch offers hulls to RELEASE as AI companions (so it excludes nothing). The
    /// roster, the meta-value filtering, the de-duplication and the mini-hull build are identical
    /// either way, and a second copy of the curated list is a second list to forget to update
    /// when a vessel ships.
    /// </summary>
    public static class ToyVesselRoster
    {
        /// <summary>
        /// Curated default so a matrix isn't every ship in the enum (four of which are
        /// unimplemented planned classes). Override per-asset wherever a toy authors its own list.
        ///
        /// <para><b>A SHIPPING VESSEL BELONGS HERE, and this is a CODE list — so it is the one
        /// registration a vessel's setup tool cannot perform for you.</b> Every other place a new
        /// hull has to be named is an asset (<c>Vessel Prefab Container</c>,
        /// <c>DefaultNetworkPrefabs</c>, the class lists), so the editor tool that authors the
        /// vessel writes them and there is nothing to remember; this array is not, so it is
        /// exactly the one that gets missed. A hull the game can SPAWN but the changer does not
        /// OFFER is unreachable from freestyle, and nothing says so — the matrix simply has one
        /// fewer station than the fleet has ships. <c>ToyVesselRosterCoverageTests</c> is the
        /// gate: a vessel registered in the prefab container and absent from here fails the
        /// build.</para>
        ///
        /// <para>Listing a hull before its prefab is authored is safe and deliberate —
        /// <see cref="ResolveOffered"/> drops any class the prefab container cannot answer for,
        /// so a declared-but-unbuilt vessel is simply not offered yet rather than being offered
        /// as a swap that fails.</para>
        /// </summary>
        public static readonly VesselClassType[] Default =
        {
            VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino,
            VesselClassType.Squirrel, VesselClassType.Serpent, VesselClassType.Sparrow,
            VesselClassType.Urchin, VesselClassType.Scarab, VesselClassType.Butterfly,
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
        /// <see cref="Resolve"/>, then minus every class the <paramref name="context"/>'s prefab
        /// container has no prefab for. <b>This is what a toy that ACTS on a hull must call.</b>
        ///
        /// <para>It splits a declaration from an availability: <see cref="Default"/> says which
        /// hulls the fleet means to offer, and the prefab container says which ones exist on this
        /// build. Without the split, adding a vessel to the roster in the same branch that
        /// designs it — which is the only way the roster stays complete, since the prefab is
        /// authored later in the editor — would put a station in the matrix whose swap resolves
        /// to nothing (<c>"No Vessel Prefab found"</c>, three LogErrors and no vessel).</para>
        /// </summary>
        public static void ResolveOffered(ToyContext context, VesselClassType[] authored,
            List<VesselClassType> into, VesselClassType? exclude = null)
        {
            Resolve(authored, into, exclude);

            var container = context?.VesselPrefabContainer;
            if (!container) return;

            for (int i = into.Count - 1; i >= 0; i--)
            {
                if (container.TryGetShipPrefab(into[i], out _, reportMissing: false)) continue;
                WarnMissingPrefab(into[i]);
                into.RemoveAt(i);
            }
        }

        /// <summary>
        /// Say — once per class, per session — that a rostered hull was dropped for want of a
        /// prefab, and name the fix.
        ///
        /// <para><b>This warning is the whole reason the filter is safe.</b> Dropping the hull
        /// silently would reproduce the exact defect <see cref="Default"/> exists to prevent: a
        /// matrix one ship shorter than the fleet, with no error, no warning and no empty
        /// station, which is indistinguishable from a matrix that is correct. It cost a playtest
        /// to learn that the first time — <b>a filter that hides a fault is the fault wearing a
        /// deliberate face</b>, and the only thing separating "not authored yet" from "quietly
        /// broken" is that one of them says so.</para>
        ///
        /// <para>Unconditional rather than a <c>CSLogChannel</c>: a rostered hull with no prefab
        /// is a real fault every time — either its setup tool has not been run on this machine,
        /// or the prefab container lost an entry — and both are things somebody has to act on.
        /// Keyed so a matrix that rebuilds on every domain change cannot spam.</para>
        /// </summary>
        static void WarnMissingPrefab(VesselClassType vessel)
        {
            if (!_warnedMissingPrefab.Add(vessel)) return;
            CSDebug.LogWarning(
                $"[ToyVesselRoster] {vessel} is on the toybox roster but the Vessel Prefab " +
                "Container has no prefab for it, so no station is offered for it in the Vessel " +
                "Changer or the Spawn Matrix hangar. If this vessel is newly designed, run " +
                $"FrogletTools > Vessels > Create {vessel} Vessel (it authors the prefab and " +
                "registers it); otherwise the container has lost its entry.");
        }

        static readonly HashSet<VesselClassType> _warnedMissingPrefab = new();

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
            if (!container || !container.TryGetShipPrefab(vessel, out Transform prefab, reportMissing: false)) return false;
            return VesselModelBuilder.TryBuild(prefab, radius, previewColor, out model);
        }

        /// <summary>As above, painted with a material the caller owns (an emblem's shared material).</summary>
        public static bool TryBuildHull(ToyContext context, VesselClassType vessel,
            float radius, Material shared, out GameObject model)
        {
            model = null;
            var container = context?.VesselPrefabContainer;
            if (!container || !container.TryGetShipPrefab(vessel, out Transform prefab, reportMissing: false)) return false;
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
            if (!container || !container.TryGetShipPrefab(vessel, out Transform prefab, reportMissing: false)) return false;
            if (!VesselModelBuilder.TryBuildLive(prefab, radius, DomainMaterial(context), out model))
                return false;

            VesselVisionShading.StampDisplayModel(model.transform, DomainSignalColor(context));
            return true;
        }

        /// <summary>
        /// Re-apply the local player's domain to an already-built mini hull, whichever kind it is.
        ///
        /// <para>One list can hold both kinds (the Spawn Matrix's <c>_hullBodies</c> holds its
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
