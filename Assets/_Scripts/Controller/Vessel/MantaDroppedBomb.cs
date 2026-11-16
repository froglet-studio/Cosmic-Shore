using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure motion and contact math of a dropped Manta bomb, kept apart so edit-mode tests can hold it.
    /// </summary>
    public static class MantaDropMath
    {
        /// <summary>
        /// One step of a dropped bomb: it keeps the velocity it left the Manta with and falls along the Manta's down at
        /// the moment of the drop (<paramref name="down"/>, unit) at <paramref name="fallAcceleration"/>.
        /// </summary>
        public static void Step(ref Vector3 position, ref Vector3 velocity, Vector3 down, float fallAcceleration, float dt)
        {
            velocity += down * (fallAcceleration * dt);
            position += velocity * dt;
        }

        /// <summary>Shortest distance from <paramref name="point"/> to the segment <paramref name="a"/>-<paramref name="b"/>.</summary>
        public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return Vector3.Distance(a, point);
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / len2);
            return Vector3.Distance(a + ab * t, point);
        }

        /// <summary>The point on the segment closest to <paramref name="point"/> - where a contact is placed.</summary>
        public static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(point - a, ab) / len2);
        }
    }

    /// <summary>
    /// A bomb the Manta let go of (Caleb, 2026-11-16): it leaves with the Manta's velocity, falls along the Manta's own
    /// down as it was at the drop, and goes off on the FIRST vessel or prism it touches. It has no fuse and is on no
    /// fuse: the shared board neither waits for it nor resets for it. One at a time (<see cref="MantaStingConfigSO.MaxDroppedBombs"/>).
    ///
    /// <para>The bomb itself is an ordinary <see cref="MantaBomb"/> on this object (so it blooms, relays, spreads
    /// Contagion and is set off by other blasts exactly as a planted one); this component only moves it and looks for
    /// the contact. It sweeps the segment it flew this frame, so a bomb at Soar speed cannot step through a prism.
    /// The dropper is ignored for <see cref="MantaStingConfigSO.DropArmSeconds"/>, which keeps it from going off on
    /// the hull it just left. One that touches nothing for <see cref="MantaStingConfigSO.DropMaxSeconds"/> fizzles
    /// without a bloom: a safety limit, not a fuse.</para>
    ///
    /// Local, like every Manta bomb: it exists only on the machine that simulates its planter, and its bloom reaches
    /// peers through <see cref="MantaBombNetworkRelay"/>.
    /// </summary>
    public sealed class MantaDroppedBomb : MonoBehaviour
    {
        static readonly List<Prism> s_prisms = new();

        MantaBomb _bomb;
        MantaStingConfigSO _config;
        GameDataSO _gameData;
        Transform _dropper;
        Vector3 _velocity;
        Vector3 _down;
        float _bornAt;
        bool _localHuman;
        bool _done;

        /// <summary>The bomb this object carries (null once it has gone off or fizzled).</summary>
        public MantaBomb Bomb => _bomb;

        /// <summary>
        /// Lets a bomb go at <paramref name="position"/> with <paramref name="velocity"/>, falling along
        /// <paramref name="down"/>. Returns null when the bomb could not be made.
        /// </summary>
        public static MantaDroppedBomb Spawn(Vector3 position, Vector3 velocity, Vector3 down, Transform dropper,
                                             in MantaBombSnapshot snapshot)
        {
            if (snapshot.Config == null) return null;

            var go = new GameObject("MantaDroppedBomb");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(
                velocity.sqrMagnitude > 1e-4f ? velocity : Vector3.forward,
                down.sqrMagnitude > 1e-4f ? -down : Vector3.up));

            var mover = go.AddComponent<MantaDroppedBomb>();
            mover._config = snapshot.Config;
            mover._gameData = snapshot.GameData;
            mover._dropper = dropper;
            mover._velocity = velocity;
            mover._down = down.sqrMagnitude > 1e-4f ? down.normalized : Vector3.zero;
            mover._bornAt = Time.time;
            mover._localHuman = snapshot.LocalHumanPlanter;

            mover._bomb = MantaBomb.Drop(go, snapshot);
            if (!mover._bomb)
            {
                Destroy(go);
                return null;
            }
            return mover;
        }

        void Update()
        {
            if (_done) return;
            if (!_bomb)
            {
                // Set off by another blast (MantaBlastField), or shed: the bomb is gone, so is its body.
                Finish();
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Time.time - _bornAt >= _config.DropMaxSeconds)
            {
                _bomb.Shed();
                Finish();
                return;
            }

            Vector3 from = transform.position;
            Vector3 to = from;
            MantaDropMath.Step(ref to, ref _velocity, _down, _config.DropFallAcceleration, dt);

            if (TryFindContact(from, to, out Vector3 contact))
            {
                transform.position = contact;
                PlayContactCue();
                var bomb = _bomb;
                _bomb = null;
                bomb.Detonate(_config.DropBlastScale, _config.BloomGrowSeconds, MantaDetonation.Dropped);
                Finish();
                return;
            }

            transform.position = to;
        }

        bool TryFindContact(Vector3 from, Vector3 to, out Vector3 contact)
        {
            contact = to;
            float best = float.MaxValue;
            bool found = false;

            // Prisms: any prism, any domain (Caleb: "upon contact with any vessels or prism").
            var index = PrismSpatialIndex.Instance;
            if (index != null)
            {
                float prismReach = _config.DropPrismContactRadius;
                Vector3 mid = (from + to) * 0.5f;
                float queryRadius = Vector3.Distance(from, to) * 0.5f + prismReach;
                s_prisms.Clear();
                index.QuerySphere(mid, queryRadius, s_prisms);
                for (int i = 0; i < s_prisms.Count; i++)
                {
                    var p = s_prisms[i];
                    if (!p || p.destroyed) continue;
                    Vector3 pos = p.transform.position;
                    if (MantaDropMath.SegmentDistance(from, to, pos) > prismReach) continue;
                    Vector3 at = MantaDropMath.ClosestOnSegment(from, to, pos);
                    float along = (at - from).sqrMagnitude;
                    if (along < best) { best = along; contact = at; found = true; }
                }
                s_prisms.Clear();
            }

            // Vessels: every hull in the match, the dropper too once the bomb has armed.
            var players = _gameData ? _gameData.Players : null;
            if (players != null)
            {
                bool armed = Time.time - _bornAt >= _config.DropArmSeconds;
                float vesselReach = _config.DropVesselContactRadius;
                for (int i = 0; i < players.Count; i++)
                {
                    var t = players[i]?.Vessel?.Transform;
                    if (!t || !t.gameObject.activeInHierarchy) continue;
                    if (!armed && t == _dropper) continue;
                    Vector3 pos = t.position;
                    if (MantaDropMath.SegmentDistance(from, to, pos) > vesselReach) continue;
                    Vector3 at = MantaDropMath.ClosestOnSegment(from, to, pos);
                    float along = (at - from).sqrMagnitude;
                    if (along < best) { best = along; contact = at; found = true; }
                }
            }

            return found;
        }

        void PlayContactCue()
        {
            var reference = _config.DroppedBombContactEvent;
            if (!_localHuman || reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        void Finish()
        {
            _done = true;
            Destroy(gameObject);
        }
    }
}
