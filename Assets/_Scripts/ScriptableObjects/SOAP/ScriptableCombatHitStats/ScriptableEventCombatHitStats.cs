using CosmicShore.Gameplay;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The channel a landed vessel-vs-vessel hit travels on: raised by the impact effects
    /// (<c>VesselCombatHitByProjectileEffectSO</c> / <c>VesselCombatHitByExplosionEffectSO</c>)
    /// on the machine that simulated the shot, consumed by the single-writer
    /// <c>StatsManager.CombatHitLanded</c>. Same shape as the joust's
    /// <c>ScriptableEventString</c> channel, but carrying the hit CLASS alongside the shooter
    /// so one channel serves both weapons.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_" + nameof(CombatHitStats),
        menuName = "ScriptableObjects/Events/" + nameof(CombatHitStats))]
    public class ScriptableEventCombatHitStats : ScriptableEvent<CombatHitStats>
    {
    }
}
