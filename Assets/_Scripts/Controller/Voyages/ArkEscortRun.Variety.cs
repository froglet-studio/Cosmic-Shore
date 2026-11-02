using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The voyage's variety (<see cref="ArkEscortVariety"/>): every run rolls its worlds, their variants and 0-2 named
    /// modifiers from one seed, applies them to its runtime copy of the escort, shows them on the departure banner and
    /// writes them in the report. The card previews the same roll (<see cref="PreviewVoyage"/>) before sailing.
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        int _voyageSeed = 1;
        ArkEscortVariety.RolledVoyage _voyage;

        /// <summary>The seed the next escort sails with (the card's preview picks it, the next sail uses it, then it is
        /// cleared so the card after shows a new voyage). 0 = the run picks one.</summary>
        public static int NextVoyageSeed { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetVarietyStatics() => NextVoyageSeed = 0;

        /// <summary>The voyage the next escort at <paramref name="difficulty"/> would sail (the card's preview).</summary>
        public static ArkEscortVariety.RolledVoyage PreviewVoyage(ArkEscortSO escort, int difficulty)
        {
            if (NextVoyageSeed == 0) NextVoyageSeed = NewSeed();
            return ArkEscortVariety.Roll(escort, difficulty, NextVoyageSeed);
        }

        static int NewSeed()
        {
            int s = System.Environment.TickCount ^ (int)(Random.value * int.MaxValue);
            return s == 0 ? 1 : s;
        }

        /// <summary>This run's voyage (worlds in order, modifiers), or null before the first sail.</summary>
        public ArkEscortVariety.RolledVoyage Voyage => _voyage;

        void RollAndApplyVoyage(int difficulty)
        {
            var t = _paramTarget;
            _voyageSeed = t?.VoyageSeed ?? (NextVoyageSeed != 0 ? NextVoyageSeed : NewSeed());
            if (_voyageSeed == 0) _voyageSeed = 1;
            NextVoyageSeed = 0;
            _voyage = ArkEscortVariety.Roll(_escort, difficulty, _voyageSeed, t?.ModifierMask ?? -1);
            if (_voyage.Legs.Count > 0) _escort.SetLegs(new List<ArkEscortSO.Leg>(_voyage.Legs));
            foreach (var m in _voyage.Modifiers) ApplyModifier(m, t);
        }

        void ApplyModifier(ArkEscortSO.VoyageModifier m, ArkEscortParams.Target t)
        {
            float f = Mathf.Max(0f, m.Factor);
            switch (m.Kind)
            {
                case ArkEscortSO.ModifierKind.Population:
                    if (t == null) return;
                    t.World[("all", "populationScale")] = t.WorldValue(0, "populationScale", 1f) * f;
                    break;
                case ArkEscortSO.ModifierKind.RegrowSeconds:
                    foreach (var tier in _escort.Tiers) tier.RegrowSeconds = Mathf.Max(0.05f, tier.RegrowSeconds * f);
                    break;
                case ArkEscortSO.ModifierKind.FaunaSpeed:
                    ScaleFauna(t, "speedScale", f);
                    break;
                case ArkEscortSO.ModifierKind.ArmourBite:
                    ScaleFauna(t, "biteScale", f);
                    break;
                case ArkEscortSO.ModifierKind.AllHostile:
                    foreach (var leg in _escort.Legs) { leg.Hostile = true; leg.HostileFrom = 1; }
                    foreach (var tier in _escort.Tiers) tier.HostileShare = 1f;
                    break;
                case ArkEscortSO.ModifierKind.ArkCrawl:
                    var p = _escort.ArkPacing;
                    p.Crawl *= f;
                    p.Entry *= f;
                    _escort.ArkPacing = p;
                    break;
                case ArkEscortSO.ModifierKind.PacifyShare:
                    _escort.PacifyShare = Mathf.Clamp01(_escort.PacifyShare * f);
                    break;
            }
        }

        static void ScaleFauna(ArkEscortParams.Target t, string field, float f)
        {
            if (t?.Fauna == null) return;
            float now = t.Fauna.TryGet(FaunaRuntimeTuning.AllSpecies, field, out float v) ? v : 1f;
            t.Fauna.Set(FaunaRuntimeTuning.AllSpecies, field, now * f);
        }

        /// <summary>The voyage's modifiers under the departure line (nothing without any).</summary>
        string VoyageBannerSuffix() =>
            _voyage is { ModifierNames: { Count: > 0 } } ? "\n" + string.Join("  /  ", _voyage.ModifierNames) : string.Empty;

        void ReportVoyage()
        {
            _report.VoyageSeed = _voyageSeed;
            _report.Worlds = _voyage != null ? new List<string>(_voyage.WorldNames) : new List<string>();
            _report.Modifiers = _voyage != null ? new List<string>(_voyage.ModifierNames) : new List<string>();
        }
    }
}
