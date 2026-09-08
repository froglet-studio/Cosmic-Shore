using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
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
    /// freestyle control once the swap completes (mirroring
    /// <c>MenuVesselSelectionPanelController.RestoreFreestyleAfterSwapAsync</c>).
    /// </summary>
    public sealed class VesselChangerToy : MatrixToy, IToyShellSurface
    {
        const int RestoreDelayMs = 600;

        VesselChangerToyDefinitionSO _def;

        // The ships the open matrix is showing, index-aligned with _stationBodies.
        readonly List<VesselClassType> _offered = new();
        readonly List<Transform> _stationBodies = new();
        readonly List<VesselClassType> _emblemScratch = new();

        // Its own list, never _offered or _emblemScratch: the shell can be asked at any moment,
        // including while the matrix is open (whose list is index-aligned with live stations) or
        // between two emblem slot builds.
        readonly List<VesselClassType> _shellScratch = new();

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
        /// what you fly). Recomputed per slot - it is an array walk, and it must not depend on
        /// <c>_offered</c>, which only exists while the matrix is open.
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

            // Into a scratch list, not _offered: that one only exists while the matrix is open,
            // and the emblem is built (and rebuilt on a domain change) whether it is or not.
            ToyVesselRoster.Resolve(_def ? _def.VesselCollection : null, _emblemScratch,
                hasCurrent ? current : null);

            int wanted = slot - 1;
            if (wanted < 0 || wanted >= _emblemScratch.Count) return false;
            vessel = _emblemScratch[wanted];
            return true;
        }

        // ── Layout ───────────────────────────────────────────────────────────

        protected override int StationCount => _offered.Count;
        protected override float StationSpacing => _def.StationSpacing;
        protected override float StationRadius => Placement.BodyRadius > 0.01f ? Placement.BodyRadius : 20f;
        protected override float MatrixDistanceFactor => _def.MatrixDistanceFactor;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (IsMatrixOpen)
            {
                CloseMatrix();
                return;
            }

            // Resolve what to offer BEFORE the base opens - StationCount reads from it.
            if (!ResolveOffer()) return;
            base.OnActivated(localVessel);
        }

        bool ResolveOffer()
        {
            _stationBodies.Clear();

            // You are already flying one of them, so that hull is not on offer here.
            bool hasCurrent = TryGetCurrentVessel(out var current);
            ToyVesselRoster.Resolve(_def ? _def.VesselCollection : null, _offered,
                hasCurrent ? current : null);

            if (_offered.Count != 0) return true;
            CSDebug.LogWarning("[VesselChanger] No other vessels to offer.");
            return false;
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

        protected override void BuildStation(int index, Transform parent, Vector3 position, float radius)
        {
            var vessel = _offered[index];
            var station = CreateStation(parent, position, vessel.ToString(), radius * 1.6f);

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

            var captured = vessel;
            station.OnVesselPassed = () => SelectVessel(captured);
        }

        void SelectVessel(VesselClassType target)
        {
            var init = Context?.VesselInitializer;
            if (!init || init.IsSwapping)
            {
                CSDebug.Log("[VesselChanger] A swap is already in flight - ignoring this pass.");
                return;
            }

            // The matrix closes behind you: it was "everything except what you fly", and what you
            // fly is about to change.
            CloseMatrix();

            init.RequestSwap(target);
            RestoreControlAfterSwap(this.GetCancellationTokenOnDestroy()).Forget();
            CSDebug.Log($"[VesselChanger] → {target}.");
        }

        // ── App-shell face ───────────────────────────────────────────────────

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        // A swap in flight has no settled answer to "what are you flying", so the card greys out
        // rather than offering a hull against a hull that no longer exists.
        bool IToyShellSurface.ShellAvailable
        {
            get
            {
                var init = Context?.VesselInitializer;
                return init != null && !init.IsSwapping;
            }
        }

        /// <summary>
        /// The whole collection, the hull you fly flagged as current - not the matrix's
        /// "everything except what you fly". The matrix says that by having no station for your
        /// own ship; a flat list has to name it, and dropping the row would leave the player
        /// unable to see which hull they are on.
        /// </summary>
        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into)
        {
            bool hasCurrent = TryGetCurrentVessel(out var current);
            ToyVesselRoster.Resolve(_def ? _def.VesselCollection : null, _shellScratch, exclude: null);

            Color accent = PreviewColor();

            foreach (var vessel in _shellScratch)
            {
                var captured = vessel;
                bool isCurrent = hasCurrent && vessel == current;

                // No Apply on the hull you are already flying: that row is there to be READ.
                System.Action apply = null;
                if (!isCurrent) apply = () => SelectVessel(captured);

                into.Add(new ToyShellOption
                {
                    Label = vessel.ToString(),
                    Detail = isCurrent ? "flying" : "",
                    Accent = accent,
                    IsCurrent = isCurrent,
                    Apply = apply,
                });
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
