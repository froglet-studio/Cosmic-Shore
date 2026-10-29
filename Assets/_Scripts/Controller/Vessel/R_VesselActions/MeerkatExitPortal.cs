using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Meerkat's Mass ability, the passive <b>exit portal</b> (Garrett's design, approved by Yash 2026-10-28;
    /// <c>R_VesselActions/MEERKAT.md</c> §10.4), ported from the page's <c>kitPortal</c>. Garrett: <i>"go minimal. we want some
    /// low mass prisms that make a cool looking exit ... a neat geometric signature."</i>
    /// <list type="bullet">
    /// <item>Once per soliton, when it forms: six prisms on a regular hexagon square to the exit heading, its corners
    /// <c>portalShare</c> (1.5) × the exit radius out, each at its edge's midpoint and laid along the edge, so the six close
    /// into one outline (<see cref="MeerkatSolitonMath.PortalPoint"/>).</item>
    /// <item>Each is the hull's own trail prism (<c>VesselPrismController</c>'s block, x halved as the fleet's single trail
    /// lays it) × Mass's <c>portalSize</c> (0.4 → 0.9), of the Meerkat's domain, owned by its pilot.</item>
    /// <item>Mass L5, <b>Danger portal</b>: they are danger prisms (a silenced Meerkat's come out plain, SILENCE.md).</item>
    /// <item>Mass is conserved: a portal is never taken back; it stays in the world like any laid prism.</item>
    /// </list>
    /// Every peer lays the same six from the same tunnel (its owner publishes the size and the danger bit with it).
    /// </summary>
    sealed class MeerkatExitPortal
    {
        // the last portal's prisms, each with the creation stamp of the life it was laid in (vessel rule 4.x: a pooled prism
        // that died and came back is a different prism)
        readonly List<Prism> _last = new();
        readonly List<float> _born = new();
        bool _warnedNoChannel;

        /// <summary>How many of the last portal's prisms still stand.</summary>
        public int Standing { get; private set; }

        /// <summary>Lays <paramref name="t"/>'s exit portal. False when nothing could be laid (no prism channel).</summary>
        public bool Lay(MeerkatSolitonKit kit, MeerkatWormholeExecutor.Tunnel t)
        {
            var status = kit.Status;
            var prisms = status?.VesselPrismController;
            var channel = prisms ? prisms.PrismSpawnChannel : null;
            if (!channel)
            {
                if (!_warnedNoChannel)
                {
                    _warnedNoChannel = true;
                    CSDebug.LogWarning($"[Meerkat] {kit.name}: the vessel has no prism spawn channel (VesselPrismController), " +
                                       "so no exit portal can be laid.");
                }
                return false;
            }
            var exit = t.At(t.L, out var tangent);
            var block = prisms.TrailBaseScale;
            var scale = new Vector3(0.5f * block.x, block.y, block.z) * Mathf.Max(0.05f, t.PortalSize);
            bool danger = (t.Kit & MeerkatSolitonKit.KitDangerPortal) != 0;
            _last.Clear();
            _born.Clear();
            for (int k = 0; k < MeerkatSolitonMath.PortalCount; k++)
            {
                var at = MeerkatSolitonMath.PortalPoint(exit, tangent, t.M, kit.Config.PortalShare, k, out var along, out var outward);
                // the block's width (x) along the edge, its thin side (y) along the exit heading, its depth (z) out
                var rotation = Quaternion.LookRotation(outward, tangent);
                var p = LayOne(channel, prisms.SpawnPrismType, at, rotation, scale, danger, status);
                if (!p) continue;
                _last.Add(p);
                _born.Add(p.prismProperties != null ? p.prismProperties.TimeCreated : 0f);
            }
            Standing = _last.Count;
            return _last.Count > 0;
        }

        // RhinoSwordAfterimage.LayOne's shape: the flags before Initialize, a stated size admitted after it
        static Prism LayOne(PrismEventChannelWithReturnSO channel, PrismType type, Vector3 position, Quaternion rotation,
            Vector3 scale, bool danger, IVesselStatus status)
        {
            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = status.Domain,
                Rotation = rotation,
                SpawnPosition = position,
                Scale = scale,
                PrismType = type,
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return null;
            prism.TargetScale = scale;
            prism.ownerID = status.PlayerName;
            prism.ChangeTeam(status.Domain);
            if (danger) prism.prismProperties.IsDangerous = true;   // Initialize runs MakeDangerous through the real pipeline
            prism.Initialize(status.PlayerName);
            prism.AdmitTargetScale(scale);   // after Initialize, whose reset re-clamps to the pool's window
            prism.TargetScale = scale;
            return prism;
        }

        /// <summary>Counts the last portal's prisms still standing (same object, same life).</summary>
        public void Prune()
        {
            int n = 0;
            for (int i = 0; i < _last.Count; i++)
            {
                var p = _last[i];
                if (p && !p.destroyed && p.prismProperties != null && p.prismProperties.TimeCreated == _born[i]) n++;
            }
            Standing = n;
        }

        public void Clear()
        {
            _last.Clear();
            _born.Clear();
            Standing = 0;
        }
    }
}
