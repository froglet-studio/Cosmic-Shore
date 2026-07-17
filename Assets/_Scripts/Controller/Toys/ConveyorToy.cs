using CosmicShore.Utility;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The microscene conveyor toy: fly through it and a belt of little worlds - prism gate runs,
    /// helix weaves, tunnels, canyons, orchards, meadows, menageries and more - starts blooming in
    /// ahead of your flight path, scene after scene, like an open world crossed with an infinite
    /// runner. The belt follows you anywhere at any speed; once the pool is full it recycles the
    /// scene farthest behind you into a fresh arrangement ahead (a closed system: the same
    /// conserved mass, endlessly re-arranged). Fly through the toy again to switch the flow OFF -
    /// the toy's body and label flip to show which way the next pass will toggle it. No score, no
    /// end condition - fly it forever.
    /// </summary>
    public class ConveyorToy : Toy
    {
        ConveyorConfig _cfg;
        MicrosceneConveyor _conveyor;
        TMP_Text _label;
        MeshRenderer _body;
        Color _accent = Color.white;

        // ── Stripped-branch conveyor mode (breadcrumb + cell power-down) ─────
        Vector3 _homePosition;
        Quaternion _homeRotation;
        Cell _dimmedCell;
        VesselPrismController _breadcrumbSource;
        float _nextTailFollow;

        public void Configure(ConveyorConfig cfg) => _cfg = cfg;

        protected override void OnInitialized()
        {
            _label = GetComponentInChildren<TMP_Text>(true);
            _body = GetComponentInChildren<MeshRenderer>(true);
            if (Definition) _accent = Definition.AccentColor;

            _homePosition = transform.position;
            _homeRotation = transform.rotation;

            // Show the "off" affordance from the start so the first pass reads as a switch.
            if (_label)
                _label.text = $"{DisplayName}\n<size=60%>fly through to start</size>";
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_cfg == null)
            {
                CSDebug.LogWarning("[ConveyorToy] No config assigned - nothing to run.");
                return;
            }
            if (localVessel?.Vessel == null) return;

            // Toggle: a pass while the belt is flowing switches it off (scenes stay in the
            // world - conserved mass and released citizens are not toy props to vanish).
            if (_conveyor && _conveyor.IsRunning)
            {
                _conveyor.StopBelt();
                ExitConveyorMode();
                ShowState(on: false);
                return;
            }

            if (_conveyor)
            {
                _conveyor.Resume(localVessel);
                EnterConveyorMode(localVessel);
                ShowState(on: true);
                return;
            }

            if (!_cfg.PrismPrefab)
                CSDebug.LogWarning("[ConveyorToy] No prism prefab wired - scenes will carry only " +
                                   "crystals and lifeforms. Author the definition asset (or run " +
                                   "Tools > Cosmic Shore > Setup Freestyle Toybox) to wire one.");

            // Sibling of the toy under the toybox root (NOT a child of the toy - the toy's root
            // scale animates on bloom/rebloom and must never scale the belt's laid mass). Still
            // torn down with the toybox root on scene exit.
            var go = new GameObject("MicrosceneConveyor");
            go.transform.SetParent(transform.parent, false);
            _conveyor = go.AddComponent<MicrosceneConveyor>();
            _conveyor.Begin(_cfg, localVessel, Context?.IsFreestyleActive, Context?.GameData);
            EnterConveyorMode(localVessel);
            ShowState(on: true);
        }

        /// <summary>
        /// Stripped-branch conveyor mode: while the belt flows, (a) the menu Cell powers down
        /// (membrane/nucleus stop rendering + ticking — a further perf win; restored on stop) and
        /// (b) the local vessel lays a capped <b>breadcrumb trail</b> whose tail this toy rides —
        /// the player can always follow their own trail back to the switch. Both explicitly
        /// requested design; the cell SetActive toggle is scoped to this mode only.
        /// </summary>
        void EnterConveyorMode(IVesselStatus localVessel)
        {
            if (!CosmicShore.Utility.PerfStrip.Enabled) return;

            // Cell off (nearest active cell — Menu_Main has exactly one). Keep the reference:
            // an inactive cell leaves the ActiveCells registry, so it can't be re-found later.
            if (!_dimmedCell)
            {
                var cell = Cell.FindNearestActiveCell(transform.position);
                if (cell)
                {
                    _dimmedCell = cell;
                    cell.gameObject.SetActive(false);
                }
            }

            // Breadcrumb on: the vessel's own (otherwise strip-disabled) trail becomes the way home.
            CosmicShore.Utility.PerfStrip.CappedTrailLimit = CosmicShore.Utility.PerfStrip.ConveyorBreadcrumbPrisms;
            CosmicShore.Utility.PerfStrip.CappedTrailActive = true;
            _breadcrumbSource = localVessel.VesselPrismController;
            if (_breadcrumbSource)
            {
                _breadcrumbSource.BreadcrumbAnchor = transform;
                _breadcrumbSource.StartSpawn();
            }
        }

        /// <summary>Belt stopped: trail stops extending (what's laid stays — conserved), the toy
        /// returns to its home placement by the cell, and the cell powers back on.</summary>
        void ExitConveyorMode()
        {
            if (!CosmicShore.Utility.PerfStrip.Enabled) return;

            CosmicShore.Utility.PerfStrip.CappedTrailActive = false;
            if (_breadcrumbSource)
            {
                _breadcrumbSource.StopSpawn();
                _breadcrumbSource.BreadcrumbAnchor = null;
                _breadcrumbSource = null;
            }

            if (_dimmedCell)
            {
                _dimmedCell.gameObject.SetActive(true);
                _dimmedCell = null;
            }

            // Home to the cell it just re-lit; the regrow bloom covers the move (continuity), and
            // the exit-gated re-arm means it cannot re-fire until the vessel flies clear.
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
        }

        /// <summary>Ride the breadcrumb's tail while the belt flows (throttled to 4Hz — the tail
        /// only moves when the cap consumes a prism). The toy IS the way home.</summary>
        protected override void Tick()
        {
            if (_conveyor == null || !_conveyor.IsRunning || _breadcrumbSource == null) return;
            if (Time.unscaledTime < _nextTailFollow) return;
            _nextTailFollow = Time.unscaledTime + 0.25f;

            if (_breadcrumbSource.TryGetBreadcrumbTail(out var tail))
                transform.position = tail;
        }

        /// <summary>
        /// Flip the toy's look so the player can read the belt state at a glance - and know the
        /// next pass toggles it the other way. ON = bright white-hot body + "flowing" label;
        /// OFF = the definition's accent + the plain name. Rebloom signals the in-place change
        /// (the established flip-set pattern).
        /// </summary>
        void ShowState(bool on)
        {
            if (_label)
                _label.text = on
                    ? $"{DisplayName}\n<size=60%>flowing - follow your trail back here to stop</size>"
                    : $"{DisplayName}\n<size=60%>fly through to start</size>";

            if (_body && _body.sharedMaterial)
                _body.sharedMaterial.color = on ? Color.Lerp(_accent, Color.white, 0.55f) : _accent;

            Rebloom();
        }
    }
}
