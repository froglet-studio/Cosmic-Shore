using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Riposte - the Swordfish's CHARGE game (RIPOSTE.md): a sword duel in Jackstraw's 0G heap of pick-up sticks (the
    /// same four cells, <see cref="SpawnableJackstraw"/>). A STRIKE - the Swordfish's sword reaching a rival hull - is one
    /// point, and the first DOMAIN to the target wins (golf-timed).
    ///
    /// <para><b>Why the heap, for this hull.</b> A sword hit lands only while the Swordfish is FASTER than its victim
    /// (<c>VesselCombatHitBySword</c>, BROADSIDE.md section 3), the Swordfish's speed is its ramp, which winds up only on
    /// a straight line, and the ramp runs on what the bill slices. The sticks are the fuel and they break straight
    /// lines. The blade cuts a plain stick and the hull rams one for free, so a pilot cuts a lane to wind up in; the
    /// shielded knots take two strokes, the red tips debuff a hull that rams them, and the I4 ironwood binds a blade
    /// that is not energized (Shield Breaker, Space 5, pops it). Charge is the Swordfish's PvP card: x1 -> x2.5 petals
    /// a hit, and at Charge 5 Sword Steal pockets them.</para>
    ///
    /// <para><b>Reused from Jackstraw:</b> the crystals in the clearings (a silenced pilot refuels there), the server
    /// winner, the snapshot and the replay. <b>Riposte's own:</b> its AI pilots (<see cref="RiposteDuelist"/>: cut for
    /// fuel, charge through a rival, run out, turn back in), its settings asset, rule words, target and hint.
    /// SWORDFISH-ONLY is enforced by the card's Vessels list.</para>
    /// </summary>
    public class RiposteController : JackstrawController
    {
        readonly RiposteDuelist _duelist = new();
        readonly System.Collections.Generic.List<RiposteLanes.Line> _lanes = new(), _rails = new();

        protected override GameToastSituation HintSituation => GameToastSituation.RiposteHint;

        protected override void ArmAI()
        {
            if (Settings is not RiposteSettingsSO duel)
            {
                CSDebug.LogWarning("[RiposteController] the settings asset is not a RiposteSettingsSO; the AI seats " +
                                   "fly Jackstraw's hunters. Re-run Tools/Build/author_riposte_assets.py.");
                base.ArmAI();
                return;
            }
            var heap = Heap();
            float radius = heap ? heap.HeapRadius * heap.transform.lossyScale.x : 0f;
            _lanes.Clear();
            _rails.Clear();
            float centreClearing = 0f;
            if (heap is SpawnableRiposteYard yard)
            {
                // The arena answers in its own space: hand the pilots world lines.
                yard.CollectLines(_lanes, _rails);
                ToWorld(_lanes, yard.transform);
                ToWorld(_rails, yard.transform);
                if (_lanes.Count > 0) centreClearing = (_lanes[0].A - ArenaCentre).magnitude;
            }
            _duelist.Arm(gameData, duel, ArenaCentre, radius, NearestLiveCrystal, _lanes, _rails, centreClearing);
        }

        static void ToWorld(System.Collections.Generic.List<RiposteLanes.Line> lines, Transform arena)
        {
            for (int i = 0; i < lines.Count; i++)
                lines[i] = new RiposteLanes.Line(arena.TransformPoint(lines[i].A), arena.TransformPoint(lines[i].B));
        }

        protected override void DisarmAI()
        {
            _duelist.Disarm();
            base.DisarmAI();
        }

        protected override bool AIArmed => _duelist.IsArmed || base.AIArmed;

        protected override void TickAI()
        {
            if (_duelist.IsArmed) _duelist.Tick();
            else base.TickAI();
        }
    }
}
