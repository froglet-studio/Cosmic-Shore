using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE JOUST'S SPEED: the least speed a hull must be flying for its joust to land, on a rival vessel or on a
    /// lifeform's heart, scaled by one of its elements. The Squirrel's is its CHARGE parameter (Garrett, 2026-11-11: it
    /// replaced the petals the joust stole, a dial in whole petals and too coarse to tune).
    ///
    /// <para>ONE asset per jousting hull, read by every place a joust is decided and by the HUD that draws it, so the tick
    /// the pilot sees is the gate itself:</para>
    /// <list type="bullet">
    /// <item>the vessel joust's score and steal - <see cref="VesselCombatHitBySkimmerEffectSO"/> asks the hull's take
    /// (<see cref="VesselOvertakeBySkimmerEffectSO"/>, through <see cref="IContactAdmission"/>) before it admits a hit;</item>
    /// <item>the vessel joust's joust point and impact blast (what the Joust mode counts) -
    /// <see cref="VesselExplosionBySkimmerEffectSO"/> asks the same take;</item>
    /// <item>the friendly joust's buff - <see cref="VesselOvertakeBySkimmerEffectSO"/>'s own <c>Execute</c>;</item>
    /// <item>the lifeform joust - <see cref="VesselWitherLifeformByCrystalEffectSO"/>;</item>
    /// <item>the Charge card's speed gauge and its threshold mark - <c>SquirrelHUDController</c>.</item>
    /// </list>
    ///
    /// <para>It is a FLOOR on top of each joust's own rule, never instead of it: a vessel joust still has to out-run its
    /// victim, and a lifeform joust its creature.</para>
    ///
    /// <para>Read at the jouster's REPLICATED level, so every machine that decides a joust (the thief's owner for a scored
    /// hit, every peer for a friendly buff or a lifeform joust) gets the same number, and the HUD shows the number they
    /// decide with. The level is a whole number there, but the speed it sets is not a count of anything: one Charge level
    /// moves it by a tenth of the authored span.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "JoustSpeedConfig",
        menuName = "ScriptableObjects/Vessel Actions/Joust Speed Config")]
    public sealed class JoustSpeedConfigSO : ScriptableObject
    {
        [Tooltip("The least speed a joust needs, u/s: Min at the element's resting level, Max at its level 10, " +
                 "extrapolated through the deficit and overcharge bands and never below Floor (keep Floor at 0 or above: " +
                 "a negative minimum means no minimum). The Squirrel authors CHARGE, 90 u/s at rest (one and a half times " +
                 "its 60 u/s cruise, so it skims for boost first) down to 45 u/s at Charge 10 (a cruising Squirrel jousts).")]
        [SerializeField] ElementalFloat minimumSpeed = ElementalFloat.Multiplier(90f, 45f, Element.Charge, 0f);

        /// <summary>The element that moves the minimum (<see cref="Element.None"/> when it is flat).</summary>
        public Element ScaledElement => minimumSpeed.ScaledElement;

        /// <summary>The least speed, u/s, <paramref name="jouster"/>'s joust needs now (its replicated level).</summary>
        public float MinimumSpeedFor(IVesselStatus jouster) => Mathf.Max(0f, minimumSpeed.EvaluateReplicated(jouster));

        /// <summary>The same minimum at a normalized element level (0 at rest, 1 at level 10, -0.5 to 1.5 at the band's
        /// ends): the pure form, for tests and the numbers the docs quote.</summary>
        public float MinimumSpeedAtLevel(float level01) =>
            Mathf.Max(0f, minimumSpeed.EvaluateAtNormalizedLevel(level01));

        /// <summary>True when <paramref name="jouster"/> is flying fast enough for its joust to land.</summary>
        public bool Admits(IVesselStatus jouster) => jouster != null && jouster.Speed >= MinimumSpeedFor(jouster);
    }
}
