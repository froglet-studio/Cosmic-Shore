using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;
using V3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Orca crew's seat AIs - the helm's shield, the bomb seat's burst and the gunners' aim, missile steering and
    /// target choice (the lab's helmAI / aiBurst / gunnerAI / bestTarget). Inputs only, like every seat AI. A part of
    /// <see cref="OrcaCrew"/> (a partial class, so these keep their access to the crew's state) that lives here because
    /// every hull's AI decision code lives under <c>Controller/AI/&lt;Hull&gt;/</c> (Docs/AI_SYSTEM/ARCHITECTURE.md
    /// 4.3). Moved verbatim from OrcaCrew.cs 2026-10-25.
    /// </summary>
    public sealed partial class OrcaCrew
    {
        // The lab's helmAI shield: raise it when a rival missile is closing inside the range this skill notices.
        bool HelmAIWantsShield()
        {
            if (Time.time < _shieldReadyAt) return false;
            var hullPos = N(transform.position);
            foreach (var other in Live)
            {
                if (other == this || other._status == null || other._status.Domain == _status.Domain) continue;
                for (int s = 1; s < Seats; s++)
                {
                    var m = other._missiles[s];
                    if (m != null && OrcaCrewCore.IsIncoming(hullPos, m.Position, m.Frame.F, _skill.ShieldLate)
                        && _rng.NextDouble() > _skill.ShieldLate * 0.2f)
                        return true;
                }
            }
            return false;
        }

        // The bomb seat's AI bursts its bomb (the lab's aiBurst): a rival hull inside the blast, the bomb at its target, or
        // past it - a bomb nobody bursts would hold the seat's only throw.
        bool AIBurstsBomb()
        {
            var at = _bomb.transform.position;
            float radius = _cfg.BombBlastRadiusAt(_status);
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
                foreach (var player in players)
                {
                    var vessel = player?.Vessel;
                    var st = vessel?.VesselStatus;
                    if (player == _player || st == null || !UnityLiveness.Alive(vessel.Transform) || st.Domain == _status.Domain) continue;
                    if (Vector3.Distance(vessel.Transform.position, at) < radius * _skill.Detonate + _s.HullRadius) return true;
                }
            var tg = _targets[BombSeat];
            if (tg.Kind == TargetKind.None) return _bombAge > 2f;
            float d = Vector3.Distance(at, Refresh(tg).Position);
            bool passed = _bombLastD >= 0f && d > _bombLastD + 0.01f;
            _bombLastD = d;
            return d < radius * 0.5f || passed || _bombAge > 4f;
        }

        // ------------------------------------------------------------------ AI gunners (the lab's gunnerAI, inputs only)

        // Round 5: the gun when the target is in its range and it is ready, else a missile (always for a crystal, which only a
        // missile collects). Returns whether to launch a missile; gunHold whether to fire the gun.
        bool TurretAI(int s, OrcaTurret t, float dt, out bool gunHold)
        {
            gunHold = false;
            var mount = OrcaCrewCore.MountPosition(N(transform.position), _pod, s, _s.HullRadius);
            float reach = _s.MissileSpeed * _s.MissileLife * 0.8f, gr = _cfg.GunsOn ? GunRange(s) : 0f;
            var tg = TargetFor(s, U(mount), Mathf.Max(reach, gr), _mags[s].Count);
            if (tg.Kind == TargetKind.None) { t.React = 0f; return false; }
            if (_rng.NextDouble() < 1.5 * dt)   // the lab re-rolls its aim error 5% of its 30 Hz steps; a sniper steadies its aim
                t.Noise = RandDir() * (Gauss() * _skill.AimNoiseDeg * OrcaCrewCore.Deg2Rad * (s == SniperSeat ? 0.4f : 1f));
            float dm = Vector3.Distance(U(mount), tg.Position);
            bool crystal = tg.Kind == TargetKind.Omni || tg.Kind == TargetKind.Crystal;
            bool useGun = _cfg.GunsOn && !crystal && dm < gr && GunReady(s);
            float speed = useGun ? (s == FastGunSeat ? _cfg.FastGunRoundSpeed : s == BombSeat ? _cfg.BombSpeed : 1e9f) : _s.MissileSpeed;
            var aim = OrcaCrewCore.LeadAim(mount, N(transform.position), N(tg.Position), N(tg.Velocity), speed, _skill.Lead, t.Noise);
            t.React += dt;
            float tol = useGun && s == SniperSeat ? 4f : useGun && s == FastGunSeat ? 8f : 15f;
            bool ok = V3.Dot(aim, t.Aim) > Mathf.Cos(tol * Mathf.Deg2Rad) && t.React > _skill.ReactSeconds;
            t.SlewToward(aim, dt, _s.TurretTurnDeg);
            gunHold = useGun && ok;
            return !useGun && ok && _mags[s].Count > 0 && dm < reach && (crystal || !_cfg.GunsOn || dm >= gr * 0.6f || !GunReady(s));
        }

        void MissileAI(int s, OrcaMissile m)
        {
            var tg = TargetFor(s, U(m.Position), _s.MissileSpeed * _s.MissileLife * 0.8f, _mags[s].Count + 1);
            if (tg.Kind == TargetKind.None) { m.StickYaw = m.StickPitch = m.StickRoll = 0f; return; }
            var pos = U(m.Position);
            float d = Vector3.Distance(pos, tg.Position), lt = d / Mathf.Max(1f, MissileSpeed) * _skill.Lead;
            var aim = (tg.Position + tg.Velocity * lt - pos).normalized;
            var (yaw, pitch, roll) = OrcaCrewCore.SteerToward(m.Frame, N(aim), Gauss() * _skill.SteerNoise);
            m.StickYaw = yaw; m.StickPitch = pitch; m.StickRoll = roll;
            bool burst = (tg.Kind == TargetKind.Hull && d < BlastRadius * _skill.Detonate)
                         || (tg.Kind == TargetKind.Prism && d < _cfg.BlastRadiusAtRest * 0.4f);
            if (burst && m.CanBurstByHand) Burst(s);
        }

        // The lab's bestTarget: an omni crystal when the seat's magazine runs low, else the nearest rival hull or hostile
        // prism, else any crystal. Re-picked every aiRetargetSeconds.
        Target TargetFor(int s, Vector3 from, float reach, int mag)
        {
            if (Time.time < _retargetAt[s] && _targets[s].Kind != TargetKind.None) return Refresh(_targets[s]);
            _retargetAt[s] = Time.time + _cfg.AiRetargetSeconds;
            float best = float.MaxValue;
            var pick = default(Target);
            void Offer(TargetKind kind, Vector3 p, Vector3 v, float cost, Transform follow = null) { if (cost < best) { best = cost; pick = new Target { Kind = kind, Position = p, Velocity = v, Follow = follow }; } }

            var crystals = Crystal.Active;
            if (mag > 0 && mag < 2)
                for (int i = 0; i < crystals.Count; i++)
                {
                    var c = crystals[i];
                    if (!c || c.IsEmbedded || c.crystalProperties.Element != Element.Omni) continue;
                    float d = Vector3.Distance(from, c.transform.position);
                    if (d < reach) Offer(TargetKind.Omni, c.transform.position, Vector3.zero, d * 0.5f);
                }
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
                foreach (var player in players)
                {
                    var vessel = player?.Vessel;
                    var st = vessel?.VesselStatus;
                    if (player == _player || st == null || !UnityLiveness.Alive(vessel.Transform) || st.Domain == _status.Domain) continue;
                    float d = Vector3.Distance(from, vessel.Transform.position);
                    if (d < reach) Offer(TargetKind.Hull, vessel.Transform.position, vessel.Transform.forward * st.Speed, d, vessel.Transform);
                }
            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                _query.Clear();
                index.QuerySphere(from, Mathf.Min(reach, 250f), _query);   // the sniper also reaches past this; prisms beyond come in its cone
                foreach (var p in _query)
                {
                    if (!p || p.destroyed || p.Domain == _status.Domain || p.prismProperties is { IsSuperShielded: true }) continue;
                    Offer(TargetKind.Prism, p.transform.position, Vector3.zero, Vector3.Distance(from, p.transform.position) * 1.1f);
                }
            }
            if (pick.Kind == TargetKind.None)
                for (int i = 0; i < crystals.Count; i++)
                {
                    var c = crystals[i];
                    if (!c || c.IsEmbedded) continue;
                    float d = Vector3.Distance(from, c.transform.position);
                    if (d < reach) Offer(TargetKind.Crystal, c.transform.position, Vector3.zero, 1e9f + d);
                }
            _targets[s] = pick;
            return pick;
        }

        // a held hull target follows its hull between re-picks; a crystal or prism stays where it was picked
        static Target Refresh(Target t)
        {
            if (t.Kind == TargetKind.Hull && UnityLiveness.Alive(t.Follow)) t.Position = t.Follow.position;
            return t;
        }

        V3 RandDir()
        {
            float z = (float)_rng.NextDouble() * 2f - 1f, a = (float)_rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt(1f - z * z);
            return new V3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        float Gauss()
        {
            double u = 0.0;
            while (u <= 0.0) u = _rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u)) * System.Math.Cos(2.0 * System.Math.PI * _rng.NextDouble()));
        }
    }
}
