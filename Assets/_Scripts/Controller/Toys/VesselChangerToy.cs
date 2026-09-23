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
    /// The vessel changer: <b>one toy that opens into the hangar</b>. Fly it and a matrix of mini
    /// ship models blooms out ahead - every vessel in the collection except the one you are
    /// currently flying. Fly a ship and you swap into it through the existing networked
    /// <c>MenuServerPlayerVesselInitializer.RequestSwap</c>; the matrix closes behind you.
    ///
    /// The models are built by <see cref="VesselModelBuilder"/> straight from the ship PREFAB
    /// ASSET (never instantiated, so no NetworkObject / VesselStatus / controllers ever run) and
    /// wear your domain colour, so each one previews "you, different hull". They re-tint in place
    /// the moment your domain changes.
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

        readonly List<Transform> _stationBodies = new();

        // The emblem's own scratch: it is rebuilt on a domain change whether the matrix is open
        // or not, so it must never borrow the base's WorldOptions (which exist only while it is).
        readonly List<VesselClassType> _emblemScratch = new();

        Domains _lastDomain;
        bool _hasDomain;

        public void Configure(VesselChangerToyDefinitionSO definition) => _def = definition;

        // ── The toy's own emblem: your hull, ringed by the hulls you could fly ──

        protected override void OnInitialized() => AttachEmblem(new EmblemSource(this), 10f);

        /// <summary>
        /// The hangar in one glyph: the CORE is the ship you are flying right now, the SATELLITES
        /// are the first three you'd be offered - the same "you are this, these are the others"
        /// reading the domain changer already ships, on a root that doesn't unfold.
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            readonly VesselChangerToy _toy;
            public EmblemSource(VesselChangerToy toy) => _toy = toy;

            public int SatelliteCount => 3;

            // The hulls are painted with the emblem's one material, so a domain change is a
            // three-write re-tint rather than a rebuild.
            public bool UsesSharedMaterial => true;

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                heavy = false;
                if (!_toy.TryGetEmblemVessel(slot, out var vessel)) return false;

                // Built UNPARENTED first: the model builder fits by world bounds and assumes an
                // origin-anchored, unrotated, unit-scale root.
                if (!ToyVesselRoster.TryBuildHull(_toy.Context, vessel, radius, shared, out var model))
                    return false;
                model.transform.SetParent(holder, false);
                return true;
            }

            public bool TryGetLiveKey(out object key)
            {
                key = null;
                // False while mid-swap (the vessel status is destroyed), so the emblem holds its
                // current hulls until the swap settles rather than rebuilding against nothing.
                if (!_toy.TryGetCurrentVessel(out var current)) return false;
                key = current;
                return true;
            }

            public bool TryGetLiveTint(out Color tint)
            {
                tint = _toy.PreviewColor();
                return true;
            }
        }

        /// <summary>
        /// Slot 0 is the vessel you're flying; slots 1..N walk the offer list (the collection minus
        /// what you fly). Recomputed per slot - it is an array walk, and it must not depend on the
        /// base's <c>WorldOptions</c>, which only exist while the matrix is open.
        /// </summary>
        bool TryGetEmblemVessel(int slot, out VesselClassType vessel)
        {
            vessel = VesselClassType.Any;

            bool hasCurrent = TryGetCurrentVessel(out var current);
            if (slot == 0)
            {
                if (!hasCurrent) return false;
                vessel = current;
                return true;
            }

            // Into a scratch list, not the base's WorldOptions: those exist only while the matrix
            // is open, and the emblem is built (and rebuilt on a domain change) whether it is or not.
            ToyVesselRoster.ResolveOffered(Context, _def ? _def.VesselCollection : null,
                _emblemScratch, hasCurrent ? current : null);

            int wanted = slot - 1;
            if (wanted < 0 || wanted >= _emblemScratch.Count) return false;
            vessel = _emblemScratch[wanted];
            return true;
        }

        // ── The one declaration ──────────────────────────────────────────────

        /// <summary>
        /// Every hull the roster offers, the one you are flying flagged as current. The matrix
        /// builds no station for that one (<see cref="WorldOmitsCurrentOption"/>) because flying
        /// it would swap your hull for itself; the window keeps the row so you can see which you
        /// are on. Same list, one declared difference in how it is shown.
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
                    Label = vessel.ToString(),
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

        /// <summary>Your own hull has no station - flying it would swap it for itself.</summary>
        protected override bool WorldOmitsCurrentOption => true;

        // ── Layout ───────────────────────────────────────────────────────────

        protected override float StationSpacing => _def.StationSpacing;
        protected override float StationRadius => Placement.BodyRadius > 0.01f ? Placement.BodyRadius : 20f;
        protected override float MatrixDistanceFactor => _def.MatrixDistanceFactor;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (!IsMatrixOpen) _stationBodies.Clear();
            base.OnActivated(localVessel);
        }

        bool TryGetCurrentVessel(out VesselClassType current)
        {
            current = VesselClassType.Any;
            var status = Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus;
            // Null or mid-swap (VesselStatus destroyed) - treat as "unknown", offer everything.
            if (status == null || (status is UnityEngine.Object o && !o)) return false;
            current = status.VesselType;
            return true;
        }

        // ── Stations: the ship, and nothing but the ship ─────────────────────

        protected override void BuildStation(ToyShellOption option, Transform parent, Vector3 position, float radius)
        {
            var vessel = (VesselClassType)option.Payload;
            var station = CreateStation(option, parent, position, vessel.ToString(), radius * 1.6f);

            var body = new GameObject("Body").transform;
            body.SetParent(station.transform, false);

            Color previewColor = PreviewColor();

            // The ACTUAL ship, wearing its own materials, marked by the vessel vision band. The
            // matrix blooms 360 units out (StationSpacing 60 x MatrixDistanceFactor 6), which is
            // just past the band's nearFullStart - so a station arrives already at full mark, reads
            // as a domain-coloured cel silhouette for the whole approach while you are choosing,
            // and resolves into the real hull over the last stretch as you commit to it. That is
            // what retired the flat silhouette fill: something else supplies the at-a-glance read
            // now, so the station can show the thing itself.
            if (ToyVesselRoster.TryBuildLiveHull(Context, vessel, radius, out var model))
            {
                model.transform.SetParent(body, false);
                // Only real models are re-tint targets. The fallback sphere wears ToyFactory's
                // SHARED accent material - re-tinting that would repaint every toy using it.
                _stationBodies.Add(body);
            }
            else
            {
                ToyFactory.AddSphereBody(body, radius, previewColor);
            }

            ToyFactory.AddRingedLabel(station.transform, vessel.ToString(), previewColor,
                StationRingRadius(radius * 1.6f), radius);
            // The action is the option's own Apply, wired by CreateStation - see MatrixToy.
        }

        void SelectVessel(VesselClassType target)
        {
            var init = Context?.VesselInitializer;
            if (!init || init.IsSwapping)
            {
                CSDebug.LogVerbose(CSLogChannel.ToyBox, "[VesselChanger] A swap is already in flight - ignoring this pass.");
                return;
            }

            // The matrix closes behind you: it was "everything except what you fly", and what you
            // fly is about to change.
            CloseMatrix();

            init.RequestSwap(target);
            RestoreControlAfterSwap(this.GetCancellationTokenOnDestroy()).Forget();

            // The cloud record's "last hull the player deliberately picked". This moved here from
            // the retired freestyle vessel-selection panel (2026-09-23), and moving it is what
            // made it RUN: that panel was inactive in the scene with no caller for its Open(), so
            // `HANGAR_DATA.SelectedVessel` had no live writer at all and
            // Docs/Analytics/DATA_ARCHITECTURE.md's "SelectedVessel gets a writer" had never been
            // true. A deliberate pick is exactly what a pass through this matrix is, and this toy
            // is now the only place in the game where one happens.
            UGSStatsManager.Instance?.ReportVesselSelected(target.ToString());

            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[VesselChanger] -> {target}.");
        }

        // ── App-shell face ───────────────────────────────────────────────────
        // There is no second option list here: MatrixToy answers the window with this toy's own
        // BuildOptions. All that is left to say is when the answer is not settled yet.

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

        // ── Domain recolour ──────────────────────────────────────────────────

        // Re-tint every mini ship the instant the player's domain changes (via the domain-changer
        // toy or anywhere else). The models are built once at open, so without this they keep the
        // colour they were born with.
        protected override void Update()
        {
            base.Update();
            if (!IsMatrixOpen) return;

            var lp = Context?.GameData ? Context.GameData.LocalPlayer : null;
            if (lp == null) return;

            Domains domain = lp.Domain;
            if (_hasDomain && domain == _lastDomain) return;
            _hasDomain = true;
            _lastDomain = domain;

            Color color = PreviewColor();
            foreach (var body in _stationBodies)
                ToyVesselRoster.ApplyDomain(Context, body, color);
        }

        /// <summary>
        /// Colour the mini ships read as - the local player's domain colour (so they preview "you,
        /// different hull"), falling back to the toy's accent when the theme/player isn't available.
        /// </summary>
        Color PreviewColor() => ToyVesselRoster.PreviewColor(Context, Definition.AccentColor);

        /// <summary>
        /// The hull for the app shell's preview window - the same LIVE model the station shows, in
        /// the ship's own materials, so a name in a list becomes a ship.
        ///
        /// <para>It is marked for the vessel vision band exactly as the station is, and that costs
        /// nothing here: the preview camera sits about three radii off the model, which is far
        /// inside the band's near cutoff, so the mark resolves to zero and what renders is the
        /// hull itself. Which is the right answer for a picture whose whole job is "what does this
        /// ship look like".</para>
        /// </summary>
        GameObject BuildShellPreview(VesselClassType vessel, Transform parent)
        {
            if (!parent) return null;
            if (!ToyVesselRoster.TryBuildLiveHull(Context, vessel, StationRadius, out var model))
                return null;

            model.transform.SetParent(parent, false);
            return model;
        }

        async UniTaskVoid RestoreControlAfterSwap(CancellationToken ct)
        {
            await UniTask.Delay(RestoreDelayMs, ignoreTimeScale: true, cancellationToken: ct);

            var init = Context?.VesselInitializer;
            for (int i = 0; i < 20 && init && init.IsSwapping; i++)
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
