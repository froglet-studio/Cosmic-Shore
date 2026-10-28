using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Meerkat's Charge ability, <b>Sentinel mode</b>'s sentinels (Garrett's design, approved by Yash 2026-10-28;
    /// <c>R_VesselActions/MEERKAT.md</c> §10.3), ported from the page's <c>kitAddSentinel</c>, <c>kitRemoveSentinel</c>,
    /// <c>kitSentinelsSweep</c>, <c>kitPickTarget</c>, <c>kitZap</c> and <c>kitZaps</c>.
    /// <list type="bullet">
    /// <item>Each soliton formed with Sentinel mode on leaves a sentinel just back from its way in: an AFTERIMAGE of the hull,
    /// standing up (its nose to the sky, its back to the way in). The pool is 3 (6 at Charge L5): with it used up, the oldest
    /// sentinel retires, and its soliton with it.</item>
    /// <item>A soliton with a sentinel is HELD: it stands as long as its sentinel (its stay never runs out, and a new tunnel
    /// does not collapse it).</item>
    /// <item>The guard is a sphere of <c>guardShare</c> (1.5) × the opening radius round the way in. An empty sentinel's AI zaps
    /// <c>aiZapRate</c> (5) times a second: the nearest RIVAL hull in the guard (<see cref="MeerkatSolitonKit.ZapVessel"/>), or
    /// failing that the nearest of its own domain's unshielded prisms there, which it SHIELDS (a shielded prism the soliton
    /// fires bounces instead of breaking, <c>Docs/PRISM_MOTION.md</c> 1a).</item>
    /// </list>
    /// Every peer stands the same sentinels from the same tunnels (the owner publishes whether each soliton has one); only the
    /// Meerkat's owner settles a zap's petals. Not built: a sentinel zapping another Meerkat's sentinel (the page's
    /// "sentinels zap sentinels"), the seat a person can take, and the hop - they need dynamic crew seats.
    /// </summary>
    sealed class MeerkatSentinels
    {
        sealed class Sentinel
        {
            public int Id;
            public MeerkatWormholeExecutor.Tunnel Tube;
            public Vector3 Position;
            public float Owed, IdleUntil;
            public GameObject Ghost;
        }

        readonly List<Sentinel> _live = new();
        readonly List<int> _order = new();
        readonly List<int> _retired = new();
        readonly List<Prism> _query = new();
        int _nextId;
        Mesh _ghostMesh;
        Material[] _ghostMaterials;

        public int Count => _live.Count;

        /// <summary>A sentinel stands at <paramref name="t"/>'s way in; the oldest retires while the pool is used up.</summary>
        public void Place(MeerkatSolitonKit kit, MeerkatWormholeExecutor.Tunnel t, int poolSize)
        {
            _retired.Clear();
            int id = ++_nextId;
            MeerkatSolitonMath.AddToPool(_order, id, poolSize, _retired);
            for (int i = 0; i < _retired.Count; i++)
            {
                var old = Find(_retired[i]);
                if (old != null) Remove(kit, old, collapse: true);
            }
            var s = new Sentinel { Id = id, Tube = t, Position = MeerkatSolitonMath.SentinelPosition(t.A, t.T0) };
            t.Held = true;
            s.Ghost = MakeGhost(kit, s.Position, t.T0);
            _live.Add(s);
            kit.OnSentinelPlaced(s.Position);
        }

        Sentinel Find(int id)
        {
            for (int i = 0; i < _live.Count; i++) if (_live[i].Id == id) return _live[i];
            return null;
        }

        /// <summary>A sentinel goes (retired, or its soliton gone): its soliton is released, and collapses now when
        /// <paramref name="collapse"/>.</summary>
        void Remove(MeerkatSolitonKit kit, Sentinel s, bool collapse)
        {
            _live.Remove(s);
            _order.Remove(s.Id);
            var t = s.Tube;
            if (t != null)
            {
                t.Held = false;
                if (collapse && !t.Dying) MeerkatWormholeMath.CollapseNow(ref t.W);
            }
            if (s.Ghost) Object.Destroy(s.Ghost);
            if (kit) kit.OnSentinelLost(s.Position);
        }

        /// <summary>Every sentinel goes (a vessel swap or despawn).</summary>
        public void Clear(MeerkatSolitonKit kit, bool collapse)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                if (s.Tube != null)
                {
                    s.Tube.Held = false;
                    if (collapse && !s.Tube.Dying) MeerkatWormholeMath.CollapseNow(ref s.Tube.W);
                }
                if (s.Ghost) Object.Destroy(s.Ghost);
            }
            _live.Clear();
            _order.Clear();
        }

        /// <summary>The kit is gone: the baked afterimage goes with it.</summary>
        public void DisposeGhost()
        {
            if (_ghostMesh) Object.Destroy(_ghostMesh);
            _ghostMesh = null;
            _ghostMaterials = null;
        }

        /// <summary>The page's <c>kitSentinelsSweep</c>: a sentinel whose soliton is gone or collapsing goes with it.</summary>
        public void Sweep(MeerkatSolitonKit kit)
        {
            var live = MeerkatWormholeExecutor.Live;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                var t = s.Tube;
                bool gone = t == null || t.Dying || t.W.Phase is MeerkatWormholeMath.Phase.Collapse or MeerkatWormholeMath.Phase.Gone
                            || !Contains(live, t);
                if (gone) Remove(kit, s, collapse: false);
            }
        }

        static bool Contains(IReadOnlyList<MeerkatWormholeExecutor.Tunnel> list, MeerkatWormholeExecutor.Tunnel t)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == t) return true;
            return false;
        }

        /// <summary>The page's <c>kitZaps</c>: each sentinel's AI owes <c>aiZapRate</c> zaps a second and pays them on the nearest
        /// rival in its guard, else on one of its own domain's unshielded prisms there; nothing there, it looks again in 0.2 s.</summary>
        public void Zap(MeerkatSolitonKit kit, float dt)
        {
            var cfg = kit.Config;
            var maker = kit.Status;
            float now = Time.time;
            for (int i = _live.Count - 1; i >= 0 && i < _live.Count; i--)
            {
                var s = _live[i];
                var t = s.Tube;
                if (t == null || !MeerkatSwallow.Standing(t)) continue;
                s.Owed = MeerkatSolitonMath.OweZaps(s.Owed, dt, cfg.AiZapRate);
                if (now < s.IdleUntil) continue;
                float guard = MeerkatSolitonMath.GuardRadius(cfg.GuardShare, t.M);
                while (s.Owed >= 1f)
                {
                    var rival = NearestRival(kit, t.A, guard);
                    Prism own = rival == null ? NearestOwnPlainPrism(maker, t.A, guard) : null;
                    if (rival == null && !own)
                    {
                        s.Owed = Mathf.Min(s.Owed, 0.999f);
                        s.IdleUntil = now + MeerkatSolitonMath.ZapIdleSeconds;
                        break;
                    }
                    s.Owed -= 1f;
                    if (rival != null) kit.ZapVessel(rival, s.Position);
                    else own.ActivateShield();   // shielding is never a harm: a silenced Meerkat still shields its own
                }
            }
        }

        static IVesselStatus NearestRival(MeerkatSolitonKit kit, Vector3 a, float radius)
        {
            var gameData = kit.GameData;
            var maker = kit.Status;
            if (!gameData || gameData.Players == null || maker == null) return null;
            IVesselStatus best = null;
            float bd = radius * radius;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = players[i]?.Vessel?.VesselStatus;
                if (victim == null || ReferenceEquals(victim, maker) || victim.Domain == maker.Domain || !victim.Transform) continue;
                float d = (victim.Transform.position - a).sqrMagnitude;
                if (d > bd) continue;
                bd = d;
                best = victim;
            }
            return best;
        }

        Prism NearestOwnPlainPrism(IVesselStatus maker, Vector3 a, float radius)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || maker == null) return null;
            int n = index.QuerySphere(a, radius, _query);
            Prism best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var p = _query[i];
                if (!p || p.destroyed || p.Domain != maker.Domain || !p.IsCreationComplete) continue;
                var props = p.prismProperties;
                if (props == null || props.IsShielded || props.IsSuperShielded) continue;
                float d = (p.transform.position - a).sqrMagnitude;
                if (d >= bd) continue;
                bd = d;
                best = p;
            }
            _query.Clear();
            return best;
        }

        // ------------------------------------------------------------------ the afterimage

        /// <summary>
        /// The sentinel's look (Garrett: "an afterimage standing up like a real sentinel meerkat"): one still copy of the hull
        /// as it is now, its nose to the sky and its back to the way in, at the hull's own size, drawn with the hull's own
        /// materials. One mesh per Meerkat, baked from its renderers the first time it is needed and shared by every
        /// sentinel; no collider, so it is a look and never a body.
        /// </summary>
        GameObject MakeGhost(MeerkatSolitonKit kit, Vector3 position, Vector3 t0)
        {
            var hull = kit.Status?.Transform;
            if (!hull) return null;
            if (!_ghostMesh) BakeGhost(hull);
            if (!_ghostMesh) return null;
            var up = Vector3.up - t0 * Vector3.Dot(Vector3.up, t0);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.Cross(t0, Vector3.right);
            var go = new GameObject($"MeerkatSentinel::{kit.Status.PlayerName}") { layer = hull.gameObject.layer };
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(up.normalized, -t0));
            go.transform.localScale = hull.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = _ghostMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = _ghostMaterials;
            return go;
        }

        void BakeGhost(Transform hull)
        {
            var parts = new List<CombineInstance>();
            var materials = new List<Material>();
            var toHull = hull.worldToLocalMatrix;
            foreach (var r in hull.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                Mesh mesh = null;
                bool baked = false;
                if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh)
                {
                    mesh = new Mesh();
                    skinned.BakeMesh(mesh);
                    baked = true;
                }
                else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter filter) && filter.sharedMesh)
                    mesh = filter.sharedMesh;
                if (!mesh || !mesh.isReadable) continue;
                var mats = r.sharedMaterials;
                var matrix = toHull * (baked ? Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one)
                                             : r.transform.localToWorldMatrix);
                for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
                {
                    if (!mats[sub]) continue;
                    parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = matrix });
                    materials.Add(mats[sub]);
                }
            }
            if (parts.Count == 0) return;
            _ghostMesh = new Mesh { name = "MeerkatSentinelAfterimage", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _ghostMesh.CombineMeshes(parts.ToArray(), mergeSubMeshes: false, useMatrices: true);
            _ghostMaterials = materials.ToArray();
        }
    }
}
