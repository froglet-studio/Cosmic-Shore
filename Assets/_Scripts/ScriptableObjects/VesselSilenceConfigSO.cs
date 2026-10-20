using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for SILENCE (Docs/ElementalAbilitySystem/SILENCE.md): a pilot who has petals stripped
    /// or stolen is silenced for a time proportional to the petals they lost. While silenced, every
    /// ability they have still works and still flies, but nothing it does can harm another pilot's
    /// vessel, fauna or prisms, and it lays no new danger prisms.
    ///
    /// <para>The numbers here are STARTING values. They are meant to be set by the balancing plan
    /// in the doc (each hull's AI plateau in its Charge arcade game, then in the Time arena game),
    /// not by feel.</para>
    ///
    /// Place the asset at <c>Resources/VesselSilenceConfig</c>. With no asset the defaults below
    /// apply and a warning names the missing asset once.
    /// </summary>
    [CreateAssetMenu(fileName = "VesselSilenceConfig",
                     menuName = "ScriptableObjects/Vessel/Vessel Silence Config")]
    public class VesselSilenceConfigSO : ScriptableObject
    {
        [Header("Duration")]
        [Tooltip("Seconds of silence per petal the victim lost to a strip or a steal. One petal is " +
                 "one element level; a hit that takes one petal on each of four elements silences " +
                 "for four times this.")]
        [Min(0f)]
        [SerializeField] float secondsPerPetal = 1f;

        [Tooltip("The most silence one pilot can carry at once. A new take EXTENDS the remaining " +
                 "silence by its own seconds, up to this ceiling. 0 disables silence entirely.")]
        [Min(0f)]
        [SerializeField] float maxSeconds = 8f;

        [Header("What silences")]
        [Tooltip("The take classes that silence their victim. Danger prisms BURN rather than strip " +
                 "or steal, so they never silence. The Butterfly's wormhole toll is a price the " +
                 "rival chose to pay by threading the mouth, so it is left out by default.")]
        [SerializeField] ElementalDebuffSources silencingSources =
            ElementalDebuffSources.Explosion | ElementalDebuffSources.VesselContact | ElementalDebuffSources.Other;

        [Header("What silence blocks")]
        [Tooltip("A silenced pilot lays no NEW danger prisms: a prism they lay or turn dangerous is " +
                 "laid plain instead. Danger prisms already standing keep their danger.")]
        [SerializeField] bool suppressDangerProduction = true;

        public float SecondsPerPetal => secondsPerPetal;
        public float MaxSeconds => maxSeconds;
        public ElementalDebuffSources SilencingSources => silencingSources;
        public bool SuppressDangerProduction => suppressDangerProduction;
    }
}
