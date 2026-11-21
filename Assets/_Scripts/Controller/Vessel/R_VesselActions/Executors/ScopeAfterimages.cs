using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// <b>Hunter's Sight</b>, the Serpent's Charge-5 upgrade (Garrett, 2026-10-30: "it causes the
    /// scope to produce short lived after images that the serpent can snipe instead. (much like
    /// hawkeye in marvel rivals)"). While the scope is up, every rival in range leaves an
    /// afterimage - its hull frozen where it stood - every <see cref="SniperScopeActionSO.AfterimageInterval"/>
    /// seconds, and each one stands for <see cref="SniperScopeActionSO.AfterimageLifetime"/>. A
    /// sniper round whose cone contains a live afterimage strips the pilot it was taken from, exactly
    /// as if it had hit them (<see cref="SniperShotActionExecutor"/>), and the afterimage shatters.
    ///
    /// <para><b>Where it runs.</b> Only on the machine that decides the sniper's strip
    /// (<see cref="ElementalTransfer.IsDecidedHere"/>: the Serpent's owner, or the server for an AI),
    /// because only that machine's answer counts and each peer's history of a rival differs by its
    /// lag. The ghosts are drawn only for the local pilot; an AI Serpent records them and draws
    /// nothing. Nothing here replicates.</para>
    ///
    /// <para><b>The ghost</b> is the rival's hull baked into a mesh (the cloak's illusion does the
    /// same, <see cref="CloakSeedWallActionExecutor"/>) in the afterimage material, tinted with the
    /// RIVAL's domain colour and fading over its life. Each slot owns its mesh and is reused, so a
    /// held scope allocates nothing after its first second.</para>
    /// </summary>
    public sealed class ScopeAfterimages
    {
        static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        sealed class Slot
        {
            public IVesselStatus Owner;
            public Vector3 Position;
            public float BornAt;
            public bool Live;
            public GameObject Ghost;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Material Material;
        }

        readonly List<Slot> _slots = new();
        readonly List<Transform> _roster = new();
        readonly Dictionary<Transform, float> _lastStamp = new();
        readonly Dictionary<Transform, SkinnedMeshRenderer> _skins = new();
        GameDataSO _gameData;

        /// <summary>Live afterimages right now (recorded on this machine).</summary>
        public int LiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Count; i++) if (_slots[i].Live) n++;
                return n;
            }
        }

        public void Tick(IVesselStatus serpent, SniperScopeActionSO so, bool draw, GameDataSO gameData = null)
        {
            if (serpent == null || so == null) return;
            if (gameData) _gameData = gameData;
            float now = Time.time;

            // Age out first, so a slot freed this frame can be reused by a stamp below.
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.Live) continue;
                float age01 = (now - slot.BornAt) / Mathf.Max(0.0001f, so.AfterimageLifetime);
                if (age01 >= 1f || slot.Owner == null) { Retire(slot); continue; }
                if (slot.Material) slot.Material.SetFloat(FadeId, 1f - age01);
            }

            var hull = serpent.ShipTransform;
            if (hull == null) return;
            float rangeSqr = so.AfterimageRange * so.AfterimageRange;

            VesselVisionShading.CollectStampedVessels(_roster);
            for (int i = 0; i < _roster.Count; i++)
            {
                var candidate = _roster[i];
                if (candidate == null) continue;
                if (_lastStamp.TryGetValue(candidate, out float last) && now - last < so.AfterimageInterval) continue;

                var component = candidate.GetComponentInChildren<VesselStatus>();
                if (component == null) continue;
                IVesselStatus rival = component;
                if (ReferenceEquals(rival, serpent) || rival.Domain == serpent.Domain) continue;
                if ((component.transform.position - hull.position).sqrMagnitude > rangeSqr) continue;

                _lastStamp[candidate] = now;
                Stamp(candidate, rival, component.transform, so, draw, now);
            }
            _roster.Clear();
        }

        /// <summary>
        /// The rivals whose live afterimages stand inside the round's cone, each once; the afterimages
        /// hit are shattered. The same predicate the round applies to prisms and pilots
        /// (<see cref="PrismSpatialIndex.ConeContains"/>), so one cone answers for all three.
        /// </summary>
        public void CollectHits(Vector3 origin, Vector3 direction, float reach, float tanHalf, float minRadius,
                                List<IVesselStatus> into)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.Live || slot.Owner == null) continue;
                if (!PrismSpatialIndex.ConeContains(slot.Position, origin, direction, reach, tanHalf, minRadius))
                    continue;
                if (!into.Contains(slot.Owner)) into.Add(slot.Owner);
                Retire(slot);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Count; i++) Retire(_slots[i]);
            _lastStamp.Clear();
        }

        public void Dispose()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Ghost) Object.Destroy(slot.Ghost);
                if (slot.Mesh) Object.Destroy(slot.Mesh);
                if (slot.Material) Object.Destroy(slot.Material);
            }
            _slots.Clear();
            _lastStamp.Clear();
            _skins.Clear();
        }

        void Stamp(Transform vessel, IVesselStatus rival, Transform at, SniperScopeActionSO so, bool draw, float now)
        {
            var slot = FreeSlot();
            slot.Owner = rival;
            slot.Position = at.position;
            slot.BornAt = now;
            slot.Live = true;

            if (!draw || !so.AfterimageMaterial) return;
            var skin = SkinOf(vessel);
            if (skin == null) return;

            if (!slot.Ghost)
            {
                slot.Ghost = new GameObject("ScopeAfterimage");
                slot.Mesh = new Mesh { name = "ScopeAfterimage" };
                slot.Ghost.AddComponent<MeshFilter>().sharedMesh = slot.Mesh;
                slot.Renderer = slot.Ghost.AddComponent<MeshRenderer>();
            }
            if (!slot.Material || slot.Material.shader != so.AfterimageMaterial.shader)
            {
                if (slot.Material) Object.Destroy(slot.Material);
                slot.Material = new Material(so.AfterimageMaterial) { name = so.AfterimageMaterial.name + " (Afterimage)" };
            }

            skin.BakeMesh(slot.Mesh, true);
            int subs = Mathf.Max(1, slot.Mesh.subMeshCount);
            var mats = slot.Renderer.sharedMaterials;
            if (mats.Length != subs) mats = new Material[subs];
            for (int i = 0; i < subs; i++) mats[i] = slot.Material;
            slot.Renderer.sharedMaterials = mats;

            var colorSet = _gameData ? _gameData.ThemeManagerData?.ColorSet : null;
            if (colorSet != null) slot.Material.SetColor(GhostColorId, colorSet.GetDomainSignalColor(rival.Domain));
            slot.Material.SetFloat(FadeId, 1f);

            var t = skin.transform;
            slot.Ghost.transform.SetPositionAndRotation(t.position, t.rotation);
            slot.Ghost.transform.localScale = t.lossyScale;
            slot.Ghost.SetActive(true);
        }

        SkinnedMeshRenderer SkinOf(Transform vessel)
        {
            if (_skins.TryGetValue(vessel, out var skin) && skin) return skin;
            skin = vessel.GetComponentInChildren<SkinnedMeshRenderer>();
            _skins[vessel] = skin;
            return skin;
        }

        Slot FreeSlot()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (!_slots[i].Live) return _slots[i];
            var slot = new Slot();
            _slots.Add(slot);
            return slot;
        }

        static void Retire(Slot slot)
        {
            slot.Live = false;
            slot.Owner = null;
            if (slot.Ghost) slot.Ghost.SetActive(false);
        }
    }
}
