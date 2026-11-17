using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The squad's launch from the Ark at the start of a voyage (Shombith, 2026-11-17: "show the intro as the vessels
    /// flying outside the ark from the tube and making the formation"). Each vessel this machine flies waits on its pad in
    /// the Ark's hangar (the doors held open), launches straight out past the jaws when the intro says so, and turns out
    /// and back into its slot beside the Ark (<see cref="ArkFormation"/>): seat 1 to port, seat 2 to starboard, seat 3 over
    /// the back. It holds them there, sailing with the Ark, until the intro hands the stick back; then each flies on from
    /// its slot at the Ark's speed. With no intro the run sets them down in their slots at once (<see cref="SetDown"/>).
    ///
    /// <para>While it holds a vessel the vessel is stationary (its own motion off), its trail is penned (no mass laid in
    /// the hangar), its colliders are off (a hull launched through its own Ark's plating would take plates off it) and an
    /// AI wingmate's AI and escort role wait. Everything is put back exactly once, however the hold ends: released,
    /// completed by a skip, or this component torn down with the voyage.</para>
    ///
    /// <para>Party voyages (Docs/Voyages/PARTY.md): a machine moves only the vessels it owns - its own pilot, and on the
    /// host the AI wingmates. A friend's vessel is launched by the friend's own machine from its own Ark; the poses travel
    /// as they always do.</para>
    /// </summary>
    [DefaultExecutionOrder(200)]   // after the Ark sails and its hull solid pushes vessels out, before the cameras
    public sealed class ArkSortie : MonoBehaviour
    {
        sealed class Member
        {
            public IVesselStatus Status;
            public Transform Hull;
            public int Seat;
            public int Order;
            public Vector3 Pad, Exit, Slot;   // the Ark's frame
            public bool WasAi;
            public ArkEscortPilot Escort;
            public bool EscortWasOn;
            public readonly List<Collider> Colliders = new();
        }

        readonly List<Member> _members = new();
        Ark _ark;
        ArkFormationSettings _settings;
        float _launchedAt = -1f;
        bool _holding;

        /// <summary>True from <see cref="Stow"/> until the vessels are released.</summary>
        public bool IsHolding => _holding;

        /// <summary>
        /// Whether this machine moves <paramref name="status"/>'s vessel: a vessel not on the network, or one this machine
        /// owns (its own pilot; on the host, the AI wingmates). Never another player's.
        /// </summary>
        public static bool Moves(IVesselStatus status)
        {
            if (status is not Component c || !c) return false;
            if (!c.TryGetComponent(out NetworkObject net) || !net.IsSpawned) return true;
            return net.IsOwner;
        }

        /// <summary>
        /// Put each vessel this machine flies of <paramref name="squad"/> (vessel and its seat) straight into its slot beside
        /// <paramref name="ark"/> (whose own extent is <paramref name="bounds"/>, <see cref="ArkFormation.LocalBounds"/>), facing its heading: the voyage's opening with no intro, and the place a straying
        /// wingmate is brought back to.
        /// </summary>
        public static void SetDown(Ark ark, IReadOnlyList<(IVesselStatus status, int seat)> squad, Bounds bounds,
                                   ArkFormationSettings settings)
        {
            if (!ark || squad == null) return;
            foreach (var (status, seat) in squad)
            {
                if (status?.Vessel == null || !status.Transform || !Moves(status)) continue;
                Repose(status, ArkFormation.SlotPose(ark, seat, bounds, settings), ark.Speed);
            }
        }

        /// <summary>
        /// Stow <paramref name="squad"/>'s vessels on the hangar's pads and hold them there with the doors open, ready
        /// for <see cref="Launch"/>. Returns the component, or null when nothing could be stowed (no Ark, no vessel this
        /// machine flies) - the caller then sets the squad down instead.
        /// </summary>
        public static ArkSortie Stow(Transform parent, Ark ark, IReadOnlyList<(IVesselStatus status, int seat)> squad,
                                     float halfLength, Bounds bounds, ArkFormationSettings settings)
        {
            if (!ark || squad == null) return null;
            var go = new GameObject("ArkSortie");
            go.transform.SetParent(parent, false);
            var sortie = go.AddComponent<ArkSortie>();
            sortie._ark = ark;
            sortie._settings = settings ?? new ArkFormationSettings();
            foreach (var (status, seat) in squad)
            {
                if (status?.Vessel == null || !status.Transform || !Moves(status)) continue;
                var m = new Member { Status = status, Hull = status.Transform, Seat = seat };
                ArkFormation.Hangar(ark, seat, halfLength, sortie._settings, out m.Pad, out m.Exit);
                m.Slot = ArkFormation.SlotLocal(seat, bounds, sortie._settings);
                sortie._members.Add(m);
            }
            if (sortie._members.Count == 0)
            {
                Destroy(go);
                return null;
            }
            // The pad nearest the doors launches first.
            sortie._members.Sort((a, b) => b.Pad.z.CompareTo(a.Pad.z));
            for (int i = 0; i < sortie._members.Count; i++)
            {
                sortie._members[i].Order = i;
                sortie.Hold(sortie._members[i]);
            }
            sortie._holding = true;
            if (ark.Model) ark.Model.HoldHangarOpen = true;
            sortie.LateUpdate();
            return sortie;
        }

        /// <summary>The squad launches now, one pad after another (the intro's launch shot).</summary>
        public void Launch()
        {
            if (_holding && _launchedAt < 0f) _launchedAt = Time.time;
        }

        /// <summary>
        /// Let every vessel go from where it is now (in its slot once the launch has run): its motion, trail, colliders and
        /// AI back, flying on at the Ark's speed. A launch the intro cut short (skipped, stopped) finishes in the slots
        /// first, so nobody is left in the hangar.
        /// </summary>
        public void Release()
        {
            if (!_holding) return;
            _holding = false;
            if (_ark && _ark.Model) _ark.Model.HoldHangarOpen = false;
            float speed = _ark ? _ark.Speed : 0f;
            foreach (var m in _members)
            {
                if (m.Status?.Vessel == null || !m.Hull) continue;
                var pose = _ark ? new Pose(_ark.transform.TransformPoint(m.Slot), _ark.transform.rotation)
                    : new Pose(m.Hull.position, m.Hull.rotation);
                if (!Landed(m) && _ark) m.Hull.SetPositionAndRotation(pose.position, pose.rotation);
                Unhold(m, speed);
            }
            _members.Clear();
            if (this) Destroy(gameObject);
        }

        bool Landed(Member m) =>
            _launchedAt >= 0f && Time.time - _launchedAt >= m.Order * _settings.LaunchStagger + _settings.LaunchSeconds;

        void OnDestroy()
        {
            // Torn down with the voyage mid-hold: hand every vessel back where it is.
            if (!_holding) return;
            _holding = false;
            if (_ark && _ark.Model) _ark.Model.HoldHangarOpen = false;
            foreach (var m in _members)
                if (m.Status?.Vessel != null && m.Hull) Unhold(m, _ark ? _ark.Speed : 0f);
            _members.Clear();
        }

        void LateUpdate()
        {
            if (!_holding) return;
            if (!_ark)
            {
                Release();
                return;
            }
            var t = _ark.transform;
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (!m.Hull) continue;
                Vector3 local;
                Quaternion facing;
                if (_launchedAt < 0f)
                {
                    local = m.Pad;
                    facing = Quaternion.identity;
                }
                else
                {
                    float into = (Time.time - _launchedAt - m.Order * _settings.LaunchStagger) / _settings.LaunchSeconds;
                    if (into <= 0f)
                    {
                        local = m.Pad;
                        facing = Quaternion.identity;
                    }
                    else
                    {
                        local = ArkFormation.LaunchPath(m.Pad, m.Exit, m.Slot, _settings.ExitShare, into, out var v);
                        float settle = Mathf.InverseLerp(_settings.ExitShare, 1f, into);
                        facing = ArkFormation.LaunchFacing(v, settle * settle);
                    }
                }
                m.Hull.SetPositionAndRotation(t.TransformPoint(local), t.rotation * facing);
            }
        }

        void Hold(Member m)
        {
            var status = m.Status;
            status.IsStationary = true;
            var pen = status.VesselPrismController;
            if (pen) pen.SetSpawnerPaused(true);
            m.Hull.GetComponentsInChildren(false, m.Colliders);
            for (int i = m.Colliders.Count - 1; i >= 0; i--)
            {
                var col = m.Colliders[i];
                if (col && col.enabled) col.enabled = false;
                else m.Colliders.RemoveAt(i);
            }
            // An AI flying it (a wingmate, or the escort sim's own seat) waits; the pilot's own stick is the run's to hold.
            m.WasAi = status.AutoPilotEnabled;
            if (m.WasAi) status.Vessel.ToggleAIPilot(false);
            if (m.Hull.TryGetComponent(out m.Escort))
            {
                m.EscortWasOn = m.Escort.enabled;
                m.Escort.enabled = false;
            }
        }

        static void Unhold(Member m, float speed)
        {
            var status = m.Status;
            foreach (var col in m.Colliders)
                if (col) col.enabled = true;
            m.Colliders.Clear();
            if (m.Escort) m.Escort.enabled = m.EscortWasOn;
            status.IsStationary = false;
            // One pose write through the vessel, so its own motion starts from here on every machine.
            Repose(status, new Pose(m.Hull.position, m.Hull.rotation), speed);
            if (m.WasAi) status.Vessel.ToggleAIPilot(true);
        }

        /// <summary>A pose write with the trail penned across it (a spawner live across a teleport lays mass bridging the
        /// jump), at <paramref name="speed"/>.</summary>
        static void Repose(IVesselStatus status, Pose pose, float speed)
        {
            var pen = status.VesselPrismController;
            if (pen) pen.SetSpawnerPaused(true);
            status.Vessel.SetPose(pose);
            status.Vessel.SetInitialSpeed(Mathf.Max(speed, status.Speed));
            if (pen) UnpenNextFrame(pen).Forget();
        }

        static async UniTaskVoid UnpenNextFrame(VesselPrismController pen)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (pen) pen.SetSpawnerPaused(false);
        }
    }
}
