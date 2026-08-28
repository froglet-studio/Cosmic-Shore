using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Plasma-claw spray: while held, scans a forward cone each tick (via
    /// PrismSpatialIndex.QuerySphere + angle filter) and ignites enemy prisms
    /// through PrismBurnManager. Free to use — no energy cost by design.
    /// Mass-5 state is sampled at IGNITE time: burns started while the upgrade is
    /// active convert (steal) on burnout even if the level later drops.
    /// </summary>
    public sealed class GrizzlyFlamethrowerActionExecutor : ShipActionExecutorBase
    {
        /// <summary>HUD: spray active.</summary>
        public event Action<bool> OnSprayChanged;

        IVesselStatus _status;
        CancellationTokenSource _sprayCts;
        readonly List<Prism> _scratch = new();
        GrizzlyClawConeVisual _cone;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            EndSpray();
        }

        void OnDisable() => EndSpray();

        public void BeginSpray(GrizzlyFlamethrowerActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;
            EndSpray();

            _sprayCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());

            // Draw the reach. The cone is built from this ability's own Range and
            // ConeHalfAngle - the same two numbers IgniteCone filters on - so the
            // visual cannot drift from what actually catches fire.
            var vesselTf = status.Transform;
            if (vesselTf != null)
            {
                _cone = GrizzlyClawConeVisual.EnsureFor(vesselTf);
                _cone?.Show(so.Range, so.ConeHalfAngle);
            }

            OnSprayChanged?.Invoke(true);
            SprayAsync(so, status, _sprayCts.Token).Forget();
        }

        public void EndSpray()
        {
            _cone?.Hide();
            if (_sprayCts == null) return;
            _sprayCts.Cancel();
            _sprayCts.Dispose();
            _sprayCts = null;
            OnSprayChanged?.Invoke(false);
        }

        async UniTaskVoid SprayAsync(GrizzlyFlamethrowerActionSO so, IVesselStatus status, CancellationToken token)
        {
            try
            {
                float tickSeconds = so.IgniteTicksPerSecond > 0f ? 1f / so.IgniteTicksPerSecond : 0.25f;
                var burn = PrismBurnManager.EnsureInstance();

                while (!token.IsCancellationRequested)
                {
                    IgniteCone(so, status, burn);
                    await UniTask.Delay(TimeSpan.FromSeconds(tickSeconds),
                        DelayType.DeltaTime, PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        void IgniteCone(GrizzlyFlamethrowerActionSO so, IVesselStatus status, PrismBurnManager burn)
        {
            var index = PrismSpatialIndex.Instance;
            var vesselTf = status?.Vessel?.Transform;
            if (index == null || burn == null || vesselTf == null) return;

            var origin = vesselTf.position;
            var forward = vesselTf.forward;

            // Sphere centered mid-cone, then angle-filter — QuerySphere is the only
            // spatial primitive and this keeps the scan cheap.
            _scratch.Clear();
            index.QuerySphere(origin + forward * (so.Range * 0.5f), so.Range * 0.6f, _scratch);

            bool convert = status.ElementalAbilityHandler != null &&
                           status.ElementalAbilityHandler.IsUpgradeActive(Element.Mass);
            float cosLimit = Mathf.Cos(so.ConeHalfAngle * Mathf.Deg2Rad);
            int ignited = 0;

            foreach (var prism in _scratch)
            {
                if (ignited >= so.IgnitesPerTick) break;
                if (prism == null || prism.destroyed) continue;
                if (prism.Domain == status.Domain) continue;

                var to = prism.transform.position - origin;
                float dist = to.magnitude;
                if (dist > so.Range || dist < 0.01f) continue;
                if (Vector3.Dot(to / dist, forward) < cosLimit) continue;

                burn.Ignite(prism, status.PlayerName, status.Domain, convert);
                ignited++;
            }
        }
    }
}
