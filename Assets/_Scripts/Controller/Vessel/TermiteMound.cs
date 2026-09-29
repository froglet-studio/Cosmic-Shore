using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A Termite mound — a cathedral spire of the queen's own prisms that her WORKERS build.
    /// Founded by the New Mound card, fed one prism at a time by Mound Drones. Design record:
    /// <c>R_VesselActions/TERMITE.md</c> §5.4.
    ///
    /// <para><b>It is conserved mass and nothing else.</b> Every piece of a mound is an ordinary
    /// pooled prism laid through <see cref="BoostRingBuilder.LayOne"/> in the queen's domain and
    /// name — a full-size collider from its first frame, trail membership stamped after
    /// Initialize, grazed by fauna and destroyed by weapons exactly like her trail. The mound owns
    /// no health, no timer and no decay: it is ALIVE while any of its prisms stands, and it falls
    /// only when an active force has removed the last of them (the platform's "a creature dies
    /// when its last body prism is destroyed", met by a structure).</para>
    ///
    /// <para><b>One prism in, one prism out.</b> A worker's <see cref="Deposit"/> lays exactly the
    /// prisms it grazed, so a mound grows by moving mass out of other domains into the queen's —
    /// never by minting it. Only the founding seed is new mass, the same class of event as any
    /// vessel ability that places a structure (an Urchin track, a Scarab switch).</para>
    ///
    /// <para><b>Not networked; laid on every peer.</b> The New Mound press is replayed everywhere,
    /// so each machine founds the same mound at the same pose with the same slot geometry; the
    /// slots are a pure function of the pose and the budget. What can diverge is growth, because
    /// workers are simulated per peer (TERMITE.md §8).</para>
    /// </summary>
    public sealed class TermiteMound : MonoBehaviour
    {
        const int LayPerFrame = 8;

        PrismEventChannelWithReturnSO _channel;
        TermiteDeckExecutor _deck;
        Domains _domain;
        string _playerName;
        Vector3 _prismScale;
        Trail _trail;

        readonly List<Pose> _slots = new();
        Prism[] _occupant;
        int _pending;
        bool _everLaid;
        bool _fallen;
        int _serial;

        /// <summary>Where workers deliver: just outside the base, on the side they approach from.</summary>
        public Vector3 Entrance => transform.position + transform.up * 1.5f;

        /// <summary>How close a worker must come to the entrance to deliver.</summary>
        public float EntranceRadius { get; private set; } = 8f;

        /// <summary>Prisms standing in this mound right now.</summary>
        public int LivePrisms { get; private set; }

        /// <summary>Most prisms this mound can hold.</summary>
        public int Capacity => _slots.Count;

        public bool IsFallen => _fallen;

        /// <summary>
        /// Found a mound at this transform's pose (its +y is the spire's axis) and lay
        /// <paramref name="seed"/> prisms, bottom up. The slot table is built here once.
        /// </summary>
        public void Found(TermiteDeckExecutor deck, PrismEventChannelWithReturnSO channel,
                          Domains domain, string playerName, float baseRadius, float height,
                          float spacing, Vector3 prismScale, int seed)
        {
            _deck = deck;
            _channel = channel;
            _domain = domain;
            _playerName = playerName;
            _prismScale = prismScale;
            _trail = new Trail { Dimension = PrismscapeDimension.Volume };
            EntranceRadius = Mathf.Max(6f, baseRadius * 0.6f);

            BuildSlots(baseRadius, height, spacing);
            _occupant = new Prism[_slots.Count];
            _pending = Mathf.Clamp(seed, 1, _slots.Count);
        }

        /// <summary>Would the mound take <paramref name="count"/> more prisms?</summary>
        public bool HasRoomFor(int count) => !_fallen && LivePrisms + _pending + count <= _slots.Count;

        /// <summary>A worker delivers <paramref name="count"/> grazed prisms; the mound lays them.</summary>
        public void Deposit(int count)
        {
            if (_fallen || count <= 0) return;
            _pending = Mathf.Min(_pending + count, _slots.Count - LivePrisms);
        }

        /// <summary>
        /// A spire: rings of prisms stacked up the axis, narrowing with height like a termite
        /// cathedral. Ring count and ring populations are DERIVED from the spacing so neighbouring
        /// prisms sit about one spacing apart at every height. Slots are ordered bottom-up, so a
        /// mound always grows from its base and a half-built mound reads as a stump, not a crown.
        /// </summary>
        void BuildSlots(float baseRadius, float height, float spacing)
        {
            _slots.Clear();
            spacing = Mathf.Max(0.5f, spacing);
            int layers = Mathf.Max(2, Mathf.RoundToInt(height / spacing));
            for (int k = 0; k < layers; k++)
            {
                float t = k / (float)(layers - 1);
                float radius = Mathf.Max(spacing * 0.35f, baseRadius * Mathf.Pow(1f - t, 0.8f));
                int count = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * radius / spacing));
                float twist = k * 0.5f;   // stagger successive rings so seams don't line up
                for (int i = 0; i < count; i++)
                {
                    float a = (i + twist) / count * Mathf.PI * 2f;
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var local = outward * radius + Vector3.up * (t * height);
                    // Prism +z runs UP the spire (a vertical column of mass); its face turns out.
                    var rot = Quaternion.LookRotation(Vector3.up, outward);
                    _slots.Add(new Pose(local, rot));
                }
            }
        }

        void Update()
        {
            if (_fallen || _occupant == null) return;

            // Count what still stands — the identity test is "still one of MINE", because a prism
            // that died and was recycled by the pool is somebody else's mass now.
            int live = 0;
            for (int i = 0; i < _occupant.Length; i++)
            {
                var p = _occupant[i];
                if (p && !p.destroyed && p.Trail == _trail) live++;
                else _occupant[i] = null;
            }
            LivePrisms = live;

            if (_pending > 0 && _channel) LayPending();

            if (_everLaid && LivePrisms == 0 && _pending == 0) Fall();
        }

        void LayPending()
        {
            int laid = 0;
            for (int i = 0; i < _occupant.Length && _pending > 0 && laid < LayPerFrame; i++)
            {
                if (_occupant[i]) continue;
                var slot = _slots[i];
                Vector3 pos = transform.TransformPoint(slot.position);
                Quaternion rot = transform.rotation * slot.rotation;
                var prism = BoostRingBuilder.LayOne(_channel, pos, rot, _prismScale, PrismKind.Plain,
                    _domain, _playerName, $"{_playerName}::Mound::{GetInstanceID()}::{_serial++}", _trail);
                _pending--;
                laid++;
                if (!prism) continue;
                _occupant[i] = prism;
                LivePrisms++;
                _everLaid = true;
            }
            if (laid == 0) _pending = 0;   // nowhere left to put it
        }

        /// <summary>The last prism is gone: the mound is over. Its workers are re-homed by the deck.</summary>
        void Fall()
        {
            if (_fallen) return;
            _fallen = true;
            _deck?.NotifyMoundFallen(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// A turn boundary returns the mound's prisms to the pool — the same active, explicit event
        /// class as a scene load, and the Urchin track's precedent. Never called on a clock.
        /// </summary>
        public void ReturnToPool()
        {
            if (_occupant != null)
                for (int i = 0; i < _occupant.Length; i++)
                {
                    var p = _occupant[i];
                    if (!p || p.destroyed || p.Trail != _trail) continue;
                    PrismKinds.Clear(p);
                    p.ReturnToPool();
                    _occupant[i] = null;
                }
            _pending = 0;
            _fallen = true;
            Destroy(gameObject);
        }
    }
}
