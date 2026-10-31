using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Dolphin as the squad's <b>blaster</b>. Its weapon is the cone a free crystal (omni or team) fires along its
    /// nose when touched, so steering it at a creature disarms it (the Bends lesson). This role steers it at the free
    /// crystal whose approach best lines up with the threats - minimising the angle between (crystal - self) and
    /// (threats - crystal), with the threats inside the cone's reach of the crystal - and aims the commit drift's nose
    /// at the threat centroid (<see cref="AIPilot.SetDriftLookTargetProvider"/>, the Bends pattern with creatures for
    /// rivals), vetoed whenever the cone's axis would pass the hull. With no threats or no crystal it skims the Ark's
    /// wake astern to bank energy.
    /// </summary>
    public sealed class DolphinRole : ArkEscortRoleBase
    {
        const float RechooseSeconds = 1f;
        const float ReachedWithin = 25f;

        Crystal _crystal;
        float _nextChoose;
        ArkEscortContext _ctx;
        int _picks, _taken, _noCrystalChoices, _vetoedLooks, _candidates;
        float _threatSeconds, _onCrystalSeconds;

        public override string Name => "DolphinBlaster";

        public override string Notes() =>
            $"{Who} Dolphin: crystals chosen {_picks}, reached {_taken}, choices with no usable crystal {_noCrystalChoices} " +
            $"(candidates seen {_candidates}), looks vetoed by the hull {_vetoedLooks}, {_threatSeconds:0}s with threats, " +
            $"{_onCrystalSeconds:0}s steering at a crystal";

        public override void Arm(ArkEscortPilot pilot)
        {
            base.Arm(pilot);
            _crystal = null;
            if (Ai) Ai.SetDriftLookTargetProvider(LookAt);
        }

        public override void Disarm()
        {
            if (Ai) Ai.ClearDriftLookTargetProvider();
            base.Disarm();
        }

        public override Vector3 Steer(ArkEscortContext ctx)
        {
            _ctx = ctx;
            var ark = ctx.Ark;
            if (ctx.HasThreats)
            {
                _threatSeconds += Time.deltaTime;
                if (Time.time >= _nextChoose || !Usable(_crystal))
                {
                    _nextChoose = Time.time + RechooseSeconds;
                    if (_crystal && !Usable(_crystal)) _taken++;   // gone since we chose it: picked up (by us, most likely)
                    var pick = Choose(ctx);
                    if (pick && pick != _crystal) _picks++;
                    if (!pick) _noCrystalChoices++;
                    _crystal = pick;
                }
                if (_crystal)
                {
                    _onCrystalSeconds += Time.deltaTime;
                    return _crystal.transform.position;
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

        Crystal Choose(ArkEscortContext ctx)
        {
            var cell = ctx.Cell;
            var ark = ctx.Ark;
            if (!cell || !cell.RuntimeData || !ark) return null;
            Vector3 threats = ctx.ThreatCentroid;
            float reach = EscortGeneRegistry.DolphinReach * EscortGeneRegistry.DolphinReachUse;
            float search = EscortGeneRegistry.DolphinCrystalSearchRadius;
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
                if ((threats - at).magnitude > reach) continue;
                Vector3 approach = at - Position;
                Vector3 fire = threats - at;
                if (fire.sqrMagnitude < 1e-3f) continue;
                // The blast must not pass the hull on its way to the creatures.
                if (!ArkEscortContext.SegmentClearsSphere(at, at + fire.normalized * EscortGeneRegistry.DolphinReach,
                        ark.Position, ctx.HullRadius + EscortGeneRegistry.DolphinConeArkClearance)) continue;
                float angle = approach.sqrMagnitude > 1e-3f ? Vector3.Angle(approach, fire) : 0f;
                float cost = angle + approach.magnitude * 0.02f;   // alignment first, then nearness
                if (angle > EscortGeneRegistry.DolphinApproachAlignDeg) cost += 90f;
                if (cost < bestCost) { bestCost = cost; best = c; }
            }
            return best;
        }

        /// <summary>Where the drifting nose points: the threats, unless the hull lies on that line.</summary>
        Vector3? LookAt()
        {
            var ctx = _ctx;
            if (ctx == null || !ctx.HasThreats) return null;
            var ark = ctx.Ark;
            if (!ark) return null;
            Vector3 dir = ctx.ThreatCentroid - Position;
            if (dir.sqrMagnitude < 1e-3f) return null;
            bool clear = ArkEscortContext.SegmentClearsSphere(Position, Position + dir.normalized * EscortGeneRegistry.DolphinReach,
                ark.Position, ctx.HullRadius + EscortGeneRegistry.DolphinConeArkClearance);
            if (!clear) _vetoedLooks++;
            return clear ? ctx.ThreatCentroid : null;
        }
    }
}
