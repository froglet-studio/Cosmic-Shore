using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The vessel changer: the hangar, in the Toy Box. It lists every vessel in the collection,
    /// the one you fly flagged; picking one swaps you into it through the existing networked
    /// <c>MenuServerPlayerVesselInitializer.RequestSwap</c>. Each row previews the live hull
    /// (<see cref="ToyVesselRoster"/>).
    ///
    /// The swap pipeline drops the new vessel into autopilot with input paused, so this restores
    /// freestyle control once the swap completes. (It used to say it mirrored the freestyle HUD's
    /// vessel-selection panel; that panel was retired 2026-09-23 after measuring that it was
    /// inactive in the scene with no caller for its <c>Open()</c> - this toy is the only thing
    /// that has actually restored freestyle control for some time.)
    /// </summary>
    public sealed class VesselChangerToy : MatrixToy
    {
        const int RestoreDelayMs = 600;

        VesselChangerToyDefinitionSO _def;

        public void Configure(VesselChangerToyDefinitionSO definition) => _def = definition;

        // ── The one declaration ──────────────────────────────────────────────

        /// <summary>
        /// Every hull the roster offers, the one you are flying flagged as current (listed, with no
        /// Apply, so you can see which you are on).
        /// </summary>
        protected override void BuildOptions(List<ToyShellOption> into)
        {
            bool hasCurrent = TryGetCurrentVessel(out var current);
            ToyVesselRoster.ResolveOffered(Context, _def ? _def.VesselCollection : null,
                _shellScratch, exclude: null);

            Color accent = PreviewColor();

            foreach (var vessel in _shellScratch)
            {
                var captured = vessel;
                bool isCurrent = hasCurrent && vessel == current;

                into.Add(new ToyShellOption
                {
                    Label = LabelOf(captured),
                    Detail = isCurrent ? "flying" : "",
                    Accent = accent,
                    IsCurrent = isCurrent,
                    Payload = captured,
                    // No Apply on the hull you are already flying: that row is there to be READ.
                    Apply = isCurrent ? null : () => SelectVessel(captured),
                    BuildPreview = parent => BuildShellPreview(captured, parent),
                });
            }
        }

        readonly List<VesselClassType> _shellScratch = new();

        /// <summary>
        /// A row's name: its class asset's card name (a crew vessel reads "Wrasse and Grouper"), else the class
        /// name. A probe on the definition's class list, quiet on a miss.
        /// </summary>
        string LabelOf(VesselClassType vessel) =>
            _def && _def.VesselArt && _def.VesselArt.TryGetVesselByClass(vessel, out var so, reportMissing: false)
            && !string.IsNullOrWhiteSpace(so.CardName)
                ? so.CardName
                : vessel.ToString();

        bool TryGetCurrentVessel(out VesselClassType current)
        {
            current = VesselClassType.Any;
            var status = Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus;
            // Null or mid-swap (VesselStatus destroyed) - treat as "unknown", offer everything.
            if (status == null || (status is UnityEngine.Object o && !o)) return false;
            current = status.VesselType;
            return true;
        }

        void SelectVessel(VesselClassType target)
        {
            var init = Context?.VesselInitializer;
            if (!init || init.IsSwapping)
            {
                CSDebug.LogVerbose(CSLogChannel.ToyBox, "[VesselChanger] A swap is already in flight - ignoring this pass.");
                return;
            }

            init.RequestSwap(target);
            RestoreControlAfterSwap(this.GetCancellationTokenOnDestroy()).Forget();

            // The cloud record's "last hull the player deliberately picked". This moved here from
            // the retired freestyle vessel-selection panel (2026-09-23), and moving it is what
            // made it RUN: that panel was inactive in the scene with no caller for its Open(), so
            // `HANGAR_DATA.SelectedVessel` had no live writer at all and
            // Docs/Analytics/DATA_ARCHITECTURE.md's "SelectedVessel gets a writer" had never been
            // true. A deliberate pick is exactly what this is.
            UGSStatsManager.Instance?.ReportVesselSelected(target.ToString());

            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[VesselChanger] -> {target}.");
        }

        // ── App-shell face ───────────────────────────────────────────────────

        /// <summary>
        /// A swap in flight has no settled answer to "what are you flying", so the card greys out
        /// rather than offering a hull against a hull that no longer exists.
        /// </summary>
        protected override bool ShellAvailable
        {
            get
            {
                var init = Context?.VesselInitializer;
                return init != null && !init.IsSwapping;
            }
        }

        /// <summary>
        /// Colour the rows read as - the local player's domain colour (so they preview "you,
        /// different hull"), falling back to the toy's accent when the theme/player isn't available.
        /// </summary>
        Color PreviewColor() => ToyVesselRoster.PreviewColor(Context, Definition.AccentColor);

        /// <summary>
        /// The hull for the app shell's preview window - the LIVE model, in the ship's own
        /// materials, so a name in a list becomes a ship.
        /// </summary>
        const float PreviewRadius = 20f;

        GameObject BuildShellPreview(VesselClassType vessel, Transform parent)
        {
            if (!parent) return null;
            if (!ToyVesselRoster.TryBuildLiveHull(Context, vessel, PreviewRadius, out var model))
                return null;

            model.transform.SetParent(parent, false);
            return model;
        }

        async UniTaskVoid RestoreControlAfterSwap(CancellationToken ct)
        {
            await UniTask.Delay(RestoreDelayMs, ignoreTimeScale: true, cancellationToken: ct);

            // Wait out the WHOLE swap. This used to give up after 20 x 100 ms, but a party
            // guest's swap latch runs until the new hull is bound (up to its 5 s deadline) and a
            // relay round trip can outlast 2.6 s - restoring early found the old hull destroyed and
            // did nothing, and the swapped-in ship then stayed on autopilot under a pilot who was
            // flying. IsSwapping is bounded on both sides (the server's finally, the client's
            // deadline), so this cannot hang.
            var init = Context?.VesselInitializer;
            while (init && init.IsSwapping)
                await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: ct);

            // Only hand control back if the player is still flying freestyle.
            if (Context?.IsFreestyleActive != null && !Context.IsFreestyleActive()) return;

            // IVessel is an INTERFACE, so `!= null` never reaches UnityEngine.Object's overload:
            // a vessel destroyed during the swap sails through it and throws
            // MissingReferenceException inside ToggleAIPilot. That fires on every FAILED swap,
            // where the outgoing hull is already gone and no incoming one arrived — which is
            // exactly when this path runs. VesselLiveness is the shared guard.
            var p = Context?.GameData?.LocalPlayer;
            if (p != null && p.Vessel.IsAlive())
            {
                p.Vessel.ToggleAIPilot(false);
                p.InputController?.SetPause(false);
            }
        }
    }
}
