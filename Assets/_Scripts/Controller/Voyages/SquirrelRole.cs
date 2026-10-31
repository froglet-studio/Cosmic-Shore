using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Squirrel as the squad's <b>cultivator</b>, two jobs by state:
    /// <list type="bullet">
    /// <item><b>Joust.</b> The Squirrel is the one hull whose heart hit kills (its container withers a lifeform by
    /// crystal), but only when it is faster than the creature by the effect's margin. It commits to a heart only with
    /// the speed in hand and the heart within a turn it can make; otherwise it flies a run-up straight (the boost ring
    /// pressed, by gene) until it is.</item>
    /// <item><b>Cultivate.</b> With no threats (fewer than <c>ai.squirrel.cultivateWhenThreatsBelow</c>) in a world a
    /// hostile domain controls, it races to the nucleus and weaves inside the control zone laying trail - own
    /// environment volume inside the nucleus is the only input to the world's dominant domain - until own volume beats
    /// the hostile's by the pacify margin, when the world's creatures spawn in the squad's colour. Its leash is let out
    /// to the nucleus for the run (the run's leash otherwise teleports it back to the Ark's flank).</item>
    /// </list>
    /// </summary>
    public sealed class SquirrelRole : ArkEscortRoleBase
    {
        const float RingCooldownSeconds = 20f;
        const float WeaveReachedWithin = 30f;

        enum Mode { Station, Joust, Runup, Cultivate }

        Mode _mode;
        Fauna _target;
        float _nextRing;
        int _weaveIndex;
        Vector3 _weavePoint;
        bool _weaving;

        public override string Name => "SquirrelCultivator";

        public override void Arm(ArkEscortPilot pilot)
        {
            base.Arm(pilot);
            _mode = Mode.Station;
            _target = null;
            _weaving = false;
        }

        public override Vector3 Steer(ArkEscortContext ctx)
        {
            _target = ctx.Claim(Seat, _target);
            if (_target)
            {
                _weaving = false;
                Vector3 heart = ArkEscortContext.HeartOf(_target);
                Vector3 to = heart - Position;
                bool fast = Speed - _target.CurrentSpeed >= EscortGeneRegistry.SquirrelJoustMargin;
                bool turnable = AngleFrom(Heading, to) <= EscortGeneRegistry.SquirrelJoustMaxTurnDeg;
                if (fast && turnable)
                {
                    _mode = Mode.Joust;
                    return heart;
                }
                // The run-up: a straight that swings gently toward the creature, long enough to gain the speed.
                _mode = Mode.Runup;
                float run = EscortGeneRegistry.SquirrelRunupDistance;
                Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : Heading;
                return Position + Heading * run + dir * (run * 0.5f);
            }

            if (ShouldCultivate(ctx, out var cell))
            {
                _mode = Mode.Cultivate;
                return CultivatePoint(cell);
            }

            _mode = Mode.Station;
            _weaving = false;
            return Station(ctx);
        }

        bool ShouldCultivate(ArkEscortContext ctx, out Cell cell)
        {
            cell = ctx.Cell;
            if (!EscortGeneRegistry.SquirrelCultivate.On || !cell || !cell.HasNucleusControlZone) return false;
            if (ctx.Threats.Count >= EscortGeneRegistry.SquirrelCultivateWhenThreatsBelow.Int) return false;
            var dominant = cell.DominantDomain;
            if (dominant == ctx.Domain) return false;
            if (dominant == Domains.Blue) return true;   // nobody's yet: take it
            float own = cell.GetNucleusDomainVolume(ctx.Domain);
            float theirs = cell.GetNucleusDomainVolume(dominant);
            return own <= theirs * EscortGeneRegistry.SquirrelPacifyMargin;
        }

        /// <summary>Into the nucleus, then a golden-angle weave inside it at the weave radius.</summary>
        Vector3 CultivatePoint(Cell cell)
        {
            Vector3 centre = cell.transform.position;
            if (!cell.IsInsideNucleus(Position))
            {
                _weaving = false;
                return centre;
            }
            float r = cell.NucleusWorldRadius * EscortGeneRegistry.SquirrelWeaveRadiusFrac;
            if (!_weaving || (Position - _weavePoint).sqrMagnitude < WeaveReachedWithin * WeaveReachedWithin)
            {
                _weaveIndex++;
                float a = _weaveIndex * 2.39996f;
                float z = 1f - 2f * ((_weaveIndex * 0.381966f) % 1f);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                _weavePoint = centre + new Vector3(Mathf.Cos(a) * ring, z * 0.6f, Mathf.Sin(a) * ring) * r;
                _weaving = true;
            }
            return _weavePoint;
        }

        protected override void OnTick(ArkEscortContext ctx)
        {
            if (_mode != Mode.Runup || !EscortGeneRegistry.SquirrelRingOnRunup.On || Time.time < _nextRing) return;
            if (!TryInputFor<SquirrelTubeActionSO>(out var ring)) { _nextRing = float.PositiveInfinity; return; }
            Tap(ring);
            _nextRing = Time.time + RingCooldownSeconds;
        }

        public override float Leash(ArkEscortContext ctx, float leash)
        {
            if (_mode != Mode.Cultivate) return leash;
            var cell = ctx.Cell;
            var ark = ctx.Ark;
            if (!cell || !ark) return leash;
            float toNucleus = (cell.transform.position - ark.Position).magnitude + cell.NucleusWorldRadius
                              + EscortGeneRegistry.SquirrelNucleusLeashExtra;
            return Mathf.Max(leash, toNucleus);
        }
    }
}
