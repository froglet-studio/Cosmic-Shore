using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs the Urchin's Slip: release the trail, then phase the hull out for a moment so the
    /// vessel can clear the mass it was riding without being re-latched by it.
    ///
    /// Per-vessel state (the hull colliders, the live ghost window) lives here rather than on
    /// the shared <see cref="UrchinSlipActionSO"/> asset.
    /// </summary>
    public class UrchinSlipActionExecutor : ShipActionExecutorBase
    {
        IVesselStatus _status;
        CancellationTokenSource _cts;

        readonly List<Collider> _hullColliders = new();
        bool _ghosted;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;

            // Unconditional teardown ABOVE any pilot gate: Initialize re-runs on a LIVE
            // component when a vessel changes hands, and a ghost left running would restore
            // the previous pilot's colliders onto the new one at an arbitrary moment.
            CancelGhost(restore: true);

            // Harvested LAZILY, not here. VesselController.Initialize calls
            // ActionHandler.Initialize (which reaches this) ~50 lines BEFORE
            // Customization.Initialize, and Customization is the only writer of
            // IVesselStatus.ShipGeometries - so reading the list here always found it empty and
            // the ghost half of Slip silently never ran. Just drop the stale set; the next Slip
            // rebuilds it against a fully-initialized vessel.
            _hullColliders.Clear();
        }

        /// <summary>
        /// Resolve the hull colliders on first use. Returns false when the vessel genuinely has
        /// no geometry to phase, which is worth one warning: the detach still works, but the
        /// vessel does not go intangible, so it can re-attach immediately.
        /// </summary>
        bool EnsureHullColliders()
        {
            if (_hullColliders.Count > 0) return true;

            var geometries = _status?.ShipGeometries;
            if (geometries == null || geometries.Count == 0)
            {
                if (!_warnedNoGeometry)
                {
                    _warnedNoGeometry = true;
                    CSDebug.LogWarning(
                        $"{name}: UrchinSlipActionExecutor found no ShipGeometries - the detach " +
                        "will work but the vessel will not phase out, so it can re-attach " +
                        "immediately. Populate VesselCustomization._shipGeometries on the prefab.");
                }
                return false;
            }

            foreach (var geometry in geometries)
            {
                if (!geometry) continue;
                if (geometry.TryGetComponent(out Collider collider)) _hullColliders.Add(collider);
            }
            return _hullColliders.Count > 0;
        }

        bool _warnedNoGeometry;

        public void Slip(UrchinSlipActionSO so)
        {
            if (so == null || _status == null) return;

            bool wasAttached = _status.IsAttached;

            // Let go first, so the ghost window covers the escape rather than the ride.
            _status.IsAttached = false;
            _status.AttachedPrism = null;

            if (wasAttached && so.DetachImpulse != 0f)
                _status.Course = (_status.Course + transform.up * so.DetachImpulse).normalized;

            float ghostSeconds = so.ResolveGhostSeconds(_status);
            if (ghostSeconds <= 0f || !EnsureHullColliders()) return;

            CancelGhost(restore: true);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            GhostAsync(ghostSeconds, _cts.Token).Forget();
        }

        async UniTaskVoid GhostAsync(float seconds, CancellationToken token)
        {
            SetHullSolid(false);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime,
                                    PlayerLoopTiming.Update, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogError($"[UrchinSlipActionExecutor] Ghost error: {ex}");
            }
            finally
            {
                // A cancelled UniTask never runs its tail, so the restore MUST live in a
                // finally. Without it an interrupted slip leaves the Urchin permanently
                // intangible - a vessel that can fly through the entire prismscape, which
                // reads as a physics bug rather than as a spent ability.
                SetHullSolid(true);
            }
        }

        void CancelGhost(bool restore)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (restore) SetHullSolid(true);
        }

        void SetHullSolid(bool solid)
        {
            _ghosted = !solid;
            for (int i = 0; i < _hullColliders.Count; i++)
                if (_hullColliders[i]) _hullColliders[i].enabled = solid;
        }

        /// <summary>True while the hull is phased out — read by the HUD's Time slot.</summary>
        public bool IsGhosted => _ghosted;

        void OnDisable() => CancelGhost(restore: true);
    }
}
