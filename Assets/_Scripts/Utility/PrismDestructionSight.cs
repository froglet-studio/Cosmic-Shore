using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the Dolphin's Echo Sight: while a pilot holds the sight, every prism
    /// standing inside the volume their next crystal blast would sweep lights up.
    ///
    /// It publishes a handful of global shader uniforms once per frame and does nothing else.
    /// There is no per-prism work of any kind — no trigger volumes, no spatial query, no material
    /// swaps, no per-instance overrides, no tracking list. The containment test runs on the GPU in
    /// <c>PrismDestructionSight.hlsl</c>, wired into the prism graphs.
    ///
    /// <para><b>Two channels — mine, and everyone else's.</b> Every viewer has at most ONE sight of
    /// their own, which is why it stays a plain uniform set and why its code path is untouched: a
    /// prism your own cone covers is painted exactly as it was before other pilots' sights existed
    /// (proven bit-identical over the shipped shader by
    /// <c>Tools/Shaders/verify_prism_sight_composition.py</c>). Other pilots' sights ride a fixed
    /// bank of <see cref="PeerSlots"/> array slots, each carrying its holder's DOMAIN colour, so a
    /// lit patch of mass says WHO is about to take it. Your own cone always wins on any prism it
    /// covers — the instrument you are aiming with is never recoloured by a rival sweeping past.</para>
    ///
    /// <para><b>Why a global uniform and not a query.</b> "Is this prism in the blast" is
    /// camera-and-vessel-relative LIVE data: the answer changes every frame for every prism as a
    /// ship turns and its energy meter fills. So it can never be a per-prism stamp — and the
    /// clock-material law's escape hatch for exactly this case (Docs/PRISM_ANIMATION.md §1,
    /// "animation vs. live gameplay data"; §4.7, the ONE sanctioned shape for a view-dependent
    /// prism visual) is a global uniform: an O(1) write per frame that every prism reads. This is
    /// the sibling of <see cref="PrismOcclusionCorridor"/> and earns its per-frame write the same
    /// way. Adding pilots did not change that: the cost is still O(1) per frame in total, not per
    /// sight and certainly not per prism. Running <c>PrismSpatialIndex</c>'s conic sweep every
    /// frame just to tint would be the per-prism CPU pass the law exists to prevent.</para>
    ///
    /// <para><b>The volume is not re-derived here.</b> It comes from
    /// <c>VesselExplosionByCrystalEffectSO.TryResolveBlastVolume</c>, which reads the same authored
    /// scales, the same energy resource and the same Space multiplier the detonation itself uses.
    /// A sight that computed its own cone would be a lie the first time anyone retuned a scale —
    /// and a targeting aid that lies is worse than none.</para>
    ///
    /// Unlike the occlusion corridor and the speed tunnel this is NOT a platform law: it is one
    /// vessel's ability, engaged only while its trigger is held.
    /// </summary>
    public static class PrismDestructionSight
    {
        /// <summary>
        /// How many OTHER pilots' sights can be shown at once. Mirrors
        /// <c>PRISM_SIGHT_PEER_SLOTS</c> in <c>PrismDestructionSight.hlsl</c> — change both
        /// together, since the shader's arrays are declared at this length.
        ///
        /// Four is the roster of both Dolphin-only modes (Rampage and The Bends,
        /// <c>MaxPlayersAllowed 4</c>), and one of those four is the viewer, so no roster the game
        /// ships can overflow this. <see cref="Flush"/> keeps the strongest sights if one ever does.
        /// </summary>
        public const int PeerSlots = 4;

        // --- own sight (one per viewer, so plain uniforms) ---
        static readonly int ApexId = Shader.PropertyToID("_PrismSightApex");
        static readonly int AxisId = Shader.PropertyToID("_PrismSightAxis");
        static readonly int GapeId = Shader.PropertyToID("_PrismSightGape");
        static readonly int ParamsId = Shader.PropertyToID("_PrismSightParams");
        static readonly int StrengthId = Shader.PropertyToID("_PrismSightStrength");

        // --- other pilots' sights (a fixed bank of array slots) ---
        static readonly int PeerApexId = Shader.PropertyToID("_PrismSightPeerApex");
        static readonly int PeerAxisId = Shader.PropertyToID("_PrismSightPeerAxis");
        static readonly int PeerGapeId = Shader.PropertyToID("_PrismSightPeerGape");
        static readonly int PeerTintId = Shader.PropertyToID("_PrismSightPeerTint");
        static readonly int PeerCountId = Shader.PropertyToID("_PrismSightPeerCount");

        static bool _publishedActive;

        /// <summary>
        /// One other pilot's sight, as reported this frame. <see cref="Frame"/> is what makes the
        /// bank self-cleaning: an executor that stops reporting — because its vessel was destroyed,
        /// its scene unloaded, or its owner disconnected — has its slot dropped on the next flush
        /// with nothing needing to have called <see cref="ClearPeer"/>. A highlight that outlives
        /// the ship casting it is the one failure mode a registry like this actually has.
        /// </summary>
        struct PeerSight
        {
            public BlastVolume Volume;
            public float Strength;
            public Color Tint;
            public int Frame;
        }

        static readonly Dictionary<int, PeerSight> _peers = new();
        static readonly List<int> _stale = new();

        // Always sent at full length: Unity binds an array global at the length of its first write,
        // so a short write later would silently leave the tail of the previous frame's bank live.
        // Unused slots are zeroed and _PrismSightPeerCount is the real bound.
        static readonly Vector4[] _peerApex = new Vector4[PeerSlots];
        static readonly Vector4[] _peerAxis = new Vector4[PeerSlots];
        static readonly Vector4[] _peerGape = new Vector4[PeerSlots];
        static readonly Vector4[] _peerTint = new Vector4[PeerSlots];
        static int _publishedPeerCount;

        /// <summary>True while any sight — yours or another pilot's — is publishing a live volume.</summary>
        public static bool IsActive => _publishedActive || _publishedPeerCount > 0;

        // ---------------- The viewer's own sight ----------------

        /// <summary>
        /// Publish the volume the LOCAL pilot is aiming with. <paramref name="strength01"/> fades
        /// the highlight in and out so the sight never pops on — continuity of existence applies to
        /// a targeting overlay as much as to mass.
        ///
        /// Written straight through rather than through <see cref="Flush"/> because there is
        /// exactly one of these per machine and therefore no bank to arbitrate: this is the same
        /// single-writer path, and the same uniforms, the sight has always used.
        ///
        /// Called every frame by the engaged sight executor; call <see cref="ClearLocal"/> on release.
        /// </summary>
        public static void PublishLocal(in BlastVolume volume, float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (!volume.IsValid || volume.Height <= 0f || strength01 <= 0.001f)
            {
                ClearLocal();
                return;
            }

            // Three direction/point vectors plus one params vector. The scalars ride their own
            // vector rather than the others' w channels because the prism graphs carry Vector3
            // property donors and no Vector4 one — synthesising a property type neither graph
            // contains is exactly the hand-authored schema the asset-surgery protocol forbids.
            // (The peer bank below has no such constraint: its arrays are declared in the HLSL
            // itself, which is why they can pack four floats to a slot.)
            //   Params = (height, core radius per unit depth, capsule half-length per unit depth)
            //   height <= 0 is the shader's "sight off" sentinel.
            Shader.SetGlobalVector(ApexId, volume.Apex);
            Shader.SetGlobalVector(AxisId, volume.Axis);
            Shader.SetGlobalVector(GapeId, volume.GapeAxis);
            Shader.SetGlobalVector(ParamsId,
                new Vector4(volume.Height, volume.TanCorePerUnit, volume.TanGapePerUnit, 0f));

            // Its own scalar rather than Params' spare slot: a fade sharing a vector with the
            // blast's geometry reads fine today and gets misinterpreted six months from now.
            Shader.SetGlobalFloat(StrengthId, strength01);

            _publishedActive = true;
        }

        /// <summary>Turn the local sight off. Idempotent — safe to call every frame while disengaged.</summary>
        public static void ClearLocal()
        {
            if (!_publishedActive) return;
            PublishLocalOff();
        }

        // ---------------- Other pilots' sights ----------------

        /// <summary>
        /// Report that another pilot is holding their sight, and where. <paramref name="sourceId"/>
        /// identifies the reporting vessel (its executor's instance id) so one vessel can only ever
        /// occupy one slot across a swap or a re-initialise.
        ///
        /// <paramref name="tint"/> is that pilot's domain signal colour, read live rather than
        /// snapshotted — a domain change mid-flight re-colours their mark. The shader pulls it
        /// toward white before adding it, so it reads as coloured light rather than as the prism
        /// having changed team.
        ///
        /// Must be called every frame the sight is up; a slot that stops being reported is dropped
        /// by the next <see cref="Flush"/>.
        /// </summary>
        public static void PublishPeer(int sourceId, in BlastVolume volume, float strength01, Color tint)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (!volume.IsValid || volume.Height <= 0f || strength01 <= 0.001f)
            {
                ClearPeer(sourceId);
                return;
            }

            _peers[sourceId] = new PeerSight
            {
                Volume = volume,
                Strength = strength01,
                Tint = tint,
                Frame = Time.frameCount,
            };
        }

        /// <summary>
        /// Drop another pilot's sight. Idempotent, and not strictly required — the frame stamp in
        /// <see cref="PeerSight"/> collects an abandoned slot anyway — but calling it on release
        /// retires the mark on the same frame instead of the next one.
        /// </summary>
        public static void ClearPeer(int sourceId) => _peers.Remove(sourceId);

        /// <summary>
        /// Pack this frame's reported peer sights into the shader's bank. Called once per frame from
        /// <see cref="Driver"/> in LateUpdate — after every executor's Update has reported, and
        /// before anything renders.
        ///
        /// The whole cost of showing every other pilot's sight is this method: four array writes and
        /// a float, independent of how many pilots are aiming and completely independent of how many
        /// prisms are on screen.
        /// </summary>
        public static void Flush()
        {
            int frame = Time.frameCount;

            // Collect slots nobody reported this frame. Deferred into a list because the dictionary
            // cannot be mutated while it is being walked.
            _stale.Clear();
            foreach (var kv in _peers)
                if (kv.Value.Frame != frame)
                    _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
                _peers.Remove(_stale[i]);

            int count = 0;
            foreach (var kv in _peers)
            {
                var peer = kv.Value;
                if (count < PeerSlots)
                {
                    Write(count++, peer);
                    continue;
                }

                // Unreachable with any roster the game ships (see PeerSlots), but a bank that
                // silently drops whoever it happened to enumerate last would be an invisible,
                // machine-dependent difference in what each player sees. Evict the weakest instead:
                // the faintest mark is the one whose absence is least noticeable.
                int weakest = 0;
                for (int i = 1; i < PeerSlots; i++)
                    if (_peerTint[i].w < _peerTint[weakest].w)
                        weakest = i;
                if (peer.Strength > _peerTint[weakest].w)
                    Write(weakest, peer);
            }

            for (int i = count; i < PeerSlots; i++)
            {
                _peerApex[i] = _peerAxis[i] = _peerGape[i] = _peerTint[i] = Vector4.zero;
            }

            // Nothing to say and nothing said last frame: skip the writes entirely, so a match with
            // no Dolphin in it costs this system literally nothing per frame.
            if (count == 0 && _publishedPeerCount == 0) return;

            Shader.SetGlobalVectorArray(PeerApexId, _peerApex);
            Shader.SetGlobalVectorArray(PeerAxisId, _peerAxis);
            Shader.SetGlobalVectorArray(PeerGapeId, _peerGape);
            Shader.SetGlobalVectorArray(PeerTintId, _peerTint);
            Shader.SetGlobalFloat(PeerCountId, count);
            _publishedPeerCount = count;
        }

        static void Write(int slot, in PeerSight peer)
        {
            var v = peer.Volume;
            _peerApex[slot] = new Vector4(v.Apex.x, v.Apex.y, v.Apex.z, v.Height);
            _peerAxis[slot] = new Vector4(v.Axis.x, v.Axis.y, v.Axis.z, v.TanCorePerUnit);
            _peerGape[slot] = new Vector4(v.GapeAxis.x, v.GapeAxis.y, v.GapeAxis.z, v.TanGapePerUnit);
            _peerTint[slot] = new Vector4(peer.Tint.r, peer.Tint.g, peer.Tint.b, peer.Strength);
        }

        // ---------------- Lifecycle ----------------

        static void PublishLocalOff()
        {
            // Everything is zeroed so nothing stale survives into a later frame; Params.x <= 0 is
            // the sentinel the shader actually branches on.
            Shader.SetGlobalVector(ApexId, Vector4.zero);
            Shader.SetGlobalVector(AxisId, Vector4.zero);
            Shader.SetGlobalVector(GapeId, Vector4.zero);
            Shader.SetGlobalVector(ParamsId, Vector4.zero); // x <= 0 is the shader's "off" sentinel
            Shader.SetGlobalFloat(StrengthId, 0f);
            _publishedActive = false;
        }

        /// <summary>
        /// Shader globals survive play-mode exit in the editor, so a sight left engaged when play
        /// stopped would otherwise keep highlighting around a vessel that no longer exists. Publish
        /// the off state before anything renders — the same guard the occlusion corridor installs —
        /// and install the driver that flushes the peer bank.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            _publishedActive = true; // force PublishLocalOff to actually write
            PublishLocalOff();

            _peers.Clear();
            _stale.Clear();
            for (int i = 0; i < PeerSlots; i++)
                _peerApex[i] = _peerAxis[i] = _peerGape[i] = _peerTint[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(PeerApexId, _peerApex);
            Shader.SetGlobalVectorArray(PeerAxisId, _peerAxis);
            Shader.SetGlobalVectorArray(PeerGapeId, _peerGape);
            Shader.SetGlobalVectorArray(PeerTintId, _peerTint);
            Shader.SetGlobalFloat(PeerCountId, 0f);
            _publishedPeerCount = 0;

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern VesselSpeedTunnel's and the occlusion corridor's
            // publishers use.
            var go = new GameObject("[PrismDestructionSight]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// LateUpdate so the bank is packed after every sight executor's Update has reported this
        /// frame's volume, and after the vessels those volumes hang off have moved — the same
        /// reasoning as the occlusion corridor's publisher.
        /// </summary>
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Flush();

            void OnDisable()
            {
                _peers.Clear();
                Flush();
            }
        }
    }
}
