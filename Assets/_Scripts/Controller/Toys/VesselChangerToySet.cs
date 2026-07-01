using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
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
        static readonly VesselClassType[] DefaultCollection =
        {
            VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino,
            VesselClassType.Squirrel, VesselClassType.Serpent, VesselClassType.Sparrow,
        };

        const int RestoreDelayMs = 600;

        VesselClassType[] _collection;

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
            if (container && container.TryGetShipPrefab(slot.Option, out Transform prefab)
                && VesselModelBuilder.TryBuild(prefab, BodyRadius, out var model))
            {
                model.transform.SetParent(slot.BodyHolder, false);
            }
            else
            {
                // Fallback when no prefab container / mesh is available.
                ToyFactory.AddSphereBody(slot.BodyHolder, BodyRadius, Definition.AccentColor);
            }

            if (slot.Label)
            {
                slot.Label.text = LabelFor(slot.Option);
                slot.Label.color = Definition.AccentColor;
            }
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
