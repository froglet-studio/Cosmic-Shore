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
    public sealed class VesselChangerToy : MatrixToy
    {
        /// <summary>Curated default so the matrix isn't all 11 ships. Override per-asset.</summary>
        static readonly VesselClassType[] DefaultCollection =
        {
            VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino,
            VesselClassType.Squirrel, VesselClassType.Serpent, VesselClassType.Sparrow,
            VesselClassType.Urchin, VesselClassType.Scarab,
        };

        const int RestoreDelayMs = 600;

        VesselChangerToyDefinitionSO _def;

        // The ships the open matrix is showing, index-aligned with _stationBodies.
        readonly List<VesselClassType> _offered = new();
        readonly List<Transform> _stationBodies = new();

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

                var container = _toy.Context?.VesselPrefabContainer;
                if (!container || !container.TryGetShipPrefab(vessel, out Transform prefab)) return false;

                // Built UNPARENTED first: the model builder fits by world bounds and assumes an
                // origin-anchored, unrotated, unit-scale root.
                if (!VesselModelBuilder.TryBuild(prefab, radius, shared, out var model)) return false;
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
            var collection = _def && _def.VesselCollection is { Length: > 0 }
                ? _def.VesselCollection
                : DefaultCollection;

            bool hasCurrent = TryGetCurrentVessel(out var current);
            if (slot == 0)
            {
                if (!hasCurrent) return false;
                vessel = current;
                return true;
            }

            int wanted = slot - 1, seen = 0;
            foreach (var candidate in collection)
            {
                if (candidate == VesselClassType.Any || candidate == VesselClassType.Random) continue;
                if (hasCurrent && candidate == current) continue;
                if (seen++ != wanted) continue;
                vessel = candidate;
                return true;
            }
            return false;
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
            _offered.Clear();
            _stationBodies.Clear();

            var collection = _def && _def.VesselCollection is { Length: > 0 }
                ? _def.VesselCollection
                : DefaultCollection;

            TryGetCurrentVessel(out var current);
            foreach (var vessel in collection)
            {
                if (vessel == VesselClassType.Any || vessel == VesselClassType.Random) continue;
                if (vessel == current) continue;   // you are already flying it
                if (!_offered.Contains(vessel)) _offered.Add(vessel);
            }

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
            var container = Context?.VesselPrefabContainer;
            if (container && container.TryGetShipPrefab(vessel, out Transform prefab)
                && VesselModelBuilder.TryBuild(prefab, radius, previewColor, out var model))
            {
                model.transform.SetParent(body, false);
                // Only real models are recolour targets: each owns a preview material built for
                // it, so tinting its renderers is local. The fallback sphere wears ToyFactory's
                // SHARED accent material - re-tinting that would repaint every toy using it.
                _stationBodies.Add(body);
            }
            else
            {
                ToyFactory.AddSphereBody(body, radius, previewColor);
            }

            ToyFactory.AddLabel(station.transform, vessel.ToString(), previewColor, radius * 1.9f);

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
                Recolor(body, color);
        }

        /// <summary>
        /// Colour the mini ships read as - the local player's domain colour (so they preview "you,
        /// different hull"), falling back to the toy's accent when the theme/player isn't available.
        /// </summary>
        Color PreviewColor()
        {
            var gd = Context?.GameData;
            var tm = gd ? gd.ThemeManagerData : null;
            if (tm && gd.LocalPlayer != null)
                return tm.GetDomainUIColor(gd.LocalPlayer.Domain);
            return Definition.AccentColor;
        }

        // Re-tint in place (no rebuild) so the recolour is instant and pop-free. Each mini model /
        // fallback sphere owns its own preview material, so tinting its renderers only affects that
        // station; mirrors the property writes in ToyModelBuilder.BuildPreviewMaterial.
        static void Recolor(Transform body, Color color)
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
