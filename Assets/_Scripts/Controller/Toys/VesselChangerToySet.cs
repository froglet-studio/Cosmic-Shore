using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Vessel-changer toy set. Shows a collection of toys, each a mini model of a ship you can switch
    /// into (every vessel in the collection except the one you're currently flying). Flying through a
    /// toy swaps you into that ship via the existing networked
    /// <c>MenuServerPlayerVesselInitializer.RequestSwap</c>, and the toy flips to a mini model of the
    /// ship you just left.
    ///
    /// Fixes the "lost control after swap" bug: the swap pipeline drops the new vessel into autopilot
    /// with input paused, so this restores freestyle control once the swap completes (mirroring
    /// <c>MenuVesselSelectionPanelController.RestoreFreestyleAfterSwapAsync</c>).
    /// </summary>
    public class VesselChangerToySet : SwapToySetCoordinator<VesselClassType>
    {
        // Curated default so the ring isn't crowded with all 11 ships. Override per-asset via the
        // definition's collection. The current vessel is always excluded from the visible set.
        // Gibbon (internal name "Spider", enum value 12) is included so the swing vessel is
        // reachable from freestyle for playtesting.
        static readonly VesselClassType[] DefaultCollection =
        {
            VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino,
            VesselClassType.Squirrel, VesselClassType.Serpent, VesselClassType.Sparrow,
            VesselClassType.Gibbon,
        };

        const int RestoreDelayMs = 600;

        VesselClassType[] _collection;

        Domains _lastDomain;
        bool _hasDomain;

        public void SetCollection(VesselClassType[] collection) => _collection = collection;

        protected override IReadOnlyList<VesselClassType> InitialUniverse()
            => (_collection is { Length: > 0 }) ? _collection : DefaultCollection;

        protected override bool TryGetCurrent(out VesselClassType current)
        {
            current = VesselClassType.Any;
            var status = Context.GameData?.LocalPlayer?.Vessel?.VesselStatus;
            // Null or mid-swap (VesselStatus destroyed) → treat as "unknown", skip reconcile this frame.
            if (status == null || (status is UnityEngine.Object o && !o)) return false;
            current = status.VesselType;
            return true;
        }

        protected override bool IsValid(VesselClassType t) => t != VesselClassType.Any && t != VesselClassType.Random;

        protected override void Apply(VesselClassType target)
        {
            var init = Context.VesselInitializer;
            if (!init || init.IsSwapping) return;
            init.RequestSwap(target);
            RestoreControlAfterSwap(this.GetCancellationTokenOnDestroy()).Forget();
        }

        protected override void ConfigureVisual(Slot slot)
        {
            ClearChildren(slot.BodyHolder);

            var container = Context.VesselPrefabContainer;
            Color previewColor = PreviewColor();
            if (container && container.TryGetShipPrefab(slot.Option, out Transform prefab)
                && VesselModelBuilder.TryBuild(prefab, BodyRadius, previewColor, out var model))
            {
                model.transform.SetParent(slot.BodyHolder, false);
            }
            else
            {
                // Fallback when no prefab container / mesh is available.
                ToyFactory.AddSphereBody(slot.BodyHolder, BodyRadius, previewColor);
            }

            if (slot.Label)
            {
                slot.Label.text = LabelFor(slot.Option);
                slot.Label.color = previewColor;
            }
        }

        /// <summary>
        /// Colour the mini ships read as — the local player's domain colour (so they preview "you,
        /// different hull"), falling back to the toy's accent when the theme/player isn't available.
        /// </summary>
        Color PreviewColor()
        {
            var gd = Context.GameData;
            var tm = gd ? gd.ThemeManagerData : null;
            if (tm && gd.LocalPlayer != null)
                return tm.GetDomainUIColor(gd.LocalPlayer.Domain);
            return Definition.AccentColor;
        }

        // Recolour EVERY mini ship the instant the player's domain changes (via the domain-changer
        // toy or anywhere else) — not just the one slot that flips on a ship swap. ConfigureVisual
        // only runs on create/flip, so without this the other ships keep the old domain colour.
        protected override void OnTick()
        {
            var lp = Context.GameData ? Context.GameData.LocalPlayer : null;
            if (lp == null) return;

            Domains domain = lp.Domain;
            if (_hasDomain && domain == _lastDomain) return;
            _hasDomain = true;
            _lastDomain = domain;

            Color color = PreviewColor();
            ForEachSlot(s => RecolorSlot(s, color));
        }

        // Re-tint in place (no rebuild) so the recolour is instant and pop-free. Each mini model /
        // fallback sphere owns its own preview material, so tinting its renderers only affects that
        // slot; mirrors the property writes in VesselModelBuilder.BuildPreviewMaterial.
        static void RecolorSlot(Slot slot, Color color)
        {
            if (slot.BodyHolder)
            {
                foreach (var r in slot.BodyHolder.GetComponentsInChildren<Renderer>(true))
                {
                    var m = r.sharedMaterial;
                    if (!m) continue;
                    m.color = color;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 0.6f);
                }
            }
            if (slot.Label) slot.Label.color = color;
        }

        async UniTaskVoid RestoreControlAfterSwap(CancellationToken ct)
        {
            await UniTask.Delay(RestoreDelayMs, ignoreTimeScale: true, cancellationToken: ct);

            var init = Context.VesselInitializer;
            for (int i = 0; i < 20 && init && init.IsSwapping; i++)
                await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: ct);

            // Only hand control back if the player is still flying freestyle.
            if (Context.IsFreestyleActive != null && !Context.IsFreestyleActive()) return;

            var p = Context.GameData?.LocalPlayer;
            if (p?.Vessel != null)
            {
                p.Vessel.ToggleAIPilot(false);
                p.InputController.SetPause(false);
            }
        }
    }
}
