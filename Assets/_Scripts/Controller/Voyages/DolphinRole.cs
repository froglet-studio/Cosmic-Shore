using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Dolphin as the squad's <b>blaster</b>. Its weapon is the cone a free crystal (omni or team) fires along its
    /// nose when touched, sized by the energy it has skimmed, so steering it at a creature disarms it (the Bends
    /// lesson). This role claims a threat like every seat, steers the hull at the free crystal whose approach best
    /// lines up with that creature, and aims the commit drift's nose at the creature's heart
    /// (<see cref="AIPilot.SetDriftLookTargetProvider"/>, the Bends pattern with creatures for rivals). The hull veto
    /// is asked only below Space 5 (Clean Blast spares the squad's own domain) and only of the crystal-to-creature
    /// segment: round 2 vetoed against the THREAT CENTROID plus the whole reach, and with 150 grazers on the plating the
    /// centroid IS the Ark, so no crystal ever passed. With no threats or no crystal it skims the Ark's wake astern to
    /// bank energy. Blasts are counted through <see cref="VesselExplosionByCrystalEffectSO.Fired"/>.
    /// </summary>
    public sealed class DolphinRole : ArkEscortRoleBase
    {
        const float RechooseSeconds = 1f;
        const float PickupJump = 120f;   // a collected crystal is RELOCATED by the manager, not deactivated

        Fauna _target;
        Crystal _crystal;
        Vector3 _crystalLastAt;
        float _nextChoose;
        ArkEscortContext _ctx;
        int _picks, _taken, _noCrystalChoices, _vetoedLooks, _candidates, _blasts;
        float _threatSeconds, _onCrystalSeconds, _energyAtBlasts;

        public override string Name => "DolphinBlaster";

        public override string Notes() =>
            $"{Who} Dolphin: blasts {_blasts} (mean energy {(_blasts > 0 ? _energyAtBlasts / _blasts : 0f):0.00}), crystals chosen {_picks}, " +
            $"picked up {_taken}, choices with no usable crystal {_noCrystalChoices} (candidates seen {_candidates}), " +
            $"looks vetoed by the hull {_vetoedLooks}, {_threatSeconds:0}s with threats, {_onCrystalSeconds:0}s steering at a crystal, " +
            $"clean blast {(CleanBlast ? "on" : "OFF")}";

        public override void Arm(ArkEscortPilot pilot)
        {
            base.Arm(pilot);
            _crystal = null;
            _target = null;
            if (Ai) Ai.SetDriftLookTargetProvider(LookAt);
            VesselExplosionByCrystalEffectSO.Fired -= OnBlast;
            VesselExplosionByCrystalEffectSO.Fired += OnBlast;
        }

        public override void Disarm()
        {
            VesselExplosionByCrystalEffectSO.Fired -= OnBlast;
            if (Ai) Ai.ClearDriftLookTargetProvider();
            base.Disarm();
        }

        void OnBlast(IVesselStatus status, int resourceIndex)
        {
            if (status == null || !ReferenceEquals(status, Status)) return;
            _blasts++;
            _energyAtBlasts += Ammo(resourceIndex);
        }

        /// <summary>Space 5: the cone spares the squad's own domain, the hull included.</summary>
        bool CleanBlast => Status?.ElementalAbilityHandler && Status.ElementalAbilityHandler.IsUpgradeActive(Element.Space);

        public override Vector3 Steer(ArkEscortContext ctx)
        {
            _ctx = ctx;
            var ark = ctx.Ark;
            _target = ctx.Claim(Seat, _target);
            if (_target)
            {
                _threatSeconds += Time.deltaTime;
                if (_crystal && Usable(_crystal) && (_crystal.transform.position - _crystalLastAt).sqrMagnitude > PickupJump * PickupJump)
                {
                    _taken++;            // it jumped: collected (by us, most likely) and relocated
                    _crystal = null;
                }
                if (Time.time >= _nextChoose || !Usable(_crystal))
                {
                    _nextChoose = Time.time + RechooseSeconds;
                    var pick = Choose(ctx, ArkEscortContext.HeartOf(_target));
                    if (pick && pick != _crystal) _picks++;
                    if (!pick) _noCrystalChoices++;
                    _crystal = pick;
                }
                if (_crystal)
                {
                    _onCrystalSeconds += Time.deltaTime;
                    _crystalLastAt = _crystal.transform.position;
                    return _crystalLastAt;
                }
            }
            else _crystal = null;

            // Bank energy on the wake astern of the hull (own-domain mass to skim), on the seat's side.
            if (!ark) return Station(ctx);
            float side = Seat % 2 == 0 ? -1f : 1f;
            return ark.Position - ark.Forward * (ctx.HullRadius + EscortGeneRegistry.DolphinWakeOffset)
                   + ark.transform.right * (side * 30f);
        }

        static bool Usable(Crystal c) => c && !c.IsEmbedded && c.gameObject.activeInHierarchy;

        /// <summary>The free crystal to take for a blast at <paramref name="aim"/> (the claimed creature's heart).</summary>
        Crystal Choose(ArkEscortContext ctx, Vector3 aim)
        {
            var cell = ctx.Cell;
            var ark = ctx.Ark;
            if (!cell || !cell.RuntimeData || !ark) return null;
            float reach = EscortGeneRegistry.DolphinReach * EscortGeneRegistry.DolphinReachUse;
            float search = EscortGeneRegistry.DolphinCrystalSearchRadius;
            bool veto = !CleanBlast;
            Crystal best = null;
            float bestCost = float.MaxValue;
            foreach (var item in cell.RuntimeData.CellItems)
            {
                if (item is not Crystal c || !Usable(c)) continue;
                if (item.ItemType != ItemType.Buff) continue;
                if (item.ownDomain != Domains.Blue && item.ownDomain != ctx.Domain) continue;
                _candidates++;
                Vector3 at = c.transform.position;
                if ((at - ark.Position).magnitude > search) continue;
                Vector3 fire = aim - at;
                float range = fire.magnitude;
                if (range > reach || range < 1e-3f) continue;
                // Below Clean Blast the cone must not cross the hull on its way to the creature (the segment to the
                // creature, not the whole reach: a grazer sits ON the plating).
                if (veto && !ArkEscortContext.SegmentClearsSphere(at, aim - fire.normalized * 20f, ark.Position,
                        ctx.HullRadius + EscortGeneRegistry.DolphinConeArkClearance)) continue;
                Vector3 approach = at - Position;
                float angle = approach.sqrMagnitude > 1e-3f ? Vector3.Angle(approach, fire) : 0f;
                float cost = angle + approach.magnitude * 0.05f;   // alignment first, then nearness
                if (angle > EscortGeneRegistry.DolphinApproachAlignDeg) cost += 60f;
                if (cost < bestCost) { bestCost = cost; best = c; }
            }
            return best;
        }

        /// <summary>Where the drifting nose points: the claimed creature's heart (the hull vetoes it below Clean Blast).</summary>
        Vector3? LookAt()
        {
            var ctx = _ctx;
            if (ctx == null || !_target || !_target.IsAlivePrey) return null;
            var ark = ctx.Ark;
            if (!ark) return null;
            Vector3 heart = ArkEscortContext.HeartOf(_target);
            if (CleanBlast) return heart;
            Vector3 dir = heart - Position;
            if (dir.sqrMagnitude < 1e-3f) return null;
            bool clear = ArkEscortContext.SegmentClearsSphere(Position, heart - dir.normalized * 20f,
                ark.Position, ctx.HullRadius + EscortGeneRegistry.DolphinConeArkClearance);
            if (!clear) _vetoedLooks++;
            return clear ? heart : null;
        }
    }
}
