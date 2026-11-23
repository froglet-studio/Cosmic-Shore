using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent as a <b>guardian</b>: it puts seed walls between the threats and the Ark. The cloak drops a seed
    /// that grows a herringbone wall perpendicular to the Serpent's heading, out of loose prisms within its steal
    /// reach, so the role (a) picks a wall point on the Ark-to-threats line, past the hull plus the steal reach (the
    /// assembler also refuses hull prisms outright), (b) approaches it flying along that line so the wall faces the
    /// threats, and (c) presses the cloak there, off the pilot's 4.3 s timer. By gene it also stops in the stance at the
    /// wall point and holds. Lightly built and lightly tested (see the brief): a Serpent escort has barely been played.
    /// </summary>
    public sealed class SerpentWallRole : ArkEscortRoleBase
    {
        const float PreApproach = 140f;

        ShipActionSO _cloak;
        float _nextWall;
        bool _hasWallPoint;
        Vector3 _wallPoint;
        Vector3 _wallDir;      // from the Ark toward the threats
        bool _passedPre;
        float _stanceUntil;
        bool _inStance;

        public override string Name => "SerpentWall";

        public override void Arm(ArkEscortPilot pilot)
        {
            base.Arm(pilot);
            if (TryAbility(out CosmicShore.UI.CloakSeedWallActionSO cloak))
            {
                _cloak = cloak;
                Suppress(_cloak);
            }
            _hasWallPoint = false;
            _inStance = false;
        }

        public override void Disarm()
        {
            if (_inStance && TryInputFor<ToggleTranslationModeActionSO>(out var stance)) Tap(stance);
            _inStance = false;
            base.Disarm();
        }

        public override Vector3 Steer(ArkEscortContext ctx)
        {
            var ark = ctx.Ark;
            if (!ark) return Station(ctx);

            if (!ctx.HasThreats || Time.time < _nextWall)
            {
                _hasWallPoint = false;
                return Station(ctx);
            }

            if (!_hasWallPoint)
            {
                Vector3 dir = ctx.ThreatCentroid - ark.Position;
                if (dir.sqrMagnitude < 1e-3f) dir = ark.Forward;
                dir.Normalize();
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                if (side.sqrMagnitude < 1e-3f) side = ark.transform.right;
                side.Normalize();
                float lateral = (Seat - 1) * EscortGeneRegistry.SerpentWallLateralSpread;
                _wallDir = dir;
                _wallPoint = ark.Position + dir * EscortGeneRegistry.SerpentWallStandoff + side * lateral;
                _hasWallPoint = true;
                _passedPre = false;
            }
            else
            {
                // The Ark moves: the wall point rides with it until the seed is dropped.
                _wallPoint += ark.Forward * (ark.Speed * Time.deltaTime);
            }

            // Come in from the threat side, so the heading at the wall point runs threats -> Ark and the wall faces out.
            Vector3 pre = _wallPoint + _wallDir * PreApproach;
            if (!_passedPre && (Position - pre).sqrMagnitude > 50f * 50f
                && Vector3.Dot(_wallPoint - Position, _wallDir) < 0f)
                return pre;
            _passedPre = true;
            return _wallPoint - _wallDir * 80f;
        }

        protected override void OnTick(ArkEscortContext ctx)
        {
            if (_inStance && Time.time >= _stanceUntil)
            {
                if (TryInputFor<ToggleTranslationModeActionSO>(out var stance)) Tap(stance);
                _inStance = false;
            }
            if (!_hasWallPoint || Time.time < _nextWall) return;
            float within = EscortGeneRegistry.SerpentWallPlaceRadius;
            if ((Position - _wallPoint).sqrMagnitude > within * within) return;

            if (_cloak) Press(_cloak, true);
            _nextWall = Time.time + EscortGeneRegistry.SerpentWallCooldown;
            _hasWallPoint = false;

            if (EscortGeneRegistry.SerpentUseStance.On && EscortGeneRegistry.SerpentHoldSeconds > 0f
                && TryInputFor<ToggleTranslationModeActionSO>(out var stanceOn))
            {
                Tap(stanceOn);
                _inStance = true;
                _stanceUntil = Time.time + EscortGeneRegistry.SerpentHoldSeconds;
            }
        }
    }
}
