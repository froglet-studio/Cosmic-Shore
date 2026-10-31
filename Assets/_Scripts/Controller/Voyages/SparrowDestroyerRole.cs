using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Sparrow as the squad's <b>destroyer</b>: it kills the creatures attacking the Ark with its real weapons, not
    /// by flying through them (a Sparrow through a heart jousts nothing - only the Squirrel's container withers a
    /// lifeform by crystal). It takes the pulsefire guns and the Skyburst rocket off the pilot's blind timer and fires
    /// each only with the claimed creature on the nose, in range, and the Ark out of the lane (the rocket's blast
    /// included). It sits between the creature and the hull at a standoff, leading the creature's motion. With fewer
    /// threats than <c>ai.sparrow.nucleusSweepWhenThreatsBelow</c> in a hostile world it sweeps the nucleus with the
    /// guns, eating the hostile mass that decides the world (gunfire also restocks the rocket rack).
    /// </summary>
    public sealed class SparrowDestroyerRole : ArkEscortRoleBase
    {
        const float PassThroughFactor = 1.3f;   // inside standoff x this, fly the lead point itself (a strafing pass)

        Fauna _target;
        FullAutoActionSO _guns;
        FireGunActionSO _rocket;
        bool _gunsHeld;
        float _nextRocket;
        Vector3 _lastHeart;
        float _lastHeartAt;
        Vector3 _heartVelocity;
        Fauna _velocityOf;
        int _sweepIndex;
        Vector3 _sweepPoint;
        bool _sweeping;
        int _rockets, _rocketsHeldByLane;
        float _gunSeconds, _targetSeconds, _sweepSeconds;

        public override string Name => "SparrowDestroyer";

        public override string Notes() =>
            $"{Who} Sparrow: rockets {_rockets} (held by the Ark lane {_rocketsHeldByLane}), guns {_gunSeconds:0}s, " +
            $"{_targetSeconds:0}s on a target, {_sweepSeconds:0}s sweeping a nucleus, guns {(_guns ? "found" : "MISSING")}, rocket {(_rocket ? "found" : "MISSING")}";

        public override void Arm(ArkEscortPilot pilot)
        {
            base.Arm(pilot);
            if (TryAbility(out _guns)) Suppress(_guns);
            if (TryAbility(out _rocket)) Suppress(_rocket);
            _gunsHeld = false;
            _target = null;
            _sweeping = false;
        }

        public override void Disarm()
        {
            if (_gunsHeld) Press(_guns, false);
            _gunsHeld = false;
            base.Disarm();
        }

        public override Vector3 Steer(ArkEscortContext ctx)
        {
            _target = ctx.Claim(Seat, _target);
            if (_target)
            {
                _sweeping = false;
                Vector3 heart = ArkEscortContext.HeartOf(_target);
                TrackVelocity(heart);
                Vector3 lead = heart + _heartVelocity * EscortGeneRegistry.SparrowLeadSeconds;
                float standoff = EscortGeneRegistry.SparrowStandoff;
                float dist = (heart - Position).magnitude;
                // Far out: make for the firing position between the creature and the hull. Close in: fly the pass.
                return dist > standoff * PassThroughFactor ? ctx.ArkSideOf(lead, standoff) : lead;
            }

            if (ctx.Threats.Count < EscortGeneRegistry.SparrowSweepWhenThreatsBelow.Int && TrySweep(ctx, out var sweep))
            {
                _sweeping = true;
                return sweep;
            }
            _sweeping = false;
            return Station(ctx);
        }

        void TrackVelocity(Vector3 heart)
        {
            float now = Time.time;
            if (_velocityOf == _target && now - _lastHeartAt > 0.05f && now - _lastHeartAt < 1f)
                _heartVelocity = Vector3.Lerp(_heartVelocity, (heart - _lastHeart) / (now - _lastHeartAt), 0.5f);
            else if (_velocityOf != _target)
                _heartVelocity = Vector3.zero;
            _velocityOf = _target;
            _lastHeart = heart;
            _lastHeartAt = now;
        }

        /// <summary>A hostile nucleus to sweep: a golden-angle walk around its centre inside the control zone.</summary>
        bool TrySweep(ArkEscortContext ctx, out Vector3 point)
        {
            point = default;
            var cell = ctx.Cell;
            if (!cell || !cell.HasNucleusControlZone) return false;
            var dominant = cell.DominantDomain;
            if (dominant == ctx.Domain || dominant == Domains.Blue) return false;
            float r = cell.NucleusWorldRadius * EscortGeneRegistry.SparrowSweepRadiusFrac;
            Vector3 centre = cell.transform.position;
            if (!_sweeping || (Position - _sweepPoint).sqrMagnitude < 40f * 40f)
            {
                _sweepIndex++;
                float a = _sweepIndex * 2.39996f;   // the golden angle
                float z = 1f - 2f * ((_sweepIndex * 0.381966f) % 1f);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                _sweepPoint = centre + new Vector3(Mathf.Cos(a) * ring, z * 0.5f, Mathf.Sin(a) * ring) * r;
            }
            point = _sweepPoint;
            return true;
        }

        protected override void OnTick(ArkEscortContext ctx)
        {
            bool fireGuns = false;
            Vector3 pos = Position;
            Vector3 nose = Forward;

            if (_target && _target.IsAlivePrey)
            {
                Vector3 heart = ArkEscortContext.HeartOf(_target);
                Vector3 to = heart - pos;
                float dist = to.magnitude;
                float angle = AngleFrom(nose, to);

                fireGuns = _guns && dist <= EscortGeneRegistry.SparrowGunRange
                           && angle <= EscortGeneRegistry.SparrowGunAimDeg
                           && ctx.LaneClear(pos, pos + nose * Mathf.Max(dist, EscortGeneRegistry.SparrowGunRange));

                _targetSeconds += Time.deltaTime;
                if (_rocket && Time.time >= _nextRocket
                    && dist >= EscortGeneRegistry.SparrowRocketMinRange && dist <= EscortGeneRegistry.SparrowRocketMaxRange
                    && angle <= EscortGeneRegistry.SparrowRocketAimDeg
                    && Ammo(_rocket.AmmoIndex) >= _rocket.AmmoCost)
                {
                    if (ctx.LaneClear(pos, heart, EscortGeneRegistry.SparrowRocketBlastRadius))
                    {
                        Press(_rocket, true);
                        _rockets++;
                        _nextRocket = Time.time + EscortGeneRegistry.SparrowRocketCooldown;
                    }
                    else _rocketsHeldByLane++;
                }
            }
            else if (_sweeping && _guns)
            {
                var cell = ctx.Cell;
                fireGuns = cell && cell.IsInsideNucleus(pos)
                           && ctx.LaneClear(pos, pos + nose * EscortGeneRegistry.SparrowGunRange);
            }

            if (fireGuns) _gunSeconds += Time.deltaTime;
            if (_sweeping) _sweepSeconds += Time.deltaTime;
            if (fireGuns != _gunsHeld)
            {
                Press(_guns, fireGuns);
                _gunsHeld = fireGuns;
            }
        }
    }
}
