using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab as a <b>guardian</b>: its shove plate jousts a creature through the heart, and its goal rings put
    /// fresh own-domain mass (a ring's dais, a super-shielded sun core at Mass 5) beside the Ark's route as decoy and
    /// armour. The plate: the autopilot dash (<see cref="ScarabJukeController.TryAutopilotDash"/>, the Broadside
    /// pattern) when the claimed creature's heart is within plate range. The rings: only on crystal sites - where a
    /// creature just swam or a free crystal lies (<see cref="ArkEscortCrystalSites"/>, installed by the run as the
    /// switch placement resolver) - chosen on the Ark-to-threats line within the ring standoff; the press lands when
    /// the ring's centre (150 u ahead along the course) is within site reach of the site. Lightly built and lightly
    /// tested (see the brief): the Scarab kit was re-cut this week.
    /// </summary>
    public sealed class ScarabRingRole : ArkEscortRoleBase
    {
        const float RingAhead = 150f;   // PlaceSwitchActionExecutor's placementDistance

        Fauna _target;
        ScarabJukeController _juke;
        bool _hasSwitch;
        InputEvents _switchInput;
        float _nextRing;
        bool _hasSite;
        Vector3 _site;

        public override string Name => "ScarabRing";

        public override void Arm(ArkEscortPilot pilot)
        {
            base.Arm(pilot);
            _juke = Self ? Self.GetComponent<ScarabJukeController>() : null;
            _hasSwitch = TryInputFor<PlaceSwitchActionSO>(out _switchInput);
            _target = null;
            _hasSite = false;
        }

        public override Vector3 Steer(ArkEscortContext ctx)
        {
            _target = ctx.Claim(Seat, _target);
            if (_target)
            {
                _hasSite = false;
                return ArkEscortContext.HeartOf(_target);
            }

            var ark = ctx.Ark;
            if (_hasSwitch && ark && Time.time >= _nextRing)
            {
                if (!_hasSite || (_site - ark.Position).magnitude > EscortGeneRegistry.ScarabRingStandoff * 1.5f)
                    _hasSite = ctx.Sites.TryBestFor(ark.Position, ctx.HasThreats ? ctx.ThreatCentroid : ark.Position + ark.Forward * 300f,
                        EscortGeneRegistry.ScarabRingStandoff, out _site);
                if (_hasSite) return _site;
            }
            return Station(ctx);
        }

        protected override void OnTick(ArkEscortContext ctx)
        {
            if (_target && _target.IsAlivePrey)
            {
                if (_juke == null) return;
                Vector3 to = ArkEscortContext.HeartOf(_target) - Position;
                float range = EscortGeneRegistry.ScarabPlateRange;
                if (to.sqrMagnitude <= range * range) _juke.TryAutopilotDash(to);
                return;
            }

            if (!_hasSite || !_hasSwitch || Time.time < _nextRing) return;
            Vector3 centre = Position + Heading * RingAhead;
            float reach = EscortGeneRegistry.ScarabSiteReach;
            if ((centre - _site).sqrMagnitude > reach * reach) return;
            Tap(_switchInput);
            _nextRing = Time.time + EscortGeneRegistry.ScarabRingCooldown;
            _hasSite = false;
        }
    }
}
